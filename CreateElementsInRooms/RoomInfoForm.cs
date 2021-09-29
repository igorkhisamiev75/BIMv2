using System;
using System.Data;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.Drawing;
using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Form = System.Windows.Forms.Form;

namespace CreateElementsInRooms
{
    public partial class roomsInformationForm : Form
    {
        RoomsData m_data; // Room's data for current active document

        private ExternalCommandData m_commandData;
        public roomsInformationForm(RoomsData data, ExternalCommandData commandData)
        {
            InitializeComponent();
        }
    }
}
