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

            using (var t = new Transaction(doc))
            {
                t.Start("Отделка балок");

                //собрать все балки

                var allBeamsElements=GetAllBeamsElements(doc);
                Options options = new Options();
                CurveArray curves = new CurveArray();

                

                //получить у от них  нижние линии

                foreach (var beam in allBeamsElements)
                {

                    GeometryElement geomElem = beam.get_Geometry(options);

                    var levelName = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsValueString();

                    

                    foreach(var g in geomElem)
                    {
                        Solid solid = g as Solid;

                        Curve curve = g as Curve;

                        GeometryInstance geomInst = g as GeometryInstance;

                        GeometryElement transformedGeomElem = geomInst.GetInstanceGeometry(geomInst.Transform);

                        foreach (GeometryObject g2 in transformedGeomElem)
                        {
                            Solid solid2 = g2 as Solid;

                            if (solid2.Id != -1)
                            {
                                var ed = solid2.Edges;

                                foreach (var ed2 in ed)
                                {
                                    var c = ed2 as Curve;

                                    curves.Append(c);
                                }

                                TaskDialog.Show("NEW", $"{curves.Size.ToString()}");
                            }

                            
                        }

                      

                    }

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
            public Level NameLevel { get; set; }

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

    }
}
