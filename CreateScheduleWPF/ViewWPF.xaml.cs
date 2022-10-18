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
        public List<Element> checkLevels = new List<Element>();
        public List<string> checkSection = new List<string>();

        private readonly ICollection<Element> filterCollection;
        private readonly ICollection<string> filterCollection2;

        public ViewWPF(ICollection<Element> elements, ICollection<string> elements2)
        {
            InitializeComponent();
            filterCollection = elements;
            filterCollection2 = elements2;

            ListBox.ItemsSource = filterCollection;
            ListBox.DisplayMemberPath = "Name";

            ListBox2.ItemsSource = filterCollection2;
            //ListBox2.DisplayMemberPath = filterCollection2.ToString();

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
                        checkLevels.Add(element);
                        // MessageBox.Show("Выбрано" + element.Name);

                    }
                }
            }

            foreach (var element in filterCollection2)
            {
                foreach (var variable in ListBox2.SelectedItems)
                {
                    if (variable.GetHashCode() == element.GetHashCode())
                    {
                        checkSection.Add(element);
                        
                    }
                }
            }

            MessageBox.Show("Выбрано " + checkLevels.Count + " уровне для создания спецификаций");
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}