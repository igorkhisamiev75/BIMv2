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

namespace DelFiltersWPF
{
    /// <summary>
    /// Логика взаимодействия для ViewWPF.xaml
    /// </summary>
    public partial class ViewWPF : Window
    {
        private readonly ICollection<Element> filterCollection;
        public ViewWPF(ICollection<Element> elements)
        {
            InitializeComponent();
            filterCollection = elements;

            ListBox.ItemsSource = filterCollection;
            ListBox.DisplayMemberPath = "Name";

            TreeView.SelectedValuePath = filterCollection.ToString();
            TreeView.ItemsSource = filterCollection;
            TreeView.DisplayMemberPath = "Name";

        }
    }
}
