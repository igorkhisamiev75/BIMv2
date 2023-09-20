#region Namespace

using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;


#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class MakingHolesForWindowsCheck : IExternalCommand
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
                t.Start("Create openings for FCD windows");

                IList<Element> listWindowsOnlyWindows = new List<Element>();

                //собрать окна из модели FCD
                IList<Element> listWindows33 = GetAllWindows(_doc);

                //собрать окна в линке


                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {

                    if (linkedInstances != null)
                    {
                        foreach (var linkInstance in linkedInstances)
                        {
                            Document linkDocument = linkInstance.GetLinkDocument();

                            //IList<Element> listWindowsOnlyWindows = new List<Element>();

                            //собрать окна из связанной модели FCD
                            listWindowsOnlyWindows = GetAllWindows(linkDocument);


                        }
                    }
                }
                
                //сравнить по ид если одинаковые ид, то сравниваем размеры и положения

                foreach (Element element in listWindows33)
                {
                    var idWind = element.LookupParameter("Марка").AsString();

                    var el1Shirina = element.LookupParameter("Ширина").AsDouble();
                    var el1Visota = element.LookupParameter("Высота").AsDouble();

                    foreach (Element element1 in listWindowsOnlyWindows)
                    {
                        ElementId idWindows = element1.Id;
                        string markForWindows = idWindows.ToString();

                        var el1Shirina1= element1.LookupParameter("Ширина").AsDouble();
                        var el1Visota1 = element1.LookupParameter("Высота").AsDouble();


                        if (markForWindows == idWind)
                        {
                            if(el1Shirina!= el1Shirina1 || el1Visota != el1Visota1)

                            {

                            }

                        }

                    }

                }



                TaskDialog.Show("Complite!", $"0409 Отверстия под окна сделаны и обновлены, всего ");

                t.Commit();

            }

            return Result.Succeeded;
        }


        public Level GetLevel(ElementId elementId)
        {
            FilteredElementCollector lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (Element l in lvlCollection)
            {
                if (l.Id == elementId)
                {
                    return (Level)l;
                }

            }

            return null;

        }

        public IList<Element> GetAllWindows(Document document)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Windows)
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

        public FamilySymbol ST(Document document)
        {

            // Find a StructuralColumns
            FilteredElementCollector winCollector2 = new FilteredElementCollector(document);
            IList<Element> windowTypes2 = winCollector2.OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsElementType().ToElements();

            FamilySymbol winType2 = windowTypes2.First() as FamilySymbol;

            return winType2;


        }

        public FamilySymbol GetSymbol(Document document, string familyName, string symbolName)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(Family)).OfType<Family>()
                .FirstOrDefault(f => f.Name.Equals(familyName))?.GetFamilySymbolIds()
                .Select(id => document.GetElement(id)).OfType<FamilySymbol>().FirstOrDefault(symbol => symbol.Name.Equals(symbolName));
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


