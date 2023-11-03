#region Namespace


using System.Collections.Generic;


using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;

using Autodesk.Revit.UI;



#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class FindDuctFiting : IExternalCommand
    {
        private Document _doc;

        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("Create duct info");

                //все возудховоды           
                IList<Element> allDuct = GetAllDuct(_doc);
                IList<Element> allDuctFiting = GetAllDuctFiting(_doc);

                foreach (Element elementFiting in allDuctFiting)
                {
                    foreach (Element elementDuct in allDuct)
                    {
                        //bondingbox wall
                        BoundingBoxXYZ boundingBoxXYZ = elementFiting.get_BoundingBox(_doc.ActiveView);

                        //bounding Box window
                        BoundingBoxXYZ boundingBoxXYZ3 = elementDuct.get_BoundingBox(_doc.ActiveView);

                        if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ3))
                        {
                            try
                            {

                                var mark = elementDuct.LookupParameter("Размер").AsString();

                                var mark2 = elementFiting.LookupParameter("Диаметр воздуховода").AsValueString();

                                //заполняем его нужными параметрами
                                elementFiting.LookupParameter("Комментарии").Set("из "+mark+" в "+ mark2);


                                //_doc.Regenerate();

                                //TaskDialog.Show("N", $"отверстие сделано{markForNK}");
                                break;
                            }

                            catch
                            {
                                elementFiting.LookupParameter("Комментарии").Set("См.общий размер");
                                //TaskDialog.Show("N", "отверстие не сделано сделано");
                                continue;
                            }

                        }

                    }


                }
                TaskDialog.Show("Fiting", $"{allDuctFiting.Count}");
                t.Commit();

                
                //_doc.Regenerate();
            }

            return Result.Succeeded;
        }

        

        public IList<Element> GetAllDuctFiting(Document document)
        {
            var room_collector = new FilteredElementCollector(document)
                                 .OfCategory(BuiltInCategory.OST_DuctFitting)
                                 .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            IList<Element> allWalls = room_collector.ToElements();

            return allWalls;

        }

        public IList<Element> GetAllDuct(Document document)
        {
            var room_collector = new FilteredElementCollector(document)
                                 .OfCategory(BuiltInCategory.OST_DuctCurves)
                                 .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            IList<Element> allWalls = room_collector.ToElements();

            return allWalls;

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


