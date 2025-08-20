using System;
using System.IO;
using System.Drawing;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class ExportScheduleToExcel : IExternalCommand
    {
        private List<string> _errorLog = new List<string>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;

                if (!(doc.ActiveView is ViewSchedule schedule))
                {
                    message = "Активное представление не является спецификацией";
                    return Result.Failed;
                }

                // Выбор места сохранения файла
                string filePath = GetSaveFilePath(schedule.Name);
                if (string.IsNullOrEmpty(filePath))
                {
                    return Result.Cancelled;
                }

                // Экспорт данных
                ExportScheduleData(schedule, filePath, doc);

                if (_errorLog.Any())
                {
                    TaskDialog.Show("Экспорт завершен с ошибками",
                        $"Экспорт завершен с {_errorLog.Count} ошибками.\n\n" +
                        "Первые 5 ошибок:\n" +
                        string.Join("\n", _errorLog.Take(5)) +
                        $"\n\nПодробный лог сохранен рядом с файлом Excel.");

                    // Сохраняем лог ошибок
                    SaveErrorLog(filePath);
                }
                else
                {
                    TaskDialog.Show("Успех", $"Спецификация экспортирована в:\n{filePath}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                TaskDialog.Show("Ошибка экспорта", message);
                return Result.Failed;
            }
        }

        private void ExportScheduleData(ViewSchedule schedule, string filePath, Document document)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Спецификация");

                TableData tableData = schedule.GetTableData();
                TableSectionData body = tableData.GetSectionData(SectionType.Body);

                // Экспорт заголовков
                for (int col = 0; col < body.NumberOfColumns; col++)
                {
                    worksheet.Cell(1, col + 1).Value = schedule.GetCellText(SectionType.Body, 0, col);
                    worksheet.Cell(1, col + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Экспорт данных
                int totalRows = body.NumberOfRows - 1;
                for (int row = 1; row < body.NumberOfRows; row++)
                {
                    // Периодически обновляем UI для отзывчивости
                    if (row % 50 == 0)
                    {
                        Application.DoEvents();
                    }

                    for (int col = 0; col < body.NumberOfColumns; col++)
                    {
                        try
                        {
                            var cellValue = schedule.GetCellText(SectionType.Body, row, col);
                            var excelCell = worksheet.Cell(row + 1, col + 1);

                            if (IsImageReference(cellValue))
                            {
                                using (Image image = GetImage(cellValue, document))
                                {
                                    if (image != null)
                                    {
                                        AddImageToWorksheet(worksheet, image, row + 1, col + 1);
                                    }
                                    else
                                    {
                                        excelCell.Value = $"[Изображение: {cellValue}]";
                                        _errorLog.Add($"Строка {row + 1}, Колонка {col + 1}: Изображение не найдено - {cellValue}");
                                    }
                                }
                            }
                            else
                            {
                                excelCell.Value = cellValue;

                                // Попытка парсинга чисел
                                //if (double.TryParse(cellValue, out double numericValue))
                                //{
                                //    excelCell.DataType = XLDataType.Number;
                                //}
                            }
                        }
                        catch (Exception ex)
                        {
                            _errorLog.Add($"Строка {row + 1}, Колонка {col + 1}: {ex.Message}");
                            worksheet.Cell(row + 1, col + 1).Value = $"[Ошибка]";
                        }
                    }
                }

                // Форматирование таблицы
                FormatWorksheet(worksheet, body.NumberOfColumns);

                // Сохранение с обработкой ошибок
                SaveWorkbook(workbook, filePath);
            }
        }

        private void FormatWorksheet(IXLWorksheet worksheet, int columnCount)
        {
            // Автоподбор ширины столбцов с ограничением
            worksheet.Columns().AdjustToContents();

            for (int col = 1; col <= columnCount; col++)
            {
                var currentWidth = worksheet.Column(col).Width;
                worksheet.Column(col).Width = Math.Min(currentWidth, 50);
            }

            // Добавляем границы
            if (worksheet.LastRowUsed() != null)
            {
                var dataRange = worksheet.Range(1, 1, worksheet.LastRowUsed().RowNumber(), columnCount);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // Заморозка заголовков
                worksheet.SheetView.FreezeRows(1);
            }
        }

        private bool IsImageReference(string cellValue)
        {
            if (string.IsNullOrEmpty(cellValue))
                return false;

            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif" };
            return imageExtensions.Any(ext =>
                cellValue.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        private Image GetImage(string imageName, Document doc)
        {
            try
            {
                ImageType imageType = GetImageTypeByName(doc, imageName);
                if (imageType != null)
                {
                    // Создаем копию изображения для безопасного использования
                    using (var originalImage = imageType.GetImage())
                    {
                        return new Bitmap(originalImage);
                    }
                }

                // Попробуем найти по пути файла
                if (File.Exists(imageName))
                {
                    return Image.FromFile(imageName);
                }
            }
            catch (Exception ex)
            {
                _errorLog.Add($"Ошибка загрузки изображения '{imageName}': {ex.Message}");
            }

            return null;
        }

        private void AddImageToWorksheet(IXLWorksheet worksheet, Image image, int row, int col)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;

                    var picture = worksheet.AddPicture(stream)
                        .MoveTo(worksheet.Cell(row, col))
                        .WithSize(100, (int)(100 * ((double)image.Height / image.Width)));

                    // Настраиваем размеры ячейки
                    worksheet.Row(row).Height = picture.Height + 2;
                    worksheet.Column(col).Width = Math.Max(worksheet.Column(col).Width, 15);
                }
            }
            catch (Exception ex)
            {
                _errorLog.Add($"Ошибка вставки изображения в строку {row}, колонку {col}: {ex.Message}");
                worksheet.Cell(row, col).Value = $"[Ошибка изображения]";
            }
        }

        private void SaveWorkbook(XLWorkbook workbook, string filePath, int maxAttempts = 3)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // Создаем директорию если не существует
                    string directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    workbook.SaveAs(filePath);
                    return;
                }
                catch (IOException ex) when (attempt < maxAttempts)
                {
                    _errorLog.Add($"Попытка {attempt}: Ошибка доступа к файлу - {ex.Message}");
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    _errorLog.Add($"Критическая ошибка сохранения: {ex.Message}");
                    throw;
                }
            }

            throw new IOException($"Не удалось сохранить файл после {maxAttempts} попыток");
        }

        private void SaveErrorLog(string excelFilePath)
        {
            try
            {
                string logFilePath = Path.Combine(
                    Path.GetDirectoryName(excelFilePath),
                    $"{Path.GetFileNameWithoutExtension(excelFilePath)}_errors.log");

                File.WriteAllLines(logFilePath, _errorLog);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Не удалось сохранить лог ошибок: {ex.Message}");
            }
        }

        public static List<ImageType> GetAllImageTypes(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(ImageType))
                           .Cast<ImageType>()
                           .ToList();
        }

        public static ImageType GetImageTypeByName(Document doc, string imageName)
        {
            var allImages = GetAllImageTypes(doc);

            // Точное совпадение
            var exactMatch = allImages.FirstOrDefault(img =>
                img.Name.Equals(imageName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null) return exactMatch;

            // Совпадение по имени файла без расширения
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imageName);
            return allImages.FirstOrDefault(img =>
                Path.GetFileNameWithoutExtension(img.Name)
                    .Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
        }

        private string GetSaveFilePath(string baseName)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "Выберите место для сохранения спецификации";
                saveFileDialog.FileName = $"{CleanFileName(baseName)}.xlsx";
                saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*";
                saveFileDialog.DefaultExt = "xlsx";
                saveFileDialog.AddExtension = true;
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog.OverwritePrompt = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string directory = Path.GetDirectoryName(saveFileDialog.FileName);
                    if (!Directory.Exists(directory))
                    {
                        if (MessageBox.Show($"Папка {directory} не существует. Создать?",
                            "Создать папку", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            Directory.CreateDirectory(directory);
                        }
                        else
                        {
                            return null;
                        }
                    }
                    return saveFileDialog.FileName;
                }
            }

            return null;
        }

        private string CleanFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }
    }
}