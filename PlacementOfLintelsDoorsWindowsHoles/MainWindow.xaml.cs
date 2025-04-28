using Autodesk.Revit.DB;

using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace PlacementOfLintelsDoorsWindowsHoles
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ICollectionView _doorsView;
        private List<DoorData> _allDoors;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void SetDoorData(List<DoorData> doors)
        {
            _allDoors = doors;
            _doorsView = CollectionViewSource.GetDefaultView(_allDoors);
            DoorsDataGrid.ItemsSource = _doorsView;
            
            // Initialize filters
            InitializeFilters();
        }

        public void InitializeFilters()
        {
            // Level filter
            var levels = _allDoors.Select(d => d.LevelName).Distinct().OrderBy(l => l);
            LevelFilter.Items.Add(new KeyValuePair<string, string>("All Levels", null));
            foreach (var level in levels)
            {
                LevelFilter.Items.Add(new KeyValuePair<string, string>(level, level));
            }
            LevelFilter.SelectedIndex = 0;

            // Type filter
            var types = _allDoors.Select(d => d.TypeName).Distinct().OrderBy(t => t);
            TypeFilter.Items.Add(new KeyValuePair<string, string>("All Types", null));
            foreach (var type in types)
            {
                TypeFilter.Items.Add(new KeyValuePair<string, string>(type, type));
            }
            TypeFilter.SelectedIndex = 0;

            // Room filter
            var rooms = _allDoors.Select(d => d.RoomName).Distinct().OrderBy(r => r);
            RoomFilter.Items.Add(new KeyValuePair<string, string>("All Rooms", null));
            foreach (var room in rooms)
            {
                RoomFilter.Items.Add(new KeyValuePair<string, string>(room, room));
            }
            RoomFilter.SelectedIndex = 0;
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_doorsView == null) return;

            _doorsView.Filter = item =>
            {
                var door = item as DoorData;
                if (door == null) return false;

                // Level filter
                var selectedLevel = (KeyValuePair<string, string>)LevelFilter.SelectedItem;
                if (selectedLevel.Value != null && door.LevelName != selectedLevel.Value)
                    return false;

                // Type filter
                var selectedType = (KeyValuePair<string, string>)TypeFilter.SelectedItem;
                if (selectedType.Value != null && door.TypeName != selectedType.Value)
                    return false;

                // Room filter
                var selectedRoom = (KeyValuePair<string, string>)RoomFilter.SelectedItem;
                if (selectedRoom.Value != null && door.RoomName != selectedRoom.Value)
                    return false;

                // Mark filter
                if (!string.IsNullOrWhiteSpace(MarkFilter.Text))
                {
                    if (!door.Mark.Contains(MarkFilter.Text))
                        return false;
                }

                return true;
            };
        }

        private void OnTextFilterChanged(object sender, TextChangedEventArgs e)
        {
            if (_doorsView != null)
                _doorsView.Refresh();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            LevelFilter.SelectedIndex = 0;
            TypeFilter.SelectedIndex = 0;
            RoomFilter.SelectedIndex = 0;
            MarkFilter.Text = string.Empty;
            _doorsView.Refresh();
        }

        public void DoorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedDoors = DoorsDataGrid.SelectedItems.Cast<DoorData>().ToList();
            var ids = selectedDoors.Select(d => new ElementId(int.Parse(d.Id))).ToList();

            // You'll need to pass the Revit UIApplication to your window
            _uiApp.ActiveUIDocument.Selection.SetElementIds(ids);
        }
    }
}
