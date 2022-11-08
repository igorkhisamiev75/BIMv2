using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB.Architecture;
using System.Windows.Controls;

namespace CreateFinishWallinRooms
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {
        public List<String> checkRooms = new List<String>();        
        public List<String> roomCollection;

        //public RoutedEventHandler routEvent;
        public string whatsButton;
        public bool roomBoording;

        public ViewWPF(List<String> rooms, IList<WallType> typeWallCollection)
        {
            InitializeComponent();
            roomCollection=rooms;

            ListBox.ItemsSource = rooms;
            ListBox.DisplayMemberPath = "";

            ComboBox.ItemsSource = typeWallCollection;
            ComboBox.SelectedIndex = 0; 
            ComboBox.DisplayMemberPath = "Name";

            //var g=chekRoomBoarding.GetValue;
            
            
        }

        private void cansel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //public bool GetChekRoomBoarding() => chekRoomBoarding;

        public void finish1_Click(object sender, RoutedEventArgs e, bool chekRoomBoarding)
        {
            whatsButton = "отделкаВыборомПомещения";
            roomBoording = chekRoomBoarding;
            Close();
        }

        private void finishAll_Click(object sender, RoutedEventArgs e)
        {
            whatsButton = "отделкаПоСпеке";
            foreach (var room in roomCollection)
            {
                foreach (var variable in ListBox.SelectedItems)
                {
                    if (room.GetHashCode() == variable.GetHashCode())
                    {
                        checkRooms.Add(room);
                        //MessageBox.Show("Выбрано" + element.Name);

                    }
                }
            }
            MessageBox.Show("Выбрано " + checkRooms.Count + " комнат для отделки");
            Close();
        }

        private void finishDel_Click(object sender, RoutedEventArgs e)
        {
            whatsButton = "удалитьОтделку";
            Close();
        }

        private void finish1_Click(object sender, RoutedEventArgs e)
        {
            whatsButton = "отделкаВыборомПомещения";
            
            Close();
        }
    }
}
