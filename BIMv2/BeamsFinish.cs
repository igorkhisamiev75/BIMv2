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

                 IList<Line> AllLine = new List<Line>();
                IList<Line> AllLine2 = new List<Line>();

                CurveArray curvesForWall = new CurveArray();

                infoAboutBeams infoAboutBeams = new infoAboutBeams();

                //получить у от них  нижние линии

                foreach (var beam in allBeamsElements)
                {
                    GeometryElement geometryElement = beam.get_Geometry(options);

                    CurveArray curves2 = new CurveArray();
                    List<Solid> solids2 = new List<Solid>();

                    CurveArray curves3 = new CurveArray();

                    //Find all solids and insert them into solid array
                    AddCurvesAndSolids(geometryElement, ref curves2, ref solids2);

                    foreach (Solid solid in solids2)
                    {
                        if (solid.SurfaceArea > 0 && solid.Id!=-1)
                        {
                            foreach (Edge curve in solid.Edges)
                            {
                                if (curve.ApproximateLength > 3.3)
                                {
                                    curve.AsCurve().Tessellate();


                                    curves.Append(curve.AsCurve());
                                }
                                
                            }
                            
                        }
                    }

                    XYZ start = new XYZ(0, 0, 0);
                    XYZ end = new XYZ(10, 10, 0);
                    Line geomLine = Line.CreateBound(start, end);

                    List<int> delNumber= new List<int>();


                    foreach (Line curve in curves)
                    {
                        AllLine.Add(curve);
                    }


                    for (int j = 0; j < AllLine.Count; j++)
                    {
                       
                            XYZ first = AllLine[j].Tessellate().First(); //получили первую
                            XYZ second = AllLine[j].Tessellate().Last(); // вторую

                            var x1 = Math.Abs(first.X); var y1 = Math.Abs(first.Y);
                            var x2 = Math.Abs(second.X); var y2 = Math.Abs(second.Y);

                            for (int i = 0; i < AllLine.Count; i++)
                            {
                            if (j != i && i>j)
                            {
                                var lin2 = AllLine[i];

                                XYZ first2 = lin2.Tessellate().First(); //получили первую
                                XYZ second2 = lin2.Tessellate().Last(); // вторую

                                var x11 = Math.Abs(first2.X); var y11 = Math.Abs(first2.Y);
                                var x22 = Math.Abs(second2.X); var y22 = Math.Abs(second2.Y);

                                if (x1 == x22 && y1 == y22 && x2 == x11 && y2 == y11 ||
                                    x1 == x11 && y1 == y11 && x2 == x22 && y2 == y22)
                                {
                                    delNumber.Add(i); //3 
                                }
                            }
                                

                            }
                      
                        
                    }


                   
                   var nedDel = delNumber.Distinct();

                  
                    foreach(var i in nedDel)
                    {
                        if(i != null)
                            try { AllLine.Remove(AllLine[i]); }
                            catch { }
                        
                    }

                    //TaskDialog.Show("N", $"{delNumber.Count}"); 


                    //var gpou1 = AllLine.GroupBy(p => p.Direction.Z).ToList();

                    //foreach (IGrouping<double, Line> gpou2 in gpou1)
                    //{
                    //    //foreach (Line line in gpou2)
                    //    //{

                    //    //}
                    //    AllLine2.Add(gpou2.First());continue;
                    //    //foreach (Line line in gpou2)
                    //    //{
                            
                    //    //}
                        
                    //}

                    //AllLine.Distinct();

                    foreach (Line line in AllLine)
                    {

                        //XYZ start = line.Tessellate().First();
                        //XYZ end = line.Tessellate().();
                        //Line geomLine = Line.CreateBound(start, end);

                        curves3.Append(line);
                    }

                    //TaskDialog.Show("NEW", $"{AllLine2.Count}");

                    var levelName = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsValueString();

                    var level=FindlevelByName(levelName, doc);

                    var levelId = level.Id;

                    //высота стены
                    double hightForWall = beam.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP).AsDouble() -
                        beam.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM).AsDouble();

                    double offset = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).AsDouble()- hightForWall;


                    foreach (Curve g in curves3)
                    {
                        if (g != null)

                            if (!g.IsCyclic)
                            {
                                try
                                {
                                    var wall = Wall.Create(doc, g, levelId, false);
                                    //wall.LookupParameter("Room Bounding").Set(0);
                                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);
                                    wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(offset);
                                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(hightForWall);
                                    wall.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).Set(412992); //412992 6065806

                                    doc.Regenerate();

                                    //IList<Reference> sideFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);
                                    ////MOVE WALL
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
                    curves3.Clear();
                    AllLine.Clear();
                    delNumber.Clear();


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
