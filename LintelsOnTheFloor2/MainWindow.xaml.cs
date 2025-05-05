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
        public MainWindow(List<Level> levelCollection, ICollection<Element> lintelsCollection)
        {
            InitializeComponent();

            cmbLevels.ItemsSource = levelCollection;
            cmbLevels.SelectedIndex = 0;
            cmbLevels.DisplayMemberPath = "Name";

            cmbLintelTypes.ItemsSource = lintelsCollection;
            cmbLintelTypes.SelectedIndex = 0;
            cmbLintelTypes.DisplayMemberPath = "Name";


        }
    }
}
