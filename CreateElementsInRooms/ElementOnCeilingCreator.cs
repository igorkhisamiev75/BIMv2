using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BIMv2Buttons
{
    class ElementOnCeilingCreator
    {
        private Document _doc;
        public Element elem;
        List<Room> m_rooms = new List<Room>();

        public ElementOnCeilingCreator()
        {

        }


        
        private void ElementCreator(Element elem)
        {
            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();
            if (linkedInstances != null)
            {
                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                    Transform transform = linkInstance.GetTotalTransform();

                    FamilyInstance familyInstance = elem as FamilyInstance;
                    FamilySymbol familySymbol = familyInstance.Symbol;

                    List<Room> roomsLink = m_rooms;

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
                                
                            }
                        }
                    }
                


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
