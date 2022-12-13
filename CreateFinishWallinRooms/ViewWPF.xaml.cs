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
        public WallType typeWallCollectionSelected;
        public Level selectedLevel;
        public string heighWallUser;

        public ViewWPF(List<String> rooms, IList<WallType> typeWallCollection, List<Level> levels, IList<FloorType> typeFloorCollection)
        {
            InitializeComponent();
            roomCollection=rooms;

            ListBox.ItemsSource = rooms;
            ListBox.DisplayMemberPath = "";

            ComboBox.ItemsSource = typeWallCollection;
            ComboBox.SelectedIndex = 5; 
            ComboBox.DisplayMemberPath = "Name";

            ComboBoxLevel.ItemsSource = levels;
            ComboBoxLevel.DisplayMemberPath = "Name";
            ComboBoxLevel.SelectedIndex = 0;

            ComboBoxFloor.ItemsSource = typeFloorCollection;
            ComboBoxFloor.SelectedIndex = 2;
            ComboBoxFloor.DisplayMemberPath = "Name";

        }

        private void cansel_Click(object sender, RoutedEventArgs e)
        {
            selectedLevel = ComboBoxLevel.SelectedItem as Level;
            typeWallCollectionSelected = ComboBox.SelectedValue as WallType;
            heighWallUser= heightWallNotRooms.Text;
            //typeWallCollectionSelected = ComboBox.SelectedValue as WallType;
            //MessageBox.Show("Выбрано " + typeWallCollectionSelected.ToString());

            //var nameWallType = typeWallCollectionSelected.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).AsString();
            //MessageBox.Show("Выбрано " + nameWallType);

            Close();
        }

        

        public void finish1_Click(object sender, RoutedEventArgs e)
        {
            selectedLevel = ComboBoxLevel.SelectedItem as Level;
            typeWallCollectionSelected = ComboBox.SelectedValue as WallType;
            heighWallUser = heightWallNotRooms.Text;

            whatsButton = "отделкаВыборомПомещения";

            if (chekRoomBoarding.IsChecked.GetValueOrDefault())
            {
                roomBoording = true;
            }
            else
            {
                roomBoording = false;
            }

            Close();
        }

        private void finishAll_Click(object sender, RoutedEventArgs e)
        {
            heighWallUser = heightWallNotRooms.Text;
            selectedLevel =ComboBoxLevel.SelectedItem as Level;

            typeWallCollectionSelected = ComboBox.SelectedValue as WallType;
            whatsButton = "отделкаПоСпеке";

            if (chekRoomBoarding.IsChecked.GetValueOrDefault())
            {
                roomBoording = true;
            }
            else
            {
                roomBoording = false;
            }

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
            //MessageBox.Show("Выбрано " + checkRooms.Count + " комнат для отделки");
            Close();
        }

        private void finishDel_Click(object sender, RoutedEventArgs e)
        {
            if (ListBox.SelectedIndex==-1)
            {
                ListBox.SelectAll();

            }
            else 
            {
                ListBox.UnselectAll();
            }    

        }

        private void floorAll_Click(object sender, RoutedEventArgs e)
        {
            heighWallUser = heightWallNotRooms.Text;
            selectedLevel = ComboBoxLevel.SelectedItem as Level;

            typeWallCollectionSelected = ComboBox.SelectedValue as WallType;
            whatsButton = "отделкаПоСпекеПолы";

            if (chekRoomBoarding.IsChecked.GetValueOrDefault())
            {
                roomBoording = true;
            }
            else
            {
                roomBoording = false;
            }

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
            //MessageBox.Show("Выбрано " + checkRooms.Count + " комнат для отделки");
            Close();

        }

        private void finishFloorInOneRoom_Click(object sender, RoutedEventArgs e)
        {
            selectedLevel = ComboBoxLevel.SelectedItem as Level;
            typeWallCollectionSelected = ComboBox.SelectedValue as WallType;
            heighWallUser = heightWallNotRooms.Text;

            whatsButton = "полыВыборомПомещения";

            if (chekRoomBoarding.IsChecked.GetValueOrDefault())
            {
                roomBoording = true;
            }
            else
            {
                roomBoording = false;
            }

            Close();

        }
    }
}
