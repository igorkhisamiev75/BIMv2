using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
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

namespace DelFiltersWPF
{
    /// <summary>
    /// Логика взаимодействия для ViewWPF.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {

        //public readonly DeletedFilter m_data_filters;  // Filters data for current active document
        public List<Element> checkFilters=new List<Element>();

        private readonly ICollection<Element> filterCollection;
        public ViewWPF(ICollection<Element> elements)
        {
            InitializeComponent();
            filterCollection = elements;
            
            ListBox.ItemsSource = filterCollection;
            ListBox.DisplayMemberPath = "Name";

            //TreeView.SelectedValuePath = filterCollection.ToString();
            //TreeView.ItemsSource = filterCollection;
            //TreeView.DisplayMemberPath = "Name";

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (var element in filterCollection)
            {
                foreach (var variable in ListBox.SelectedItems)
                {
                    if (variable.GetHashCode() == element.GetHashCode())
                    {
                        checkFilters.Add(element);
                        MessageBox.Show("Выбрано" + element.Name);

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
            MessageBox.Show("Выбрано " + checkFilters.Count+" фильтров для удаления");
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}