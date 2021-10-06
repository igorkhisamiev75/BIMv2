using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CreateElementsInRooms
{
    public class RoomsData
    {
        private UIApplication m_revit;
        private List<Room> m_rooms = new List<Room>();
        

        public ReadOnlyCollection<Room> Rooms
        {
            get
            {
                return new ReadOnlyCollection<Room>(m_rooms);
            }
        }

        public RoomsData(ExternalCommandData commandData) // Get all the rooms in the current document
        {
            m_revit = commandData.Application;

            // get all the rooms
            GetAllRoomsLinked();
        }

      
        public List<Room> GetAllRoomsLinked()
        {
            Document document = m_revit.ActiveUIDocument.Document;
            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances(document);

            if (linkedInstances != null)
            {
                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                    Transform transform = linkInstance.GetTotalTransform();

                    FilteredElementCollector roomCollect = new FilteredElementCollector(linkDocument);
                    roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
                    Room room = null;
                    m_rooms = new List<Room>();

                    foreach (Element elem in roomCollect)
                    {
                        room = elem as Room;
                        if (room != null)
                        {
                            m_rooms.Add(room);
                        }
                    }
                }
            }

            return m_rooms;
        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances(Document document)
        {
            var collector = new FilteredElementCollector(document);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        public void CreateAnyElementInLinkedFiles(List<string> checListRoom)
        {
            using (SubTransaction subTransaction = new SubTransaction(m_revit.ActiveUIDocument.Document))
            {
                Document document = m_revit.ActiveUIDocument.Document;
                string earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
                var elem = document.GetElement(earthId);

                FamilyInstance familyInstance = elem as FamilyInstance;
                FamilySymbol familySymbol = familyInstance.Symbol;

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();
                int j = 0;

                if (linkedInstances != null)
                {
                    foreach (var linkInstance in linkedInstances)
                    {
                        Document linkDocument = linkInstance.GetLinkDocument();
                        Transform transform = linkInstance.GetTotalTransform();

                        List<Room> roomsLink = m_rooms;

                        if (roomsLink != null)
                        {
                            foreach (var room in roomsLink)
                            {
                                string roomName = room.Name;

                                string justname = "";
                                for (int i = 0; i < roomName.Length; i++)
                                {
                                    if (!Char.IsDigit(roomName[i]) && !(roomName[i]).Equals('.') && !(roomName[i]).Equals(','))

                                    {
                                        justname += roomName[i];
                                    }
                                }

                                string FindRoom = checListRoom.Find(item => item == justname);

                                if (FindRoom != null)
                                {
                                    subTransaction.Start();
                                    var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                                    var volumeRoom = room.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                                    var heightRoom = volumeRoom / areaRoom;

                                    if (areaRoom != 0)
                                    {
                                        var level = room.LevelId;

                                        Level level1 = GetLevelFromRoom(level, room);

                                        if (level1 == null)
                                        {
                                            level1 = GetLevelFromRoomByName(room);
                                        }

                                        LocationPoint lpPoint = (LocationPoint)room.Location;

                                        XYZ pointFoLigh = lpPoint.Point;

                                        var targetPoint = transform.OfPoint(pointFoLigh);

                                        XYZ placeXyzPoint = new XYZ(targetPoint.X, targetPoint.Y, heightRoom);

                                        FamilyInstance instLight = document.Create.NewFamilyInstance(
                                            placeXyzPoint, familySymbol, level1, StructuralType.NonStructural);
                                        j++;
                                        subTransaction.Commit();
                                    }
                                }


                                
                            }

                        }
                    }
                }
                
                TaskDialog.Show("Размещено", $"{j} элементов");

                
                document.Regenerate();
            }
            
            
        }

        private Level GetLevelFromRoom(ElementId elementId, Room room)
        {
            var _doc = m_revit.ActiveUIDocument.Document; 
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
            FilteredElementCollector lvlCollector = new FilteredElementCollector(m_revit.ActiveUIDocument.Document);
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

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(m_revit.ActiveUIDocument.Document);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }
    }
}