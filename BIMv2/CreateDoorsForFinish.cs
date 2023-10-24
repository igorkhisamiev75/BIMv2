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
    public class CreateDoorsForFinish : IExternalCommand
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
                FamilySymbol symbol2 = GetSymbol(_doc, "ADSK_Дверь_Проем", "ADSK_Дверь_Проем");

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

                            //собрать двери из связанной модели 
                            IList<Element> listDoors = GetAllDoors(linkDocument);

                            foreach (Element element in listDoors)
                            {
                                if (element != null)
                                {
                                    listDoorsOnlyDoors.Add(element);
                                }

                            }

                            TaskDialog.Show("Complite!", $"Всего дверей {listDoors.Count}");

                            //собрать отверстия если они есть и получить марку

                            IList<Element> listOpenings = GetAllWindows(_doc);

                            IList<Element> listOpeningsClear = new List<Element>();
                            IList<Element> listOpeningsClear2 = new List<Element>();

                           
                           
                            //перебор по каждой двери
                            foreach (var doorElement in listDoorsOnlyDoors)
                            {
                                try
                                {
                                    if (doorElement != null)
                                    {
                                        //если есть отверстия по ID то передвигаем, если нет то создаем новое
                                        //получаем ID окна

                                        ElementId idWindows = doorElement.Id;
                                        string markForWindows = idWindows.ToString();

                                        //получить координаты окона
                                        XYZ locacionForWall = new XYZ();
                                        LocationPoint location = doorElement.Location as LocationPoint;

                                        //получаем размеры окна
                                        ElementType type = linkDocument.GetElement(doorElement.GetTypeId()) as ElementType;

                                        //double heightWindows = 1;
                                        //double widhtWindows = 1;

                                        //double widhtWindows = 10;
                                        //double heightWindows =10;

                                        double widhtWindows = doorElement.LookupParameter("W_Полная ширина").AsDouble();
                                        double heightWindows = doorElement.LookupParameter("W_Полная высота").AsDouble();


                                        double x2 = location.Point.X;
                                        double y2 = location.Point.Y;

                                        //double z2 = location.Point.Z - 2.62467;
                                        double z2 = location.Point.Z;



                                        XYZ locationWindow = new XYZ(x2, y2, z2);

                                        locacionForWall = location.Point;

                                        int moveI = 0;

                                        //находим одинаковые отверстия и окна по ID
                                        foreach (Element openingElement in listOpeningsClear)
                                        {
                                            string markOpenings = openingElement.LookupParameter("Марка").AsString();

                                            if (markOpenings == markForWindows)
                                            {
                                                //проверяем размеры и забиваем нужные

                                                openingElement.LookupParameter("Ширина").Set(widhtWindows);
                                                openingElement.LookupParameter("Высота").Set(heightWindows);

                                                //проверить координаты

                                                LocationPoint locationOpenings = openingElement.Location as LocationPoint;

                                                if (location != locationOpenings)
                                                {
                                                    double x3 = locationOpenings.Point.X;
                                                    double y3 = locationOpenings.Point.Y;
                                                    double z3 = locationOpenings.Point.Z;

                                                    XYZ locationMove = new XYZ(x2 - x3, y2 - y3, z2 - z3);

                                                    ElementTransformUtils.MoveElement(_doc, openingElement.Id, locationMove);

                                                    _doc.Regenerate();

                                                    moveI = 1;

                                                }

                                            }

                                            if (moveI == 1)
                                            {
                                                break;
                                            }



                                        }
                                        if (moveI == 0)
                                        {
                                            string markForNK = "";

                                            //BoundingBoxXYZ boundingBoxXYZ2 = instance.get_BoundingBox(_doc.ActiveView);


                                            foreach (Element wall in allWalls)
                                            {
                                                //bondingbox wall
                                                BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(_doc.ActiveView);

                                                //bounding Box window
                                                BoundingBoxXYZ boundingBoxXYZ3 = doorElement.get_BoundingBox(_doc.ActiveView);


                                                if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ3))
                                                {
                                                    try
                                                    {
                                                        ElementId idWall = wall.Id;
                                                        markForNK = idWall.ToString();

                                                        Level level = GetLevel(wall.LevelId);


                                                        //создаем окно
                                                        FamilyInstance instance2 =
                                                            _doc.Create.NewFamilyInstance(locationWindow, symbol2, wall, level, StructuralType.NonStructural);

                                                        //заполняем его нужными параметрами
                                                        instance2.LookupParameter("Ширина").Set(widhtWindows);
                                                        instance2.LookupParameter("Высота").Set(heightWindows);

                                                        instance2.LookupParameter("Марка").Set(markForWindows);

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
                                }

                                catch
                                {
                                    continue;
                                }



                                //TaskDialog.Show("N",/*$"{x} {y} {z}"*/"new");
                            }

                           

                        }

                        TaskDialog.Show("Complite!", $"Отверстия под двери сделаны");

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



