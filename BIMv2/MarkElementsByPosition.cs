#region Namespaces

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

#endregion

namespace RevitAddIn2BIMRU.Commands.AN
{
    [Transaction(TransactionMode.Manual)]
    public class MarkElementsWPF : IExternalCommand
    {
        private Document _doc;
        private UIDocument _uiDoc;
        private const string POSITION_PARAMETER_NAME = "ADSK_Позиция";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

            try
            {
                // Выбор элементов
                TaskDialog.Show("Выбор элементов",
                    "Выберите элементы для маркировки. После выбора нажмите Finish на панели или ESC для отмены.");

                IList<Reference> references;
                try
                {
                    references = _uiDoc.Selection.PickObjects(
                        ObjectType.Element,
                        "Выберите элементы для маркировки");
                }
                catch
                {
                    return Result.Cancelled;
                }

                if (references == null || references.Count == 0)
                    return Result.Cancelled;

                List<Element> selectedElements = references
                    .Select(r => _doc.GetElement(r))
                    .Where(e => e != null && HasPositionParameter(e))
                    .ToList();

                if (selectedElements.Count == 0)
                {
                    TaskDialog.Show("Ошибка",
                        "Среди выбранных элементов нет ни одного с параметром ADSK_Позиция");
                    return Result.Cancelled;
                }

                // Показываем WPF окно
                var dialog = new MarkingWindow(selectedElements.Count);
                if (dialog.ShowDialog() != true)
                    return Result.Cancelled;

                // Сортируем элементы
                List<Element> sortedElements = SortElements(
                    selectedElements,
                    dialog.SelectedDirection,
                    dialog.SelectedOrder);

                // Применяем маркировку
                using (Transaction tx = new Transaction(_doc))
                {
                    tx.Start("Маркировка элементов");

                    for (int i = 0; i < sortedElements.Count; i++)
                    {
                        Element element = sortedElements[i];
                        Parameter param = element.LookupParameter(POSITION_PARAMETER_NAME);

                        if (param != null && !param.IsReadOnly)
                        {
                            string markValue = (dialog.StartNumber + i).ToString();

                            if (param.StorageType == StorageType.String)
                            {
                                param.Set(markValue);
                            }
                            else if (param.StorageType == StorageType.Integer)
                            {
                                if (int.TryParse(markValue, out int intValue))
                                {
                                    param.Set(intValue);
                                }
                            }
                        }
                    }

                    tx.Commit();
                }

                TaskDialog.Show("Готово",
                    $"Успешно промаркировано {sortedElements.Count} элементов\n" +
                    $"Начальный номер: {dialog.StartNumber}\n" +
                    $"Направление: {GetDirectionDescription(dialog.SelectedDirection, dialog.SelectedOrder)}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }
        }

        private bool HasPositionParameter(Element element)
        {
            Parameter param = element.LookupParameter(POSITION_PARAMETER_NAME);
            return param != null && !param.IsReadOnly;
        }

        private List<Element> SortElements(List<Element> elements, SortDirection direction, SortOrder order)
        {
            Func<Element, double> keySelector = direction == SortDirection.ByX
                ? e => GetElementCenter(e).X
                : e => GetElementCenter(e).Y;

            return order == SortOrder.Ascending
                ? elements.OrderBy(keySelector).ToList()
                : elements.OrderByDescending(keySelector).ToList();
        }

        private XYZ GetElementCenter(Element element)
        {
            BoundingBoxXYZ bb = element.get_BoundingBox(null);
            return bb != null ? (bb.Min + bb.Max) * 0.5 : XYZ.Zero;
        }

        private string GetDirectionDescription(SortDirection direction, SortOrder order)
        {
            if (direction == SortDirection.ByX)
            {
                return order == SortOrder.Ascending ? "Слева направо (по оси X)" : "Справа налево (по оси X)";
            }
            else
            {
                return order == SortOrder.Ascending ? "Снизу вверх (по оси Y)" : "Сверху вниз (по оси Y)";
            }
        }

        public enum SortDirection { ByX, ByY }
        public enum SortOrder { Ascending, Descending }
    }

    // WPF Window XAML (MarkingWindow.xaml)
    /*
    <Window x:Class="RevitAddIn2BIMRU.Commands.AN.MarkingWindow"
            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            Title="Маркировка элементов" 
            Height="300" Width="400"
            WindowStartupLocation="CenterScreen"
            ResizeMode="NoResize">
        <Grid Margin="10">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            
            <TextBlock Grid.Row="0" Text="Настройки маркировки" FontSize="16" FontWeight="Bold" Margin="0,0,0,10"/>
            
            <StackPanel Grid.Row="1" Margin="0,0,0,10">
                <TextBlock Text="Начинаем маркировать с:" Margin="0,0,0,5"/>
                <TextBox x:Name="txtStartNumber" Text="1" Width="100" HorizontalAlignment="Left"/>
            </StackPanel>
            
            <StackPanel Grid.Row="2" Margin="0,0,0,10">
                <TextBlock Text="Направление маркировки:" Margin="0,0,0,5"/>
                <RadioButton x:Name="rbXLeftToRight" Content="По оси X (слева направо)" Margin="5,2" IsChecked="True"/>
                <RadioButton x:Name="rbYBottomToTop" Content="По оси Y (снизу вверх)" Margin="5,2"/>
                <RadioButton x:Name="rbXRightToLeft" Content="По оси X (справа налево)" Margin="5,2"/>
                <RadioButton x:Name="rbYTopToBottom" Content="По оси Y (сверху вниз)" Margin="5,2"/>
            </StackPanel>
            
            <TextBlock Grid.Row="3" Text="{Binding ElementCountText}" Margin="0,0,0,10"/>
            
            <StackPanel Grid.Row="5" Orientation="Horizontal" HorizontalAlignment="Right">
                <Button x:Name="btnOk" Content="ОК" Width="80" Height="25" Margin="0,0,10,0" Click="BtnOk_Click"/>
                <Button x:Name="btnCancel" Content="Отмена" Width="80" Height="25" Click="BtnCancel_Click"/>
            </StackPanel>
        </Grid>
    </Window>
    */

    // WPF Window Code-behind (MarkingWindow.xaml.cs)
    /*
    public partial class MarkingWindow : Window
    {
        public int StartNumber { get; private set; }
        public MarkElementsWPF.SortDirection SelectedDirection { get; private set; }
        public MarkElementsWPF.SortOrder SelectedOrder { get; private set; }
        
        public MarkingWindow(int elementCount)
        {
            InitializeComponent();
            DataContext = this;
            
            // Устанавливаем текст с количеством элементов
            ElementCountText = $"Выбрано элементов для маркировки: {elementCount}";
            
            StartNumber = 1;
            SelectedDirection = MarkElementsWPF.SortDirection.ByX;
            SelectedOrder = MarkElementsWPF.SortOrder.Ascending;
        }
        
        public string ElementCountText { get; set; }
        
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtStartNumber.Text, out int startNumber) || startNumber < 1)
            {
                MessageBox.Show("Введите корректное начальное число (больше 0)", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            StartNumber = startNumber;
            
            // Определяем направление и порядок
            if (rbXLeftToRight.IsChecked == true)
            {
                SelectedDirection = MarkElementsWPF.SortDirection.ByX;
                SelectedOrder = MarkElementsWPF.SortOrder.Ascending;
            }
            else if (rbYBottomToTop.IsChecked == true)
            {
                SelectedDirection = MarkElementsWPF.SortDirection.ByY;
                SelectedOrder = MarkElementsWPF.SortOrder.Ascending;
            }
            else if (rbXRightToLeft.IsChecked == true)
            {
                SelectedDirection = MarkElementsWPF.SortDirection.ByX;
                SelectedOrder = MarkElementsWPF.SortOrder.Descending;
            }
            else if (rbYTopToBottom.IsChecked == true)
            {
                SelectedDirection = MarkElementsWPF.SortDirection.ByY;
                SelectedOrder = MarkElementsWPF.SortOrder.Descending;
            }
            
            DialogResult = true;
            Close();
        }
        
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
    */
}