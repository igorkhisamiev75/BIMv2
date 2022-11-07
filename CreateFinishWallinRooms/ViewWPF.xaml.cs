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

        //public readonly DeletedFilter m_data_filters;  // Filters data for current active document
        public List<Room> checkRooms = new List<Room>();

        private readonly List<Room> roomCollection;
        public ViewWPF(List<Room> elements)
        {
            InitializeComponent();
            roomCollection = elements;

            ListBox.ItemsSource = roomCollection;
            ListBox.DisplayMemberPath = "Name";

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
            MessageBox.Show("Выбрано " + checkRooms.Count + " фильтров для удаления");
            Close();
        }

        private void cansel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void finish1_Click(object sender, RoutedEventArgs e)
        {

        }

        private void finishAll_Click(object sender, RoutedEventArgs e)
        {

        }

        private void finishDel_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
