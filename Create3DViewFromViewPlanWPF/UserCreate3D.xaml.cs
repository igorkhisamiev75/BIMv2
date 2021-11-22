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
using Autodesk.Revit.DB;

namespace Create3DViewFromViewPlanWPF
{
    /// <summary>
    /// Логика взаимодействия для UserCreate3D.xaml
    /// </summary>
    public partial class UserCreate3D : Window
    {
        private IList<ViewPlan> allView;

        public UserCreate3D(IList<ViewPlan> elements)
        {
            InitializeComponent();
            allView = elements;

            //AllView.


            DataGrid2.ItemsSource = allView;
            DataGrid2.DisplayMemberPath = "Name";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
