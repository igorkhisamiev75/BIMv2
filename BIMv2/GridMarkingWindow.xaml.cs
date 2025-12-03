using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RevitAddIn2BIMRU.Commands.AN
{
    public partial class GridMarkingWindow : Window
    {
        public GridMarkingSettings Settings { get; private set; }
        public string SelectedParameterName { get; private set; }

        public GridMarkingWindow(int elementCount)
        {
            InitializeComponent();

            Settings = new GridMarkingSettings();
            SelectedParameterName = "ADSK_Позиция"; // значение по умолчанию

            UpdateInfoText();
            txtInfo.Text = $"Выбрано элементов: {elementCount}\n\n" +
                          "Элементы будут сгруппированы по строкам (Y),\n" +
                          "затем в каждой строке отсортированы по X\n" +
                          "и пронумерованы в выбранном порядке.";
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранное имя параметра
            if (cmbParameterName.SelectedItem is ComboBoxItem selectedItem)
            {
                SelectedParameterName = selectedItem.Content.ToString();
            }

            // Проверка начального номера
            if (!int.TryParse(txtStartNumber.Text, out int startNumber) || startNumber < 1)
            {
                MessageBox.Show("Введите корректное начальное число (больше 0)",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtStartNumber.Focus();
                txtStartNumber.SelectAll();
                return;
            }

            // Проверка точности
            if (!double.TryParse(txtTolerance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double tolerance) || tolerance <= 0)
            {
                MessageBox.Show("Введите корректное значение точности (больше 0)",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtTolerance.Focus();
                txtTolerance.SelectAll();
                return;
            }

            // Сохраняем настройки
            Settings.StartNumber = startNumber;
            Settings.XDirection = rbXLeftToRight.IsChecked == true
                ? MarkElementsByGrid.SortDirection.Ascending
                : MarkElementsByGrid.SortDirection.Descending;

            Settings.YDirection = rbYBottomToTop.IsChecked == true
                ? MarkElementsByGrid.SortDirection.Ascending
                : MarkElementsByGrid.SortDirection.Descending;

            Settings.MarkByRows = rbByRows.IsChecked == true;
            Settings.RowTolerance = tolerance;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateInfoText();
        }

        private void UpdateInfoText()
        {
            string parameterName = "ADSK_Позиция";
            if (cmbParameterName.SelectedItem is ComboBoxItem selectedItem)
            {
                parameterName = selectedItem.Content.ToString();
            }

            string xDir = rbXLeftToRight.IsChecked == true ? "слева направо" : "справа налево";
            string yDir = rbYBottomToTop.IsChecked == true ? "снизу вверх" : "сверху вниз";

            if (rbByRows.IsChecked == true)
            {
                txtInfo.Text = $"Параметр: {parameterName}\n" +
                              $"Порядок маркировки:\n" +
                              $"1. Группировка по строкам ({yDir})\n" +
                              $"2. В каждой строке: {xDir}\n" +
                              $"Начальный номер: {txtStartNumber.Text}";
            }
            else
            {
                txtInfo.Text = $"Параметр: {parameterName}\n" +
                              $"Порядок маркировки:\n" +
                              $"Сначала {xDir}, затем {yDir}\n" +
                              $"(непрерывная нумерация)\n" +
                              $"Начальный номер: {txtStartNumber.Text}";
            }
        }

        private void TxtStartNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }

            // Проверяем, чтобы число не было слишком большим
            string newText = txtStartNumber.Text.Insert(txtStartNumber.SelectionStart, e.Text);
            if (newText.Length > 6) // Максимум 6 цифр
            {
                e.Handled = true;
            }
        }

        private void TxtTolerance_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем цифры и точку/запятую для десятичных чисел
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c.ToString() != decimalSeparator)
                {
                    e.Handled = true;
                    return;
                }
            }

            // Проверяем, чтобы не было двух разделителей
            string newText = txtTolerance.Text.Insert(txtTolerance.SelectionStart, e.Text);
            if (newText.IndexOf(decimalSeparator) != newText.LastIndexOf(decimalSeparator))
            {
                e.Handled = true;
            }
        }

        private void InitializeComponent2()
        {
            // Этот метод вызывается автоматически при компиляции XAML
        }
    }
}