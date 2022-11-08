using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB.Architecture;

namespace CreateFinishWallinRooms
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {
        public List<Room> checkRooms = new List<Room>();
        
        public List<Room> roomCollection;
        
        public ViewWPF(List<Room> rooms, IList<WallType> typeWallCollection)
        {
            InitializeComponent();
            roomCollection=rooms;

            ListBox.ItemsSource = rooms;
            ListBox.DisplayMemberPath = "Name";

            ComboBox.ItemsSource = typeWallCollection;
            ComboBox.SelectedIndex = 0; 
            ComboBox.DisplayMemberPath = "Name";
            
            
            
            
        }

        private void cansel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void finish1_Click(object sender, RoutedEventArgs e)
        {
           checkRooms = null;
           Close();
        }

        private void finishAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (Room element in roomCollection)
            {
                foreach (var variable in ListBox.SelectedItems)
                {
                    if (element.GetHashCode() == variable.GetHashCode())
                    {
                        checkRooms.Add(element);
                        MessageBox.Show("Выбрано" + element.Name);

                    }
                }
            }
            MessageBox.Show("Выбрано " + checkRooms.Count + " комнат для отделки");
            Close();
        }

        private void finishDel_Click(object sender, RoutedEventArgs e)
        {
            //checkRooms.Add();
            Close();
        }
    }
}
