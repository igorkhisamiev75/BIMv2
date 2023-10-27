#region Namespace

using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;

using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;


#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateWindowsForFinish : IExternalCommand
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

                //все стены                   
                IList<Element> allWalls = GetAllWall(_doc);

                //семйество для вставки

                //FamilySymbol symbol = GetSymbol(_doc, "2bimRU_SC", "2bimRU_SC");
                FamilySymbol symbol2 = GetSymbol(_doc, "ADSK_Окно_ПроемПрямоугольный", "ADSK_Окно_ПроемПрямоугольный");

                //получить связанную модель FCD

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {
                    if (linkedInstances != null)
                    {
                        foreach (var linkInstance in linkedInstances)
                        {

                            Document linkDocument = linkedInstance.GetLinkDocument();

                            IList<Element> listWindowsOnlyWindows = new List<Element>();

                            IList<Element> listDoorsOnlyDoors = new List<Element>();

                            IList<Element> listVitragOnly = new List<Element>();

                            //собрать окна из связанной модели FCD
                            IList<Element> listWindows = GetAllWindows(linkDocument);

                            foreach (Element element in listWindows)
                            {
                                if (element != null && element.Name != "ПВХ" && element.Name != "Отлив_металлический")
                                {
                                    listWindowsOnlyWindows.Add(element);
                                }

                            }

                            TaskDialog.Show("Complite!", $"Всего окон: {listWindowsOnlyWindows.Count}");

                            //перебор по каждому окну
                            foreach (var windowsElement in listWindows)
                            {
                                try
                                {
                                    if (windowsElement != null)
                                    {
                                        //если есть отверстия по ID то передвигаем, если нет то создаем новое
                                        //получаем ID окна

                                        ElementId idWindows = windowsElement.Id;
                                        string markForWindows = idWindows.ToString();

                                        //получить координаты окона
                                        XYZ locacionForWall = new XYZ();
                                        LocationPoint location = windowsElement.Location as LocationPoint;

                                        //получаем размеры окна
                                        ElementType type = linkDocument.GetElement(windowsElement.GetTypeId()) as ElementType;

                                        double widhtWindows = type.LookupParameter("Ширина").AsDouble();
                                        double heightWindows = type.LookupParameter("Высота").AsDouble();

                                        //double widhtWindows = windowsElement.LookupParameter("Ширина").AsDouble();
                                        //double heightWindows = windowsElement.LookupParameter("Высота").AsDouble();


                                        double x2 = location.Point.X;
                                        double y2 = location.Point.Y;
                                       double z2 = location.Point.Z - 2.62467;
                                        //double z2 = location.Point.Z;

                                        XYZ locationWindow = new XYZ(x2, y2, z2);

                                        locacionForWall = location.Point;

                                        foreach (Element wall in allWalls)
                                        {
                                            //bondingbox wall
                                            BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(null);

                                            //bounding Box window
                                            BoundingBoxXYZ boundingBoxXYZ3 = windowsElement.get_BoundingBox(null);
                                            // BoundingBoxXYZ boundingBoxXYZ3 = instance.get_BoundingBox(null);


                                            if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ3))
                                            {
                                                try
                                                {
                                                    ElementId idWall = wall.Id;
                                                    //markForNK = idWall.ToString();

                                                    Level level = GetLevel(wall.LevelId);

                                                    //создаем окно
                                                    FamilyInstance instance2 =
                                                        _doc.Create.NewFamilyInstance(locationWindow, symbol2, wall, level, StructuralType.NonStructural);

                                                    //заполняем его нужными параметрами
                                                    instance2.LookupParameter("Ширина").Set(widhtWindows);
                                                    instance2.LookupParameter("Высота").Set(heightWindows);

                                                    _doc.Regenerate();

                                                    //TaskDialog.Show("N", $"отверстие сделано{markForNK}");
                                                    continue;
                                                }

                                                catch
                                                {
                                                    continue;
                                                }

                                            }

                                        }

                                        _doc.Regenerate();

                                    }
                                }

                                catch
                                {
                                    continue;
                                }

                            }


                        }


                        TaskDialog.Show("Complite!", $"Отверстия под окна сделаны");

                        t.Commit();
                    }
                }

                return Result.Succeeded;
            }
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

        public IList<Element> GetAllVitrag(Document document)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

        public IList<Element> GetAllDoors(Document document)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
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



