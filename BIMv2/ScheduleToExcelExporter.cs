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
    // Форма настроек размера изображений
    public partial class ImageSettingsForm : System.Windows.Forms.Form
    {
        public NumericUpDown WidthNumeric { get; private set; }
        public NumericUpDown HeightNumeric { get; private set; }
        public CheckBox KeepAspectRatioCheckBox { get; private set; }
        public Button OkButton { get; private set; }
        public Button CancelButton { get; private set; }

        public int ImageWidth => (int)WidthNumeric.Value;
        public int ImageHeight => (int)HeightNumeric.Value;
        public bool KeepAspectRatio => KeepAspectRatioCheckBox.Checked;

        public ImageSettingsForm(int defaultWidth = 200, int defaultHeight = 200)
        {
            InitializeComponent();
            WidthNumeric.Value = defaultWidth;
            HeightNumeric.Value = defaultHeight;
        }

        private void InitializeComponent()
        {
            this.WidthNumeric = new NumericUpDown();
            this.HeightNumeric = new NumericUpDown();
            this.KeepAspectRatioCheckBox = new CheckBox();
            this.OkButton = new Button();
            this.CancelButton = new Button();
            var label1 = new Label();
            var label2 = new Label();

            // WidthNumeric
            this.WidthNumeric.Location = new System.Drawing.Point(120, 20);
            this.WidthNumeric.Minimum = 50;
            this.WidthNumeric.Maximum = 1000;
            this.WidthNumeric.Value = 200;

            // HeightNumeric
            this.HeightNumeric.Location = new System.Drawing.Point(120, 50);
            this.HeightNumeric.Minimum = 50;
            this.HeightNumeric.Maximum = 1000;
            this.HeightNumeric.Value = 200;

            // KeepAspectRatioCheckBox
            this.KeepAspectRatioCheckBox.Location = new System.Drawing.Point(20, 80);
            this.KeepAspectRatioCheckBox.Size = new System.Drawing.Size(200, 20);
            this.KeepAspectRatioCheckBox.Text = "Сохранять пропорции";
            this.KeepAspectRatioCheckBox.Checked = true;

            // OkButton
            this.OkButton.Location = new System.Drawing.Point(70, 110);
            this.OkButton.Size = new System.Drawing.Size(75, 23);
            this.OkButton.Text = "OK";
            this.OkButton.DialogResult = DialogResult.OK;

            // CancelButton
            this.CancelButton.Location = new System.Drawing.Point(155, 110);
            this.CancelButton.Size = new System.Drawing.Size(75, 23);
            this.CancelButton.Text = "Отмена";
            this.CancelButton.DialogResult = DialogResult.Cancel;

            // label1
            label1.Location = new System.Drawing.Point(20, 22);
            label1.Size = new System.Drawing.Size(100, 20);
            label1.Text = "Ширина";

            // label2
            label2.Location = new System.Drawing.Point(20, 52);
            label2.Size = new System.Drawing.Size(100, 20);
            label2.Text = "Высота";

            // ImageSettingsForm
            this.ClientSize = new System.Drawing.Size(250, 150);
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                label1, label2, this.WidthNumeric, this.HeightNumeric,
                this.KeepAspectRatioCheckBox, this.OkButton, this.CancelButton
            });
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Настройки изображений";
        }
    }

    // Форма для отображения прогресса
    public partial class ProgressForm : System.Windows.Forms.Form
    {
        public ProgressBar ProgressBar { get; private set; }
        public Label StatusLabel { get; private set; }
        public Button CancelButton { get; private set; }

        public bool IsCancelled { get; private set; }

        public ProgressForm(string title, int maxValue)
        {
            InitializeComponent();
            this.Text = title;
            ProgressBar.Maximum = maxValue;
            ProgressBar.Value = 0;
            IsCancelled = false;
        }

        private void InitializeComponent()
        {
            this.ProgressBar = new ProgressBar();
            this.StatusLabel = new Label();
            this.CancelButton = new Button();
            this.SuspendLayout();

            // ProgressBar
            this.ProgressBar.Location = new System.Drawing.Point(12, 30);
            this.ProgressBar.Size = new System.Drawing.Size(360, 23);
            this.ProgressBar.Style = ProgressBarStyle.Continuous;

            // StatusLabel
            this.StatusLabel.Location = new System.Drawing.Point(12, 10);
            this.StatusLabel.Size = new System.Drawing.Size(360, 20);
            this.StatusLabel.Text = "Подготовка...";
            this.StatusLabel.TextAlign = ContentAlignment.MiddleLeft;

            // CancelButton
            this.CancelButton.Location = new System.Drawing.Point(297, 60);
            this.CancelButton.Size = new System.Drawing.Size(75, 23);
            this.CancelButton.Text = "Отмена";
            this.CancelButton.Click += (s, e) =>
            {
                IsCancelled = true;
                this.Close();
            };

            // ProgressForm
            this.ClientSize = new System.Drawing.Size(384, 95);
            this.Controls.Add(this.ProgressBar);
            this.Controls.Add(this.StatusLabel);
            this.Controls.Add(this.CancelButton);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
        }

        public void UpdateProgress(int value, string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int, string>(UpdateProgress), value, status);
                return;
            }

            ProgressBar.Value = Math.Min(value, ProgressBar.Maximum);
            StatusLabel.Text = status;
            //System.Windows.Application.DoEvents();
        }

        public void Complete()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(Complete));
                return;
            }
            this.Close();
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ExportScheduleToExcel : IExternalCommand
    {
        private ProgressForm progressForm;
        private bool cancellationRequested = false;
        private ImageSettings imageSettings = new ImageSettings();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;

                // Запрашиваем настройки изображений
                using (var settingsForm = new ImageSettingsForm())
                {
                    if (settingsForm.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    imageSettings.Width = settingsForm.ImageWidth;
                    imageSettings.Height = settingsForm.ImageHeight;
                    imageSettings.KeepAspectRatio = settingsForm.KeepAspectRatio;
                }

                // Создаем форму прогресса
                using (progressForm = new ProgressForm("Экспорт спецификации в Excel", 100))
                {
                    progressForm.Show();

                    // Собираем все семейства в проекте
                    progressForm.UpdateProgress(10, "Сбор информации о семействах...");
                    var allFamilies5 = CollectAllFamilies(doc);

                    // Собираем все ImageType в проекте
                    progressForm.UpdateProgress(20, "Сбор информации об изображениях...");
                    var allImageTypes5 = CollectAllImageTypes(doc);

                    // Собираем информацию о семействах и связанных изображениях
                    progressForm.UpdateProgress(30, "Анализ связей...");
                    var familyImageInfo = CollectFamilyImageInfo(doc, allFamilies5, allImageTypes5);

                    if (!(doc.ActiveView is ViewSchedule schedule))
                    {
                        message = "Активное представление не является спецификацией";
                        return Result.Failed;
                    }

                    // Проверяем отмену
                    if (progressForm.IsCancelled)
                    {
                        message = "Операция отменена пользователем";
                        return Result.Cancelled;
                    }

                    // Выбор места сохранения файла
                    progressForm.UpdateProgress(40, "Выбор места сохранения...");
                    string filePath = GetSaveFilePath(schedule.Name);
                    if (string.IsNullOrEmpty(filePath))
                    {
                        return Result.Cancelled;
                    }

                    // Экспорт данных с прогрессом
                    progressForm.UpdateProgress(50, "Экспорт данных...");
                    ExportScheduleData(schedule, filePath, doc);

                    // Проверяем отмену
                    if (cancellationRequested)
                    {
                        message = "Операция отменена пользователем";
                        return Result.Cancelled;
                    }

                    progressForm.UpdateProgress(100, "Завершение...");
                    TaskDialog.Show("Успех", $"Спецификация экспортирована в:\n{filePath}");

                    progressForm.Complete();
                    return Result.Succeeded;
                }
            }
            catch (Exception ex)
            {
                progressForm?.Complete();
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
                progressForm.UpdateProgress(55, "Экспорт заголовков...");
                for (int col = 0; col < body.NumberOfColumns; col++)
                {
                    worksheet.Cell(1, col + 1).Value = schedule.GetCellText(SectionType.Body, 0, col);
                    worksheet.Cell(1, col + 1).Style.Font.Bold = true;
                }

                // Экспорт данных с прогрессом
                int totalRows = body.NumberOfRows - 1;
                for (int row = 1; row < body.NumberOfRows; row++)
                {
                    if (progressForm.IsCancelled)
                    {
                        cancellationRequested = true;
                        return;
                    }

                    int progress = 55 + (int)((row * 40.0) / totalRows);
                    progressForm.UpdateProgress(progress, $"Обработка строки {row} из {totalRows}...");

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
                                        if (typ != null)
                                        {
                                            var path = typ.Path;
                                            string imagePath = path;
                                            Image image2 = Image.FromFile(imagePath);
                                            AddImageToWorksheet(worksheet, image2, row + 1, col + 1);
                                        }
                                        else
                                        {
                                            excelCell.Value = $"[Изображение не найдено: {cellValue}]";
                                        }
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

                progressForm.UpdateProgress(95, "Сохранение файла...");
                SaveWorkbook(workbook, filePath);
            }
        }

        private void AddImageToWorksheet(IXLWorksheet worksheet, Image image, int row, int col)
        {
            string tempImagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");

            try
            {
                image.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Png);

                // Рассчитываем размеры с учетом настроек
                int width = imageSettings.Width;
                int height = imageSettings.Height;

                if (imageSettings.KeepAspectRatio)
                {
                    // Сохраняем пропорции
                    double aspectRatio = (double)image.Height / image.Width;
                    height = (int)(width * aspectRatio);
                }

                var picture = worksheet.AddPicture(tempImagePath)
                    .MoveTo(worksheet.Cell(row, col))
                    .WithSize(width, height);

                worksheet.Row(row).Height = height + 2;
                worksheet.Column(col).Width = Math.Max(worksheet.Column(col).Width, width / 7.0 + 2);
            }
            finally
            {
                if (File.Exists(tempImagePath))
                {
                    try { File.Delete(tempImagePath); } catch { }
                }
            }
        }

        // Класс для хранения настроек изображений
        private class ImageSettings
        {
            public int Width { get; set; } = 200;
            public int Height { get; set; } = 150;
            public bool KeepAspectRatio { get; set; } = true;
        }

        // Остальные методы без изменений...
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

        public List<ImageType> GetAllImageTypes(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(ImageType))
                           .Cast<ImageType>()
                           .ToList();
        }

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
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog.OverwritePrompt = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return saveFileDialog.FileName;
                }
            }
            return null;
        }

        private List<Family> CollectAllFamilies(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(Family))
                          .Cast<Family>()
                          .ToList();
        }

        private List<ImageType> CollectAllImageTypes(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(ImageType))
                          .Cast<ImageType>()
                          .ToList();
        }

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