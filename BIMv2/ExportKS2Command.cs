using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Excel = Microsoft.Office.Interop.Excel;

namespace RevitKS2ExcelExport
{
    [Transaction(TransactionMode.Manual)]
    public class ExportKS2Command : IExternalCommand
    {
        private static string _templatePath = "";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. ПРОВЕРЯЕМ/ВЫБИРАЕМ ШАБЛОН
                if (string.IsNullOrEmpty(_templatePath) || !File.Exists(_templatePath))
                {
                    if (!SelectTemplateFile())
                    {
                        TaskDialog.Show("Отменено", "Шаблон не выбран. Экспорт отменен.");
                        return Result.Cancelled;
                    }
                }

                // 2. ПРОВЕРЯЕМ ВЫБОР ЭЛЕМЕНТА
                var selectedIds = uiDoc.Selection.GetElementIds();
                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show("Внимание", "Пожалуйста, выберите элемент в модели.");
                    return Result.Cancelled;
                }

                // 3. ПОЛУЧАЕМ ДАННЫЕ ЭЛЕМЕНТА
                var selectedElement = doc.GetElement(selectedIds.First());
                string elementName = GetElementName(selectedElement);
                string elementId = selectedElement.Id.IntegerValue.ToString();
                double elementArea = GetElementArea(selectedElement);
                string elementType = GetElementType(selectedElement);
                string elementLevel = GetElementLevel(selectedElement, doc);
                string projectName = doc.Title;

                // 4. ВЫБИРАЕМ КУДА СОХРАНИТЬ РЕЗУЛЬТАТ
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
                    saveDialog.FileName = $"КС-2_{elementName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                    saveDialog.Title = "Сохранить файл КС-2";

                    if (saveDialog.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string outputPath = saveDialog.FileName;

                    // 5. ЗАПОЛНЯЕМ ШАБЛОН ДАННЫМИ
                    bool success = FillExcelTemplate(_templatePath, outputPath, elementName,
                        elementId, elementArea, elementType, elementLevel);

                    if (success)
                    {
                        // 6. ПОКАЗЫВАЕМ РЕЗУЛЬТАТ
                        TaskDialog tdResult = new TaskDialog("Экспорт завершен");
                        tdResult.MainInstruction = "✅ Файл успешно создан!";
                        tdResult.MainContent =
                            $"📋 Элемент: {elementName}\n" +
                            $"🔧 Тип: {elementType}\n" +
                            $"📏 Площадь: {elementArea:F2} м²\n" +
                           
                            $"💾 Файл: {Path.GetFileName(outputPath)}";

                        tdResult.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                        tdResult.DefaultButton = TaskDialogResult.Yes;

                        if (tdResult.Show() == TaskDialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(outputPath);
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Ошибка", "❌ Не удалось заполнить Excel файл.");
                        return Result.Failed;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"❌ Ошибка при экспорте:\n{ex.Message}");
                return Result.Failed;
            }
        }

        // МЕТОД ДЛЯ ВЫБОРА ШАБЛОНА ФАЙЛА
        private bool SelectTemplateFile()
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
                openDialog.Title = "Выберите шаблон файла КС-2";
                openDialog.CheckFileExists = true;
                openDialog.Multiselect = false;

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    _templatePath = openDialog.FileName;

                    // Сохраняем путь в файл настроек
                    try
                    {
                        string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ks2_settings.txt");
                        File.WriteAllText(settingsFile, _templatePath);
                    }
                    catch { }

                    return true;
                }
            }

            return false;
        }

        // МЕТОДЫ ДЛЯ ПОЛУЧЕНИЯ ДАННЫХ ЭЛЕМЕНТА
        private string GetElementName(Element element)
        {
            if (element == null) return "Неизвестный элемент";

            string name = element.Name;

            if (string.IsNullOrEmpty(name))
            {
                Parameter nameParam = element.LookupParameter("Имя");
                if (nameParam != null && nameParam.HasValue)
                {
                    name = nameParam.AsString();
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                Parameter typeParam = element.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM);
                if (typeParam != null)
                {
                    ElementId typeId = typeParam.AsElementId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        ElementType type = element.Document.GetElement(typeId) as ElementType;
                        if (type != null)
                        {
                            name = type.Name;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                name = $"Элемент_{element.Id.IntegerValue}";
            }

            return name.Length > 100 ? name.Substring(0, 100) : name;
        }

        private double GetElementArea(Element element)
        {
            try
            {
                Parameter areaParam = element.LookupParameter("Площадь");
                if (areaParam != null && areaParam.HasValue)
                {
                    return UnitUtils.ConvertFromInternalUnits(areaParam.AsDouble(), UnitTypeId.SquareMeters);
                }

                areaParam = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null && areaParam.HasValue)
                {
                    return UnitUtils.ConvertFromInternalUnits(areaParam.AsDouble(), UnitTypeId.SquareMeters);
                }
            }
            catch { }

            return 0.0;
        }

        private string GetElementType(Element element)
        {
            try
            {
                Parameter typeParam = element.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM);
                if (typeParam != null)
                {
                    ElementId typeId = typeParam.AsElementId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        ElementType type = element.Document.GetElement(typeId) as ElementType;
                        if (type != null)
                        {
                            return type.Name;
                        }
                    }
                }
            }
            catch { }

            return "Неизвестный тип";
        }

        private string GetElementLevel(Element element, Document doc)
        {
            try
            {
                if (element.LevelId != ElementId.InvalidElementId)
                {
                    Level level = doc.GetElement(element.LevelId) as Level;
                    if (level != null)
                    {
                        return level.Name;
                    }
                }
            }
            catch { }

            return "Неизвестный уровень";
        }

        // ОСНОВНОЙ МЕТОД ДЛЯ ЗАПОЛНЕНИЯ EXCEL (ТОЛЬКО ДАННЫЕ, БЕЗ ФОРМАТИРОВАНИЯ)
        private bool FillExcelTemplate(string templatePath, string outputPath,
            string elementName, string elementId, double area, string type, string level)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                // ОТКРЫВАЕМ ШАБЛОН
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                workbook = excelApp.Workbooks.Open(templatePath);

                // ИЩЕМ ЛИСТ КС-2
                worksheet = FindWorksheet(workbook, "КС-2");
                if (worksheet == null)
                {
                    worksheet = (Excel.Worksheet)workbook.Worksheets[1];
                }

                // === ЗАПОЛНЯЕМ ТОЛЬКО ДАННЫЕ НАЧИНАЯ СО СТРОКИ 33 ===

                // СТРОКА 33: ЗАГОЛОВКИ ТАБЛИЦЫ (уже заполнены в шаблоне)
                // Ничего не меняем - оставляем как в шаблоне

                // СТРОКА 34: НОМЕРА КОЛОНОК (уже заполнены в шаблоне)
                // Ничего не меняем

                // СТРОКА 35: ПЕРВАЯ СТРОКА ДАННЫХ (ЭЛЕМЕНТ)
                SetCellValueOnly(worksheet, 35, 1, "1");                     // A35: Номер по порядку
                SetCellValueOnly(worksheet, 35, 2, "1.0");                   // B35: Позиция по смете
                SetCellValueOnly(worksheet, 35, 3, elementName);            // C35: НАИМЕНОВАНИЕ ЭЛЕМЕНТА - ГЛАВНОЕ!
                SetCellValueOnly(worksheet, 35, 4, "шт.");                   // D35: Единица измерения
                SetCellValueOnly(worksheet, 35, 5, "1");                     // E35: Выполнено работ
                SetCellValueOnly(worksheet, 35, 6, "1.00");                  // F35: Количество
                SetCellValueOnly(worksheet, 35, 7, "1000.00");               // G35: Цена материалы
                SetCellValueOnly(worksheet, 35, 8, "2000.00");               // H35: Цена работа
                SetCellFormulaOnly(worksheet, 35, 9, "=G35+H35");           // I35: Стоимость за ед.
                SetCellFormulaOnly(worksheet, 35, 10, "=F35*I35");          // J35: Стоимость всего

                // НЕ МЕНЯЕМ ФОРМАТИРОВАНИЕ!
                // НЕ МЕНЯЕМ ШИРИНУ СТОЛБЦОВ!
                // НЕ МЕНЯЕМ ВЫСОТУ СТРОК!
                // НЕ МЕНЯЕМ ШРИФТ!

                // СТРОКИ 36-...: МОЖНО ДОБАВИТЬ ДОПОЛНИТЕЛЬНЫЕ ДАННЫЕ НИЖЕ ТАБЛИЦЫ
                // (если в шаблоне есть место)
                SetCellValueOnly(worksheet, 50, 1, $"Экспортировано из Revit: {DateTime.Now:dd.MM.yyyy HH:mm}");
                SetCellValueOnly(worksheet, 51, 1, $"ID элемента: {elementId}");
                SetCellValueOnly(worksheet, 52, 1, $"Тип: {type}");
                SetCellValueOnly(worksheet, 53, 1, $"Площадь: {area:F2} м²");
                SetCellValueOnly(worksheet, 54, 1, $"Уровень: {level}");

                // СОХРАНЯЕМ КАК НОВЫЙ ФАЙЛ
                workbook.SaveAs(outputPath);

                return true;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка Excel", $"❌ Ошибка при заполнении шаблона:\n{ex.Message}");
                return false;
            }
            finally
            {
                CleanupExcel(excelApp, workbook, worksheet);
            }
        }

        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        private Excel.Worksheet FindWorksheet(Excel.Workbook workbook, string sheetName)
        {
            try
            {
                foreach (Excel.Worksheet sheet in workbook.Worksheets)
                {
                    if (sheet.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return sheet;
                    }
                }
            }
            catch { }

            return null;
        }

        // ТОЛЬКО ЗАПОЛНЕНИЕ ЗНАЧЕНИЙ, БЕЗ ФОРМАТИРОВАНИЯ
        private void SetCellValueOnly(Excel.Worksheet worksheet, int row, int column, object value)
        {
            try
            {
                // Используем Value2 вместо Value - меньше побочных эффектов
                worksheet.Cells[row, column].Value2 = value;
            }
            catch { }
        }

        // ТОЛЬКО УСТАНОВКА ФОРМУЛЫ, БЕЗ ФОРМАТИРОВАНИЯ
        private void SetCellFormulaOnly(Excel.Worksheet worksheet, int row, int column, string formula)
        {
            try
            {
                worksheet.Cells[row, column].Formula = formula;
            }
            catch { }
        }

        private void CleanupExcel(Excel.Application excelApp, Excel.Workbook workbook, Excel.Worksheet worksheet)
        {
            try
            {
                if (workbook != null)
                {
                    workbook.Close(true);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                }

                if (excelApp != null)
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }

                if (worksheet != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch { }
        }

        // МЕТОД ДЛЯ ЗАГРУЗКИ НАСТРОЕК ИЗ ФАЙЛА
        private void LoadSettings()
        {
            try
            {
                string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ks2_settings.txt");
                if (File.Exists(settingsFile))
                {
                    _templatePath = File.ReadAllText(settingsFile).Trim();

                    if (!File.Exists(_templatePath))
                    {
                        _templatePath = "";
                    }
                }
            }
            catch { }
        }

        // Добавим конструктор для загрузки настроек
        public ExportKS2Command()
        {
            LoadSettings();
        }
    }
}