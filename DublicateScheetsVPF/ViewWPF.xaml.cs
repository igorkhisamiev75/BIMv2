using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows;


namespace DublicateScheetsVPF
{
    /// <summary>
    /// Логика взаимодействия для ViewWPF.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {

        //public readonly DeletedFilter m_data_filters;  // Filters data for current active document
        public List<ViewSheet> checkFilters=new List<ViewSheet>();

        private readonly ICollection<ViewSheet> filterCollection;
        public ViewWPF(ICollection<ViewSheet> elements)
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
                       // MessageBox.Show("Выбрано" + element.Name);

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
            MessageBox.Show("Selected " + checkFilters.Count+" sheets for copy");
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("What are you doing??? ☼");
            Close();
        }
    }
}