using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Form = System.Windows.Forms.Form;
using Point = System.Drawing.Point;

namespace CreateElementsInRooms
{
    public partial class roomsInformationForm : Form
    {
        private UIApplication m_revit;

        public readonly RoomsData m_data; // Room's data for current active document

        //private Application _app;
        //private Document _doc;
        private int i = 0;
        //var uiApp = m_commandData.Application;
        //var uiDoc = uiApp.ActiveUIDocument;
        ////_app = uiApp.Application;
        //_doc = uiDoc.Document;

        private readonly ExternalCommandData m_commandData;

        public roomsInformationForm(RoomsData data, ExternalCommandData commandData)
        {
            m_revit = commandData.Application;
            m_data = data;
            m_commandData = commandData;
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            var document = m_commandData.Application.ActiveUIDocument.Document;
        }

        private void roomsInformationForm_Load(object sender, EventArgs e)
        {
            DisplayRooms(m_data.Rooms);
        }

        private void DisplayRooms(ReadOnlyCollection<Room> roomList)
        {
            //Document document = m_commandData.Application.ActiveUIDocument.Document;
            //int i = 1;

            var roomName = new List<string>();

            foreach (var r in roomList)
            {
                var fullname = r.Name;
                var justname = "";
                for (var i = 0; i < fullname.Length; i++)
                    if (!char.IsDigit(fullname[i]) && !fullname[i].Equals('.') && !fullname[i].Equals(','))
                        justname += fullname[i];
                //string justname = fullname.Substring(0, fullname.IndexOf(r.Name));
                //string[] subs = r.Name.Split(' ');
                roomName.Add(justname);
            }

            var noRepeatRoom = roomName.Distinct().ToList();

            foreach (var tmpRoom in noRepeatRoom)
            {
                var tmpItem = new ListViewItem();

                tmpItem.SubItems.Add(tmpRoom); //display room name.

                dataGridView1.Rows.Add(tmpRoom);
                //dataGridView1.Rows[i - 1].Cells[1].Value = tmpRoom.Name;
            }
        }

        private Point lastPoint;

        private void roomsInformationForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Left += e.X - lastPoint.X;
                Top += e.Y - lastPoint.Y;
            }
        }

        private void roomsInformationForm_MouseDown(object sender, MouseEventArgs e)
        {
            lastPoint = new Point(e.X, e.Y);
        }

        private void roomsInformationForm_MouseEnter(object sender, EventArgs e)
        {
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnCeiling_Click(object sender, EventArgs e)
        {
            
            //this.Dispose();
            UIDocument uidoc = m_commandData.Application.ActiveUIDocument;

            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            
            //IList<Reference> refs = sel.PickObjects(ObjectType.Element, "Please select some elements");
            //TaskDialog.Show("R", $"{refs}");
            //this.Show();

            var rowsCount = dataGridView1.Rows.Count;

            var chekRoom = new List<int>();
            
            for (var j = 0; j < rowsCount; j++)
            {
                var chek = dataGridView1.Rows[j].Cells[1].Value;

                if (chek != null)
                    if (chek == "1")
                        chekRoom.Add(j);
            }

            var checListRoom = new List<string>();

            foreach (var index in chekRoom)
            {
                var nameRoomsValue = dataGridView1.Rows[index].Cells[0].Value.ToString();

                checListRoom.Add(nameRoomsValue);
            }

            this.Close();
            //this.Hide();
            m_data.CreateAnyElementInLinkedFiles(checListRoom);

            //TaskDialog.Show("R", "разместили");
            this.Close();
        }


        private void btnFloor_Click(object sender, EventArgs e)
        {
            //аналогично с потолком
        }

        private void dataGridView1_MultiSelectChanged(object sender, EventArgs e)
        {
           
        }
    }
}