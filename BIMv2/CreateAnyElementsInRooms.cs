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

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateAnyElementsInRooms2 : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        public List<Room> projectRooms = new List<Room>();
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
                Reference r = uiDoc.Selection.PickObject(ObjectType.Element, "Выбрать элемент для размещения во всех помещениях");
                Element elem = uiDoc.Document.GetElement(r);
                
                CreateAnyElementInRooms(elem);
               CreateAnyElementInLinkedFiles(elem);

                TaskDialog.Show("Complite!", $"Размещено {countlightComplite} элементов");
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

                    var level = room.LevelId;

                    Level level1 = GetLevelFromRoom(level);

                    LocationPoint lpPoint = (LocationPoint)room.Location;

                    XYZ pointFoLigh = lpPoint.Point;

                    XYZ placeXyzPoint = new XYZ(pointFoLigh.X, pointFoLigh.Y, heighRoom);

                    FamilyInstance instLight = _doc.Create.NewFamilyInstance(
                            placeXyzPoint, familySymbol, level1, StructuralType.NonStructural);
                    i++;
                }

                countlightComplite = i;
            }

        }

        private void CreateAnyElementInLinkedFiles(Element elem)
        {
            FamilyInstance familyInstance = elem as FamilyInstance;
            FamilySymbol familySymbol = familyInstance.Symbol;
            
            var linkedInstances = FindLinkedInstances();

            if (linkedInstances != null)
            {
                int j = 0;
                foreach (var linkInstance in linkedInstances)
                {
                    var linkDocument = linkInstance.GetLinkDocument();
                    var transform = linkInstance.GetTotalTransform();
                    

                    var roomsLink = GetAllRoomsLinked(linkDocument);

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
                    countlightComplite += j;

                }
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

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }


    }
}