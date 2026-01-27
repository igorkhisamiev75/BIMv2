using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Excel = Microsoft.Office.Interop.Excel;

namespace RevitToKS2ExcelExport
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
                // Запрашиваем выбор элемента
                TaskDialog selectionDialog = new TaskDialog("Выбор элемента");
                selectionDialog.MainInstruction = "Выберите элемент для экспорта";
                selectionDialog.MainContent = "Пожалуйста, выберите элемент в модели, данные которого нужно экспортировать";
                selectionDialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;

                if (selectionDialog.Show() != TaskDialogResult.Ok)
                {
                    return Result.Cancelled;
                }

                // Получаем выбранный элемент
                Element selectedElement = null;
                try
                {
                    ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                    if (selectedIds.Count == 0)
                    {
                        TaskDialog.Show("Ошибка", "Пожалуйста, выберите элемент в модели");
                        return Result.Cancelled;
                    }

                    selectedElement = doc.GetElement(selectedIds.First());
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Ошибка выбора", "Не удалось получить выбранный элемент. Пожалуйста, выберите элемент в модели.");
                    return Result.Cancelled;
                }

                // Получаем имя элемента
                string elementName = GetElementName(selectedElement);

                // Создаем диалог для выбора места сохранения
                System.Windows.Forms.SaveFileDialog saveDialog = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"{elementName.Replace(" ", "_")}_КС2_№1_от_{DateTime.Now:dd.MM.yyyy}_v1.xlsx",
                    Title = "Сохранить файл КС-2"
                };

                if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                string filePath = saveDialog.FileName;

                // Экспортируем в Excel
                ExportToExcel(doc, filePath, selectedElement);

                // Показываем сообщение об успехе
                TaskDialog mainDialog = new TaskDialog("Экспорт завершен");
                mainDialog.MainInstruction = $"Файл успешно сохранен!";
                mainDialog.MainContent = $"Имя элемента: {elementName}\n\nФайл: {filePath}\n\nХотите открыть файл?";
                mainDialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                mainDialog.DefaultButton = TaskDialogResult.Yes;

                if (mainDialog.Show() == TaskDialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(filePath);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка при экспорте: {ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        private string GetElementName(Element element)
        {
            if (element == null) return "Неизвестный_элемент";

            // Пытаемся получить имя из разных параметров
            string name = element.Name;

            if (string.IsNullOrEmpty(name))
            {
                // Проверяем параметр "Имя"
                Parameter nameParam = element.LookupParameter("Имя");
                if (nameParam != null && nameParam.HasValue)
                {
                    name = nameParam.AsString();
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                // Проверяем параметр "Тип"
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
                // Используем номер Id
                name = $"Элемент_{element.Id.IntegerValue}";
            }

            // Убираем недопустимые символы для имени файла
            string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
            foreach (char c in invalidChars)
            {
                name = name.Replace(c.ToString(), "_");
            }

            return name;
        }

        private void ExportToExcel(Document doc, string filePath, Element selectedElement)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;

            try
            {
                // Создаем Excel приложение
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                // Создаем новую книгу
                workbook = excelApp.Workbooks.Add();

                // Создаем лист КС-2
                Excel.Worksheet ks2Worksheet = workbook.Worksheets.Add();
                ks2Worksheet.Name = "КС-2";

                // Создаем лист КС-3
                Excel.Worksheet ks3Worksheet = workbook.Worksheets.Add();
                ks3Worksheet.Name = "КС-3";

                // Заполняем лист КС-2
                FillKS2Worksheet(ks2Worksheet, doc, selectedElement);

                // Заполняем лист КС-3
                FillKS3Worksheet(ks3Worksheet, ks2Worksheet);

                // Удаляем лишние листы
                while (workbook.Worksheets.Count > 2)
                {
                    workbook.Worksheets[3].Delete();
                }

                // Сохраняем файл
                workbook.SaveAs(filePath);
            }
            finally
            {
                // Закрываем Excel
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
            }
        }

        private void FillKS2Worksheet(Excel.Worksheet worksheet, Document doc, Element selectedElement)
        {
            // Получаем имя выбранного элемента
            string elementName = GetElementName(selectedElement);

            // Стили для форматирования
            Excel.Range headerRange;

            // ===== ЗАГОЛОВОК И ШАПКА =====

            // Объединение ячеек для заголовка
            worksheet.Range["J2:T2"].Merge();
            worksheet.Range["J2:T2"].Value = "Унифицированная форма № КС-2";
            worksheet.Range["J2:T2"].Font.Bold = true;
            worksheet.Range["J2:T2"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            worksheet.Range["J3:T3"].Merge();
            worksheet.Range["J3:T3"].Value = "Утверждена постановлением Госкомстата России от 11.11.99. № 100";
            worksheet.Range["J3:T3"].Font.Size = 9;
            worksheet.Range["J3:T3"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // Код
            worksheet.Range["J4:T4"].Merge();
            worksheet.Range["J4:T4"].Value = "Код";
            worksheet.Range["J4:T4"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // Форма по ОКУД
            worksheet.Range["A5:H5"].Merge();
            worksheet.Range["A5:H5"].Value = "Форма по ОКУД";
            worksheet.Range["I5:T5"].Merge();
            worksheet.Range["I5:T5"].Value = "0322005";

            // ===== СЕКЦИЯ 1: УЧАСТНИКИ =====

            // Инвестор
            worksheet.Range["A6:D6"].Merge();
            worksheet.Range["A6:D6"].Value = "Инвестор:";
            worksheet.Range["E6:T6"].Merge();
            worksheet.Range["E6:T6"].Value = "";

            worksheet.Range["A7:D7"].Merge();
            worksheet.Range["A7:D7"].Value = "организация, адрес, телефон, факс";
            worksheet.Range["A7:D7"].Font.Italic = true;
            worksheet.Range["A7:D7"].Font.Color = System.Drawing.Color.Gray;

            // Технический заказчик
            worksheet.Range["A8:D8"].Merge();
            worksheet.Range["A8:D8"].Value = "Технический заказчик:";
            worksheet.Range["E8:T8"].Merge();
            worksheet.Range["E8:T8"].Value = "ООО \"ГРАВИОН-КОНСТРАКШН\" 127287, г. Москва, вн. тер. г. Муниципальный округ Савеловский, ул. Хуторская 2-я, д.38А, стр. 15, помещ./этаж X/4, ком. 44 , ИНН 7743130595, КПП 771301001";

            worksheet.Range["A9:D9"].Merge();
            worksheet.Range["A9:D9"].Value = "организация, адрес, телефон, факс";
            worksheet.Range["A9:D9"].Font.Italic = true;
            worksheet.Range["A9:D9"].Font.Color = System.Drawing.Color.Gray;

            // Подрядчик
            worksheet.Range["A10:D10"].Merge();
            worksheet.Range["A10:D10"].Value = "Подрядчик:";
            worksheet.Range["E10:T10"].Merge();
            worksheet.Range["E10:T10"].Value = "ООО «Энтер Констракшн» 115114, Г. Москва, Дебреневская набережная, дом 11, Этаж 1 ком. 72,72а,72б,72в, ИНН 9725037942, КПП 772501001";

            worksheet.Range["A11:D11"].Merge();
            worksheet.Range["A11:D11"].Value = "организация, адрес, телефон, факс";
            worksheet.Range["A11:D11"].Font.Italic = true;
            worksheet.Range["A11:D11"].Font.Color = System.Drawing.Color.Gray;

            // ===== СЕКЦИЯ 2: ОБЪЕКТ =====

            // Стройка
            worksheet.Range["A12:D12"].Merge();
            worksheet.Range["A12:D12"].Value = "Стройка:";
            worksheet.Range["E12:T12"].Merge();
            worksheet.Range["E12:T12"].Value = $"«Жилой комплекс с подземной автостоянкой, квартал В01, корпуса 1,2,3,4,5,6,7» по адресу: г. Москва, внутригородское муниципальное образование Кунцево, квартал 120». Подземная часть (общестроительные работы). Элемент: {elementName}";

            worksheet.Range["A13:D13"].Merge();
            worksheet.Range["A13:D13"].Value = "наименование, адрес";
            worksheet.Range["A13:D13"].Font.Italic = true;
            worksheet.Range["A13:D13"].Font.Color = System.Drawing.Color.Gray;

            // Объект
            worksheet.Range["A14:D14"].Merge();
            worksheet.Range["A14:D14"].Value = "Объект:";
            worksheet.Range["E14:T14"].Merge();
            worksheet.Range["E14:T14"].Value = $"«Жилой комплекс с подземной автостоянкой, квартал В01, корпуса 1,2,3,4,5,6,7» по адресу: г. Москва, внутригородское муниципальное образование Кунцево, квартал 120» Элемент: {elementName}";

            worksheet.Range["A15:D15"].Merge();
            worksheet.Range["A15:D15"].Value = "наименование";
            worksheet.Range["A15:D15"].Font.Italic = true;
            worksheet.Range["A15:D15"].Font.Color = System.Drawing.Color.Gray;

            // Вид деятельности
            worksheet.Range["A16:T16"].Merge();
            worksheet.Range["A16:T16"].Value = "Вид деятельности по ОКДП";

            // ===== СЕКЦИЯ 3: ДОГОВОР =====

            // Договор подряда
            worksheet.Range["G17:H17"].Merge();
            worksheet.Range["G17:H17"].Value = "Договор подряда";
            worksheet.Range["I17:J17"].Merge();
            worksheet.Range["I17:J17"].Value = "номер";
            worksheet.Range["K17:T17"].Merge();
            worksheet.Range["K17:T17"].Value = $"ТЗ-59/В01-ГП-{elementName.Substring(0, Math.Min(10, elementName.Length))}";

            worksheet.Range["G18:H18"].Merge();
            worksheet.Range["G18:H18"].Value = "";
            worksheet.Range["I18:J18"].Merge();
            worksheet.Range["I18:J18"].Value = "дата";
            worksheet.Range["K18:T18"].Merge();
            worksheet.Range["K18:T18"].Value = DateTime.Now.ToString("yyyy-MM-dd 00:00:00");

            // Дополнительное соглашение
            worksheet.Range["G19:H19"].Merge();
            worksheet.Range["G19:H19"].Value = "Дополнительное соглашение";
            worksheet.Range["I19:J19"].Merge();
            worksheet.Range["I19:J19"].Value = "номер";
            worksheet.Range["K19:T19"].Merge();
            worksheet.Range["K19:T19"].Value = "1";

            worksheet.Range["G20:H20"].Merge();
            worksheet.Range["G20:H20"].Value = "";
            worksheet.Range["I20:J20"].Merge();
            worksheet.Range["I20:J20"].Value = "дата";
            worksheet.Range["K20:T20"].Merge();
            worksheet.Range["K20:T20"].Value = DateTime.Now.ToString("yyyy-MM-dd 00:00:00");

            // Вид операции
            worksheet.Range["G21:H21"].Merge();
            worksheet.Range["G21:H21"].Value = "";
            worksheet.Range["I21:J21"].Merge();
            worksheet.Range["I21:J21"].Value = "Вид операции";
            worksheet.Range["K21:T21"].Merge();
            worksheet.Range["K21:T21"].Value = "";

            // ===== СЕКЦИЯ 4: ДАТЫ И ПЕРИОД =====

            // Пустая строка
            worksheet.Range["A22:T22"].Merge();
            worksheet.Range["A22:T22"].Value = "";

            // Заголовки для дат
            worksheet.Range["E23:F23"].Merge();
            worksheet.Range["E23:F23"].Value = "Номер документа";
            worksheet.Range["G23:H23"].Merge();
            worksheet.Range["G23:H23"].Value = "Дата составления";
            worksheet.Range["I23:J23"].Merge();
            worksheet.Range["I23:J23"].Value = "Отчетный период";

            worksheet.Range["I24:J24"].Merge();
            worksheet.Range["I24:J24"].Value = "с";
            worksheet.Range["K24:T24"].Merge();
            worksheet.Range["K24:T24"].Value = "по";

            // Данные
            worksheet.Range["E25:F25"].Merge();
            worksheet.Range["E25:F25"].Value = "1";
            worksheet.Range["G25:H25"].Merge();
            worksheet.Range["G25:H25"].Value = DateTime.Now.ToString("yyyy-MM-dd 00:00:00");
            worksheet.Range["I25:J25"].Merge();
            worksheet.Range["I25:J25"].Value = "=J17";
            worksheet.Range["K25:T25"].Merge();
            worksheet.Range["K25:T25"].Value = DateTime.Now.ToString("yyyy-MM-dd 00:00:00");

            // ===== СЕКЦИЯ 5: АКТ ПРИЕМКИ =====

            // Пустая строка
            worksheet.Range["A26:T26"].Merge();
            worksheet.Range["A26:T26"].Value = "";

            // Заголовок Акта
            worksheet.Range["A27:T27"].Merge();
            worksheet.Range["A27:T27"].Value = "АКТ";
            worksheet.Range["A27:T27"].Font.Bold = true;
            worksheet.Range["A27:T27"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            worksheet.Range["A28:T28"].Merge();
            worksheet.Range["A28:T28"].Value = "О ПРИЕМКЕ ВЫПОЛНЕННЫХ РАБОТ";
            worksheet.Range["A28:T28"].Font.Bold = true;
            worksheet.Range["A28:T28"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // Пустая строка
            worksheet.Range["A29:T29"].Merge();
            worksheet.Range["A29:T29"].Value = "";

            // Сметная стоимость
            worksheet.Range["A30:T30"].Merge();
            worksheet.Range["A30:T30"].Value = $"Сметная (договорная) стоимость в соответствии с договором подряда (субподряда): 11 181 791 365 руб. 08 коп. (Элемент: {elementName})";
            worksheet.Range["A30:T30"].Font.Bold = true;

            // ===== НАСТРОЙКА ШИРИНЫ СТОЛБЦОВ =====

            worksheet.Columns["A"].ColumnWidth = 5;
            worksheet.Columns["B"].ColumnWidth = 8;
            worksheet.Columns["C"].ColumnWidth = 15;
            worksheet.Columns["D"].ColumnWidth = 12;
            worksheet.Columns["E"].ColumnWidth = 10;
            worksheet.Columns["F"].ColumnWidth = 10;
            worksheet.Columns["G"].ColumnWidth = 15;
            worksheet.Columns["H"].ColumnWidth = 15;
            worksheet.Columns["I"].ColumnWidth = 12;
            worksheet.Columns["J"].ColumnWidth = 12;
            worksheet.Columns["K"].ColumnWidth = 15;
            worksheet.Columns["L"].ColumnWidth = 12;
            worksheet.Columns["M"].ColumnWidth = 15;
            worksheet.Columns["N"].ColumnWidth = 15;
            worksheet.Columns["O"].ColumnWidth = 15;
            worksheet.Columns["P"].ColumnWidth = 15;
            worksheet.Columns["Q"].ColumnWidth = 15;
            worksheet.Columns["R"].ColumnWidth = 15;
            worksheet.Columns["S"].ColumnWidth = 15;
            worksheet.Columns["T"].ColumnWidth = 15;

            // ===== ДОБАВЛЯЕМ ГРАНИЦЫ =====

            // Границы для основных ячеек
            Excel.Range[] rangesToBorder = {
                worksheet.Range["A5:T5"],
                worksheet.Range["A6:T6"],
                worksheet.Range["A7:T7"],
                worksheet.Range["A8:T8"],
                worksheet.Range["A9:T9"],
                worksheet.Range["A10:T10"],
                worksheet.Range["A11:T11"],
                worksheet.Range["A12:T12"],
                worksheet.Range["A13:T13"],
                worksheet.Range["A14:T14"],
                worksheet.Range["A15:T15"],
                worksheet.Range["A16:T16"],
                worksheet.Range["G17:T17"],
                worksheet.Range["G18:T18"],
                worksheet.Range["G19:T19"],
                worksheet.Range["G20:T20"],
                worksheet.Range["G21:T21"],
                worksheet.Range["E23:T23"],
                worksheet.Range["E24:T24"],
                worksheet.Range["E25:T25"],
                worksheet.Range["A27:T28"],
                worksheet.Range["A30:T30"]
            };

            foreach (Excel.Range range in rangesToBorder)
            {
                range.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                range.Borders.Weight = Excel.XlBorderWeight.xlThin;
            }
        }

        private void FillKS3Worksheet(Excel.Worksheet ks3Worksheet, Excel.Worksheet ks2Worksheet)
        {
            // Создаем ссылки на данные из КС-2
            ks3Worksheet.Range["B7"].Formula = "='КС-2'!B7";
            ks3Worksheet.Range["M7"].Formula = "='КС-2'!J7";
            ks3Worksheet.Range["B9"].Formula = "='КС-2'!B9";
            ks3Worksheet.Range["M9"].Formula = "='КС-2'!J9";
            ks3Worksheet.Range["B11"].Formula = "='КС-2'!B11";
            ks3Worksheet.Range["B13"].Formula = "='КС-2'!B13:D13";
            ks3Worksheet.Range["K15"].Formula = "='КС-2'!J16";
            ks3Worksheet.Range["K16"].Formula = "='КС-2'!J17";
            ks3Worksheet.Range["K17"].Formula = "=IF('КС-2'!J18=\"\",\"\",'КС-2'!J18)";
            ks3Worksheet.Range["K18"].Formula = "=IF('КС-2'!J19=\"\",\"\",'КС-2'!J19)";
            ks3Worksheet.Range["E23"].Formula = "='КС-2'!F24";
            ks3Worksheet.Range["F23"].Formula = "='КС-2'!G24";
            ks3Worksheet.Range["I23"].Formula = "='КС-2'!I24";
            ks3Worksheet.Range["K23"].Formula = "='КС-2'!J24";

            // Заголовки КС-3
            ks3Worksheet.Range["M1:P1"].Merge();
            ks3Worksheet.Range["M1:P1"].Value = "Унифицированная форма № КС-3";
            ks3Worksheet.Range["M1:P1"].Font.Bold = true;
            ks3Worksheet.Range["M1:P1"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            ks3Worksheet.Range["M2:P2"].Merge();
            ks3Worksheet.Range["M2:P2"].Value = "Утверждена постановлением Госкомстата России от 11.11.99. № 100";
            ks3Worksheet.Range["M2:P2"].Font.Size = 9;
            ks3Worksheet.Range["M2:P2"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            ks3Worksheet.Range["M3:P3"].Merge();
            ks3Worksheet.Range["M3:P3"].Value = "Коды";
            ks3Worksheet.Range["M3:P3"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // Форма по ОКУД
            ks3Worksheet.Range["L4"].Value = "Форма по ОКУД";

            // Заголовок Инвестор
            ks3Worksheet.Range["A5"].Value = "Инвестор:";
            ks3Worksheet.Range["L5"].Value = "по ОКПО";

            // Технический заказчик
            ks3Worksheet.Range["A6"].Value = "Технический заказчик:";

            ks3Worksheet.Range["L7"].Value = "по ОКПО";

            // Подрядчик
            ks3Worksheet.Range["A8"].Value = "Подрядчик:";

            // Стройка
            ks3Worksheet.Range["A10"].Value = "Стройка:";

            // Объект
            ks3Worksheet.Range["A12"].Value = "Объект:";

            // Вид деятельности
            ks3Worksheet.Range["M14"].Value = "Вид деятельности по ОКДП";

            // Договор подряда
            ks3Worksheet.Range["J15"].Value = "Договор подряда";
            ks3Worksheet.Range["K15"].Value = "номер";

            ks3Worksheet.Range["J16"].Value = "";
            ks3Worksheet.Range["K16"].Value = "дата";

            // Дополнительное соглашение
            ks3Worksheet.Range["J17"].Value = "Дополнительное соглашение";
            ks3Worksheet.Range["K17"].Value = "номер";

            ks3Worksheet.Range["J18"].Value = "";
            ks3Worksheet.Range["K18"].Value = "дата";

            // Вид операции
            ks3Worksheet.Range["J19"].Value = "";
            ks3Worksheet.Range["K19"].Value = "Вид операции";

            // Заголовок для номеров и дат
            ks3Worksheet.Range["D21"].Value = "Номер документа";
            ks3Worksheet.Range["E21"].Value = "Дата составления";
            ks3Worksheet.Range["I21"].Value = "Отчетный период";
            ks3Worksheet.Range["J21"].Value = "с";
            ks3Worksheet.Range["K21"].Value = "по";

            // Заголовок СПРАВКИ
            ks3Worksheet.Range["A25:P25"].Merge();
            ks3Worksheet.Range["A25:P25"].Value = "СПРАВКА";
            ks3Worksheet.Range["A25:P25"].Font.Bold = true;
            ks3Worksheet.Range["A25:P25"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            ks3Worksheet.Range["A26:P26"].Merge();
            ks3Worksheet.Range["A26:P26"].Value = "СТОИМОСТИ ВЫПОЛНЕННЫХ РАБОТ И ЗАТРАТ";
            ks3Worksheet.Range["A26:P26"].Font.Bold = true;
            ks3Worksheet.Range["A26:P26"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // Настраиваем ширину столбцов
            ks3Worksheet.Columns["A"].ColumnWidth = 15;
            ks3Worksheet.Columns["B"].ColumnWidth = 40;
            ks3Worksheet.Columns["C"].ColumnWidth = 10;
            ks3Worksheet.Columns["D"].ColumnWidth = 15;
            ks3Worksheet.Columns["E"].ColumnWidth = 12;
            ks3Worksheet.Columns["F"].ColumnWidth = 5;
            ks3Worksheet.Columns["G"].ColumnWidth = 15;
            ks3Worksheet.Columns["H"].ColumnWidth = 15;
            ks3Worksheet.Columns["I"].ColumnWidth = 15;
            ks3Worksheet.Columns["J"].ColumnWidth = 10;
            ks3Worksheet.Columns["K"].ColumnWidth = 12;
            ks3Worksheet.Columns["L"].ColumnWidth = 10;
            ks3Worksheet.Columns["M"].ColumnWidth = 15;
            ks3Worksheet.Columns["N"].ColumnWidth = 15;
            ks3Worksheet.Columns["O"].ColumnWidth = 15;
            ks3Worksheet.Columns["P"].ColumnWidth = 15;
        }
    }
}