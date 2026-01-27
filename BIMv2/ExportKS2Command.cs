using System;
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
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // ПРОВЕРЯЕМ ВЫБОР ЭЛЕМЕНТА
                var selectedIds = uiDoc.Selection.GetElementIds();
                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show("Внимание", "Пожалуйста, выберите элемент в модели перед запуском плагина.");
                    return Result.Cancelled;
                }

                // ПОЛУЧАЕМ ВЫБРАННЫЙ ЭЛЕМЕНТ (ТОЛЬКО ЧТЕНИЕ - ТРАНЗАКЦИЯ НЕ НУЖНА)
                var selectedElement = doc.GetElement(selectedIds.First());
                string elementName = GetElementName(selectedElement);

                // ЕСЛИ НУЖНО ЗАПИСАТЬ ЧТО-ТО В ЭЛЕМЕНТ, ТОГДА ИСПОЛЬЗУЕМ ТРАНЗАКЦИЮ:
                // using (Transaction trans = new Transaction(doc, "Экспорт элемента"))
                // {
                //     trans.Start();
                //     // Изменяем элемент
                //     selectedElement.LookupParameter("Статус").Set("Экспортирован");
                //     trans.Commit();
                // }

                // ДИАЛОГ СОХРАНЕНИЯ ФАЙЛА
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
                    saveDialog.FileName = $"В01_КС2_от_{DateTime.Now:ddMMyyyy}.xlsx";
                    saveDialog.Title = "Сохранить файл КС-2";

                    if (saveDialog.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string filePath = saveDialog.FileName;

                    // ПОКАЗЫВАЕМ ПРОГРЕСС
                    using (TaskDialog td = new TaskDialog("Экспорт КС-2"))
                    {
                        td.MainInstruction = "Экспорт данных в Excel";
                        td.MainContent = $"Элемент: {elementName}\n\nИдет создание файла...";
                        td.Show();
                    }

                    // ЭКСПОРТИРУЕМ В EXCEL
                    bool success = ExportToExcel(filePath, elementName);

                    if (success)
                    {
                        // ВОПРОС ОБ ОТКРЫТИИ ФАЙЛА
                        TaskDialog tdResult = new TaskDialog("Экспорт завершен");
                        tdResult.MainInstruction = "Файл успешно сохранен!";
                        tdResult.MainContent = $"Файл: {filePath}\n\nЭлемент: {elementName}";
                        tdResult.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                        tdResult.DefaultButton = TaskDialogResult.Yes;

                        if (tdResult.Show() == TaskDialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(filePath);
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Ошибка", "Не удалось создать Excel файл.");
                        return Result.Failed;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка при экспорте:\n{ex.Message}");
                return Result.Failed;
            }
        }

        private string GetElementName(Element element)
        {
            if (element == null) return "Неизвестный элемент";

            // ПРОБУЕМ РАЗНЫЕ СПОСОБЫ ПОЛУЧИТЬ ИМЯ
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

            // ОГРАНИЧИВАЕМ ДЛИНУ ДЛЯ EXCEL
            return name.Length > 100 ? name.Substring(0, 100) + "..." : name;
        }

        private bool ExportToExcel(string filePath, string elementName)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                // СОЗДАЕМ EXCEL
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                // СОЗДАЕМ НОВУЮ КНИГУ
                workbook = excelApp.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.Worksheets[1];
                worksheet.Name = "КС-2";

                // ЗАПОЛНЯЕМ ДАННЫЕ
                FillKS2Worksheet(worksheet, elementName);

                // СОХРАНЯЕМ
                workbook.SaveAs(filePath);
                return true;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка Excel", ex.Message);
                return false;
            }
            finally
            {
                // ОЧИСТКА РЕСУРСОВ
                try
                {
                    if (workbook != null)
                    {
                        workbook.Close(false);
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                    }

                    if (excelApp != null)
                    {
                        excelApp.Quit();
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                    }

                    // ОСВОБОЖДАЕМ COM-ОБЪЕКТЫ
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);

                    worksheet = null;
                    workbook = null;
                    excelApp = null;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch { }
            }
        }

        private void FillKS2Worksheet(Excel.Worksheet worksheet, string elementName)
        {
            // ===== ШАПКА ДОКУМЕНТА =====

            // Заголовок формы
            worksheet.Cells[2, 10] = "Унифицированная форма № КС-2";
            worksheet.Cells[2, 10].Font.Bold = true;
            worksheet.Cells[2, 10].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            worksheet.Cells[3, 10] = "Утверждена постановлением Госкомстата России от 11.11.99. № 100";
            worksheet.Cells[3, 10].Font.Size = 9;
            worksheet.Cells[3, 10].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // Основные поля
            worksheet.Cells[5, 1] = "Форма по ОКУД";
            worksheet.Cells[5, 9] = "0322005";

            // Технический заказчик
            worksheet.Cells[8, 1] = "Технический заказчик:";
            worksheet.Cells[8, 5] = "ООО \"ГРАВИОН-КОНСТРАКШН\"";

            // Подрядчик
            worksheet.Cells[10, 1] = "Подрядчик:";
            worksheet.Cells[10, 5] = "ООО «Энтер Констракшн»";

            // Стройка
            worksheet.Cells[12, 1] = "Стройка:";
            worksheet.Cells[12, 5] = "Жилой комплекс с подземной автостоянкой";

            // Объект
            worksheet.Cells[14, 1] = "Объект:";
            worksheet.Cells[14, 5] = "Жилой комплекс";

            // Договор
            worksheet.Cells[17, 7] = "Договор подряда";
            worksheet.Cells[17, 9] = "номер";
            worksheet.Cells[17, 11] = "ТЗ-59/В01-ГП";

            worksheet.Cells[18, 9] = "дата";
            worksheet.Cells[18, 11] = DateTime.Now.ToString("yyyy-MM-dd");

            // Дополнительное соглашение
            worksheet.Cells[19, 7] = "Дополнительное соглашение";
            worksheet.Cells[19, 9] = "номер";
            worksheet.Cells[19, 11] = "1";

            worksheet.Cells[20, 9] = "дата";
            worksheet.Cells[20, 11] = DateTime.Now.ToString("yyyy-MM-dd");

            // Заголовки таблицы дат
            worksheet.Cells[23, 5] = "Номер документа";
            worksheet.Cells[23, 7] = "Дата составления";
            worksheet.Cells[23, 9] = "Отчетный период";

            worksheet.Cells[24, 9] = "с";
            worksheet.Cells[24, 11] = "по";

            worksheet.Cells[25, 5] = "1";
            worksheet.Cells[25, 7] = DateTime.Now.ToString("yyyy-MM-dd");
            worksheet.Cells[25, 9] = "=J17";
            worksheet.Cells[25, 11] = DateTime.Now.ToString("yyyy-MM-dd");

            // Акт приемки
            worksheet.Cells[27, 1] = "АКТ";
            worksheet.Cells[27, 1].Font.Bold = true;
            worksheet.Cells[27, 1].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            worksheet.Cells[28, 1] = "О ПРИЕМКЕ ВЫПОЛНЕННЫХ РАБОТ";
            worksheet.Cells[28, 1].Font.Bold = true;
            worksheet.Cells[28, 1].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // Сметная стоимость
            worksheet.Cells[30, 1] = "Сметная (договорная) стоимость: 11 181 791 365 руб. 08 коп.";
            worksheet.Cells[30, 1].Font.Bold = true;

            // Примечание
            worksheet.Cells[31, 1] = "фрагмент ДС1 от 24.12.2025 (ВРЦ от 25.12.2025) согласовано с СО ГК (Буралова Н.)";
            worksheet.Cells[31, 1].Font.Italic = true;
            worksheet.Cells[31, 1].HorizontalAlignment = Excel.XlHAlign.xlHAlignRight;

            // ===== ТАБЛИЧНАЯ ЧАСТЬ =====

            // Заголовки столбцов (строка 32)
            string[] headers = {
                "Номер",
                "Наименование работ",
                "Номер единичной расценки",
                "Единица измерения",
                "Выполнено работ",
                "Количество",
                "Цена за ед. изм.\nматериалы",
                "Цена за ед. изм.\nработы",
                "Стоимость за ед. изм.",
                "Стоимость всего"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[32, i + 1] = headers[i];
                worksheet.Cells[32, i + 1].Font.Bold = true;
                worksheet.Cells[32, i + 1].WrapText = true;
            }

            // Подзаголовки (строка 33)
            worksheet.Cells[33, 1] = "по порядку";
            worksheet.Cells[33, 2] = "позиции по смете";

            // Номера колонок (строка 34)
            for (int i = 1; i <= 10; i++)
            {
                worksheet.Cells[34, i] = i.ToString();
            }

            // ===== ДАННЫЕ ЭЛЕМЕНТА (СТРОКА 35) =====
            worksheet.Cells[35, 1] = "1";                     // Номер по порядку
            worksheet.Cells[35, 2] = "1.0";                   // Позиция по смете
            worksheet.Cells[35, 3] = elementName;            // НАИМЕНОВАНИЕ ВЫБРАННОГО ЭЛЕМЕНТА
            worksheet.Cells[35, 4] = "шт.";                   // Единица измерения
            worksheet.Cells[35, 5] = "1";                     // Выполнено работ
            worksheet.Cells[35, 6] = "1.00";                  // Количество
            worksheet.Cells[35, 7] = "1000.00";               // Цена материалы
            worksheet.Cells[35, 8] = "2000.00";               // Цена работы
            worksheet.Cells[35, 9] = "=G35+H35";              // Стоимость за ед.
            worksheet.Cells[35, 10] = "=F35*I35";             // Стоимость всего

            // Форматирование числовых ячеек
            worksheet.Range["F35:J35"].NumberFormat = "#,##0.00";

            // ===== ФОРМАТИРОВАНИЕ =====

            // Границы для таблицы
            Excel.Range tableRange = worksheet.Range["A32:J35"];
            tableRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
            tableRange.Borders.Weight = Excel.XlBorderWeight.xlThin;

            // Внешняя граница таблицы
            tableRange.BorderAround(Excel.XlLineStyle.xlContinuous, Excel.XlBorderWeight.xlMedium);

            // Цвет фона для заголовков
            Excel.Range headerRange = worksheet.Range["A32:J34"];
            headerRange.Interior.Color = System.Drawing.Color.FromArgb(240, 240, 240);

            // Ширина столбцов
            worksheet.Columns["A"].ColumnWidth = 8;
            worksheet.Columns["B"].ColumnWidth = 12;
            worksheet.Columns["C"].ColumnWidth = 25;  // Широкий для имени элемента
            worksheet.Columns["D"].ColumnWidth = 12;
            worksheet.Columns["E"].ColumnWidth = 10;
            worksheet.Columns["F"].ColumnWidth = 10;
            worksheet.Columns["G"].ColumnWidth = 12;
            worksheet.Columns["H"].ColumnWidth = 12;
            worksheet.Columns["I"].ColumnWidth = 15;
            worksheet.Columns["J"].ColumnWidth = 15;

            // Высота строк
            worksheet.Rows[32].RowHeight = 40;
            worksheet.Rows[33].RowHeight = 30;
            worksheet.Rows[34].RowHeight = 20;
            worksheet.Rows[35].RowHeight = 25;

            // Выравнивание
            worksheet.Range["A1:J35"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
            worksheet.Range["A1:J35"].VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;

            // Для имени элемента - левое выравнивание
            worksheet.Cells[35, 3].HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;

            // Перенос текста
            worksheet.Range["A32:J35"].WrapText = true;

            // Шрифт
            worksheet.Range["A1:J35"].Font.Name = "Calibri";
            worksheet.Range["A1:J35"].Font.Size = 10;

            // Автоподбор остальных столбцов
            worksheet.Columns.AutoFit();
        }
    }
}