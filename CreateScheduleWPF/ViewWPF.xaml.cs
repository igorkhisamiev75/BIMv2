using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows;


namespace CreateScheduleWPF
{
    /// <summary>
    /// Логика взаимодействия для ViewWPF.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {

        //public readonly DeletedFilter m_data_filters;  // Filters data for current active document
        public List<Element> checkFilters = new List<Element>();

        private readonly ICollection<Element> filterCollection;
        private readonly ICollection<Element> filterCollection2;

        public ViewWPF(ICollection<Element> elements, ICollection<Element> elements2)
        {
            InitializeComponent();
            filterCollection = elements;
            filterCollection2 = elements2;

            ListBox.ItemsSource = filterCollection;
            ListBox.DisplayMemberPath = "Name";

            ListBox2.ItemsSource = filterCollection2;
            ListBox2.DisplayMemberPath = "Name";

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
                        // MessageBox.Show("Выбрано" + element.Name);

                    }
                }
            }
          
            MessageBox.Show("Выбрано " + checkFilters.Count + " фильтров для удаления");
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}