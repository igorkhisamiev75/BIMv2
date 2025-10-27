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
            "Жилая комната", "Гардеробная", "Кухня", "Кухня-ниша", "Кухня-гостиная",
            "Санузел", "Прихожая", "Коридор", "Лоджия"
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
            excelApp.Visible = false; // Скрываем Excel во время работы
            excelApp.DisplayAlerts = false;

            Workbook workbook = excelApp.Workbooks.Add();
            Worksheet worksheet = workbook.ActiveSheet;

            try
            {
                int col = 1;

                // Базовые заголовки столбцов в нужном порядке
                worksheet.Cells[1, col++] = "Условный номер помещения";
                worksheet.Cells[1, col++] = "Маркетинговый подтип 2_1_0Э, 2_2_0";
                worksheet.Cells[1, col++] = "Тип планировки 2_1, 2_2";
                worksheet.Cells[1, col++] = "Горизонтальная ось";
                worksheet.Cells[1, col++] = "Вертикальная ось";
                worksheet.Cells[1, col++] = "Вид помещения студия, _1 сп, _2 сп";
                worksheet.Cells[1, col++] = "Балкон/лоджия/терраса";
                worksheet.Cells[1, col++] = "Комнат пиб _студия_1 ккв _2 ккв _3 ккв";
                worksheet.Cells[1, col++] = "Маркетинговый тип 2_М, 2_L";
                worksheet.Cells[1, col++] = "Площадь жилая";
                worksheet.Cells[1, col++] = "Высота потолка";
                worksheet.Cells[1, col++] = "Назначение помещения";
                worksheet.Cells[1, col++] = "Тип помещения кв., комм., паркинг";

                // СОЗДАЕМ ФИКСИРОВАННЫЙ СПИСОК ЗАГОЛОВКОВ С 5 СТОЛБЦАМИ ДЛЯ ЖИЛЫХ КОМНАТ
                var fixedRoomHeaders = new List<string>();

                // Всегда добавляем 5 столбцов для жилых комнат
                for (int i = 1; i <= 5; i++)
                {
                    fixedRoomHeaders.Add($"Площадь жилой комнаты {i}");
                }

                // Собираем остальные уникальные имена помещений из всех квартир (кроме жилых комнат)
                var otherRoomNames = apartments
                    .SelectMany(a => a.Rooms)
                    .Where(r => _allowedRoomNames.Contains(r.CleanedName) && !r.CleanedName.Equals("Жилая комната", StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.FinalName)
                    .Distinct()
                    .ToList();

                // Группируем остальные помещения по базовым именам
                var otherRoomGroups = new List<IGrouping<string, string>>();
                var priorityOtherNames = new List<string>
                {
                    "Гардеробная", "Кухня", "Кухня-ниша", "Кухня-гостиная", "Коридор",
                    "Прихожая", "Санузел", "Лоджия"
                };

                // Добавляем остальные помещения в порядке приоритета
                foreach (var priorityName in priorityOtherNames)
                {
                    var roomsWithPriorityName = otherRoomNames
                        .Where(name =>
                        {
                            var baseName = Regex.Replace(name, @"_\d+$", "").Trim();
                            return baseName.Equals(priorityName, StringComparison.OrdinalIgnoreCase);
                        })
                        .ToList();

                    if (roomsWithPriorityName.Any())
                    {
                        var group = roomsWithPriorityName.GroupBy(name =>
                        {
                            var baseName = Regex.Replace(name, @"_\d+$", "").Trim();
                            return baseName;
                        }).FirstOrDefault();

                        if (group != null)
                        {
                            otherRoomGroups.Add(group);
                        }
                    }
                }

                // Добавляем остальные помещения (если есть)
                var remainingOtherNames = otherRoomNames
                    .Where(name =>
                    {
                        var baseName = Regex.Replace(name, @"_\d+$", "").Trim();
                        return !priorityOtherNames.Contains(baseName, StringComparer.OrdinalIgnoreCase);
                    })
                    .OrderBy(name => name);

                foreach (var name in remainingOtherNames)
                {
                    var baseName = Regex.Replace(name, @"_\d+$", "").Trim();
                    var group = otherRoomNames
                        .Where(n =>
                        {
                            var nBaseName = Regex.Replace(n, @"_\d+$", "").Trim();
                            return nBaseName.Equals(baseName, StringComparison.OrdinalIgnoreCase);
                        })
                        .GroupBy(n => baseName)
                        .FirstOrDefault();

                    if (group != null && !otherRoomGroups.Any(g => g.Key.Equals(baseName, StringComparison.OrdinalIgnoreCase)))
                    {
                        otherRoomGroups.Add(group);
                    }
                }

                // Создаем список заголовков для остальных помещений
                var otherColumnHeaders = new List<string>();
                foreach (var group in otherRoomGroups)
                {
                    if (group.Count() == 1)
                    {
                        otherColumnHeaders.Add(FormatRoomColumnName(group.Key, null));
                    }
                    else
                    {
                        int counter = 1;
                        foreach (var roomName in group.OrderBy(x => x))
                        {
                            otherColumnHeaders.Add(FormatRoomColumnName(group.Key, counter));
                            counter++;
                        }
                    }
                }

                // ОБЪЕДИНЯЕМ ФИКСИРОВАННЫЕ ЗАГОЛОВКИ ЖИЛЫХ КОМНАТ С ОСТАЛЬНЫМИ
                var allColumnHeaders = new List<string>();
                allColumnHeaders.AddRange(fixedRoomHeaders);
                allColumnHeaders.AddRange(otherColumnHeaders);

                // Добавляем заголовки для ВСЕХ помещений
                foreach (var header in allColumnHeaders)
                {
                    worksheet.Cells[1, col++] = header;
                }

                // Добавляем столбец с коэффициентами
                worksheet.Cells[1, col++] = "Площадь балкона лоджий, балконов, веранд, террас с коэффициентами, кв.м.";

                // Добавляем столбцы в конце как требуется
                worksheet.Cells[1, col++] = "Площадь общ.";
                worksheet.Cells[1, col++] = "Площадь прив.";
                worksheet.Cells[1, col++] = "Корпус";
                worksheet.Cells[1, col++] = "Секция";
                worksheet.Cells[1, col++] = "Заморозка архитекторами";

                // Форматирование заголовков - БЕЗ ЖИРНОГО ШРИФТА
                Range headerRange = worksheet.Range["A1", GetExcelColumnName(col - 1) + "1"];
                headerRange.Font.Bold = false; // Убираем жирный шрифт
                headerRange.Interior.Color = 15123099;

                // Выравнивание заголовков по центру и перенос текста
                headerRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                headerRange.VerticalAlignment = XlVAlign.xlVAlignCenter;
                headerRange.WrapText = true;

                // Устанавливаем высоту первой строки
                worksheet.Rows[1].RowHeight = 125; // Высота в пунктах

                // Устанавливаем текстовый формат для столбца с условным номером помещения (столбец A)
                Range apartmentNumberColumn = worksheet.Range["A:A"];
                apartmentNumberColumn.NumberFormat = "@";

                // Устанавливаем числовые форматы для столбцов с площадями и высотой потолка
                // Площадь жилая (столбец J)
                if (col > 9) worksheet.Range["J:J"].NumberFormat = "0.000";
                // Высота потолка (столбец K)
                if (col > 10) worksheet.Range["K:K"].NumberFormat = "0.00";

                // Форматирование для всех столбцов с площадями помещений
                int startRoomColumns = 14; // Столбцы с площадями помещений начинаются с 14-го
                for (int i = startRoomColumns; i < startRoomColumns + allColumnHeaders.Count; i++)
                {
                    worksheet.Range[GetExcelColumnName(i) + ":" + GetExcelColumnName(i)].NumberFormat = "0.000";
                }

                // Площадь с коэффициентами
                int coefficientColumn = startRoomColumns + allColumnHeaders.Count;
                worksheet.Range[GetExcelColumnName(coefficientColumn) + ":" + GetExcelColumnName(coefficientColumn)].NumberFormat = "0.000";

                // Площадь общ.
                int totalAreaColumn = coefficientColumn + 1;
                worksheet.Range[GetExcelColumnName(totalAreaColumn) + ":" + GetExcelColumnName(totalAreaColumn)].NumberFormat = "0.000";

                // Площадь прив.
                int reducedAreaColumn = totalAreaColumn + 1;
                worksheet.Range[GetExcelColumnName(reducedAreaColumn) + ":" + GetExcelColumnName(reducedAreaColumn)].NumberFormat = "0.000";

                // СОРТИРУЕМ КВАРТИРЫ по параметру LG_МаркаКвартирыФРС от меньшего к большему
                var sortedApartments = apartments
                    .OrderBy(a => a.ApartmentMarkSortValue) // Сначала по числовому значению марки
                    .ThenBy(a => a.ApartmentMark) // Затем по строковому значению марки
                    .ThenBy(a => a.LevelNumber) // Затем по уровню
                    .ThenBy(a => a.ApartmentNumber) // Затем по номеру квартиры
                    .ToList();

                int row = 2;
                int totalApartments = sortedApartments.Count;
                int processedApartments = 0;

                foreach (var apartment in sortedApartments)
                {
                    // Обновляем прогресс для каждой квартиры
                    processedApartments++;
                    int progress = 70 + (int)((double)processedApartments / totalApartments * 25); // 70-95%
                    progressForm.UpdateProgress($"Экспорт квартир ({processedApartments}/{totalApartments})...", progress);

                    col = 1;

                    // Условный номер помещения - используем параметр LG_МаркаКвартирыФРС с ведущими нулями
                    string conditionalNumber = !string.IsNullOrEmpty(apartment.FormattedApartmentMark) ?
                        apartment.FormattedApartmentMark :
                        (!string.IsNullOrEmpty(apartment.ApartmentMark) ?
                            apartment.ApartmentMark :
                            FormatApartmentNumber(apartment.ApartmentNumber));
                    worksheet.Cells[row, col++] = conditionalNumber;

                    // Маркетинговый подтип 2_1_0Э, 2_2_0 - пустой столбец
                    worksheet.Cells[row, col++] = "";

                    // Тип планировки 2_1, 2_2 - пустой столбец
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

                    // Маркетинговый тип - заменяем "S-" на "Suite"
                    string marketingType = FormatMarketingType(apartment.MarketingType);
                    worksheet.Cells[row, col++] = marketingType;

                    // Площадь жилая - гарантированно форматируем до тысячных
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

                    // Назначение помещения - пустой столбец
                    worksheet.Cells[row, col++] = "";

                    // Тип помещения
                    worksheet.Cells[row, col++] = "Квартира";

                    // СОЗДАЕМ СЛОВАРЬ ПЛОЩАДЕЙ С КОЭФФИЦИЕНТАМИ С ИСПОЛЬЗОВАНИЕМ FinalName КАК КЛЮЧА
                    var roomAreasWithCoefficient = new Dictionary<string, double>();
                    foreach (var room in apartment.Rooms.Where(r => _allowedRoomNames.Contains(r.CleanedName)))
                    {
                        // ИСПОЛЬЗУЕМ ПЛОЩАДЬ С КОЭФФИЦИЕНТОМ вместо обычной площади
                        double areaToUse = room.AreaWithCoefficient > 0 ? room.AreaWithCoefficient : room.Area;
                        roomAreasWithCoefficient[room.FinalName] = areaToUse;
                    }

                    // ЗАПОЛНЯЕМ ПЛОЩАДИ ДЛЯ ВСЕХ СТОЛБЦОВ
                    foreach (var header in allColumnHeaders)
                    {
                        double area = 0;
                        bool areaFound = false;

                        // Для столбцов жилых комнат
                        if (header.StartsWith("Площадь жилой комнаты"))
                        {
                            // Извлекаем номер комнаты из заголовка
                            Match match = Regex.Match(header, @"Площадь жилой комнаты\s*(\d+)");
                            if (match.Success)
                            {
                                string roomNumber = match.Groups[1].Value;

                                // Ищем соответствующие жилые комнаты в квартире
                                var livingRooms = apartment.Rooms
                                    .Where(r => r.CleanedName.Equals("Жилая комната", StringComparison.OrdinalIgnoreCase))
                                    .OrderBy(r => r.FinalName)
                                    .ToList();

                                int roomIndex;
                                if (int.TryParse(roomNumber, out roomIndex) && roomIndex <= livingRooms.Count)
                                {
                                    // Берем площадь комнаты по порядковому номеру
                                    var livingRoom = livingRooms[roomIndex - 1];
                                    area = roomAreasWithCoefficient.ContainsKey(livingRoom.FinalName) ?
                                           roomAreasWithCoefficient[livingRoom.FinalName] : 0;
                                    areaFound = true;
                                }
                            }
                        }
                        else
                        {
                            // Для остальных помещений используем старую логику
                            var roomName = FindRoomNameForHeader(header, otherRoomNames, otherRoomGroups);
                            if (roomName != null && roomAreasWithCoefficient.ContainsKey(roomName))
                            {
                                area = roomAreasWithCoefficient[roomName];
                                areaFound = true;
                            }
                        }

                        if (areaFound)
                        {
                            worksheet.Cells[row, col] = Math.Round(area, 3);
                        }
                        else
                        {
                            worksheet.Cells[row, col] = "";
                        }
                        col++;
                    }

                    // Площадь балкона лоджий, балконов, веранд, террас с коэффициентами
                    double areaWithCoefficients = CalculateAreaWithCoefficients(apartment);
                    worksheet.Cells[row, col++] = Math.Round(areaWithCoefficients, 3);

                    // Столбцы в конце
                    // Площадь общ.
                    if (apartment.TotalArea > 0)
                    {
                        double totalArea = UnitUtils.ConvertFromInternalUnits(apartment.TotalArea, UnitTypeId.SquareMeters);
                        worksheet.Cells[row, col++] = Math.Round(totalArea, 3);
                    }
                    else
                    {
                        worksheet.Cells[row, col++] = "";
                    }

                    // Площадь прив.
                    if (apartment.ReducedArea > 0)
                    {
                        double reducedArea = UnitUtils.ConvertFromInternalUnits(apartment.ReducedArea, UnitTypeId.SquareMeters);
                        worksheet.Cells[row, col++] = Math.Round(reducedArea, 3);
                    }
                    else
                    {
                        worksheet.Cells[row, col++] = "";
                    }

                    // Корпус
                    worksheet.Cells[row, col++] = apartment.BuildingNumber;

                    // Секция
                    worksheet.Cells[row, col++] = apartment.Section;

                    // Заморозка архитекторами - пустой столбец
                    worksheet.Cells[row, col++] = "";

                    // ПРИМЕНЯЕМ ФОРМАТИРОВАНИЕ К СТРОКЕ
                    Range rowRange = worksheet.Range["A" + row, GetExcelColumnName(col - 1) + row];
                    rowRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                    rowRange.VerticalAlignment = XlVAlign.xlVAlignCenter;
                    rowRange.WrapText = true;

                    row++;
                }

                // Автоподбор ширины столбцов
                worksheet.Columns.AutoFit();

                // Добавляем границы
                Range dataRange = worksheet.Range["A1", GetExcelColumnName(col - 1) + (row - 1).ToString()];
                dataRange.Borders.LineStyle = XlLineStyle.xlContinuous;

                // Полное имя файла с путем
                string fullPath = doc.PathName;

                // Только имя файла
                string fileName = System.IO.Path.GetFileName(fullPath);

                // Имя без расширения
                string modelName = System.IO.Path.GetFileNameWithoutExtension(fullPath);

                // Сохраняем файл с временем выгрузки
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
                // Освобождаем ресурсы
                workbook.Close(true);
                excelApp.Quit();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
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
            this.Height = 280;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Экспорт квартир для ФРС LEGENDA 1.0";
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