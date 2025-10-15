using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Parameter = Autodesk.Revit.DB.Parameter;

namespace BIMv2
{
    public class FlatProcessor
    {
        private Document _doc;

        // Parameter GUIDs
        private readonly Dictionary<string, string> _paramGuids = new Dictionary<string, string>
        {
            ["flat_number"] = "211ec639-fda2-4353-86b8-ff27b9db431a",
            ["room_korpus"] = "85cc5111-91f7-4fd7-9b8c-b5fcaf98b04f",
            ["room_section"] = "566dccab-57e7-4451-9dec-9b079e3034dd",
            ["total_area_frc"] = "78f1ea4b-7fdb-4da9-abe0-b9d5a3b88bbc",
            ["rooms_number"] = "5883edb2-623b-45b8-8240-dc49b5244d7e",
            ["living_area_frc"] = "0f8b38ca-115d-4f26-b69b-feaff3f6b48b",
            ["room_area_frc"] = "e0a17adb-d7b6-400f-98c4-5cf098dee81a",
            ["room_type"] = "31e309b1-d92e-42fb-bf2a-62222c779cbc",
            ["room_filter"] = "f8b09da4-54ee-4624-b6ce-21e1ede44e19",
            ["room_name"] = "ab100924-fa74-4bad-8196-96298011446d",
            ["flat_type"] = "2b529daf-ba8d-45de-88cf-06bf2dff7aaa",
            ["marketing_type"] = "2b529daf-ba8d-45de-88cf-06bf2dff7aaa",
            ["room_level"] = "54aafbbd-7ee1-4c2e-9449-7c1c9a5354c0"
        };

        private string _roomTypeCheck = "жилая";
        private string _roomFilterChecker = "квартира";

        // Маппинг имен комнат
        private readonly Dictionary<string, RoomMapping> _roomMappings = new Dictionary<string, RoomMapping>
        {
            ["балкон"] = new RoomMapping { ColumnName = "Площадь балкона", CanHaveMultiple = false },
            ["гардеробная"] = new RoomMapping { ColumnName = "Площадь гардеробной", CanHaveMultiple = true },
            ["жилая комната"] = new RoomMapping { ColumnName = "Площадь комнаты", CanHaveMultiple = true },
            ["кухня"] = new RoomMapping { ColumnName = "Площадь кухни", CanHaveMultiple = false },
            ["кухня-гостиная"] = new RoomMapping { ColumnName = "Площадь кухни-гостиной", CanHaveMultiple = false },
            ["кухня-ниша"] = new RoomMapping { ColumnName = "Площадь кухни-ниши", CanHaveMultiple = false },
            ["лоджия"] = new RoomMapping { ColumnName = "Площадь лоджии", CanHaveMultiple = false },
            ["прихожая"] = new RoomMapping { ColumnName = "Площадь прихожей", CanHaveMultiple = false },
            ["санузел"] = new RoomMapping { ColumnName = "Площадь санузла", CanHaveMultiple = true }
        };

        private class RoomMapping
        {
            public string ColumnName { get; set; }
            public bool CanHaveMultiple { get; set; }
        }

        // Глобальные переменные для статистики
        private int _maxLivingRoomsPerFlat = 0;
        private int _maxWardrobesPerFlat = 0;
        private int _maxBathroomsPerFlat = 0;

        public FlatProcessor(Document doc)
        {
            _doc = doc;
        }

        // Безопасные методы конвертации
        private int SafeConvertToInt(object value, int defaultValue = 0)
        {
            try
            {
                if (value == null) return defaultValue;
                if (value is int) return (int)value;
                if (value is double) return (int)(double)value;

                string stringValue = value.ToString();
                if (int.TryParse(stringValue, out int result))
                    return result;

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private double SafeConvertToDouble(object value, double defaultValue = 0.0)
        {
            try
            {
                if (value == null) return defaultValue;
                if (value is double) return (double)value;
                if (value is int) return (double)(int)value;

                string stringValue = value.ToString();
                if (double.TryParse(stringValue, out double result))
                    return result;

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private string SafeConvertToString(object value, string defaultValue = "")
        {
            try
            {
                return value?.ToString() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private object GetParameterValue(Element elem, string paramKey)
        {
            try
            {
                if (!_paramGuids.ContainsKey(paramKey))
                    return null;

                string paramGuid = _paramGuids[paramKey];
                Parameter param = elem.get_Parameter(new Guid(paramGuid));

                if (param == null)
                {
                    param = elem.LookupParameter(paramKey);
                }

                if (param == null || !param.HasValue)
                    return null;

                switch (param.StorageType)
                {
                    case StorageType.String:
                        return param.AsString() ?? "";
                    case StorageType.Integer:
                        return param.AsInteger();
                    case StorageType.Double:
                        return param.AsDouble();
                    case StorageType.ElementId:
                        return param.AsElementId();
                    default:
                        return param.AsString() ?? "";
                }
            }
            catch
            {
                return null;
            }
        }

        public void ShowDiagnosticInfo()
        {
            var rooms = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .ToList();

            var diagnosticInfo = new List<string>
            {
                $"Всего помещений в проекте: {rooms.Count}",
                ""
            };

            // Проверяем параметры для первых 5 помещений
            int checkedRooms = 0;
            foreach (Room room in rooms.Take(5))
            {
                checkedRooms++;
                diagnosticInfo.Add($"Помещение {checkedRooms}: {room.Name} (ID: {room.Id})");

                // Проверяем основные параметры
                var importantParams = new[] { "flat_number", "room_name", "room_type", "room_area_frc", "rooms_number", "room_level" };
                foreach (string paramKey in importantParams)
                {
                    object value = GetParameterValue(room, paramKey);
                    string valueStr = value?.ToString() ?? "NULL";
                    string valueType = value?.GetType().Name ?? "NULL";
                    diagnosticInfo.Add($"  {paramKey}: {valueStr} ({valueType})");
                }
                diagnosticInfo.Add("");
            }

            TaskDialog.Show("Диагностика", string.Join("\n", diagnosticInfo));
        }

        public List<List<object>> ProcessFlats()
        {
            try
            {
                // Собираем все помещения
                var allRooms = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .ToList();

                // Группируем помещения по квартирам
                var roomsByFlat = GroupRoomsByFlat(allRooms);

                if (roomsByFlat.Count == 0)
                {
                    TaskDialog.Show("Информация", "Не найдено квартир с заполненными параметрами.");
                    return new List<List<object>>();
                }

                // Анализируем статистику по комнатам
                AnalyzeRoomStatistics(roomsByFlat);

                // Создаем заголовок
                var flatsInfo = new List<List<object>>();
                var header = CreateHeader();
                flatsInfo.Add(header);

                // Обрабатываем каждую квартиру
                int processedFlats = 0;
                foreach (var flat in roomsByFlat.Values)
                {
                    var flatInfo = ProcessFlatData(flat);
                    if (flatInfo != null)
                    {
                        flatsInfo.Add(flatInfo);
                        processedFlats++;
                    }
                }

                TaskDialog.Show("Успех",
                    $"Обработано квартир: {processedFlats}\n" +
                    $"Максимум жилых комнат в квартире: {_maxLivingRoomsPerFlat}\n" +
                    $"Максимум гардеробных: {_maxWardrobesPerFlat}\n" +
                    $"Максимум санузлов: {_maxBathroomsPerFlat}");

                return flatsInfo;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка при обработке данных: {ex.Message}\n\n{ex.StackTrace}");
                return new List<List<object>>();
            }
        }

        private Dictionary<string, List<Room>> GroupRoomsByFlat(List<Room> allRooms)
        {
            var roomsByFlat = new Dictionary<string, List<Room>>();

            foreach (Room room in allRooms)
            {
                try
                {
                    // Проверяем основные параметры
                    object flatNumberValue = GetParameterValue(room, "flat_number");
                    Parameter roomAreaParam = room.get_Parameter(BuiltInParameter.ROOM_AREA);

                    bool hasFlatNumber = flatNumberValue != null && !string.IsNullOrEmpty(flatNumberValue.ToString());
                    bool hasArea = roomAreaParam != null && roomAreaParam.AsDouble() > 0.1;

                    if (hasFlatNumber && hasArea)
                    {
                        string flatNumber = SafeConvertToString(flatNumberValue);

                        if (!roomsByFlat.ContainsKey(flatNumber))
                            roomsByFlat[flatNumber] = new List<Room>();

                        roomsByFlat[flatNumber].Add(room);
                    }
                }
                catch (Exception ex)
                {
                    // Пропускаем проблемные помещения
                    continue;
                }
            }

            return roomsByFlat;
        }

        private void AnalyzeRoomStatistics(Dictionary<string, List<Room>> roomsByFlat)
        {
            foreach (var flatRooms in roomsByFlat.Values)
            {
                var roomCounts = new Dictionary<string, int>();

                foreach (Room room in flatRooms)
                {
                    try
                    {
                        string roomName = SafeConvertToString(GetParameterValue(room, "room_name"));
                        string normalizedName = NormalizeRoomName(roomName);

                        if (_roomMappings.ContainsKey(normalizedName))
                        {
                            if (!roomCounts.ContainsKey(normalizedName))
                                roomCounts[normalizedName] = 0;
                            roomCounts[normalizedName]++;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Обновляем максимумы
                if (roomCounts.ContainsKey("жилая комната") && roomCounts["жилая комната"] > _maxLivingRoomsPerFlat)
                    _maxLivingRoomsPerFlat = roomCounts["жилая комната"];

                if (roomCounts.ContainsKey("гардеробная") && roomCounts["гардеробная"] > _maxWardrobesPerFlat)
                    _maxWardrobesPerFlat = roomCounts["гардеробная"];

                if (roomCounts.ContainsKey("санузел") && roomCounts["санузел"] > _maxBathroomsPerFlat)
                    _maxBathroomsPerFlat = roomCounts["санузел"];
            }

            // Гарантируем минимум 1 для основных типов комнат
            if (_maxLivingRoomsPerFlat == 0) _maxLivingRoomsPerFlat = 1;
            if (_maxWardrobesPerFlat == 0) _maxWardrobesPerFlat = 1;
            if (_maxBathroomsPerFlat == 0) _maxBathroomsPerFlat = 1;
        }

        private string NormalizeRoomName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
                return "";

            string lowerName = roomName.ToLower();

            // Сопоставляем с нашим маппингом
            foreach (string mappingKey in _roomMappings.Keys)
            {
                if (lowerName.Contains(mappingKey))
                    return mappingKey;
            }

            return lowerName;
        }

        private List<object> CreateHeader()
        {
            var header = new List<object>
            {
                "Условный номер помещения",
                "Балкон/лоджия/терраса",
                "Вертикальная ось",
                "Вид помещения студия, _1 сп, _2 сп",
                "Горизонтальная ось",
                "Жилая площадь",
                "Комнат пиб _студия_1 ккв _2 ккв _3 ккв",
                "Маркетинговый тип 2_М, 2_L",
                "Назначение",
                "Тип помещения кв., комм., паркинг",
                "Высота потолка"
            };

            // Добавляем колонки для жилых комнат
            for (int i = 1; i <= _maxLivingRoomsPerFlat; i++)
            {
                header.Add($"Площадь комнаты {i}");
            }

            // Добавляем колонки для других комнат согласно маппингу
            foreach (var mapping in _roomMappings)
            {
                if (mapping.Key == "жилая комната") continue; // Уже добавили

                if (mapping.Value.CanHaveMultiple)
                {
                    int maxCount = GetMaxCountForRoomType(mapping.Key);
                    for (int i = 1; i <= maxCount; i++)
                    {
                        header.Add($"{mapping.Value.ColumnName} {i}");
                    }
                }
                else
                {
                    header.Add(mapping.Value.ColumnName);
                }
            }

            header.Add("Площадь общ.");
            header.Add("Площадь прив.");

            return header;
        }

        private int GetMaxCountForRoomType(string roomType)
        {
            switch (roomType)
            {
                case "жилая комната": return _maxLivingRoomsPerFlat;
                case "гардеробная": return _maxWardrobesPerFlat;
                case "санузел": return _maxBathroomsPerFlat;
                default: return 1;
            }
        }

        private List<object> ProcessFlatData(List<Room> flatRooms)
        {
            try
            {
                if (flatRooms.Count == 0)
                    return null;

                // Берем первую комнату для получения общих параметров квартиры
                Room mainRoom = flatRooms[0];

                // Получаем основные параметры квартиры с безопасной конвертацией
                string flatNumber = SafeConvertToString(GetParameterValue(mainRoom, "flat_number"));
                string roomKorpus = SafeConvertToString(GetParameterValue(mainRoom, "room_korpus"));
                string roomSection = SafeConvertToString(GetParameterValue(mainRoom, "room_section"));

                object roomLevelObj = GetParameterValue(mainRoom, "room_level");
                string roomLevel = SafeConvertToString(roomLevelObj, "1");

                string flatType = SafeConvertToString(GetParameterValue(mainRoom, "flat_type"));
                string marketingType = SafeConvertToString(GetParameterValue(mainRoom, "marketing_type"));

                object livingAreaObj = GetParameterValue(mainRoom, "living_area_frc");
                double livingArea = SafeConvertToDouble(livingAreaObj);

                object roomsNumberObj = GetParameterValue(mainRoom, "rooms_number");
                int roomsNumber = SafeConvertToInt(roomsNumberObj);

                // Высота потолка
                double ceiling = 0;
                try
                {
                    Parameter ceilingParam = mainRoom.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
                    if (ceilingParam != null && ceilingParam.HasValue)
                    {
                        ceiling = Math.Round(UnitUtils.ConvertFromInternalUnits(ceilingParam.AsDouble(), UnitTypeId.Meters), 2);
                    }
                }
                catch
                {
                    ceiling = 2.7; // Значение по умолчанию
                }

                // Определяем тип балкона
                string balcony = "без балк/лодж/терр";
                bool hasBalcony = false;
                bool hasLoggia = false;
                bool hasTerrace = false;

                // Собираем площади по типам комнат
                var roomAreas = new Dictionary<string, List<double>>();

                // Инициализируем списки для всех типов комнат
                foreach (var mapping in _roomMappings)
                {
                    roomAreas[mapping.Key] = new List<double>();
                }

                double totalArea = 0;

                foreach (Room room in flatRooms)
                {
                    try
                    {
                        string roomName = SafeConvertToString(GetParameterValue(room, "room_name"));
                        string normalizedName = NormalizeRoomName(roomName);

                        object roomAreaObj = GetParameterValue(room, "room_area_frc");
                        double roomArea = SafeConvertToDouble(roomAreaObj);

                        totalArea += roomArea;

                        // Проверяем балконы/лоджии/террасы
                        if (normalizedName == "балкон") hasBalcony = true;
                        if (normalizedName == "лоджия") hasLoggia = true;
                        if (normalizedName == "терраса") hasTerrace = true;

                        // Добавляем площадь в соответствующий список
                        if (_roomMappings.ContainsKey(normalizedName))
                        {
                            roomAreas[normalizedName].Add(roomArea);
                        }
                        else
                        {
                            // Если комната не распознана, добавляем в "жилые комнаты"
                            roomAreas["жилая комната"].Add(roomArea);
                        }
                    }
                    catch
                    {
                        continue; // Пропускаем проблемные комнаты
                    }
                }

                // Определяем итоговый тип балкона
                if (hasTerrace) balcony = "терраса";
                else if (hasLoggia) balcony = "лоджия";
                else if (hasBalcony) balcony = "балкон";

                // Сортируем площади по убыванию для комнат, которых может быть несколько
                foreach (var roomType in roomAreas.Keys.ToList())
                {
                    if (_roomMappings.ContainsKey(roomType) && _roomMappings[roomType].CanHaveMultiple)
                    {
                        roomAreas[roomType] = roomAreas[roomType].OrderByDescending(x => x).ToList();
                    }
                }

                // Формируем текстовое представление количества комнат
                string roomsNumberText = "";
                if (roomsNumber > 0)
                {
                    roomsNumberText = $"{roomsNumber} ккв";
                }
                else
                {
                    // Если количество комнат не указано, пытаемся определить по жилым комнатам
                    int livingRoomsCount = roomAreas["жилая комната"].Count;
                    if (livingRoomsCount > 0)
                    {
                        roomsNumberText = $"{livingRoomsCount} ккв";
                    }
                    else
                    {
                        roomsNumberText = "1 ккв";
                    }
                }

                // Создаем основную запись
                var flatInfo = new List<object>
                {
                    flatNumber,
                    balcony,
                    roomLevel, // Вертикальная ось (временно используем уровень)
                    flatType,
                    roomSection, // Горизонтальная ось (временно используем секцию)
                    livingArea,
                    roomsNumberText,
                    marketingType,
                    "Квартира",
                    "Квартира",
                    ceiling
                };

                // Добавляем площади жилых комнат
                for (int i = 0; i < _maxLivingRoomsPerFlat; i++)
                {
                    double area = i < roomAreas["жилая комната"].Count ? roomAreas["жилая комната"][i] : 0;
                    flatInfo.Add(area);
                }

                // Добавляем площади других комнат согласно маппингу
                foreach (var mapping in _roomMappings)
                {
                    if (mapping.Key == "жилая комната") continue; // Уже добавили

                    if (mapping.Value.CanHaveMultiple)
                    {
                        int maxCount = GetMaxCountForRoomType(mapping.Key);
                        for (int i = 0; i < maxCount; i++)
                        {
                            double area = i < roomAreas[mapping.Key].Count ? roomAreas[mapping.Key][i] : 0;
                            flatInfo.Add(area);
                        }
                    }
                    else
                    {
                        double area = roomAreas[mapping.Key].Count > 0 ? roomAreas[mapping.Key][0] : 0;
                        flatInfo.Add(area);
                    }
                }

                // Добавляем общие площади
                flatInfo.Add(Math.Round(totalArea, 2));
                flatInfo.Add(Math.Round(totalArea, 2));

                return flatInfo;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка обработки квартиры",
                    $"Ошибка при обработке квартиры: {ex.Message}\n\n" +
                    $"StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        public void ExportToExcel(List<List<object>> flatsInfo, string filePath)
        {
            if (flatsInfo == null || flatsInfo.Count <= 1)
            {
                TaskDialog.Show("Ошибка", "Нет данных для экспорта");
                return;
            }

            Application excelApp = null;
            Workbook workbook = null;
            Worksheet worksheet = null;

            try
            {
                excelApp = new Application();
                excelApp.Visible = false;

                workbook = excelApp.Workbooks.Add();
                worksheet = workbook.ActiveSheet as Worksheet;
                worksheet.Name = "Данные по квартирам";

                // Записываем заголовки
                List<object> header = flatsInfo[0];
                for (int col = 0; col < header.Count; col++)
                {
                    worksheet.Cells[1, col + 1] = header[col].ToString();
                }

                // Форматируем заголовки
                Range headerRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[1, header.Count]];
                headerRange.Font.Bold = true;
                headerRange.Font.Size = 12;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                headerRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;

                // Записываем данные
                for (int row = 1; row < flatsInfo.Count; row++)
                {
                    List<object> rowData = flatsInfo[row];
                    for (int col = 0; col < rowData.Count; col++)
                    {
                        object value = rowData[col];
                        var cell = worksheet.Cells[row + 1, col + 1];

                        if (value is double doubleValue)
                        {
                            cell.Value = doubleValue;
                            // Исправленный формат чисел
                            //cell.NumberFormat = "0.00";
                        }
                        else if (value is int intValue)
                        {
                            cell.Value = intValue;
                        }
                        else
                        {
                            cell.Value = value?.ToString() ?? "";
                        }
                    }
                }

                // Автоподбор ширины колонок
                worksheet.Columns.AutoFit();

                // Добавляем границы ко всем данным
                Range dataRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[flatsInfo.Count, header.Count]];
                dataRange.Borders.LineStyle = XlLineStyle.xlContinuous;

                // Замораживаем область заголовков
                worksheet.Application.ActiveWindow.SplitRow = 1;
                worksheet.Application.ActiveWindow.FreezePanes = true;

                // Сохраняем файл
                workbook.SaveAs(filePath);

                TaskDialog.Show("Успех",
                    $"Данные успешно экспортированы!\n" +
                    $"Файл: {Path.GetFileName(filePath)}\n" +
                    $"Квартир: {flatsInfo.Count - 1}\n" +
                    $"Столбцов: {header.Count}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка при экспорте в Excel: {ex.Message}\n\n{ex.StackTrace}");
            }
            finally
            {
                // Корректное освобождение ресурсов
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
                    if (worksheet != null)
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                    }
                }
                catch (Exception ex2)
                {
                    TaskDialog.Show("Предупреждение", $"Ошибка при освобождении ресурсов Excel: {ex2.Message}");
                }
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ProcessFlatsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                var processor = new FlatProcessor(doc);

                // Показываем диагностическую информацию
                processor.ShowDiagnosticInfo();

                // Обрабатываем данные
                var flatsInfo = processor.ProcessFlats();

                if (flatsInfo.Count > 1)
                {
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx",
                        FileName = $"Квартиры_{DateTime.Now:yyyyMMdd_HHmmss}",
                        DefaultExt = ".xlsx"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        processor.ExportToExcel(flatsInfo, saveDialog.FileName);
                        return Result.Succeeded;
                    }
                    return Result.Cancelled;
                }
                else
                {
                    TaskDialog.Show("Информация",
                        "Не найдено квартир с заполненными параметрами.\n\n" +
                        "Проверьте диагностическую информацию выше.");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка выполнения: {ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}