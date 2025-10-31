using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;

using Application = Microsoft.Office.Interop.Excel.Application;
using Label = System.Windows.Forms.Label;
using Parameter = Autodesk.Revit.DB.Parameter;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class RoomExportCommand : IExternalCommand
    {
        // Список разрешенных имен помещений в нужном порядке приоритета
        private readonly List<string> _allowedRoomNames = new List<string>
        {
            "Жилая комната","Мастер-спальня", "Гардеробная", "Гостинная", "Кухня", "Кухня-ниша", "Кухня-гостиная",
            "Коридор", "Душевая", "Ванная", "Кладовая", "Санузел", "Постирочная", "Балкон", "Прихожая", "Терраса", "Лоджия"
        };

        private string _exportedFilePath;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Собираем все помещения
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> rooms = collector.OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .ToElements();

                // Создаем и показываем прогресс-бар
                using (ProgressForm progressForm = new ProgressForm())
                {
                    progressForm.Show();
                    System.Windows.Forms.Application.DoEvents();

                    // Группируем помещения по квартирам и создаем объекты квартир
                    progressForm.UpdateProgress("Обработка помещений...", 10);
                    var apartments = CreateApartmentsFromRooms(rooms, doc, progressForm);

                    // Экспортируем в Excel
                    progressForm.UpdateProgress("Экспорт в Excel...", 70);
                    ExportToExcel(apartments, doc, progressForm);

                    progressForm.UpdateProgress("Завершение...", 100);
                    System.Threading.Thread.Sleep(500);
                }

                // Показываем диалог с вопросом об открытии файла
                TaskDialog mainDialog = new TaskDialog("Экспорт завершен");
                mainDialog.MainInstruction = $"Экспорт {GetApartmentCountFromRooms(rooms)} квартир завершен успешно!";
                mainDialog.MainContent = $"Файл сохранен по пути:\n{_exportedFilePath}";
                mainDialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                mainDialog.DefaultButton = TaskDialogResult.Yes;
                mainDialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;

                TaskDialogResult result = mainDialog.Show();

                if (result == TaskDialogResult.Yes)
                {
                    // Открываем файл Excel
                    System.Diagnostics.Process.Start(_exportedFilePath);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка при экспорте: {ex.Message}");
                return Result.Failed;
            }
        }

        // Вспомогательный метод для получения количества квартир
        private int GetApartmentCountFromRooms(ICollection<Element> rooms)
        {
            var apartmentNumbers = new HashSet<string>();
            foreach (Element element in rooms)
            {
                Room room = element as Room;
                if (room == null || room.Area == 0) continue;

                string roomPurpose = GetParameterValueAsString(room, "Назначение");
                if (roomPurpose != "Квартиры")
                    continue;

                string apartmentNumber = GetParameterValueAsString(room, "ADSK_Номер квартиры");
                if (!string.IsNullOrEmpty(apartmentNumber))
                {
                    apartmentNumbers.Add(apartmentNumber);
                }
            }
            return apartmentNumbers.Count;
        }

        // Функция для обрезки числовых значений из имени помещения
        private string CleanRoomName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
                return "Без имени";

            // Удаляем числовые значения в конце строки (цифры, точки, пробелы)
            string cleanedName = Regex.Replace(roomName, @"\s*[\d\.]+\s*$", "").Trim();

            // Если после очистки строка пустая, возвращаем оригинальное имя
            if (string.IsNullOrEmpty(cleanedName))
                return roomName;

            return cleanedName;
        }

        // Функция для получения номера уровня из элемента
        private int GetLevelNumber(Element element, Document doc)
        {
            if (element.LevelId != ElementId.InvalidElementId)
            {
                Level level = doc.GetElement(element.LevelId) as Level;
                if (level != null)
                {
                    // Пытаемся извлечь числовое значение из имени уровня
                    string levelName = level.Name;
                    Match match = Regex.Match(levelName, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out int levelNumber))
                    {
                        return levelNumber;
                    }

                    // Если не удалось извлечь число, используем высоту уровня для сортировки
                    return (int)Math.Round(UnitUtils.ConvertFromInternalUnits(level.Elevation, UnitTypeId.Meters));
                }
            }
            return 0;
        }

        // Функция для форматирования номера квартиры с ведущими нулями
        private string FormatApartmentNumber(string apartmentNumber)
        {
            if (string.IsNullOrEmpty(apartmentNumber))
                return "0000";

            // Если номер содержит не только цифры, возвращаем как есть
            if (!Regex.IsMatch(apartmentNumber, @"^\d+$"))
                return apartmentNumber;

            // Форматируем числовой номер с ведущими нулями
            if (int.TryParse(apartmentNumber, out int number))
            {
                if (number < 10)
                    return $"000{number}";
                else if (number < 100)
                    return $"00{number}";
                else if (number < 1000)
                    return $"0{number}";
                else
                    return number.ToString();
            }

            return apartmentNumber;
        }

        // Функция для получения числового значения марки квартиры для сортировки
        private int GetApartmentMarkSortValue(string apartmentMark)
        {
            if (string.IsNullOrEmpty(apartmentMark))
                return int.MaxValue;

            // Пытаемся извлечь числовое значение из марки квартиры
            if (int.TryParse(apartmentMark, out int number))
            {
                return number;
            }

            // Если не число, извлекаем первую числовую последовательность
            Match match = Regex.Match(apartmentMark, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int extractedNumber))
            {
                return extractedNumber;
            }

            // Если не удалось извлечь число, используем максимальное значение для сортировки в конец
            return int.MaxValue;
        }

        private List<Apartment> CreateApartmentsFromRooms(ICollection<Element> rooms, Document doc, ProgressForm progressForm)
        {
            var apartmentDict = new Dictionary<string, Apartment>();
            int totalRooms = rooms.Count;
            int processedRooms = 0;

            foreach (Element element in rooms)
            {
                Room room = element as Room;
                if (room == null || room.Area == 0) continue;

                // Обновляем прогресс
                processedRooms++;
                int progress = 10 + (int)((double)processedRooms / totalRooms * 50); // 10-60%
                progressForm.UpdateProgress($"Обработка помещений ({processedRooms}/{totalRooms})...", progress);

                // Проверяем назначение помещения - должно быть "Квартиры"
                string roomPurpose = GetParameterValueAsString(room, "Назначение");
                if (roomPurpose != "Квартиры")
                    continue;

                string apartmentNumber = GetParameterValueAsString(room, "ADSK_Номер квартиры");
                if (string.IsNullOrEmpty(apartmentNumber))
                    apartmentNumber = "Без номера";

                // Получаем номер уровня
                int levelNumber = GetLevelNumber(room, doc);

                // Получаем марку квартиры ФРС для условного номера помещения
                string apartmentMark = GetParameterValueAsString(room, "LG_МаркаКвартирыФРС");

                // Форматируем марку квартиры с ведущими нулями (если это число)
                string formattedApartmentMark = !string.IsNullOrEmpty(apartmentMark) ?
                    FormatApartmentNumber(apartmentMark) :
                    null;

                // Получаем числовое значение для сортировки
                int sortValue = GetApartmentMarkSortValue(apartmentMark);

                // Если квартиры с таким номером еще нет в словаре, создаем ее
                if (!apartmentDict.ContainsKey(apartmentNumber))
                {
                    apartmentDict[apartmentNumber] = new Apartment
                    {
                        ApartmentNumber = apartmentNumber,
                        ApartmentMark = apartmentMark, // Оригинальная марка квартиры ФРС
                        FormattedApartmentMark = formattedApartmentMark, // Отформатированная марка с нулями
                        ApartmentMarkSortValue = sortValue, // Числовое значение для сортировки
                        LevelNumber = levelNumber,
                        MarketingSubtype = GetParameterValueAsString(room, "LG_Маркетинговый тип"),
                        HorizontalAxis = GetParameterValueAsString(room, "ADSK_Примечание"),
                        VerticalAxis = GetParameterValueAsString(room, "LG_Номер этажа"),
                        RoomType = GetParameterValueAsString(room, "ADSK_Тип квартиры"),
                        RoomsType = GetParameterValueAsString(room, "LG_Тип квартиры"),
                        MarketingType = GetParameterValueAsString(room, "LG_Маркетинговый тип"),
                        LivingArea = GetParameterValueAsDouble(room, "ADSK_Площадь квартиры жилая"),
                        CeilingHeight = GetParameterValueAsDouble(room, "LG_Высота потолка"),
                        RoomPurpose = roomPurpose,
                        BuildingNumber = GetParameterValueAsString(room, "LG_Номер корпуса"),
                        Section = GetParameterValueAsString(room, "LG_Секция"),
                        TotalArea = GetParameterValueAsDouble(room, "ADSK_Площадь квартиры общая"),
                        ReducedArea = GetParameterValueAsDouble(room, "ADSK_Площадь квартиры"),
                        Rooms = new List<RoomInfo>(),
                        BalconiesAndLoggias = new List<string>()
                    };
                }

                var apartment = apartmentDict[apartmentNumber];

                // Получаем и очищаем имя помещения
                string originalRoomName = room.Name ?? "Без имени";
                string cleanedRoomName = CleanRoomName(originalRoomName);

                // Проверяем, является ли помещение разрешенным (после очистки)
                bool isAllowedRoom = _allowedRoomNames.Any(allowedName =>
                    cleanedRoomName.Equals(allowedName, StringComparison.OrdinalIgnoreCase));

                // Или является ли помещение балконом/лоджией/террасой
                bool isBalconyOrLoggia = cleanedRoomName.IndexOf("балкон", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        cleanedRoomName.IndexOf("лоджия", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        cleanedRoomName.IndexOf("терраса", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        cleanedRoomName.IndexOf("веранда", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isAllowedRoom || isBalconyOrLoggia)
                {
                    double roomArea = UnitUtils.ConvertFromInternalUnits(room.Area, UnitTypeId.SquareMeters);

                    // ИСПОЛЬЗУЕМ AsValueString ДЛЯ ПАРАМЕТРА "ADSK_Площадь с коэффициентом"
                    double roomAreaWithCoefficient = GetParameterValueAsDoubleFromString(room, "ADSK_Площадь с коэффициентом");

                    // Создаем уникальное имя помещения с номером, если есть дубликаты
                    string finalRoomName = cleanedRoomName;
                    int sameNameCount = apartment.Rooms.Count(r => r.CleanedName == cleanedRoomName);
                    if (sameNameCount > 0)
                    {
                        finalRoomName = $"{cleanedRoomName}_{sameNameCount + 1}";
                    }

                    apartment.Rooms.Add(new RoomInfo
                    {
                        OriginalName = originalRoomName,
                        CleanedName = cleanedRoomName,
                        FinalName = finalRoomName,
                        Area = roomArea,
                        AreaWithCoefficient = roomAreaWithCoefficient
                    });

                    // Добавляем в список балконов/лоджий если нужно
                    if (isBalconyOrLoggia && !apartment.BalconiesAndLoggias.Contains(cleanedRoomName))
                    {
                        apartment.BalconiesAndLoggias.Add(cleanedRoomName);
                    }
                }
            }

            return apartmentDict.Values.ToList();
        }

        private void ExportToExcel(List<Apartment> apartments, Document doc, ProgressForm progressForm)
        {
            Application excelApp = new Application();
            excelApp.Visible = false;
            excelApp.DisplayAlerts = false;

            Workbook workbook = excelApp.Workbooks.Add();
            Worksheet worksheet = workbook.ActiveSheet;

            try
            {
                int col = 1;

                // ЖЕСТКО ЗАДАЕМ ВСЕ ЗАГОЛОВКИ В НУЖНОМ ПОРЯДКЕ
                var allHeaders = new List<string>
        {
            "Условный номер помещения",
            "Маркетинговый подтип 2_1_0Э, 2_2_0",
            "Тип планировки 2_1, 2_2",
            "Горизонтальная ось",
            "Вертикальная ось",
            "Вид помещения студия, _1 сп, _2 сп",
            "Балкон/лоджия/терраса",
            "Комнат пиб _студия_1 ккв _2 ккв _3 ккв",
            "Маркетинговый тип 2_М, 2_L",
            "Площадь жилая",
            "Высота потолка",
            "Назначение помещения",
            "Тип помещения кв., комм., паркинг",
            "Площадь комнаты 1",
            "Площадь комнаты 2",
            "Площадь комнаты 3",
            "Площадь комнаты 4",
            "Площадь комнаты 5",
            "мастер-спальня",
            "Площадь гардеробной 1",
            "Площадь гардеробной 2",
            "Площадь гардеробной 3",
            "Площадь гардеробной 4",
            "Площадь гардеробной 5",
            "Площадь гостиной 1",
            "Площадь кухни",
            "Площадь кухни-ниши",
            "Площадь кухни-гостиной",
            "Площадь коридора 1",
            "Площадь коридора 2",
            "Площадь коридора 3",
            "Площадь коридора 4",
            "Площадь коридора 5",
            "Площадь коридора 6",
            "Площадь коридора 7",
            "Площадь коридора 8",
            "Площадь коридора 9",
            "Площадь душевой",
            "Площадь душевой 1",
            "Площадь душевой 2",
            "Площадь ванной 1",
            "Площадь кладовой 1",
            "Площадь кладовой 2",
            "Площадь кладовой 3",
            "Площадь кладовой 4",
            "Площадь кладовой 5",
            "Площадь кладовой 6",
            "Площадь кладовой 7",
            "Площадь кладовой 8",
            "Площадь кладовой 9",
            "Площадь санузла 1",
            "Площадь санузла 2",
            "Площадь санузла 3",
            "Площадь санузла 4",
            "Площадь санузла 5",
            "Площадь санузла 6",
            "Площадь санузла 7",
            "Площадь санузла 8",
            "Площадь санузла 9",
            "Площадь санузла 10",
            "Площадь санузла 11",
            "Площадь постирочной",
            "Площадь балкона 1",
            "Площадь балкона 2",
            "Площадь балкона 3",
            "Площадь балкона 4",
            "Площадь балкона 5",
            "Площадь балкона 6",
            "Площадь балкона 7",
            "Площадь балкона 8",
            "Площадь балкона 9",
            "Площадь прихожей",
            "Площадь террасы 1",
            "Площадь террасы 2",
            "Площадь террасы 3",
            "Площадь террасы 4",
            "Площадь террасы 5",
            "Площадь лоджии",
            "Площадь лоджии 2",
            "Площадь лоджии 3",
            "Площадь балкона лоджий, балконов, веранд, террас с коэффициентами, кв.м.",
            "Площадь общ.",
            "Площадь прив.",
            "Корпус",
            "Секция",
            "Заморозка архитекторами"
        };

                // Добавляем все заголовки
                foreach (var header in allHeaders)
                {
                    worksheet.Cells[1, col++] = header;
                }

                // Форматирование заголовков
                Range headerRange = worksheet.Range["A1", GetExcelColumnName(col - 1) + "1"];
                headerRange.Font.Bold = false;
                headerRange.Interior.Color = System.Drawing.Color.White;
                headerRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                headerRange.VerticalAlignment = XlVAlign.xlVAlignCenter;
                headerRange.WrapText = true;

                // Устанавливаем высоту первой строки
                worksheet.Rows[1].RowHeight = 125;

                // ДОБАВЛЯЕМ ЦВЕТОВОЕ ОФОРМЛЕНИЕ ЗАГОЛОВКОВ
                ApplyHeaderFormatting(worksheet, col);

                // ФИКСИРУЕМ ПЕРВУЮ СТРОКУ
                worksheet.Application.ActiveWindow.SplitRow = 1;
                worksheet.Application.ActiveWindow.FreezePanes = true;

                // Устанавливаем текстовый формат для столбца с условным номером помещения (столбец A)
                Range apartmentNumberColumn = worksheet.Range["A:A"];
                apartmentNumberColumn.NumberFormat = "@";

                // Корпус (столбец 84) - CF в Excel
                worksheet.Range["CF:CF"].NumberFormat = "@";

                // Секция (столбец 85) - CG в Excel
                worksheet.Range["CG:CG"].NumberFormat = "@";

                // Устанавливаем числовые форматы для столбцов с площадями и высотой потолка
                // Площадь жилая (столбец J)
                worksheet.Range["J:J"].NumberFormat = "0.000";
                // Высота потолка (столбец K)
                worksheet.Range["K:K"].NumberFormat = "0.00";

                // Форматирование для всех столбцов с площадями помещений (столбцы 14-83)
                for (int i = 14; i <= 83; i++)
                {
                    worksheet.Range[GetExcelColumnName(i) + ":" + GetExcelColumnName(i)].NumberFormat = "0.000";
                }

                // Площадь с коэффициентами (столбец 84)
                worksheet.Range[GetExcelColumnName(84) + ":" + GetExcelColumnName(84)].NumberFormat = "0.000";

                // Площадь общ. (столбец 85)
                worksheet.Range[GetExcelColumnName(85) + ":" + GetExcelColumnName(85)].NumberFormat = "0.000";

                // Площадь прив. (столбец 86)
                worksheet.Range[GetExcelColumnName(86) + ":" + GetExcelColumnName(86)].NumberFormat = "0.000";

                // СОРТИРУЕМ КВАРТИРЫ
                var sortedApartments = apartments
                    .OrderBy(a => a.ApartmentMarkSortValue)
                    .ThenBy(a => a.ApartmentMark)
                    .ThenBy(a => a.LevelNumber)
                    .ThenBy(a => a.ApartmentNumber)
                    .ToList();

                int row = 2;
                int totalApartments = sortedApartments.Count;
                int processedApartments = 0;

                // СОЗДАЕМ СЛОВАРЬ ДЛЯ СООТВЕТСТВИЯ ЗАГОЛОВКОВ И ТИПОВ ПОМЕЩЕНИЙ
                var headerToRoomType = new Dictionary<string, string>
        {
            {"Площадь комнаты 1", "Жилая комната"},
            {"Площадь комнаты 2", "Жилая комната"},
            {"Площадь комнаты 3", "Жилая комната"},
            {"Площадь комнаты 4", "Жилая комната"},
            {"Площадь комнаты 5", "Жилая комната"},
            {"Площадь гардеробной 1", "Гардеробная"},
            {"Площадь гардеробной 2", "Гардеробная"},
            {"Площадь гардеробной 3", "Гардеробная"},
            {"Площадь гардеробной 4", "Гардеробная"},
            {"Площадь гардеробной 5", "Гардеробная"},
            {"Площадь гостиной 1", "Гостинная"},
            {"Площадь кухни", "Кухня"},
            {"Площадь кухни-ниши", "Кухня-ниша"},
            {"Площадь кухни-гостиной", "Кухня-гостиная"},
            {"Площадь коридора 1", "Коридор"},
            {"Площадь коридора 2", "Коридор"},
            {"Площадь коридора 3", "Коридор"},
            {"Площадь коридора 4", "Коридор"},
            {"Площадь коридора 5", "Коридор"},
            {"Площадь коридора 6", "Коридор"},
            {"Площадь коридора 7", "Коридор"},
            {"Площадь коридора 8", "Коридор"},
            {"Площадь коридора 9", "Коридор"},
            {"Площадь душевой", "Душевая"},
            {"Площадь душевой 1", "Душевая"},
            {"Площадь душевой 2", "Душевая"},
            {"Площадь ванной 1", "Ванная"},
            {"Площадь кладовой 1", "Кладовая"},
            {"Площадь кладовой 2", "Кладовая"},
            {"Площадь кладовой 3", "Кладовая"},
            {"Площадь кладовой 4", "Кладовая"},
            {"Площадь кладовой 5", "Кладовая"},
            {"Площадь кладовой 6", "Кладовая"},
            {"Площадь кладовой 7", "Кладовая"},
            {"Площадь кладовой 8", "Кладовая"},
            {"Площадь кладовой 9", "Кладовая"},
            {"Площадь санузла 1", "Санузел"},
            {"Площадь санузла 2", "Санузел"},
            {"Площадь санузла 3", "Санузел"},
            {"Площадь санузла 4", "Санузел"},
            {"Площадь санузла 5", "Санузел"},
            {"Площадь санузла 6", "Санузел"},
            {"Площадь санузла 7", "Санузел"},
            {"Площадь санузла 8", "Санузел"},
            {"Площадь санузла 9", "Санузел"},
            {"Площадь санузла 10", "Санузел"},
            {"Площадь санузла 11", "Санузел"},
            {"Площадь постирочной", "Постирочная"},
            {"Площадь балкона 1", "Балкон"},
            {"Площадь балкона 2", "Балкон"},
            {"Площадь балкона 3", "Балкон"},
            {"Площадь балкона 4", "Балкон"},
            {"Площадь балкона 5", "Балкон"},
            {"Площадь балкона 6", "Балкон"},
            {"Площадь балкона 7", "Балкон"},
            {"Площадь балкона 8", "Балкон"},
            {"Площадь балкона 9", "Балкон"},
            {"Площадь прихожей", "Прихожая"},
            {"Площадь террасы 1", "Терраса"},
            {"Площадь террасы 2", "Терраса"},
            {"Площадь террасы 3", "Терраса"},
            {"Площадь террасы 4", "Терраса"},
            {"Площадь террасы 5", "Терраса"},
            {"Площадь лоджии", "Лоджия"},
            {"Площадь лоджии 2", "Лоджия"},
            {"Площадь лоджии 3", "Лоджия"}
        };

                foreach (var apartment in sortedApartments)
                {
                    processedApartments++;
                    int progress = 70 + (int)((double)processedApartments / totalApartments * 25);
                    progressForm.UpdateProgress($"Экспорт квартир ({processedApartments}/{totalApartments})...", progress);

                    col = 1;

                    // Условный номер помещения
                    string conditionalNumber = !string.IsNullOrEmpty(apartment.FormattedApartmentMark) ?
                        apartment.FormattedApartmentMark :
                        (!string.IsNullOrEmpty(apartment.ApartmentMark) ?
                            apartment.ApartmentMark :
                            FormatApartmentNumber(apartment.ApartmentNumber));
                    worksheet.Cells[row, col++] = conditionalNumber;

                    // Маркетинговый подтип 2_1_0Э, 2_2_0 - пустой
                    worksheet.Cells[row, col++] = "";

                    // Тип планировки 2_1, 2_2 - пустой
                    worksheet.Cells[row, col++] = "";

                    // Горизонтальная ось
                    worksheet.Cells[row, col++] = apartment.HorizontalAxis;

                    // Вертикальная ось
                    worksheet.Cells[row, col++] = apartment.VerticalAxis;

                    // Вид помещения
                    worksheet.Cells[row, col++] = FormatRoomType(apartment.RoomType);

                    // Балкон/лоджия/терраса
                    string balconyLoggiaText = FormatBalconyLoggiaText(apartment);
                    worksheet.Cells[row, col++] = balconyLoggiaText;

                    // Комнат пиб
                    worksheet.Cells[row, col++] = FormatRoomType2(apartment.RoomType);

                    // Маркетинговый тип
                    string marketingType = FormatMarketingType(apartment.MarketingType);
                    worksheet.Cells[row, col++] = marketingType;

                    // Площадь жилая
                    if (apartment.LivingArea > 0)
                    {
                        double livingArea = UnitUtils.ConvertFromInternalUnits(apartment.LivingArea, UnitTypeId.SquareMeters);
                        worksheet.Cells[row, col++] = Math.Round(livingArea, 3);
                    }
                    else
                    {
                        worksheet.Cells[row, col++] = "";
                    }

                    // Высота потолка
                    if (apartment.CeilingHeight > 0)
                    {
                        double ceilingHeight = UnitUtils.ConvertFromInternalUnits(apartment.CeilingHeight, UnitTypeId.Meters);
                        worksheet.Cells[row, col++] = Math.Round(ceilingHeight, 2);
                    }
                    else
                    {
                        worksheet.Cells[row, col++] = "";
                    }

                    // Назначение помещения - пустой
                    worksheet.Cells[row, col++] = "";

                    // Тип помещения
                    worksheet.Cells[row, col++] = "Квартира";

                    // СОЗДАЕМ СЛОВАРЬ ПЛОЩАДЕЙ ПО ТИПАМ ПОМЕЩЕНИЙ
                    var roomAreasByType = new Dictionary<string, List<double>>();
                    foreach (var room in apartment.Rooms.Where(r => _allowedRoomNames.Contains(r.CleanedName)))
                    {
                        string roomType = room.CleanedName;
                        double areaToUse = room.AreaWithCoefficient > 0 ? room.AreaWithCoefficient : room.Area;

                        if (!roomAreasByType.ContainsKey(roomType))
                            roomAreasByType[roomType] = new List<double>();

                        roomAreasByType[roomType].Add(areaToUse);
                    }

                    // ЗАПОЛНЯЕМ ПЛОЩАДИ ДЛЯ ВСЕХ ФИКСИРОВАННЫХ СТОЛБЦОВ (14-83)
                    for (int i = 14; i <= 83; i++)
                    {
                        string header = allHeaders[i - 1]; // allHeaders индексируется с 0
                        double area = 0;
                        bool areaFound = false;

                        if (headerToRoomType.ContainsKey(header))
                        {
                            string roomType = headerToRoomType[header];
                            if (roomAreasByType.ContainsKey(roomType))
                            {
                                // Определяем индекс комнаты по номеру в заголовке
                                int roomIndex = GetRoomIndexFromHeader(header);
                                var areas = roomAreasByType[roomType];

                                if (roomIndex < areas.Count)
                                {
                                    area = areas[roomIndex];
                                    areaFound = true;
                                }
                            }
                        }

                        if (header == "мастер-спальня" || header == "Площадь гостиной 1")
                        {
                            // Эти столбцы всегда остаются пустыми
                            areaFound = false;
                        }

                        if (areaFound)
                        {
                            worksheet.Cells[row, col] = Math.Round(area, 3);
                        }
                        else
                        {
                            worksheet.Cells[row, col] = "";
                        }
                        col++; // УВЕЛИЧИВАЕМ СЧЕТЧИК СТОЛБЦОВ
                    }

                    // Теперь col должен быть равен 84 (после заполнения столбцов 14-83)

                    // Принудительно устанавливаем правильный столбец
                    //col = 84;

                    // Площадь балкона лоджий, балконов, веранд, террас с коэффициентами (столбец 81)
                    double areaWithCoefficients = CalculateAreaWithCoefficients(apartment);
                    worksheet.Cells[row, 81] = Math.Round(areaWithCoefficients, 3);

                    // Площадь общ. (столбец 82)
                    if (apartment.TotalArea > 0)
                    {
                        double totalArea = UnitUtils.ConvertFromInternalUnits(apartment.TotalArea, UnitTypeId.SquareMeters);
                        worksheet.Cells[row, 82] = Math.Round(totalArea, 3);
                    }
                    else
                    {
                        worksheet.Cells[row, 82] = "";
                    }

                    // Площадь прив. (столбец 83)
                    if (apartment.ReducedArea > 0)
                    {
                        double reducedArea = UnitUtils.ConvertFromInternalUnits(apartment.ReducedArea, UnitTypeId.SquareMeters);
                        worksheet.Cells[row, 83] = Math.Round(reducedArea, 3);
                    }
                    else
                    {
                        worksheet.Cells[row, 83] = "";
                    }

                    // Корпус (столбец 84) - преобразуем в текст
                    string buildingNumber = apartment.BuildingNumber ?? "";
                    worksheet.Cells[row, 84] = buildingNumber;
                    // Устанавливаем текстовый формат для ячейки
                    worksheet.Cells[row, 84].NumberFormat = "@";

                    // Секция (столбец 85) - преобразуем в текст  
                    string section = apartment.Section ?? "";
                    worksheet.Cells[row, 85] = section;
                    // Устанавливаем текстовый формат для ячейки
                    worksheet.Cells[row, 85].NumberFormat = "@";

                    // Заморозка архитекторами (столбец 86)
                    worksheet.Cells[row, 86] = "";

                    // Обновляем счетчик для форматирования
                    //col = 90; // следующий после последнего столбца

                    // Форматирование строки
                    Range rowRange = worksheet.Range["A" + row, GetExcelColumnName(col - 1) + row];
                    rowRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                    rowRange.VerticalAlignment = XlVAlign.xlVAlignCenter;
                    rowRange.WrapText = true;

                    row++;
                }

                // Автоподбор ширины столбцов
                worksheet.Columns.AutoFit();

                // Устанавливаем ширину столбцов
                Range dataColumns = worksheet.Range["A:" + GetExcelColumnName(86)];
                dataColumns.ColumnWidth = 20;

                // Добавляем границы
                Range dataRange = worksheet.Range["A1", GetExcelColumnName(86) + (row - 1).ToString()];
                dataRange.Borders.LineStyle = XlLineStyle.xlContinuous;

                // Выравниваем все данные по центру и по середине
                Range allDataRange = worksheet.Range["A2", GetExcelColumnName(86) + (row - 1).ToString()];
                allDataRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                allDataRange.VerticalAlignment = XlVAlign.xlVAlignCenter;
                allDataRange.WrapText = true;

                // Сохраняем файл
                string fullPath = doc.PathName;
                string modelName = System.IO.Path.GetFileNameWithoutExtension(fullPath);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                _exportedFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
                                   $"\\Выгрузка_квартир для модели {modelName}  дата выгрузки {timestamp}.xlsx";

                workbook.SaveAs(_exportedFilePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при работе с Excel: {ex.Message}");
            }
            finally
            {
                workbook.Close(true);
                excelApp.Quit();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
            }
        }

        // Вспомогательный метод для получения индекса комнаты из заголовка
        private int GetRoomIndexFromHeader(string header)
        {
            Match match = Regex.Match(header, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int index))
            {
                return index - 1; // преобразуем в 0-based индекс
            }
            return 0;
        }

        // НОВЫЙ МЕТОД: Применение цветового оформления заголовков
        private void ApplyHeaderFormatting(Worksheet worksheet, int totalColumns)
        {
            try
            {
                // Цвета остаются теми же...
                System.Drawing.Color lightBlue = System.Drawing.Color.FromArgb(218, 238, 243);
                System.Drawing.Color lightOrange = System.Drawing.Color.FromArgb(255, 242, 204);
                System.Drawing.Color lightGreen = System.Drawing.Color.FromArgb(226, 239, 218);
                System.Drawing.Color lightGold = System.Drawing.Color.FromArgb(255, 255, 204);
                System.Drawing.Color lightGray = System.Drawing.Color.FromArgb(242, 242, 242);
                System.Drawing.Color lightPlum = System.Drawing.Color.FromArgb(242, 226, 242);
                System.Drawing.Color yellow = System.Drawing.Color.FromArgb(255, 255, 153);

                // Столбцы 2,3,6,8,9, 29-37, 73-77 - светло-голубой
                ApplyColorToColumns(worksheet, new List<int> { 2, 3, 6, 8, 9 }, lightBlue);
                ApplyColorToColumnRange(worksheet, 29, 37, lightBlue);   // Коридоры
                ApplyColorToColumnRange(worksheet, 73, 77, lightBlue);   // Террасы

                // Столбцы 10, 38-40, 81-83 - светло-оранжевый
                ApplyColorToColumns(worksheet, new List<int> { 10 }, lightOrange);
                ApplyColorToColumnRange(worksheet, 38, 40, lightOrange); // Душевые
                ApplyColorToColumnRange(worksheet, 81, 83, lightOrange); // Лоджии

                // Столбцы 12, 14-18, 42-50, 78-80 - светло-зеленый
                ApplyColorToColumns(worksheet, new List<int> { 12 }, lightGreen);
                ApplyColorToColumnRange(worksheet, 14, 18, lightGreen);  // Комнаты
                ApplyColorToColumnRange(worksheet, 42, 50, lightGreen);  // Кладовые
                ApplyColorToColumnRange(worksheet, 78, 80, lightGreen);  // Прихожая

                // Столбцы 20-24, 51-61 - светло-золотой
                ApplyColorToColumnRange(worksheet, 20, 24, lightGold);   // Гардеробные
                ApplyColorToColumnRange(worksheet, 51, 61, lightGold);   // Санузлы

                // Столбцы 26-28 - светло-серый
                ApplyColorToColumnRange(worksheet, 26, 28, lightGray);   // Кухни

                // Столбцы 63-71 - светло-сизый
                ApplyColorToColumnRange(worksheet, 63, 71, lightPlum);   // Балконы

                // Столбцы 84-86 - желтый
                ApplyColorToColumnRange(worksheet, 84, 86, yellow);      // Итоговые площади

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при применении цветового оформления: {ex.Message}");
            }
        }

        // Вспомогательный метод для применения цвета к конкретным столбцам
        private void ApplyColorToColumns(Worksheet worksheet, List<int> columnNumbers, System.Drawing.Color color)
        {
            foreach (int col in columnNumbers)
            {
                try
                {
                    Range cell = worksheet.Cells[1, col];
                    cell.Interior.Color = color;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при окрашивании столбца {col}: {ex.Message}");
                }
            }
        }

        // Вспомогательный метод для применения цвета к диапазону столбцов
        private void ApplyColorToColumnRange(Worksheet worksheet, int startCol, int endCol, System.Drawing.Color color)
        {
            try
            {
                if (startCol <= endCol)
                {
                    Range range = worksheet.Range[worksheet.Cells[1, startCol], worksheet.Cells[1, endCol]];
                    range.Interior.Color = color;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при окрашивании диапазона столбцов {startCol}-{endCol}: {ex.Message}");
            }
        }

        // Функция для форматирования маркетингового типа - заменяет "S-" на "Suite"
        private string FormatMarketingType(string marketingType)
        {
            if (string.IsNullOrEmpty(marketingType))
                return "";

            // Заменяем "S-" на "Suite" и убираем лишние пробелы
            string formatted = marketingType.Replace("S-", "Suite").Trim();

            // Дополнительные замены для стандартизации
            formatted = formatted.Replace("Suite S", "Suite S")
                                .Replace("Suite M", "Suite M")
                                .Replace("Suite L", "Suite L")
                                .Replace("Suite XL", "Suite XL");

            return formatted;
        }

        private string FindRoomNameForHeader(string header, List<string> allRoomNames, List<IGrouping<string, string>> roomNameGroups)
        {
            // Извлекаем базовое имя из заголовка
            string baseHeader = header.Replace("Площадь ", "").Trim();

            // Удаляем номер из заголовка если есть (теперь без нижнего подчеркивания)
            string baseName = Regex.Replace(baseHeader, @"\s+\d+$", "").Trim();

            // Находим группу для этого базового имени
            var group = roomNameGroups.FirstOrDefault(g =>
                FormatRoomColumnName(g.Key, null).Replace("Площадь ", "").Trim() == baseName);

            if (group != null)
            {
                // Если в заголовке есть номер (теперь пробел вместо подчеркивания)
                if (Regex.IsMatch(header, @"\s+\d+$"))
                {
                    Match match = Regex.Match(header, @"\s+(\d+)$");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int headerNumber))
                    {
                        // Берем помещение с соответствующим номером из группы
                        var orderedGroup = group.OrderBy(x => x).ToList();
                        if (headerNumber <= orderedGroup.Count)
                        {
                            return orderedGroup[headerNumber - 1];
                        }
                    }
                }
                else
                {
                    // Если номера нет, берем первое помещение из группы
                    return group.OrderBy(x => x).First();
                }
            }

            return null;
        }

        private string FormatRoomColumnName(string roomName, int? number)
        {
            // Преобразуем имя помещения для заголовка столбца
            string baseName = roomName;
            string suffix = number.HasValue ? $" {number}" : ""; // ПРОБЕЛ вместо нижнего подчеркивания

            string lowerName = baseName.ToLower();

            // Определяем, нужно ли добавлять "ой" или "ой "
            if (lowerName.Contains("гардеробная"))
            {
                return "Площадь гардеробной" + suffix;
            }
            else if (lowerName.Contains("жилая комната"))
            {
                return "Площадь жилой комнаты" + suffix;
            }
            else if (lowerName.Contains("кухня-гостиная"))
            {
                return "Площадь кухни-гостиной" + suffix;
            }
            else if (lowerName.Contains("кухня-ниша"))
            {
                return "Площадь кухни-ниши" + suffix;
            }
            else if (lowerName.Contains("прихожая"))
            {
                return "Площадь прихожей" + suffix;
            }
            else if (lowerName.Contains("коридор"))
            {
                return "Площадь коридора" + suffix;
            }
            else if (lowerName.Contains("санузел"))
            {
                return "Площадь санузла" + suffix;
            }
            else if (lowerName.Contains("кухня"))
            {
                return "Площадь кухни" + suffix;
            }
            else if (lowerName.Contains("лоджия"))
            {
                return "Площадь лоджии" + suffix;
            }
            else
            {
                // Для остальных случаев просто добавляем "Площадь" в начале
                return "Площадь " + baseName.ToLower() + suffix;
            }
        }

        private double CalculateAreaWithCoefficients(Apartment apartment)
        {
            double totalAreaWithCoefficients = 0;

            foreach (var room in apartment.Rooms)
            {
                if (room.CleanedName.IndexOf("балкон", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    room.CleanedName.IndexOf("лоджия", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    room.CleanedName.IndexOf("веранда", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    room.CleanedName.IndexOf("терраса", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Используем площадь с коэффициентом из параметра ADSK_Площадь с коэффициентом
                    if (room.AreaWithCoefficient > 0)
                    {
                        totalAreaWithCoefficients += room.AreaWithCoefficient;
                    }
                    else
                    {
                        // Если параметр не заполнен, используем обычную площадь
                        totalAreaWithCoefficients += room.Area;
                    }
                }
            }

            return Math.Round(totalAreaWithCoefficients, 3);
        }

        private string FormatBalconyLoggiaText(Apartment apartment)
        {
            var balconyCount = apartment.Rooms.Count(r =>
                r.CleanedName.IndexOf("балкон", StringComparison.OrdinalIgnoreCase) >= 0);
            var loggiaCount = apartment.Rooms.Count(r =>
                r.CleanedName.IndexOf("лоджия", StringComparison.OrdinalIgnoreCase) >= 0);
            var terraceCount = apartment.Rooms.Count(r =>
                r.CleanedName.IndexOf("терраса", StringComparison.OrdinalIgnoreCase) >= 0);
            var verandaCount = apartment.Rooms.Count(r =>
                r.CleanedName.IndexOf("веранда", StringComparison.OrdinalIgnoreCase) >= 0);

            var parts = new List<string>();

            if (balconyCount > 0)
                parts.Add($"{balconyCount} балкон");

            if (loggiaCount > 0)
                parts.Add($"{loggiaCount} лоджия");

            if (terraceCount > 0)
                parts.Add($"{terraceCount} терраса");

            if (verandaCount > 0)
                parts.Add($"{verandaCount} веранда");

            if (parts.Count == 0)
                return "без балк/лодж/терр.";

            return string.Join(", ", parts);
        }

        private string FormatRoomType(string roomType)
        {
            if (string.IsNullOrEmpty(roomType))
                return "";

            string normalized = roomType.Trim().ToLower();

            switch (normalized)
            {
                case "ст":
                case "студия":
                    return "студия";
                case "1":
                    return "1 сп";
                case "2":
                    return "2 сп";
                case "3":
                    return "3 сп";
                case "4":
                    return "4 сп";
                default:
                    return roomType;
            }
        }

        private string FormatRoomType2(string roomType)
        {
            if (string.IsNullOrEmpty(roomType))
                return "";

            string normalized = roomType.Trim().ToLower();

            switch (normalized)
            {
                case "ст":
                case "студия":
                    return "студия";
                case "1":
                    return "1 - комнатная";
                case "2":
                    return "2 - комнатная";
                case "3":
                    return "3 - комнатная";
                case "4":
                    return "4 - комнатная";
                default:
                    return roomType;
            }
        }

        private string GetExcelColumnName(int columnNumber)
        {
            string columnName = "";
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                columnNumber = (columnNumber - modulo) / 26;
            }
            return columnName;
        }

        private string GetParameterValueAsString(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param != null && param.HasValue)
            {
                if (param.StorageType == StorageType.String)
                    return param.AsString() ?? "";
                else
                    return param.AsValueString() ?? "";
            }
            return "";
        }

        private double GetParameterValueAsDouble(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param != null && param.HasValue && param.StorageType == StorageType.Double)
            {
                return param.AsDouble();
            }
            return 0.0;
        }

        // НОВЫЙ МЕТОД: Получение значения double из параметра через AsValueString
        private double GetParameterValueAsDoubleFromString(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param != null && param.HasValue)
            {
                // Пытаемся получить значение как строку
                string stringValue = param.AsValueString();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    // Заменяем запятые на точки для корректного парсинга
                    string normalizedValue = stringValue.Replace(",", ".");
                    if (double.TryParse(normalizedValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
                    {
                        return result;
                    }
                }

                // Если не удалось получить как строку, пробуем как double
                if (param.StorageType == StorageType.Double)
                {
                    return param.AsDouble();
                }
            }
            return 0.0;
        }
    }

    // Класс формы прогресса
    public class ProgressForm : System.Windows.Forms.Form
    {
        private ProgressBar progressBar;
        private Label label;

        public ProgressForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Width = 900;
            this.Height = 320;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Экспорт квартир по шаблону СУП для ФРС LEGENDA v.2.0";
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = System.Drawing.Color.White;
            this.Padding = new Padding(120);

            // Метка статуса
            label = new Label();
            label.Location = new System.Drawing.Point(20, 75);
            label.Width = 840;
            label.Height = 30;
            label.Text = "Подготовка к экспорту...";
            label.Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Regular);
            label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Прогресс-бар
            progressBar = new ProgressBar();
            progressBar.Location = new System.Drawing.Point(60, 105);
            progressBar.Width = 740;
            progressBar.Height = 45;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;

            // Ссылка на сайт
            LinkLabel linkLabel = new LinkLabel();
            linkLabel.Text = "2bim.ru";
            linkLabel.Location = new System.Drawing.Point(380, 165);
            linkLabel.Width = 240;
            linkLabel.Height = 50;
            linkLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            linkLabel.Font = new System.Drawing.Font("Calibri", 11, System.Drawing.FontStyle.Regular);
            linkLabel.LinkColor = System.Drawing.Color.Blue;
            linkLabel.ActiveLinkColor = System.Drawing.Color.Red;
            linkLabel.VisitedLinkColor = System.Drawing.Color.Purple;

            // Обработчик клика по ссылке
            linkLabel.LinkClicked += (sender, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start("https://2bim.ru");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при открытии ссылки: {ex.Message}");
                }
            };

            // Дополнительная метка "Разработчик:"
            Label developerLabel = new Label();
            developerLabel.Text = "Разработчик:";
            developerLabel.Location = new System.Drawing.Point(180, 165);
            developerLabel.Width = 250;
            developerLabel.Height = 50;
            developerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            developerLabel.Font = new System.Drawing.Font("Calibri", 10, System.Drawing.FontStyle.Regular);

            this.Controls.Add(developerLabel);
            this.Controls.Add(linkLabel);
            this.Controls.Add(progressBar);
            this.Controls.Add(label);
        }

        public void UpdateProgress(string message, int progress)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, int>(UpdateProgress), message, progress);
                return;
            }

            label.Text = message;
            progressBar.Value = Math.Min(progress, 100);
            System.Windows.Forms.Application.DoEvents();
        }
    }

    public class Apartment
    {
        public string ApartmentNumber { get; set; }
        public string ApartmentMark { get; set; } // Оригинальная марка квартиры ФРС
        public string FormattedApartmentMark { get; set; } // Отформатированная марка с ведущими нулями
        public int ApartmentMarkSortValue { get; set; } // Числовое значение для сортировки
        public int LevelNumber { get; set; }
        public string MarketingSubtype { get; set; }
        public string HorizontalAxis { get; set; }
        public string VerticalAxis { get; set; }
        public string RoomType { get; set; }
        public List<string> BalconiesAndLoggias { get; set; }
        public string RoomsType { get; set; }
        public string MarketingType { get; set; }
        public double LivingArea { get; set; }
        public double CeilingHeight { get; set; }
        public string RoomPurpose { get; set; }
        public string BuildingNumber { get; set; }
        public string Section { get; set; }
        public double TotalArea { get; set; }
        public double ReducedArea { get; set; }
        public List<RoomInfo> Rooms { get; set; }
    }

    public class RoomInfo
    {
        public string OriginalName { get; set; }
        public string CleanedName { get; set; }
        public string FinalName { get; set; }
        public double Area { get; set; }
        public double AreaWithCoefficient { get; set; }
    }
}