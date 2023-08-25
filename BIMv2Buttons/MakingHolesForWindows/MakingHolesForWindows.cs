#region Namespace

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;



using Application = Autodesk.Revit.ApplicationServices.Application;
//using Point = Autodesk.Revit.DB.Point;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class MakingHolesForWindows : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        public List<Room> projectRooms = new List<Room>();

        public List<XYZ> pointsList = new List<XYZ>();


        int countlightComplite = 0;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;


            using (var t = new Transaction(_doc))
            {
                t.Start("Create");

                //получить связанную модель FCD

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {

                    if (linkedInstances != null)
                    {

                        foreach (var linkInstance in linkedInstances)
                        {
                            Document linkDocument = linkInstance.GetLinkDocument();
                            Transform transform = linkInstance.GetTotalTransform();

                            //собрать окна из связанной модели FCD
                            var listWindows = GetAllWindowsLinked(linkDocument);

                            //собрать отверстия елси они есть и получить марку

                            var listOpenings = GetAllWindowsLinked(_doc);

                            //получить координаты этих окон

                            foreach (var windowsElement in listWindows)
                            {

                                XYZ locacionForWall = new XYZ();

                                LocationPoint location = windowsElement.Location as LocationPoint;

                                //получаем ID окна

                                ElementId idWindows = windowsElement.Id;

                                string markForWindos = idWindows.ToString();

                                //TaskDialog.Show("N", $"{idWindows}");


                                //получаем размеры окна
                                ElementType type = linkDocument.GetElement(windowsElement.GetTypeId()) as ElementType;

                                double heightWindows = 10;
                                double widhtWindows = 10;


                                if (type != null)
                                {
                                    heightWindows = type.LookupParameter("Высота").AsDouble();
                                    widhtWindows = type.LookupParameter("Ширина").AsDouble();
                                }

                                //string x = location.Point.X.ToString();
                                //string y = location.Point.Y.ToString();
                                //string z = location.Point.Z.ToString();

                                double x2 = location.Point.X;
                                double y2 = location.Point.Y ;
                                //double z2 = location.Point.Z;
                                double z2 = location.Point.Z - 2.62467;

                                XYZ locationWindow = new XYZ(x2, y2, z2);

                                locacionForWall = location.Point;

                                int moveI = 0;

                                //находим одинаковые отверстия и окна по ID

                                foreach (var openingElement in listOpenings)
                                {
                                    string markOpenings = openingElement.LookupParameter("Марка").AsString();

                                    if (markOpenings == markForWindos)
                                    {
                                        //проверить координаты

                                        LocationPoint locationOpenings = openingElement.Location as LocationPoint;

                                        if (location != locationOpenings)
                                        {
                                            double x3 = locationOpenings.Point.X;
                                            double y3 = locationOpenings.Point.Y;
                                            double z3 = locationOpenings.Point.Z;

                                            XYZ locationMove = new XYZ(x2-x3, y2-y3, z2-z3);

                                            ElementTransformUtils.MoveElement(_doc, openingElement.Id, locationMove);

                                            _doc.Regenerate();
                                        }

                                       

                                        moveI = 1;
                                        continue;
                                    }

                                   
                                }

                                if(moveI == 0)
                                {
                                    // Find a Window type for the new windows
                                    FilteredElementCollector winCollector = new FilteredElementCollector(_doc);
                                    IList<Element> windowTypes = winCollector.OfCategory(BuiltInCategory.OST_Windows)
                                        .WhereElementIsElementType().ToElements();
                                    FamilySymbol winType = windowTypes.First() as FamilySymbol;

                                    // Find a StructuralColumns
                                    FilteredElementCollector winCollector2 = new FilteredElementCollector(_doc);
                                    IList<Element> windowTypes2 = winCollector2.OfCategory(BuiltInCategory.OST_StructuralColumns)
                                        .WhereElementIsElementType().ToElements();
                                    FamilySymbol winType2 = windowTypes2.First() as FamilySymbol;

                                    //создае несущую колонну

                                    FamilyInstance instance =
                                       _doc.Create.NewFamilyInstance(locationWindow, winType2, /*newWall,*/ StructuralType.NonStructural);

                                    var allWalls = GetAllWall(_doc);

                                    _doc.Regenerate();

                                    foreach (Element wall in allWalls)
                                    {
                                        BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(_doc.ActiveView);
                                        BoundingBoxXYZ boundingBoxXYZ2 = instance.get_BoundingBox(_doc.ActiveView);

                                        if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ2))
                                        {
                                            //создаем окно
                                            FamilyInstance instance2 =
                                                _doc.Create.NewFamilyInstance(locationWindow, winType, wall, StructuralType.NonStructural);
                                            instance2.LookupParameter("Ширина").Set(widhtWindows);
                                            instance2.LookupParameter("Высота").Set(heightWindows);

                                            instance2.LookupParameter("Марка").Set(markForWindos);

                                            //TaskDialog.Show("N", "отверстие сделано");

                                        }


                                    }

                                    //_doc.Delete(element.Id);


                                    _doc.Regenerate();

                                    _doc.Delete(instance.Id);

                                    _doc.Regenerate();

                                }
                               


                                //TaskDialog.Show("N",/*$"{x} {y} {z}"*/"new");
                            }



                            //TaskDialog.Show("N",$"{listWindows.Count}");


                        }
                    }
                }


                // получить ID окна
                //получить размер окна
                //записать в созданный класс
                // создать проемы в стенаъ по координатам каждого окна
                //записать ID окна в ID отверстия
                //обновлять координаты для уже созданных отверстий



                //Reference r = uiDoc.Selection.PickObject(ObjectType.Element, "Выбрать элемент для размещения во всех помещениях");
                //Element elem = uiDoc.Document.GetElement(r);

                //CreateAnyElementInRooms(elem);
                //CreateAnyElementInLinkedFiles(elem);

                TaskDialog.Show("Complite!", $"Отверстия под окна сделаны");

                t.Commit();

            }

            return Result.Succeeded;
        }

        private void CreateAnyElementInRooms(Element elem)
        {
            FamilyInstance familyInstance = elem as FamilyInstance;
            FamilySymbol familySymbol = familyInstance.Symbol;

            var Rooms = GetAllRooms();

            if (Rooms != null)
            {
                int i = 0;
                foreach (var room in Rooms)
                {
                    var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                    var volumeRoom = room.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                    var heighRoom = volumeRoom / areaRoom;

                    if (areaRoom != 0)
                    {
                        var level = room.LevelId;

                        Level level1 = GetLevelFromRoom(level);

                        LocationPoint lpPoint = (LocationPoint)room.Location;

                        XYZ pointFoLigh = lpPoint.Point;

                        XYZ placeXyzPoint = new XYZ(pointFoLigh.X, pointFoLigh.Y, heighRoom);

                        FamilyInstance instLight = _doc.Create.NewFamilyInstance(
                                placeXyzPoint, familySymbol, level1, StructuralType.NonStructural);
                        i++;
                    }

                }

                countlightComplite = i;
            }

        }

        private void CreateAnyElementInLinkedFiles(Element elem)
        {
            FamilyInstance familyInstance = elem as FamilyInstance;
            FamilySymbol familySymbol = familyInstance.Symbol;

            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

            if (linkedInstances != null)
            {
                int j = 0;
                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                    Transform transform = linkInstance.GetTotalTransform();

                    List<Room> roomsLink = GetAllRoomsLinked(linkDocument);

                    if (roomsLink != null)
                    {
                        foreach (var room in roomsLink)
                        {

                            var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                            var volumeRoom = room.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                            var heightRoom = volumeRoom / areaRoom;

                            if (areaRoom != 0)
                            {
                                var level = room.LevelId;

                                Level level1 = GetLevelFromRoom(level);

                                if (level1 == null)
                                {
                                    level1 = GetLevelFromRoomByName(room);
                                }

                                LocationPoint lpPoint = (LocationPoint)room.Location;

                                XYZ pointFoLigh = lpPoint.Point;

                                var targetPoint = transform.OfPoint(pointFoLigh);

                                XYZ placeXyzPoint = new XYZ(targetPoint.X, targetPoint.Y, heightRoom);

                                FamilyInstance instLight = _doc.Create.NewFamilyInstance(
                                        placeXyzPoint, familySymbol, level1, StructuralType.NonStructural);
                                j++;
                            }


                        }
                    }


                }
                countlightComplite += j;
            }

        }

        private Level GetLevelFromRoom(ElementId elementId)
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

        private Level GetLevelFromRoomByName(Room room)
        {
            FilteredElementCollector lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (Element l in lvlCollection)
            {
                if (l.Name == room.Level.Name)
                {
                    return (Level)l;
                }

            }
            return null;

        }



        public IList<Element> GetAllRooms()
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }


        public List<Room> GetAllRoomsLinked(Document document)
        {

            FilteredElementCollector roomCollect = new FilteredElementCollector(document);
            roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
            Room room = null;
            projectRooms = new List<Room>();

            foreach (Element elem in roomCollect)
            {
                room = elem as Room;
                if (room != null)
                {
                    projectRooms.Add(room);
                }
            }
            return projectRooms;
        }

        public IList<Element> GetAllWindowsLinked(Document document)
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
                    return true;
                }
            }

            return false;

        }


    }
}