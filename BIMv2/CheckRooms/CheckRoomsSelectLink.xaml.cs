using Autodesk.Revit.DB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BIMv2.CheckRooms
{
    /// <summary>
    /// Логика взаимодействия для CheckRoomsSelectLink.xaml
    /// </summary>
    public partial class CheckRoomsSelectLink : Window
    {
        public RevitLinkInstance LinkInstance;
        public CheckRoomsSelectLink(IEnumerable<RevitLinkInstance> linkedInstances)
        {
            InitializeComponent();

            cbNameLinks.ItemsSource = linkedInstances;
            cbNameLinks.DisplayMemberPath= "Name";
            cbNameLinks.SelectedIndex = 0;

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
