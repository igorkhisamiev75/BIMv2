using Autodesk.Revit.DB;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

using static RenameSheetsWPF.RenameSheets;

namespace RenameSheetsWPF
{
    /// <summary>
    /// Логика взаимодействия для ViewWPF.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {

        //public readonly DeletedFilter m_data_filters;  // Filters data for current active document
        public List<Element> checkFilters = new List<Element>();

        private readonly ICollection<Element> filterCollection;

      


        public ViewWPF( ICollection<SheetInProject> sheetInProjectCollection)
        {
            InitializeComponent();

            var newList = sheetInProjectCollection
                .GroupBy(x => new { x.stadiy, x.albom, x.numberSheet, x.nameSheet, x.idSheet })
                .Select(y => new SheetInProject()
            {
                stadiy = y.Key.stadiy,
                    albom = y.Key.albom,
                    numberSheet = y.Key.numberSheet,
                    nameSheet = y.Key.nameSheet,
                    idSheet = y.Key.idSheet,

                }); ;
            
           

            //sheetGreed.ItemsSource = sheetInProjectCollection;


            sheetGreed.ItemsSource = sheetInProjectCollection;



            //treeView1.Name= elements2.Count.ToString();

            //emp2WorkDays = (System.Windows.Controls.TreeViewItem)elements;

            //filterCollection = elements;

            //ListBox.ItemsSource = filterCollection;
            //ListBox.DisplayMemberPath = "Name";



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
            MessageBox.Show("Выбрано " + checkFilters.Count + " фильтров для удаления");
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

       

       

    }
}