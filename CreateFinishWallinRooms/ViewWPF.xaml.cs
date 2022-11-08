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
        
        public ViewWPF(List<Room> elements, IList<WallType> typeWallCollection)
        {
            InitializeComponent();
           
            ListBox.ItemsSource = elements;
            ListBox.DisplayMemberPath = "Name";

            ComboBox.ItemsSource = typeWallCollection;
            ComboBox.SelectedIndex = 0; 
            ComboBox.DisplayMemberPath = "Name";
            
            
            
            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (var element in roomCollection)
            {
                foreach (var variable in ListBox.SelectedItems)
                {
                    if (variable.GetHashCode() == element.GetHashCode())
                    {
                        checkRooms.Add(element);
                        // MessageBox.Show("Выбрано" + element.Name);

                    }
                }
            }
            //foreach (var VARIABLE in ListBox.SelectedItems)
            //{
            //    //ilterCollection.Contains(VARIABLE)
            //    MessageBox.Show("Выбрано" + VARIABLE.GetHashCode());

            //}
            ////MessageBox.Show("Selected filters removed");
            ///
            //MessageBox.Show("Выбрано " + checkRooms.Count + " фильтров для удаления");
            Close();
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
            foreach (var element in roomCollection)
            {
                foreach (var variable in ListBox.SelectedItems)
                {
                    if (variable.GetHashCode() == element.GetHashCode())
                    {
                        checkRooms.Add(element);
                        // MessageBox.Show("Выбрано" + element.Name);

                    }
                }
            }
            //foreach (var VARIABLE in ListBox.SelectedItems)
            //{
            //    //ilterCollection.Contains(VARIABLE)
            //    MessageBox.Show("Выбрано" + VARIABLE.GetHashCode());

            //}
            ////MessageBox.Show("Selected filters removed");
            ///
            //MessageBox.Show("Выбрано " + checkRooms.Count + " комнат для отделки");
            Close();
        }

        private void finishDel_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
