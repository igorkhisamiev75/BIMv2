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

namespace CreateElementsInRooms
{
    public class RoomsData
    {
        private UIApplication m_revit;
        List<Room> m_rooms = new List<Room>();

        public RoomsData(ExternalCommandData commandData)
        {
            m_revit = commandData.Application;

            // get all the rooms 
            GetAllRoomsLinked(commandData.Application.ActiveUIDocument.Document);

        }

        private void GetAllRooms()
        {
            // get the active document 
            Document document = m_revit.ActiveUIDocument.Document;
            RoomFilter roomFilter = new RoomFilter();
        }
        
        public List<Room> GetAllRoomsLinked(Document document)
        {
            FilteredElementCollector roomCollect = new FilteredElementCollector(document);
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

            return m_rooms;
        }
    }
}
