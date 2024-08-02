using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows;


namespace BIMv2.CheckRooms
{
    /// <summary>
    /// Логика взаимодействия для CheckRoomsSelectLink.xaml
    /// </summary>
    public partial class CheckRoomsSelectLink : Window
    {
        public RevitLinkInstance LinkInstance;
        public CheckRoomsSelectLink(IEnumerable<RevitLinkInstance> linkedInstances, IList<Element> roomsCurrentProject)
        {
            InitializeComponent();

            cbNameLinks.ItemsSource = linkedInstances;
            cbNameLinks.DisplayMemberPath= "Name";
            cbNameLinks.SelectedIndex = 0;

            int currentRoom=roomsCurrentProject.Count;

            tbRoomsinCurrent.Text = currentRoom.ToString();

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnChech_Click(object sender, RoutedEventArgs e)
        {
            LinkInstance=cbNameLinks.SelectedItem as RevitLinkInstance;
            Close();
        }
    }
}
