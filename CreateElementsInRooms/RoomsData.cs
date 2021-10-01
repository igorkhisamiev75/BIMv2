using System;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
using System.Linq;

namespace CreateElementsInRooms
{
    public class RoomsData
    {
        private UIApplication m_revit;
        List<Room> m_rooms = new List<Room>();

        public ReadOnlyCollection<Room> Rooms
        {
            get
            {
                return new ReadOnlyCollection<Room>(m_rooms);
            }
        }
        public RoomsData(ExternalCommandData commandData)
        {
            m_revit = commandData.Application;

            // get all the rooms 
            GetAllRoomsLinked(m_revit.ActiveUIDocument.Document);

        }

        //private void GetAllRooms()
        //{
        //    // get the active document 
        //    Document document = m_revit.ActiveUIDocument.Document;
        //    RoomFilter roomFilter = new RoomFilter();
        //}
        
        public List<Room> GetAllRoomsLinked(Document document)
        {
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
    }
}
