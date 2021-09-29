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

        public RoomsData(ExternalCommandData commandData)
        {
            m_revit = commandData.Application;

            // get all the rooms 
            GetAllRoomsAndTags();

        }

        private void GetAllRoomsAndTags()
        {
            throw new NotImplementedException();
        }
    }
}
