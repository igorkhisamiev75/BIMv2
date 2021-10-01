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
            m_data = data;
            m_commandData = commandData;
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            //Document document = m_commandData.Application.ActiveUIDocument.Document;


        }

        private void roomsInformationForm_Load(object sender, EventArgs e)
        {
            this.DisplayRooms(m_data.Rooms);
        }

        private void DisplayRooms(ReadOnlyCollection<Room> roomList)
        {
            Document document = m_commandData.Application.ActiveUIDocument.Document;
            int i = 1;

            foreach (Room tmpRoom in roomList)
            {
                // make sure the room has Level, that's it locates at level.
                if (tmpRoom.Document.GetElement(tmpRoom.LevelId) == null)
                {
                    continue;
                }

                // create a list view Item
                ListViewItem tmpItem = new ListViewItem();
                
                tmpItem.SubItems.Add(tmpRoom.Name);       //display room name.

                dataGridView1.Rows.Add((tmpRoom.Document.GetElement(tmpRoom.LevelId) as Level).Name);
                dataGridView1.Rows[i - 1].Cells[1].Value = tmpRoom.Name;
                

                i++;


            }
        }

        System.Drawing.Point lastPoint;
        private void roomsInformationForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - lastPoint.X;
                this.Top += e.Y - lastPoint.Y;
            }
        }

        private void roomsInformationForm_MouseDown(object sender, MouseEventArgs e)
        {
            lastPoint = new System.Drawing.Point(e.X, e.Y);
        }

        private void roomsInformationForm_MouseEnter(object sender, EventArgs e)
        {

        }
    }
}
