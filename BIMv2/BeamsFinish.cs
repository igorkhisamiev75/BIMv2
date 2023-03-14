using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BeamsFinish : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();
        public List<Ceiling> AllFinishListCeiling = new List<Ceiling>();
        public List<Floor> AllFinishFloor = new List<Floor>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            using (var t = new Transaction(doc))
            {
                t.Start("Отделка балок");

                //собрать все балки

                var allBeamsElements=GetAllBeamsElements(doc);
                Options options = new Options();
                options.ComputeReferences = true;
                options.View = doc.ActiveView;
                CurveArray curves = new CurveArray();

                CurveArray curvesForWall = new CurveArray();

                infoAboutBeams infoAboutBeams = new infoAboutBeams();

                //получить у от них  нижние линии

                foreach (var beam in allBeamsElements)
                {
                    GeometryElement geometryElement = beam.get_Geometry(options);

                    CurveArray curves2 = new CurveArray();
                    List<Solid> solids2 = new List<Solid>();

                    //Find all solids and insert them into solid array
                    AddCurvesAndSolids(geometryElement, ref curves2, ref solids2);

                    foreach (Solid solid in solids2)
                    {
                        if (solid.SurfaceArea > 0 && solid.Id!=-1)
                        {
                            foreach (Edge curve in solid.Edges)
                            {
                                if (curve.ApproximateLength > 2)
                                {
                                    curve.Tessellate().FirstOrDefault();
                                    curves.Append(curve.AsCurve());
                                }
                                
                            }
                            
                        }
                    }



                    TaskDialog.Show("NEW", $"{curvesForWall.Size}");





                    var levelName = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsValueString();

                    var level=FindlevelByName(levelName, doc);

                    var levelId = level.Id;

                    double offset = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).AsDouble();


                    //высота стены
                    double hightForWall = beam.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP).AsDouble() -
                        beam.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM).AsDouble();


                    //foreach (var g in geomElem)
                    //{
                    //    Solid solid = g as Solid;

                    //    Curve curve = g as Curve;

                    //    GeometryInstance geomInst = g as GeometryInstance;

                    //    GeometryElement transformedGeomElem = geomInst.GetInstanceGeometry(geomInst.Transform);

                    //    foreach (GeometryObject g2 in transformedGeomElem)
                    //    {
                    //        //Solid solid2 = g2 as Solid;

                    //        if (solid.Id != null)
                    //        {
                    //            var ed = solid.Edges;

                    //            foreach (var ed2 in ed)
                    //            {
                    //                var c = ed2 as Curve;

                    //                curves.Append(c);
                    //            }

                    //            //TaskDialog.Show("NEW", $"{curves.Size.ToString()}");
                    //        }


                    //    }

                    //}

                    foreach (Curve g in curves)
                    {
                        if (g != null)

                            if (!g.IsCyclic)
                            {
                                try
                                {
                                    var wall = Wall.Create(doc, g, levelId, false);
                                    //wall.LookupParameter("Room Bounding").Set(0);
                                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);
                                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(offset);
                                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(hightForWall);
                                    wall.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).Set(320638);

                                    doc.Regenerate();

                                    //IList<Reference> sideFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);
                                    // MOVE WALL
                                    //Face face = uiDoc.Document.GetElement(sideFaces[0])
                                    //    .GetGeometryObjectFromReference(sideFaces[0]) as Face;
                                    //PlanarFace pf = face as PlanarFace;
                                    //XYZ normal_reverted = pf.FaceNormal;
                                    //wall.Location.Move(-normal_reverted * (wall.WallType.Width / 2.0));



                                }
                                catch
                                {

                                    continue;
                                }

                            }


                    }

                    curves.Clear();


                }


                //получить высоту
                //создать отделочные стены
                //записать номера помещений в стены

               

                t.Commit();

            }
            return Result.Succeeded;

        }

        class infoAboutBeams
        {
            public List<Level> NameLevel { get; set; }

            public List<CurveArray> Curvs { get; set; }

            //public 

        }

       
        public IList<Element> GetAllBeamsElements(Document document)
        {
            // 
            var _collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = _collector.ToElements();
            return allFloors;
        }


        public IList<Element> GetAllRooms(Document doc)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

        

        public IList<Element> GetAllWFloorElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

        public IList<Element> GetAllLevels(Document doc)
        {
            // List all Levels
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

        public Level FindlevelByName(string nameLevel, Document doc)
        {
            Level findLevel = null ;

            var levels = GetAllLevels(doc);

            foreach (Level level in levels)
            {
                var n=level.Name;

                if (n == nameLevel)
                {
                    findLevel=level;
                }
                
            }
           return findLevel;

        }

        
        public void AddCurvesAndSolids(GeometryElement geomElem,
                                ref CurveArray curves2,
                                ref List<Solid> solids2)
        {
            foreach (GeometryObject geomObj in geomElem)
            {
                Curve curve2 = geomObj as Curve;
                if (null != curve2)
                {
                    curves2.Append(curve2);
                    continue;
                }
                Solid solid = geomObj as Solid;
                if (null != solid)
                {
                    solids2.Add(solid);
                    continue;
                }

                //If this GeometryObject is Instance, call AddCurvesAndSolids
                GeometryInstance geomInst = geomObj as GeometryInstance;
                if (null != geomInst)
                {
                    GeometryElement transformedGeomElem
                      = geomInst.GetInstanceGeometry(geomInst.Transform);
                    AddCurvesAndSolids(transformedGeomElem, ref curves2, ref solids2);
                }
            }
        }

    }
}
