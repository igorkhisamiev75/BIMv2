using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using System.Windows;
using System.Windows.Controls;

namespace BIMv2Buttons.CreateFinishWallinRooms
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

        public ViewWPF(List<String> rooms, IList<WallType> typeWallCollection)
        {
            InitializeComponent();
            roomCollection=rooms;

            ListBox.ItemsSource = rooms;
            ListBox.DisplayMemberPath = "";

            ComboBox.ItemsSource = typeWallCollection;
            ComboBox.SelectedIndex = 5; 
            ComboBox.DisplayMemberPath = "Name";

        }

        private void cansel_Click(object sender, RoutedEventArgs e)
        {
            //typeWallCollectionSelected = ComboBox.SelectedValue as WallType;
            //MessageBox.Show("Выбрано " + typeWallCollectionSelected.ToString());

            //var nameWallType = typeWallCollectionSelected.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).AsString();
            //MessageBox.Show("Выбрано " + nameWallType);

            Close();
        }

        

        public void finish1_Click(object sender, RoutedEventArgs e)
        {
            typeWallCollectionSelected = ComboBox.SelectedValue as WallType;

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
            whatsButton = "удалитьОтделку";
            Close();
        }

    }
}
