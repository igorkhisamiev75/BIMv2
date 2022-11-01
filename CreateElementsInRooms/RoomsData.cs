using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace CreateElementsInRooms
{
   public class RoomsData
    {
        public UIApplication m_revit;
        private List<Room> m_rooms = new List<Room>();
        private ExternalCommandData commandData;
        public RoomsData(ExternalCommandData commandData) // Get all the rooms in the current document
        {
            m_revit = commandData.Application;

            // get all the rooms
            GetAllRoomsLinked();
        }
        //Element elem;

        public ReadOnlyCollection<Room> Rooms => new ReadOnlyCollection<Room>(m_rooms);


        

        private IEnumerable<RevitLinkInstance> FindLinkedInstances(Document document)
        {
            var collector = new FilteredElementCollector(document);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }


        private Level GetLevelFromRoom(ElementId elementId, Room room)
        {
            var _doc = m_revit.ActiveUIDocument.Document;
            var lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (var l in lvlCollection)
                if (l.Id == elementId)
                    return (Level)l;

            return null;
        }

        private Level GetLevelFromRoomByName(Room room)
        {
            var lvlCollector = new FilteredElementCollector(m_revit.ActiveUIDocument.Document);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (var l in lvlCollection)
                if (l.Name == room.Level.Name)
                    return (Level)l;
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

        public Element SelectedElement(Document sDocument)
        {
            //var document = m_revit.ActiveUIDocument.Document;
            //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
            //var elem = document.GetElement(earthId);
            //uiDoc = m_revit.ActiveUIDocument;
            //using (Transaction subTransaction = new Transaction(sDocument, Guid.NewGuid().GetHashCode().ToString()))
            //using (SubTransaction subTransaction = new SubTransaction(sDocument))
            //{
            //subTransaction.Start();
            try
            {
                /*if (*/
                //subTransaction.Start("sdf");/* == TransactionStatus.Started)*/
                //if (subTransaction.Start() == TransactionStatus.Started)
                //if (subTransaction.Start() == TransactionStatus.Started)
                //{

                var uiDoc = m_revit.ActiveUIDocument;
                //uiDoc.ActiveView;

                var uidoc = new UIDocument(sDocument);

                //sDocument.Regenerate();

                var r = uidoc.Selection.PickObject(ObjectType.Element,
                    "Выбрать элемент для размещения");
                var elem = uidoc.Document.GetElement(r);
                return elem;
                //TaskDialog.Show("Revit", "Exception" + elem);

                //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
                //var elem = sDocument.GetElement(earthId);


                //if (subTransaction.Commit() == TransactionStatus.Committed)
                //{
                //    return elem;
                //}
                //}
            }

            catch (Exception ex)
            {
                TaskDialog.Show("Revit", "Exception" + ex.Message);
                //subTransaction.RollBack();
                return null;
            }


            //}
        }

        public List<Room> GetAllRoomsLinked()
        {
            var document = m_revit.ActiveUIDocument.Document;
            var linkedInstances = FindLinkedInstances(document);

            if (linkedInstances != null)
                foreach (var linkInstance in linkedInstances)
                {
                    var linkDocument = linkInstance.GetLinkDocument();
                    var transform = linkInstance.GetTotalTransform();

                    var roomCollect = new FilteredElementCollector(linkDocument);
                    roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
                    Room room = null;
                    m_rooms = new List<Room>();

                    foreach (var elem in roomCollect)
                    {
                        room = elem as Room;
                        if (room != null) m_rooms.Add(room);
                    }
                }

            return m_rooms;
        }
    }
}