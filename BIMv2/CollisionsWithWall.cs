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
    public class CollisionsWithWall : IExternalCommand
    {
        private Document _doc;

        public List<XYZ> pointsList = new List<XYZ>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("Получение информации с чем коллизирует");

                //получение всех отверстий
                IList<Element> allHoles = GetAllHolls(_doc);

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {
                    try { 
                    if (linkedInstances != null && linkedInstance.Name.Contains("AR"))
                    {
                        foreach (var linkInstance in linkedInstances)
                        {
                            Document linkDocument = linkInstance.GetLinkDocument();

                            if (linkDocument.IsLinked == true)
                            {
                                //собрать стены
                                IList<Element> listWindows = GetAllWall(linkDocument);

                                TaskDialog.Show("Complite!", $"Всего стен: {listWindows.Count}");

                                //перебор по каждому отверстию
                                foreach (var hole in allHoles)
                                {
                                    try
                                    {
                                        if (hole != null)
                                        {


                                            foreach (Element wall in listWindows)
                                            {
                                                //bondingbox wall
                                                BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(_doc.ActiveView);

                                                //bounding Box window
                                                BoundingBoxXYZ boundingBoxXYZ3 = hole.get_BoundingBox(_doc.ActiveView);


                                                if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ3))
                                                {
                                                    try
                                                    {

                                                        string nameWall = wall.Name;

                                                        hole.LookupParameter("ADSK_Примечание").Set(nameWall);


                                                        _doc.Regenerate();

                                                        //TaskDialog.Show("N", $"отверстие сделано{markForNK}");
                                                        break;
                                                    }

                                                    catch
                                                    {
                                                        //TaskDialog.Show("N", "отверстие не сделано сделано");
                                                        continue;
                                                    }

                                                }


                                            }

                                            _doc.Regenerate();
                                            //    instance.LookupParameter("Марка").Set(markForNK);
                                            //_doc.Delete(instance.Id);

                                            //_doc.Regenerate();

                                        }
                                    }



                                    catch
                                    {
                                        continue;
                                    }



                                    //TaskDialog.Show("N",/*$"{x} {y} {z}"*/"new");
                                }
                            }

                            else { break; }




                        }

                    }
                }
                    catch
                    {

                    }
                }

                TaskDialog.Show("Complite!", $"Всего отверстий {allHoles.Count}");

                t.Commit();

            }

            return Result.Succeeded;
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

        public IList<Element> GetAllHolls(Document document)
        {
            var room_collector = new FilteredElementCollector(document)
                                 .OfCategory(BuiltInCategory.OST_GenericModel)
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

