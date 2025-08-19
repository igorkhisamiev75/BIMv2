using System;
using System.IO;
using System.Drawing;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.Linq;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class ExportScheduleToExcel : IExternalCommand
    {
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

                string filePath = GetUniqueFilePath(schedule.Name);
                ExportScheduleData(schedule, filePath, doc);

                TaskDialog.Show("Успех", $"Спецификация экспортирована в:\n{filePath}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                TaskDialog.Show("Ошибка экспорта", message);
                return Result.Failed;
            }
        }

        private string GetUniqueFilePath(string baseName)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string fileName = $"{baseName}.xlsx";
            string filePath = Path.Combine(desktop, fileName);

            for (int i = 1; File.Exists(filePath); i++)
            {
                fileName = $"{baseName}_{i}.xlsx";
                filePath = Path.Combine(desktop, fileName);
            }

            return filePath;
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
                }

                // Экспорт данных
                for (int row = 1; row < body.NumberOfRows; row++)
                {
                    for (int col = 0; col < body.NumberOfColumns; col++)
                    {
                        var cellValue = schedule.GetCellText(SectionType.Body, row, col);
                        var excelCell = worksheet.Cell(row + 1, col + 1);

                        if (IsImageReference(cellValue))
                        {
                            try
                            {
                                using (Image image = GetImage(cellValue))
                                {
                                    if (image != null)
                                    {
                                        AddImageToWorksheet(worksheet, image, row + 1, col + 1);
                                    }
                                    else
                                    {
                                        var typ=GetImageTypeByName(document, cellValue);
                                        var path=typ.Path;

                                        string imagePath = path;
                                        // Загрузка изображения из файла
                                        Image image2 = Image.FromFile(imagePath);
                                        AddImageToWorksheet(worksheet, image2, row + 1, col + 1);
                                        //excelCell.Value = $"[Изображение не найдено: {cellValue}]";
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                excelCell.Value = $"[Ошибка: {ex.Message}]";
                            }
                        }
                        else
                        {
                            excelCell.Value = cellValue;
                        }
                    }
                }

                // Автоподбор ширины столбцов
                //worksheet.Columns().AdjustToContents();

                // Сохранение с обработкой ошибок
                SaveWorkbook(workbook, filePath);
            }
        }

        private bool IsImageReference(string cellValue)
        {
            return !string.IsNullOrEmpty(cellValue) &&
                   (cellValue.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    cellValue.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    cellValue.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase));
        }

        private Image GetImage(string imagePath)
        {
            try
            {
                if (File.Exists(imagePath))
                    return Image.FromFile(imagePath);
            }
            catch (UnauthorizedAccessException)
            {
                TaskDialog.Show("Ошибка", $"Нет доступа к файлу: {imagePath}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка загрузки изображения: {ex.Message}");
            }
            return null;
        }

        private void AddImageToWorksheet(IXLWorksheet worksheet, Image image, int row, int col)
        {
            string tempImagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");

            try
            {
                image.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Png);

                var picture = worksheet.AddPicture(tempImagePath)
                    .MoveTo(worksheet.Cell(row, col))
                    .WithSize(100, (int)(100 * ((double)image.Height / image.Width)));

                worksheet.Row(row).Height = picture.Height + 2;
            }
            finally
            {
                if (File.Exists(tempImagePath))
                {
                    try { File.Delete(tempImagePath); } catch { }
                }
            }
        }

        private void SaveWorkbook(XLWorkbook workbook, string filePath, int maxAttempts = 3)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    workbook.SaveAs(filePath);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    System.Threading.Thread.Sleep(500);
                }
            }
        }
        // <summary>
        /// Получает все ImageType в проекте
        /// </summary>
        public static List<ImageType> GetAllImageTypes(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(ImageType))
                           .Cast<ImageType>()
                           .ToList();
        }

        /// <summary>
        /// Получает ImageType по имени файла
        /// </summary>
        public static ImageType GetImageTypeByName(Document doc, string imageName)
        {
            return GetAllImageTypes(doc)
                .FirstOrDefault(img => img.Name.Equals(imageName, StringComparison.OrdinalIgnoreCase));
        }
    }
}