using Autodesk.Revit.DB;

using System.Collections.Generic;
using System.Linq;
using System.Windows;

//using static RenameSheetsWPF.RenameSheets;

namespace RenameSheetsWPF
{
    /// <summary>
    /// Логика взаимодействия для ViewWPF.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {

        //public readonly DeletedFilter m_data_filters;  // Filters data for current active document
        public List<Element> checkFilters = new List<Element>();

        public ICollection<Element> filterCollection;

        public ICollection<Element> selectedSheets;

        public ViewWPF(IEnumerable<SheetInProject> sheetInProjectCollection)
        {
            InitializeComponent();

            sheetGreed.ItemsSource = sheetInProjectCollection;

            
        }

       

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SheetInProject g = (SheetInProject)sheetGreed.SelectedItem;
            MessageBox.Show("Выбрано " + g.nameSheet);
            
            //foreach (var element in filterCollection)
            //{
            //    foreach (var variable in ListBox.SelectedItems)
            //    {
            //        if (variable.GetHashCode() == element.GetHashCode())
            //        {
            //            checkFilters.Add(element);
            //            // MessageBox.Show("Выбрано" + element.Name);

            //        }
            //    }
            //}

            //MessageBox.Show("Выбрано " + checkFilters.Count + " листов для перименования");
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

       

       

    }
}