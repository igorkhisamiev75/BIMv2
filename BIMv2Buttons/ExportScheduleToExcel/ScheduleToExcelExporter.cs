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
using Autodesk.Revit.Creation;
using Document = Autodesk.Revit.DB.Document;

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

                // Собираем все семейства в проекте
                var allFamilies5 = CollectAllFamilies(doc);

                // Собираем все ImageType в проекте
                var allImageTypes5 = CollectAllImageTypes(doc);

                // Собираем информацию о семействах и связанных изображениях
                var familyImageInfo = CollectFamilyImageInfo(doc, allFamilies5, allImageTypes5);

                if (!(doc.ActiveView is ViewSchedule schedule))
                {
                    message = "Активное представление не является спецификацией";
                    return Result.Failed;
                }

                // Выбор места сохранения файла
                string filePath = GetSaveFilePath(schedule.Name);
                if (string.IsNullOrEmpty(filePath))
                {
                    return Result.Cancelled; // Пользователь отменил выбор
                }

                //string filePath = GetUniqueFilePath(schedule.Name);
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
                                using (Image image = GetImage2(cellValue, document))
                                {
                                    if (image != null)
                                    {
                                        AddImageToWorksheet(worksheet, image, row + 1, col + 1);
                                    }
                                    else
                                    {
                                        var typ = GetImageTypeByName(document, cellValue);
                                        var path = typ.Path;

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


                // Сохранение с обработкой ошибок
                SaveWorkbook(workbook, filePath);
            }
        }

        private bool IsImageReference(string cellValue)
        {
            return !string.IsNullOrEmpty(cellValue) &&
                   (cellValue.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   cellValue.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    cellValue.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    cellValue.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase));
        }

        private Image GetImage2(string imagePath, Document doc)
        {

            try
            {
                List<ImageType> allImageType = GetAllImageTypes(doc);

                Image im = FindImageInFamilies(doc, imagePath);

                return im;

                //ImageType foundImage = allImageType
                //   .FirstOrDefault(img => img.Name.Equals(imagePath, StringComparison.OrdinalIgnoreCase));

                ////if (foundImage != null)
                ////{
                ////    ImageType foundImage2 = allImageType2
                ////    .FirstOrDefault(img => img.Name.Equals(imagePath, StringComparison.OrdinalIgnoreCase));
                ////    foundImage = foundImage2;
                ////}
                ////else
                ////{
                ////     TaskDialog.Show("Что-то пошло не так и уже давно", "Обратиться в службу 2bim.ru ");
                ////}



                ////попробуем вытащить JPEG
                ////ImageType typ = GetImageTypeByName(doc, imagePath);

                //Bitmap g = foundImage.GetImage();
                //return g;
                ////if (File.Exists(imagePath))
                ////    return Image.FromFile(imagePath);
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
                    .WithSize(200, (int)(200 * ((double)image.Height / image.Width)));

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

        /// <summary>
        /// Получает все ImageType в проекте
        /// </summary>
        public List<ImageType> GetAllImageTypes(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(ImageType))
                           .Cast<ImageType>()
                           .ToList();
        }

        /// <summary>
        /// Получает ImageType по имени файла
        /// </summary>
        public ImageType GetImageTypeByName(Document doc, string imageName)
        {
            return GetAllImageTypes(doc)
                .FirstOrDefault(img => img.Name.Equals(imageName, StringComparison.OrdinalIgnoreCase));
        }

        private string GetSaveFilePath(string baseName)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "Выберите место для сохранения спецификации";
                saveFileDialog.FileName = $"{baseName}.xlsx";
                saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*";
                saveFileDialog.DefaultExt = "xlsx";
                saveFileDialog.AddExtension = true;

                // Начальная директория - рабочий стол
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Проверка на уникальность имени файла
                saveFileDialog.OverwritePrompt = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return saveFileDialog.FileName;
                }
            }

            return null; // Пользователь отменил
        }

        // Сбор всех семейств в проекте
        private List<Family> CollectAllFamilies(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(Family))
                          .Cast<Family>()
                          .ToList();
        }

        // Сбор всех ImageType в проекте
        private List<ImageType> CollectAllImageTypes(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(ImageType))
                          .Cast<ImageType>()
                          .ToList();
        }
        // Сбор информации о семействах и связанных изображениях
        private List<FamilyImageInfo> CollectFamilyImageInfo(Document doc, List<Family> families, List<ImageType> imageTypes)
        {
            var result = new List<FamilyImageInfo>();

            foreach (var family in families)
            {
                var familyInfo = new FamilyImageInfo
                {
                    FamilyName = family.Name,
                    FamilyId = family.Id,
                    IsLoaded = family.IsEditable,
                    FamilyCategory = family.FamilyCategory?.Name ?? "Без категории"
                };

                // Получаем все типы семейств
                var familySymbols = GetFamilySymbols(doc, family);
                familyInfo.SymbolCount = familySymbols.Count;

                // Ищем связанные изображения
                FindRelatedImages(family, familySymbols, imageTypes, familyInfo);

                result.Add(familyInfo);
            }

            return result;
        }

        // Получение всех типов семейств
        private List<FamilySymbol> GetFamilySymbols(Document doc, Family family)
        {
            var symbols = new List<FamilySymbol>();
            var symbolIds = family.GetFamilySymbolIds();

            foreach (ElementId symbolId in symbolIds)
            {
                var symbol = doc.GetElement(symbolId) as FamilySymbol;
                if (symbol != null)
                {
                    symbols.Add(symbol);
                }
            }

            return symbols;
        }

        // Поиск связанных изображений
        private void FindRelatedImages(Family family, List<FamilySymbol> symbols, List<ImageType> imageTypes, FamilyImageInfo familyInfo)
        {
            // Поиск по имени семейства
            var relatedImages = imageTypes
                .Where(img => img.Name.IndexOf(family.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // Поиск в параметрах семейства
            foreach (var symbol in symbols)
            {
                FindImagesInElementParameters(symbol, imageTypes, familyInfo);
            }

            // Поиск в параметрах самого семейства
            FindImagesInElementParameters(family, imageTypes, familyInfo);

            familyInfo.RelatedImages.AddRange(relatedImages);
            familyInfo.TotalImageCount = familyInfo.RelatedImages.Count;
        }

        // Поиск изображений в параметрах элемента
        private void FindImagesInElementParameters(Element element, List<ImageType> imageTypes, FamilyImageInfo familyInfo)
        {
            foreach (Parameter param in element.Parameters)
            {
                if (param.StorageType == StorageType.ElementId)
                {
                    ElementId elementId = param.AsElementId();
                    if (elementId != ElementId.InvalidElementId)
                    {
                        Element paramElement = element.Document.GetElement(elementId);
                        if (paramElement is ImageType imageType)
                        {
                            if (!familyInfo.RelatedImages.Any(img => img.Id == imageType.Id))
                            {
                                familyInfo.RelatedImages.Add(imageType);
                                familyInfo.ImageParameters.Add($"{element.Name}.{param.Definition.Name}");
                            }
                        }
                    }
                }
                else if (param.StorageType == StorageType.String)
                {
                    string paramValue = param.AsString();
                    if (!string.IsNullOrEmpty(paramValue) && IsImageReference(paramValue))
                    {
                        var matchingImage = imageTypes.FirstOrDefault(img =>
                            img.Name.Equals(paramValue, StringComparison.OrdinalIgnoreCase));

                        if (matchingImage != null && !familyInfo.RelatedImages.Any(img => img.Id == matchingImage.Id))
                        {
                            familyInfo.RelatedImages.Add(matchingImage);
                            familyInfo.ImageParameters.Add($"{element.Name}.{param.Definition.Name}");
                        }
                    }
                }
            }
        }
        public class FamilyImageInfo
        {
            public string FamilyName { get; set; }
            public ElementId FamilyId { get; set; }
            public string FamilyCategory { get; set; }
            public bool IsLoaded { get; set; }
            public int SymbolCount { get; set; }
            public int TotalImageCount { get; set; }
            public List<ImageType> RelatedImages { get; set; } = new List<ImageType>();
            public List<string> ImageParameters { get; set; } = new List<string>();
        }

        private Image FindImageInFamilies(Document doc, string imageName)
        {
            try
            {
                // Ищем во всех семействах проекта
                var allFamilies = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>();

                foreach (var family in allFamilies)
                {
                    // Ищем в параметрах самого семейства
                    Image imageFromFamily = FindImageInElementParameters(family, imageName);
                    if (imageFromFamily != null)
                    {
                        return imageFromFamily;
                    }

                    // Ищем в типах семейства
                    var symbolIds = family.GetFamilySymbolIds();
                    foreach (ElementId symbolId in symbolIds)
                    {
                        var symbol = doc.GetElement(symbolId) as FamilySymbol;
                        if (symbol != null)
                        {
                            Image imageFromSymbol = FindImageInElementParameters(symbol, imageName);
                            if (imageFromSymbol != null)
                            {
                                return imageFromSymbol;
                            }
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private Image FindImageInElementParameters(Element element, string targetImageName)
        {
            foreach (Parameter param in element.Parameters)
            {
                if (param.StorageType == StorageType.ElementId)
                {
                    // Параметр содержит ElementId (возможно, ImageType)
                    ElementId elementId = param.AsElementId();
                    if (elementId != ElementId.InvalidElementId)
                    {
                        Element paramElement = element.Document.GetElement(elementId);
                        if (paramElement is ImageType imageType)
                        {
                            // Проверяем совпадение по имени
                            if (imageType.Name.Equals(targetImageName, StringComparison.OrdinalIgnoreCase))
                            {
                                return GetImageFromImageType(imageType);
                            }
                        }
                    }
                }
                else if (param.StorageType == StorageType.String)
                {
                    // Параметр содержит строку (возможно, путь к изображению)
                    string paramValue = param.AsString();
                    if (!string.IsNullOrEmpty(paramValue) &&
                        paramValue.Equals(targetImageName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Пробуем загрузить изображение из файла
                        if (File.Exists(paramValue))
                        {
                            return Image.FromFile(paramValue);
                        }
                    }
                }
            }

            return null;
        }

        private Image GetImageFromImageType(ImageType imageType)
        {
            try
            {
                // Пытаемся получить изображение через GetImage()
                Bitmap bitmap = imageType.GetImage();
                if (bitmap != null)
                {
                    return bitmap;
                }
            }
            catch
            {
                // Если не удалось, пробуем загрузить из файла
            }

            // Пробуем загрузить из файла по пути
            if (File.Exists(imageType.Path))
            {
                return Image.FromFile(imageType.Path);
            }

            // Пробуем найти файл по имени
            string fileName = Path.GetFileName(imageType.Path);
            if (File.Exists(fileName))
            {
                return Image.FromFile(fileName);
            }

            return null;
        }
    }
}