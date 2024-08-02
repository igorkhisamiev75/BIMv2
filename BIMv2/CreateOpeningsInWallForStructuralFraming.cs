#region Namespace

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateOpeningsInWallForStructuralFraming : IExternalCommand
    {
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;


            using (var t = new Transaction(_doc))
            {
                t.Start("Create openings ");

                //все стены                   
                IList<Element> allWalls = GetAllWall(_doc);

                //получить связанную модель FCD

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {
                    if (linkedInstances != null)
                    {
                        foreach (var linkInstance in linkedInstances)
                        {

                            Document linkDocument = linkedInstance.GetLinkDocument();


                            //собрать балки
                            //IList<Element> listStrFramings = GetAllStFraming(linkDocument);
                            IList<Element> listStrFramings = GetAllWall(linkDocument);


                            TaskDialog.Show("Complite!", $"Всего балок {listStrFramings.Count}");


                            //пройитись по каждой стене  

                            foreach (Element wall in allWalls)
                            {
                                int i = 0;

                                Wall wall1 = (Wall)wall;

                                foreach (var balka in listStrFramings)

                                {
                                    try
                                    {
                                        if (i == 0 && balka != null)
                                        {


                                            //bondingbox wall
                                            BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(_doc.ActiveView);

                                            //bounding Box window
                                            BoundingBoxXYZ boundingBoxXYZ3 = balka.get_BoundingBox(_doc.ActiveView);

                                            XYZ xyz1 = boundingBoxXYZ3.Max;
                                            XYZ yyz2 = boundingBoxXYZ3.Min;


                                            if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ3))
                                            {
                                                try
                                                {

                                                    _doc.Create.NewOpening(wall1, xyz1, yyz2);


                                                    continue;
                                                }

                                                catch
                                                {
                                                    //TaskDialog.Show("N", "отверстие не сделано сделано");
                                                    continue;
                                                }

                                            }
                                        }

                                    }
                                    catch
                                    {
                                        continue;
                                    }

                                }

                            }


                        }


                        TaskDialog.Show("Complite!", $"Отверстия под балки");

                        _doc.Regenerate();
                        t.Commit();
                    }
                }

                return Result.Succeeded;
            }
        }



        public IList<Element> GetAllStFraming(Document document)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }



        public IList<Element> GetAllWall(Document document)
        {
            var room_collector = new FilteredElementCollector(document)
                                 .OfCategory(BuiltInCategory.OST_Walls)
                                 .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            IList<Element> allWalls = room_collector.ToElements();

            return allWalls;

        }

       

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        public bool bbIntersect(BoundingBoxXYZ BoxXYZ, BoundingBoxXYZ BoxXYZ2)
        {
            if (BoxXYZ != null && BoxXYZ2 != null)
            {
                Outline outline1 = new Outline(BoxXYZ.Min, BoxXYZ.Max);

                Outline outline2 = new Outline(BoxXYZ2.Min, BoxXYZ2.Max);

                if (outline1.Intersects(outline2, 0))
                {
                    //TaskDialog.Show("N", $"пересекаются {BoxXYZ.ToString()} и {BoxXYZ2.ToString()}");
                    return true;
                }
            }

            return false;

        }

    }
}



