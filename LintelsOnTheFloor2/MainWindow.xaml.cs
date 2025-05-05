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

namespace LintelsOnTheFloor2
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Level selectedLevel;
        public string selectedElement;
        public string selectedElement2;
        public bool doorCheck;
        public bool windowCheck;
        public string whatsButton;

        public MainWindow(List<Level> levelCollection, ICollection<Element> lintelsCollection)
        {
            InitializeComponent();

            cmbLevels.ItemsSource = levelCollection;
            cmbLevels.SelectedIndex = 0;
            cmbLevels.DisplayMemberPath = "Name";
            
        }

        private void createLintels_Click(object sender, RoutedEventArgs e)
        {
            selectedLevel = cmbLevels.SelectedItem as Level;
            selectedElement= txtName.Text;
            selectedElement2= txtName2.Text;

            whatsButton = "все";

            //check box door
            if (cbDoors.IsChecked.GetValueOrDefault())
            {
                doorCheck = true;
            }
            else
            {
                doorCheck = false;
            }

            //check box window
            if (cbWindows.IsChecked.GetValueOrDefault())
            {
                windowCheck = true;
            }
            else
            {
                windowCheck = false;
            }



            Close();
        }

        private void createLintel_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Не жми пока сюда ");

            Close();
        }

        private void btnDelAll_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("И сюда тоже");

            Close();

        }
    }
}
