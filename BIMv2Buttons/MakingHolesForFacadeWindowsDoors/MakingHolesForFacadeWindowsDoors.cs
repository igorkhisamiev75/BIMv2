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
    public class MakingHolesForFacadeWindowsDoors : IExternalCommand
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

                FamilySymbol symbol = GetSymbol(_doc, "2bimRU_SC", "2bimRU_SC");
                FamilySymbol symbol2 = GetSymbol(_doc, "ADSK_Окно_ПроемПрямоугольный", "ADSK_Окно_ПроемПрямоугольный");

                //получить связанную модель FCD

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {

                    if (linkedInstances != null)
                    {
                        foreach (var linkInstance in linkedInstances)
                        {
                            Document linkDocument = linkInstance.GetLinkDocument();

                            IList<Element> listWindowsOnlyWindows = new List<Element>();

                            IList<Element> listDoorsOnlyDoors = new List<Element>();

                            IList<Element> listVitragOnly = new List<Element>();

                            //собрать витражи из связанной модели FCD
                            IList<Element> listVitrag = GetAllVitrag(linkDocument);

                            foreach (Element element in listVitrag)
                            {

                                //получаем размеры окна
                                ElementType type = linkDocument.GetElement(element.GetTypeId()) as ElementType;

                                var opis = type.LookupParameter("Описание").AsString();

                                if(opis == "Витраж")
                                {
                                    listVitragOnly.Add(element);
                                }


                            }

                            TaskDialog.Show("Complite!", $"Витражей {listVitragOnly.Count}");

                            //собрать двери из связанной модели FCD
                            IList<Element> listDoors = GetAllDoors(linkDocument);

                            foreach (Element element in listDoors)
                            {
                                if (element != null)
                                {
                                    listDoorsOnlyDoors.Add(element);
                                }

                            }

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

                            //собрать отверстия если они есть и получить марку

                            IList<Element> listOpenings = GetAllWindows(_doc);
                            IList<Element> listOpeningsClear = new List<Element>();
                            IList<Element> listOpeningsClear2 = new List<Element>();

                            //оставим только отверстия

                            foreach (Element element in listOpenings)
                            {
                                if (element != null && element.Name == "Оконный проем")
                                {
                                    listOpeningsClear.Add(element);
                                }
                            }

                            //перебор по каждому окну
                            foreach (var windowsElement in listWindowsOnlyWindows)
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


                                        double widhtWindows = windowsElement.LookupParameter("Ширина").AsDouble();
                                        double heightWindows = windowsElement.LookupParameter("Высота").AsDouble();

                                        double x2 = location.Point.X;
                                        double y2 = location.Point.Y;

                                        double z2 = location.Point.Z - 2.62467;
                                        //double z2 = location.Point.Z;



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

                                            //создае несущую колонну

                                            FamilyInstance instance =
                                               _doc.Create.NewFamilyInstance(locationWindow, symbol, /*newWall,*/ StructuralType.NonStructural);

                                            _doc.Regenerate();


                                            string markForNK = "";

                                            BoundingBoxXYZ boundingBoxXYZ2 = instance.get_BoundingBox(_doc.ActiveView);

                                           // allWalls.Reverse(); 

                                            foreach (Element wall in allWalls)
                                            {
                                                //bondingbox wall
                                                BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(null);

                                                //bounding Box window
                                                //BoundingBoxXYZ boundingBoxXYZ3 = windowsElement.get_BoundingBox(null);
                                                BoundingBoxXYZ boundingBoxXYZ3 = instance.get_BoundingBox(null);


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

                                            //_doc.Regenerate();
                                            instance.LookupParameter("Марка").Set(markForNK);
                                            _doc.Delete(instance.Id);

                                            _doc.Regenerate();

                                        }


                                    }
                                }

                                catch
                                {
                                    continue;
                                }



                                //TaskDialog.Show("N",/*$"{x} {y} {z}"*/"new");
                            }


                            //все отверстия
                            IList<Element> listOpeningsNew = GetAllWindows(_doc);

                            foreach (Element element in listOpeningsNew)
                            {
                                if (element != null && element.Name == "ADSK_Окно_ПроемПрямоугольный")
                                {
                                    listOpeningsClear2.Add(element);
                                }
                            }

                            //все окна
                            //IList<Element> listWindows4 = GetAllWindows(linkDocument);

                            IList<string> listWindowsSt = new List<string>();

                            foreach (Element element2 in listWindowsOnlyWindows)
                            {
                                ElementId idWindowsNew5 = element2.Id;
                                string markOpenings5 = idWindowsNew5.ToString();

                                listWindowsSt.Add(markOpenings5);

                            }

                            foreach (Element element in listOpeningsClear2)
                            {
                                string markOpenings = element.LookupParameter("Марка").AsString();

                                if (listWindowsSt.Contains(markOpenings))
                                {
                                    continue;

                                }
                                else
                                {
                                    _doc.Delete(element.Id);
                                }

                            }

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

                                        double widhtWindows = doorElement.LookupParameter("Ширина").AsDouble();
                                        double heightWindows = doorElement.LookupParameter("Высота").AsDouble();

                                        //double widhtWindows = windowsElement.LookupParameter("W_Полная ширина").AsDouble();
                                        //double heightWindows = windowsElement.LookupParameter("W_Полная высота").AsDouble();


                                        //if (type != null)
                                        //{
                                        //    //heightWindows = type.LookupParameter("Примерная высота").AsDouble();
                                        //    //widhtWindows = type.LookupParameter("Примерная ширина").AsDouble();
                                        //    try
                                        //    {
                                        //        //heightWindows = type.LookupParameter("Высота").AsDouble();
                                        //        //widhtWindows = type.LookupParameter("Ширина").AsDouble();
                                        //        heightWindows = type.LookupParameter("Примерная высота").AsDouble();
                                        //        widhtWindows = type.LookupParameter("Примерная ширина").AsDouble();
                                        //    }

                                        //    catch
                                        //    {
                                        //        heightWindows = 10;
                                        //        widhtWindows = 10;

                                        //        //heightWindows = type.LookupParameter("Примерная высота").AsDouble();
                                        //        //widhtWindows = type.LookupParameter("Примерная ширина").AsDouble();
                                        //    }

                                        //}

                                        double x2 = location.Point.X;
                                        double y2 = location.Point.Y;

                                        double z2 = location.Point.Z - 2.62467;
                                        //double z2 = location.Point.Z;



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

                                            //создае несущую колонну

                                            //FamilyInstance instance =
                                            //   _doc.Create.NewFamilyInstance(locationWindow, symbol, /*newWall,*/ StructuralType.NonStructural);

                                            //_doc.Regenerate();


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

                            //перебор по каждому витражу

                            foreach (var vitragElement in listVitragOnly)
                            {
                                try
                                {
                                    if (vitragElement != null)
                                    {
                                        //если есть отверстия по ID то передвигаем, если нет то создаем новое
                                        //получаем ID окна

                                        ElementId idWindows = vitragElement.Id;
                                        string markForWindows = idWindows.ToString();

                                        //получить координаты окона
                                        XYZ locacionForWall = new XYZ();
                                        LocationCurve locationC = vitragElement.Location as LocationCurve;

                                        //получаем размеры окна
                                        ElementType type = linkDocument.GetElement(vitragElement.GetTypeId()) as ElementType;

                                        //double heightWindows = 1;
                                        //double widhtWindows = 1;

                                        //double widhtWindows = 10;
                                        //double heightWindows =10;

                                        //ширина
                                        double widhtWindows = vitragElement.LookupParameter("Длина").AsDouble();
                                        //площадь
                                        double squerWindows = vitragElement.LookupParameter("Площадь").AsDouble();
                                        //высота
                                        double heightWindows = squerWindows/ widhtWindows;

                                    
                                        

                                        var h = locationC.Curve.Tessellate();

                                        

                                        double x2 = h[0].X;
                                        double y2 = h[0].Y+ (widhtWindows/2);
                                        //double z2 = location.Point.Z;
                                        double z2 = h[0].Z;



                                        XYZ locationWindow = new XYZ(x2, y2, z2);

                                        locacionForWall = locationWindow;

                                        int moveI = 0;

                                        //находим одинаковые отверстия и окна по ID
                                        foreach (Element openingElement in listOpeningsClear)
                                        {
                                            string markOpenings = openingElement.LookupParameter("Марка").AsString();

                                            if (markOpenings == markForWindows)
                                            {
                                                ////проверяем размеры и забиваем нужные

                                                //openingElement.LookupParameter("Ширина").Set(widhtWindows);
                                                //openingElement.LookupParameter("Высота").Set(heightWindows);

                                                ////проверить координаты

                                                //LocationPoint locationOpenings = openingElement.Location as LocationPoint;

                                                //if (locationWindow != locationOpenings)
                                                //{
                                                //    double x3 = locationOpenings.Point.X;
                                                //    double y3 = locationOpenings.Point.Y;
                                                //    double z3 = locationOpenings.Point.Z;

                                                //    XYZ locationMove = new XYZ(x2 - x3, y2 - y3, z2 - z3);

                                                //    ElementTransformUtils.MoveElement(_doc, openingElement.Id, locationMove);

                                                //    _doc.Regenerate();

                                                //    moveI = 1;

                                                //}

                                            }

                                            if (moveI == 1)
                                            {
                                                break;
                                            }



                                        }
                                        if (moveI == 0)
                                        {

                                            //создае несущую колонну

                                            //FamilyInstance instance =
                                            //   _doc.Create.NewFamilyInstance(locationWindow, symbol, /*newWall,*/ StructuralType.NonStructural);

                                            //_doc.Regenerate();


                                            string markForNK = "";

                                            //BoundingBoxXYZ boundingBoxXYZ2 = instance.get_BoundingBox(_doc.ActiveView);


                                            foreach (Element wall in allWalls)
                                            {
                                                //bondingbox wall
                                                BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(_doc.ActiveView);

                                                //bounding Box window
                                                BoundingBoxXYZ boundingBoxXYZ3 = vitragElement.get_BoundingBox(_doc.ActiveView);


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

                    }
                }

                //все отверстия
                IList<Element> listOpeningsNew2 = new List<Element>();

                //все отверстия
                IList<Element> listOpeningsNew4 = GetAllWindows(_doc);

                foreach (Element element in listOpeningsNew4)
                {
                    if (element != null && element.Name == "ADSK_Окно_ПроемПрямоугольный")
                    {
                        listOpeningsNew2.Add(element);
                    }
                }



                TaskDialog.Show("Complite!", $"Отверстия под окна, витражи и двери сделаны и обновлены, всего {listOpeningsNew2.Count}");

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



