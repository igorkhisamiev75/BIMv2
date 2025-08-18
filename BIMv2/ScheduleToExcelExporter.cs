using System;
using System.IO;
using System.Drawing;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using Autodesk.Revit.Attributes;

namespace RevitScheduleImageExport
{
    [Transaction(TransactionMode.Manual)]
    public class ExportScheduleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;

            try
            {
                if (!(doc.ActiveView is ViewSchedule schedule))
                {
                    TaskDialog.Show("Ошибка", "Пожалуйста, откройте спецификацию для экспорта");
                    return Result.Failed;
                }

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string excelPath = Path.Combine(desktopPath, $"{schedule.Name}_Export.xlsx");

                ExportScheduleData(schedule, excelPath);

                TaskDialog.Show("Экспорт завершен", $"Файл сохранен:\n{excelPath}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                TaskDialog.Show("Критическая ошибка", message);
                return Result.Failed;
            }
        }

        private void ExportScheduleData(ViewSchedule schedule, string excelPath)
        {
            Document doc = schedule.Document;

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Данные");

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
                        string cellValue = schedule.GetCellText(SectionType.Body, row, col);
                        var excelCell = worksheet.Cell(row + 1, col + 1);

                        if (IsImageFileReference(cellValue))
                        {
                            ProcessImageReference(doc, worksheet, cellValue, row + 1, col + 1);
                        }
                        else
                        {
                            excelCell.Value = cellValue;
                        }
                    }
                }

                worksheet.Columns().AdjustToContents();
                SaveExcelWithRetry(workbook, excelPath);
            }
        }

        private bool IsImageFileReference(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string ext = Path.GetExtension(text).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp";
        }

        private void ProcessImageReference(Document doc, IXLWorksheet worksheet, string imageName, int row, int col)
        {
            // Поиск ImageType в проекте
            ImageType imageType = new FilteredElementCollector(doc)
                .OfClass(typeof(ImageType))
                .Cast<ImageType>()
                .FirstOrDefault(img => img.Name.Equals(imageName, StringComparison.OrdinalIgnoreCase));

            if (imageType != null)
            {
                string imagePath = ImageType.GetCachedImagePath(imageType.GetImage());

                if (File.Exists(imagePath))
                {
                    try
                    {
                        using (Image img = Image.FromFile(imagePath))
                        {
                            AddImageToExcel(worksheet, img, row, col);
                            return;
                        }
                    }
                    catch
                    {
                        worksheet.Cell(row, col).Value = $"[Ошибка загрузки: {imageName}]";
                        return;
                    }
                }
            }

            // Поиск в папке проекта
            string projectFolder = Path.GetDirectoryName(doc.PathName);
            if (!string.IsNullOrEmpty(projectFolder))
            {
                string possiblePath = Path.Combine(projectFolder, imageName);
                if (File.Exists(possiblePath))
                {
                    try
                    {
                        using (Image img = Image.FromFile(possiblePath))
                        {
                            AddImageToExcel(worksheet, img, row, col);
                            return;
                        }
                    }
                    catch
                    {
                        // Продолжаем выполнение
                    }
                }
            }

            // Изображение не найдено
            worksheet.Cell(row, col).Value = $"[Изображение отсутствует: {imageName}]";
        }

        private void AddImageToExcel(IXLWorksheet worksheet, Image image, int row, int col)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                var picture = worksheet.AddPicture(ms)
                    .MoveTo(worksheet.Cell(row, col))
                    .WithSize(100, (int)(100 * ((double)image.Height / image.Width)));

                worksheet.Row(row).Height = picture.Height + 2;
            }
        }

        private void SaveExcelWithRetry(XLWorkbook workbook, string path, int maxAttempts = 3)
        {
            for (int i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    workbook.SaveAs(path);
                    return;
                }
                catch (IOException) when (i < maxAttempts)
                {
                    System.Threading.Thread.Sleep(500);
                }
            }
            throw new Exception($"Не удалось сохранить файл после {maxAttempts} попыток");
        }
    }
}