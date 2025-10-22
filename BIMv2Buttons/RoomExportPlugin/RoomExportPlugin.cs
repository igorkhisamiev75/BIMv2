using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;

using Application = Microsoft.Office.Interop.Excel.Application;
using Parameter = Autodesk.Revit.DB.Parameter;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class RoomExportCommand : IExternalCommand
    {
        // Список разрешенных имен помещений
        private readonly List<string> _allowedRoomNames = new List<string>
        {
            "Гардеробная", "Жилая комната", "Кухня", "Кухня-гостиная",
            "Кухня-ниша", "Лоджия", "Прихожая", "Коридор", "Санузел"
        };

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

                // Группируем помещения по квартирам и создаем объекты квартир
                var apartments = CreateApartmentsFromRooms(rooms, doc);

                // Экспортируем в Excel
                ExportToExcel(apartments, doc);

                TaskDialog.Show("Успех", $"Экспорт {apartments.Count} квартир завершен успешно!");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка при экспорте: {ex.Message}");
                return Result.Failed;
            }
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

        private List<Apartment> CreateApartmentsFromRooms(ICollection<Element> rooms, Document doc)
        {
            var apartmentDict = new Dictionary<string, Apartment>();

            foreach (Element element in rooms)
            {
                Room room = element as Room;
                if (room == null || room.Area == 0) continue;

                // Проверяем назначение помещения - должно быть "Квартиры"
                string roomPurpose = GetParameterValueAsString(room, "Назначение");
                if (roomPurpose != "Квартиры")
                    continue;

                string apartmentNumber = GetParameterValueAsString(room, "ADSK_Номер квартиры");
                if (string.IsNullOrEmpty(apartmentNumber))
                    apartmentNumber = "Без номера";

                // Получаем номер уровня
                int levelNumber = GetLevelNumber(room, doc);

                // Если квартиры с таким номером еще нет в словаре, создаем ее
                if (!apartmentDict.ContainsKey(apartmentNumber))
                {
                    apartmentDict[apartmentNumber] = new Apartment
                    {
                        ApartmentNumber = apartmentNumber,
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
                    double roomAreaWithCoefficient = GetParameterValueAsDouble(room, "ADSK_Площадь помещения с коэффициентом");

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

        private void ExportToExcel(List<Apartment> apartments, Document doc)
        {
            Application excelApp = new Application();
            excelApp.Visible = true;

            Workbook workbook = excelApp.Workbooks.Add();
            Worksheet worksheet = workbook.ActiveSheet;

            try
            {
                int col = 1;

                // Базовые заголовки столбцов в нужном порядке
                worksheet.Cells[1, col++] = "Уровень";
                worksheet.Cells[1, col++] = "Наименование";
                worksheet.Cells[1, col++] = "Условный номер помещения";
                worksheet.Cells[1, col++] = "Маркетинговый подтип";
                worksheet.Cells[1, col++] = "Горизонтальная ось";
                worksheet.Cells[1, col++] = "Вертикальная ось";
                worksheet.Cells[1, col++] = "Вид помещения студия, _1 сп, _2 сп";
                worksheet.Cells[1, col++] = "Балкон/лоджия/терраса";
                worksheet.Cells[1, col++] = "Комнат пиб _студия_1 ккв _2 ккв _3 ккв";
                worksheet.Cells[1, col++] = "Маркетинговый тип 2_М, 2_L";
                worksheet.Cells[1, col++] = "Площадь жилая";
                worksheet.Cells[1, col++] = "Высота потолка";
                worksheet.Cells[1, col++] = "Тип помещения кв., комм., паркинг";

                // Собираем все уникальные имена помещений из всех квартир
                var allRoomNames = apartments
                    .SelectMany(a => a.Rooms)
                    .Where(r => _allowedRoomNames.Contains(r.CleanedName))
                    .Select(r => r.FinalName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();

                // Группируем помещения по базовым именам для нумерации
                var roomNameGroups = allRoomNames
                    .GroupBy(name =>
                    {
                        // Получаем базовое имя без номера
                        string baseName = Regex.Replace(name, @"_\d+$", "").Trim();
                        return baseName;
                    })
                    .ToList();

                // Создаем список заголовков с правильной нумерацией
                var columnHeaders = new List<string>();
                foreach (var group in roomNameGroups)
                {
                    if (group.Count() == 1)
                    {
                        // Если помещение одно, без номера
                        columnHeaders.Add(FormatRoomColumnName(group.Key, null));
                    }
                    else
                    {
                        // Если несколько помещений с одинаковым именем, нумеруем их
                        int counter = 1;
                        foreach (var roomName in group.OrderBy(x => x))
                        {
                            columnHeaders.Add(FormatRoomColumnName(group.Key, counter));
                            counter++;
                        }
                    }
                }

                // Добавляем заголовки для ВСЕХ уникальных помещений с префиксом "Площадь" и правильной нумерацией
                foreach (var header in columnHeaders)
                {
                    worksheet.Cells[1, col++] = header;
                }

                // Добавляем заголовки для балконов/лоджий/террас
                worksheet.Cells[1, col++] = "Балкон";
                worksheet.Cells[1, col++] = "Лоджия";
                worksheet.Cells[1, col++] = "Терраса";

                // Добавляем столбец с коэффициентами ПОСЛЕ столбца Терраса
                worksheet.Cells[1, col++] = "Площадь балкона лоджий, балконов, веранд, террас с коэффициентами, кв.м.";

                // Добавляем столбцы в конце как требуется
                worksheet.Cells[1, col++] = "Площадь общ.";
                worksheet.Cells[1, col++] = "Площадь прив.";
                worksheet.Cells[1, col++] = "Корпус";
                worksheet.Cells[1, col++] = "Секция";

                // Форматирование заголовков
                Range headerRange = worksheet.Range["A1", GetExcelColumnName(col - 1) + "1"];
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = 15123099;

                // Устанавливаем высоту первой строки
                worksheet.Rows[1].RowHeight = 125; // Высота в пунктах

                // Устанавливаем текстовый формат для столбца с условным номером помещения (столбец C)
                Range apartmentNumberColumn = worksheet.Range["C:C"];
                apartmentNumberColumn.NumberFormat = "@";

                // Устанавливаем числовые форматы для столбцов с площадями и высотой потолка
                // Площадь жилая (столбец K)
                if (col > 10) worksheet.Range["K:K"].NumberFormat = "0.000";
                // Высота потолка (столбец L)
                if (col > 11) worksheet.Range["L:L"].NumberFormat = "0.00";
                // Площадь с коэффициентами (столбец после Терраса)
                int coefficientColumn = columnHeaders.Count + 16; // 16 базовых столбцов до помещений
                worksheet.Range[GetExcelColumnName(coefficientColumn) + ":" + GetExcelColumnName(coefficientColumn)].NumberFormat = "0.000";
                // Площадь общ. (предпоследние столбцы)
                if (col > 3) worksheet.Range[GetExcelColumnName(col - 4) + ":" + GetExcelColumnName(col - 4)].NumberFormat = "0.000";
                // Площадь прив. (предпоследние столбцы)
                if (col > 3) worksheet.Range[GetExcelColumnName(col - 3) + ":" + GetExcelColumnName(col - 3)].NumberFormat = "0.000";

                // Сортируем квартиры: сначала по уровню, затем по номеру квартиры
                var sortedApartments = apartments
                    .OrderBy(a => a.LevelNumber)
                    .ThenBy(a =>
                    {
                        // Для сортировки номеров квартир извлекаем числовую часть
                        if (int.TryParse(Regex.Match(a.ApartmentNumber, @"\d+").Value, out int number))
                            return number;
                        return int.MaxValue; // Если не число - в конец
                    })
                    .ThenBy(a => a.ApartmentNumber) // Для строковых номеров
                    .ToList();

                int row = 2;
                foreach (var apartment in sortedApartments)
                {
                    col = 1;

                    // Уровень
                    worksheet.Cells[row, col++] = apartment.LevelNumber;

                    // Наименование - перечисляем все ОЧИЩЕННЫЕ имена разрешенных помещений через запятую
                    var cleanedRoomNames = apartment.Rooms
                        .Where(r => _allowedRoomNames.Contains(r.CleanedName) ||
                                   r.CleanedName.IndexOf("балкон", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   r.CleanedName.IndexOf("лоджия", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   r.CleanedName.IndexOf("терраса", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   r.CleanedName.IndexOf("веранда", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(r => r.CleanedName)
                        .Distinct()
                        .OrderBy(name => name);
                    worksheet.Cells[row, col++] = string.Join(", ", cleanedRoomNames);

                    // Условный номер помещения - текстовый формат
                    worksheet.Cells[row, col++] = "'" + apartment.ApartmentNumber;

                    // Маркетинговый подтип
                    worksheet.Cells[row, col++] = apartment.MarketingSubtype;

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
                    worksheet.Cells[row, col++] = apartment.MarketingType;

                    // Площадь жилая
                    if (apartment.LivingArea > 0)
                    {
                        double livingArea = UnitUtils.ConvertFromInternalUnits(apartment.LivingArea, UnitTypeId.SquareMeters);
                        worksheet.Cells[row, col++] = Math.Round(livingArea, 3);
                    }
                    else
                    {
                        worksheet.Cells[row, col++] = "Нет данных";
                    }

                    // Высота потолка
                    if (apartment.CeilingHeight > 0)
                    {
                        double ceilingHeight = UnitUtils.ConvertFromInternalUnits(apartment.CeilingHeight, UnitTypeId.Meters);
                        worksheet.Cells[row, col++] = Math.Round(ceilingHeight, 2);
                    }
                    else
                    {
                        worksheet.Cells[row, col++] = "Нет данных";
                    }

                    // Тип помещения
                    worksheet.Cells[row, col++] = "Квартира";

                    // Создаем словарь площадей для всех помещений квартиры
                    var roomAreas = apartment.Rooms
                        .Where(r => _allowedRoomNames.Contains(r.CleanedName))
                        .ToDictionary(r => r.FinalName, r => r.Area);

                    // Заполняем площади для всех уникальных помещений в соответствии с порядком заголовков
                    foreach (var header in columnHeaders)
                    {
                        // Находим соответствующее имя помещения для этого заголовка
                        var roomName = FindRoomNameForHeader(header, allRoomNames, roomNameGroups);
                        if (roomName != null && roomAreas.ContainsKey(roomName))
                        {
                            worksheet.Cells[row, col] = Math.Round(roomAreas[roomName], 3);
                        }
                        else
                        {
                            worksheet.Cells[row, col] = "";
                        }
                        col++;
                    }

                    // Площади балконов/лоджий/террас (обычные площади)
                    var balconyArea = apartment.Rooms
                        .Where(r => r.CleanedName.IndexOf("балкон", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Sum(r => r.Area);
                    worksheet.Cells[row, col++] = Math.Round(balconyArea, 3);

                    var loggiaArea = apartment.Rooms
                        .Where(r => r.CleanedName.IndexOf("лоджия", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Sum(r => r.Area);
                    worksheet.Cells[row, col++] = Math.Round(loggiaArea, 3);

                    var terraceArea = apartment.Rooms
                        .Where(r => r.CleanedName.IndexOf("терраса", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Sum(r => r.Area);
                    worksheet.Cells[row, col++] = Math.Round(terraceArea, 3);

                    // Площадь балкона лоджий, балконов, веранд, террас с коэффициентами (ПОСЛЕ Террасы)
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
                        worksheet.Cells[row, col++] = "Нет данных";
                    }

                    // Площадь прив.
                    if (apartment.ReducedArea > 0)
                    {
                        double reducedArea = UnitUtils.ConvertFromInternalUnits(apartment.ReducedArea, UnitTypeId.SquareMeters);
                        worksheet.Cells[row, col++] = Math.Round(reducedArea, 3);
                    }
                    else
                    {
                        worksheet.Cells[row, col++] = "Нет данных";
                    }

                    // Корпус
                    worksheet.Cells[row, col++] = apartment.BuildingNumber;

                    // Секция
                    worksheet.Cells[row, col++] = apartment.Section;

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
                string savePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
                                 $"\\Выгрузка_квартир для модели {modelName}  дата выгрузки {timestamp}.xlsx";
                workbook.SaveAs(savePath);
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

        private string FindRoomNameForHeader(string header, List<string> allRoomNames, List<IGrouping<string, string>> roomNameGroups)
        {
            // Извлекаем базовое имя из заголовка
            string baseHeader = header.Replace("Площадь ", "").Trim();

            // Удаляем номер из заголовка если есть
            string baseName = Regex.Replace(baseHeader, @"_\d+$", "").Trim();

            // Находим группу для этого базового имени
            var group = roomNameGroups.FirstOrDefault(g =>
                FormatRoomColumnName(g.Key, null).Replace("Площадь ", "").Trim() == baseName);

            if (group != null)
            {
                // Если в заголовке есть номер
                if (header.Contains("_"))
                {
                    Match match = Regex.Match(header, @"_(\d+)$");
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
            string suffix = number.HasValue ? $"_{number}" : "";

            string lowerName = baseName.ToLower();

            // Определяем, нужно ли добавлять "ой" или "ой_"
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
                    // Используем площадь с коэффициентом из параметра ADSK_Площадь помещения с коэффициентом
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

            return totalAreaWithCoefficients;
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
                    return "_1 сп";
                case "2":
                    return "_2 сп";
                case "3":
                    return "_3 сп";
                case "4":
                    return "_4 сп";
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
                    return "_студия";
                case "1":
                    return "_1ккв";
                case "2":
                    return "_2ккв";
                case "3":
                    return "_3ккв";
                case "4":
                    return "_4ккв";
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
    }

    public class Apartment
    {
        public string ApartmentNumber { get; set; }
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