using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RevitAddIn2BIMRU.Commands.AN
{
    public partial class GridMarkingWindow : Window
    {
        public GridMarkingSettings Settings { get; private set; }
        private int _selectedElementsCount;

        public GridMarkingWindow(int selectedElementsCount)
        {
            try
            {
                InitializeComponent();
                _selectedElementsCount = selectedElementsCount;
                Settings = new GridMarkingSettings();
                UpdateInfoText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании окна: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateInfoText()
        {
            try
            {
                string xDirection = rbXLeftToRight?.IsChecked == true ? "слева направо" : "справа налево";
                string yDirection = rbYBottomToTop?.IsChecked == true ? "снизу вверх" : "сверху вниз";
                string mode = rbByRows?.IsChecked == true ? "по строкам" : "непрерывно";

                txtInfo.Text = $"Выбрано элементов: {_selectedElementsCount}\n" +
                              $"Параметр: {txtParameterName?.Text ?? "ADSK_Позиция"}\n" +
                              $"Начальный номер: {txtStartNumber?.Text ?? "1"}\n" +
                              $"Направление: {xDirection} по X, {yDirection} по Y\n" +
                              $"Режим: {mode}\n" +
                              $"Точность группировки: {txtTolerance?.Text ?? "0.1"} м";
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"Ошибка обновления информации: {ex.Message}";
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем имя параметра из текстового поля
                string parameterName = txtParameterName?.Text?.Trim();

                if (string.IsNullOrEmpty(parameterName))
                {
                    MessageBox.Show("Введите имя параметра для маркировки", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Settings.ParameterName = parameterName;

                // Получаем стартовый номер
                string startNumberText = txtStartNumber?.Text ?? "1";
                if (!int.TryParse(startNumberText, out int startNumber) || startNumber < 1)
                {
                    MessageBox.Show("Введите корректный стартовый номер (целое число больше 0)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Settings.StartNumber = startNumber;

                // Направление по X
                Settings.XDirection = (rbXLeftToRight?.IsChecked == true) ?
                    MarkElementsByGrid.SortDirection.Ascending :
                    MarkElementsByGrid.SortDirection.Descending;

                // Направление по Y
                Settings.YDirection = (rbYBottomToTop?.IsChecked == true) ?
                    MarkElementsByGrid.SortDirection.Ascending :
                    MarkElementsByGrid.SortDirection.Descending;

                // Точность группировки
                string toleranceText = txtTolerance?.Text ?? "0.1";
                if (!double.TryParse(toleranceText.Replace(',', '.'), out double tolerance) || tolerance < 0)
                {
                    MessageBox.Show("Введите корректное значение точности (положительное число)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Settings.RowTolerance = tolerance;

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении настроек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.DialogResult = false;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при закрытии окна: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void TxtStartNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                // Разрешаем только цифры
                e.Handled = !char.IsDigit(e.Text, 0);
            }
            catch { }
        }

        private void TxtStartNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                UpdateInfoText();
            }
            catch { }
        }

        private void TxtTolerance_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                TextBox textBox = sender as TextBox;
                if (textBox == null) return;

                string currentText = textBox.Text;
                string newText = currentText.Substring(0, textBox.SelectionStart) +
                               e.Text +
                               currentText.Substring(textBox.SelectionStart + textBox.SelectionLength);

                // Разрешаем цифры, точку и запятую
                e.Handled = !(char.IsDigit(e.Text, 0) || e.Text == "." || e.Text == ",");

                // Проверяем, что это корректное число
                if (!e.Handled)
                {
                    e.Handled = !double.TryParse(newText.Replace(',', '.'), out double result);
                }
            }
            catch { }
        }

        private void TxtTolerance_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                UpdateInfoText();
            }
            catch { }
        }

        private void TxtParameterName_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                UpdateInfoText();
            }
            catch { }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateInfoText();
            }
            catch { }
        }
    }
}