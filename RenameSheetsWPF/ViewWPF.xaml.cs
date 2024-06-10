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
        public List<SheetInProject> checkSheets = new List<SheetInProject>();

        public ICollection<SheetInProject> filterCollection;

        public ICollection<SheetInProject> selectedSheets;

        public ViewWPF(IEnumerable<SheetInProject> sheetInProjectCollection)
        {
            InitializeComponent();

            filterCollection=sheetInProjectCollection.ToList();

            sheetGreed.ItemsSource = sheetInProjectCollection;

            
        }

       

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //tbName.Text = string.Empty;
            MessageBox.Show("Выбрано " + tbName.Text);
            //SheetInProject g = (SheetInProject)sheetGreed.SelectedItems;

            //var g = sheetGreed.SelectedItems;
            //MessageBox.Show("Выбрано " + g.Count);

            foreach (var element in filterCollection)
            {
                foreach (var variable in sheetGreed.SelectedItems)
                {
                    if (variable.GetHashCode() == element.GetHashCode())
                    {
                        checkSheets.Add(element);
                        // MessageBox.Show("Выбрано" + element.Name);

                    }
                }
            }




            //var dg = sheetGreed.SelectedItems

            //MessageBox.Show("Выбрано " + dg.ToString());

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