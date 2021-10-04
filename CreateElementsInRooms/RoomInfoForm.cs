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
using Autodesk.Revit.UI.Selection;

namespace CreateElementsInRooms
{
    public partial class roomsInformationForm : Form
    {
        RoomsData m_data; // Room's data for current active document
        private Application _app;
        private Document _doc;
        private int i = 0;
        //var uiApp = m_commandData.Application;
        //var uiDoc = uiApp.ActiveUIDocument;
        ////_app = uiApp.Application;
        //_doc = uiDoc.Document;

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
            Document document = m_commandData.Application.ActiveUIDocument.Document;

        }

        private void roomsInformationForm_Load(object sender, EventArgs e)
        {
            this.DisplayRooms(m_data.Rooms);
        }

        private void DisplayRooms(ReadOnlyCollection<Room> roomList)
        {
            //Document document = m_commandData.Application.ActiveUIDocument.Document;
            //int i = 1;

            List<string> roomName = new List<string>(){};

            foreach(var r in roomList)
            {
                string fullname = r.Name.ToString();
                string justname = "";
                for (int i = 0; i < fullname.Length; i++)
                {
                    if (!Char.IsDigit(fullname[i]) && !(fullname[i]).Equals('.') && !(fullname[i]).Equals(','))
                                       
                    {
                        justname += fullname[i];
                    }
                }
                //string justname = fullname.Substring(0, fullname.IndexOf(r.Name));
                //string[] subs = r.Name.Split(' ');
                roomName.Add(justname);
            }

            List<string> noRepeatRoom = roomName.Distinct().ToList();

            foreach (var tmpRoom in noRepeatRoom)
            {
                ListViewItem tmpItem = new ListViewItem();

                tmpItem.SubItems.Add(tmpRoom);       //display room name.

                dataGridView1.Rows.Add(tmpRoom);
                //dataGridView1.Rows[i - 1].Cells[1].Value = tmpRoom.Name;
               
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

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnCeiling_Click(object sender, EventArgs e)
        {
            this.Close();
            
            var uiApp = m_commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _doc = uiDoc.Document;

            try
            {
                var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";

                var elem = _doc.GetElement(earthId);

                //Reference r = uiDoc.Selection.PickObject(ObjectType.Element, "Выбрать элемент для размещения во всех помещениях");
                //Element elem = uiDoc.Document.GetElement(r);

                m_data.CreateAnyElementInLinkedFiles(elem, _doc);

                TaskDialog.Show("Complite", $"Размещено {i} элементов");
                //t.Commit();
                //this.Close();
            }

            catch (Exception ex)
            {
                TaskDialog.Show("Revit", ex.Message);
            }
        }

        private void btnFloor_Click(object sender, EventArgs e)
        {
            //аналогично с потолком
        }
    }
}
