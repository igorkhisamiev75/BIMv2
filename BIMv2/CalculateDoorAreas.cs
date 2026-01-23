#region Namespaces
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using OperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;
#endregion

namespace DoorAreaCalculator
{
    [Transaction(TransactionMode.Manual)]
    public class CalculateOpeningAreas : IExternalCommand
    {
        // Список необходимых параметров
        private readonly List<string> requiredParameters = new List<string>
        {
            "ПлощадьПроемов_Двери",
            "ПлощадьПроемов_Окна",
            "ПлощадьПроемов_Витражи",
            "ПлощадьПроемов_РазделителиПомещений"
        };

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 0. Проверяем наличие необходимых параметров перед началом расчета
                List<string> missingParameters = CheckRequiredParameters(doc);

                if (missingParameters.Count > 0)
                {
                    ShowMissingParametersMessage(missingParameters, doc);
                    return Result.Cancelled;
                }

                using (Transaction trans = new Transaction(doc, "Расчет площадей проемов и разделителей"))
                {
                    trans.Start();

                    // 1. Собираем все помещения
                    List<Room> rooms = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .ToList();

                    if (rooms.Count == 0)
                    {
                        TaskDialog.Show("Информация", "В проекте не найдено помещений.");
                        return Result.Cancelled;
                    }

                    // Словари для хранения суммарных площадей по помещениям
                    Dictionary<ElementId, double> roomDoorAreas = new Dictionary<ElementId, double>();
                    Dictionary<ElementId, double> roomWindowAreas = new Dictionary<ElementId, double>();
                    Dictionary<ElementId, double> roomCurtainWallAreas = new Dictionary<ElementId, double>();
                    Dictionary<ElementId, double> roomSeparationLineAreas = new Dictionary<ElementId, double>();

                    // 2. Собираем все двери
                    List<FamilyInstance> doors = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .ToList();

                    // 3. Собираем все окна
                    List<FamilyInstance> windows = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Windows)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .ToList();

                    // 4. Собираем ВСЕ элементы и фильтруем их для витражей
                    List<Element> curtainWalls = new List<Element>();
                    List<Element> curtainPanels = new List<Element>();
                    List<Element> curtainFamilies = new List<Element>();

                    // Собираем все элементы категории стен
                    FilteredElementCollector wallCollector = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Walls)
                        .WhereElementIsNotElementType();

                    foreach (Element element in wallCollector)
                    {
                        // Проверяем, является ли элемент занавесной стеной
                        if (IsCurtainWall(element))
                        {
                            curtainWalls.Add(element);
                        }
                    }

                    // Собираем занавесные панели
                    FilteredElementCollector panelCollector = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                        .WhereElementIsNotElementType();

                    foreach (Element element in panelCollector)
                    {
                        curtainPanels.Add(element);
                    }

                    // Собираем все семейства и фильтруем витражи
                    FilteredElementCollector familyCollector = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .WhereElementIsNotElementType();

                    foreach (Element element in familyCollector)
                    {
                        FamilyInstance fi = element as FamilyInstance;
                        if (fi != null && IsCurtainFamily(fi) &&
                            fi.Category?.Id.IntegerValue != (int)BuiltInCategory.OST_Doors &&
                            fi.Category?.Id.IntegerValue != (int)BuiltInCategory.OST_Windows)
                        {
                            curtainFamilies.Add(element);
                        }
                    }

                    // 5. Собираем разделители помещений (Room Separation Lines)
                    List<ModelCurve> roomSeparationLines = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_RoomSeparationLines)
                        .WhereElementIsNotElementType()
                        .Cast<ModelCurve>()
                        .ToList();

                    // 6. Получаем высоту этажа для расчета площади разделителей
                    double defaultFloorHeight = GetDefaultFloorHeight(doc);

                    if (doors.Count == 0 && windows.Count == 0 && curtainWalls.Count == 0 &&
                        curtainPanels.Count == 0 && curtainFamilies.Count == 0 && roomSeparationLines.Count == 0)
                    {
                        TaskDialog.Show("Информация", "В проекте не найдено дверей, окон, витражей и разделителей.");
                        return Result.Cancelled;
                    }

                    int processedDoors = 0;
                    int processedWindows = 0;
                    int processedCurtainWalls = 0;
                    int processedCurtainPanels = 0;
                    int processedCurtainFamilies = 0;
                    int processedSeparationLines = 0;

                    // Обрабатываем двери
                    foreach (FamilyInstance door in doors)
                    {
                        // Определяем в каких помещениях находится дверь
                        Room fromRoom = door.FromRoom;
                        Room toRoom = door.ToRoom;

                        // Получаем площадь проема из параметров типа
                        double doorArea = GetDoorArea(door);

                        if (doorArea > 0)
                        {
                            processedDoors++;

                            // Распределяем площадь между помещениями
                            if (fromRoom != null && toRoom != null)
                            {
                                // Дверь между двумя помещениями - полная площадь для каждого помещения
                                AddAreaToRoom(roomDoorAreas, fromRoom.Id, doorArea);
                                AddAreaToRoom(roomDoorAreas, toRoom.Id, doorArea);
                            }
                            else if (fromRoom != null)
                            {
                                // Дверь только в одном помещении
                                AddAreaToRoom(roomDoorAreas, fromRoom.Id, doorArea);
                            }
                            else if (toRoom != null)
                            {
                                // Дверь только в одном помещении
                                AddAreaToRoom(roomDoorAreas, toRoom.Id, doorArea);
                            }
                        }
                    }

                    // Обрабатываем окна
                    foreach (FamilyInstance window in windows)
                    {
                        // Определяем в каком помещении находится окно
                        Room windowRoom = window.Room;

                        if (windowRoom != null)
                        {
                            // Получаем площадь окна из параметров типа
                            double windowArea = GetWindowArea(window);

                            if (windowArea > 0)
                            {
                                processedWindows++;
                                AddAreaToRoom(roomWindowAreas, windowRoom.Id, windowArea);
                            }
                        }
                    }

                    // Обрабатываем витражи (занавесные стены)
                    foreach (Element curtainWall in curtainWalls)
                    {
                        // Получаем помещение, в котором находится витраж
                        Room curtainWallRoom = GetRoomContainingElement(curtainWall, rooms);

                        if (curtainWallRoom != null)
                        {
                            // Получаем площадь витража
                            double curtainWallArea = GetElementArea(curtainWall);

                            if (curtainWallArea > 0)
                            {
                                processedCurtainWalls++;
                                AddAreaToRoom(roomCurtainWallAreas, curtainWallRoom.Id, curtainWallArea);
                            }
                        }
                    }

                    // Обрабатываем занавесные панели
                    foreach (Element curtainPanel in curtainPanels)
                    {
                        // Получаем помещение, в котором находится панель
                        Room panelRoom = GetRoomForElement(curtainPanel, rooms);

                        if (panelRoom != null)
                        {
                            // Получаем площадь панели
                            double panelArea = GetElementArea(curtainPanel);

                            if (panelArea > 0)
                            {
                                processedCurtainPanels++;
                                AddAreaToRoom(roomCurtainWallAreas, panelRoom.Id, panelArea);
                            }
                        }
                    }

                    // Обрабатываем семейства-витражи
                    foreach (Element curtainFamily in curtainFamilies)
                    {
                        // Получаем помещение, в котором находится семейство
                        Room familyRoom = GetRoomForElement(curtainFamily, rooms);

                        if (familyRoom != null)
                        {
                            // Получаем площадь семейства
                            double familyArea = GetElementArea(curtainFamily);

                            if (familyArea > 0)
                            {
                                processedCurtainFamilies++;
                                AddAreaToRoom(roomCurtainWallAreas, familyRoom.Id, familyArea);
                            }
                        }
                    }

                    // Обрабатываем разделители помещений
                    foreach (ModelCurve separationLine in roomSeparationLines)
                    {
                        // Получаем кривую разделителя
                        Curve curve = separationLine.GeometryCurve;
                        if (curve == null) continue;

                        // Получаем длину разделителя в метрах
                        double lengthMeters = GetCurveLengthInMeters(curve);
                        if (lengthMeters <= 0) continue;

                        // Получаем помещения, которые граничат с этим разделителем
                        List<Room> adjacentRooms = GetRoomsAdjacentToSeparationLine(separationLine, rooms);

                        if (adjacentRooms.Count > 0)
                        {
                            // Для каждого помещения вычисляем площадь разделителя
                            foreach (Room room in adjacentRooms)
                            {
                                // Получаем высоту помещения или используем высоту по умолчанию
                                double roomHeightMeters = GetRoomHeightInMeters(room) ?? 3.0; // 3 метра по умолчанию

                                // Вычисляем площадь разделителя: длина (м) * высота (м) = площадь (м²)
                                double separationArea = lengthMeters * roomHeightMeters;

                                if (separationArea > 0 && separationArea < 1000) // Проверяем разумные значения
                                {
                                    processedSeparationLines++;
                                    AddAreaToRoom(roomSeparationLineAreas, room.Id, separationArea);
                                }
                            }
                        }
                    }

                    // 7. Записываем результаты в отдельные параметры помещений
                    int updatedRooms = 0;

                    foreach (Room room in rooms)
                    {
                        bool hasDoorArea = roomDoorAreas.ContainsKey(room.Id);
                        bool hasWindowArea = roomWindowAreas.ContainsKey(room.Id);
                        bool hasCurtainWallArea = roomCurtainWallAreas.ContainsKey(room.Id);
                        bool hasSeparationLineArea = roomSeparationLineAreas.ContainsKey(room.Id);

                        bool anyAreaFound = hasDoorArea || hasWindowArea || hasCurtainWallArea || hasSeparationLineArea;

                        if (anyAreaFound)
                        {
                            double totalDoorArea = hasDoorArea ? roomDoorAreas[room.Id] : 0;
                            double totalWindowArea = hasWindowArea ? roomWindowAreas[room.Id] : 0;
                            double totalCurtainWallArea = hasCurtainWallArea ? roomCurtainWallAreas[room.Id] : 0;
                            double totalSeparationLineArea = hasSeparationLineArea ? roomSeparationLineAreas[room.Id] : 0;

                            // Обновляем отдельные параметры для каждой категории
                            bool parametersUpdated = false;

                            // Параметр для дверей
                            if (UpdateRoomParameter(room, "ПлощадьПроемов_Двери", totalDoorArea))
                                parametersUpdated = true;

                            // Параметр для окон
                            if (UpdateRoomParameter(room, "ПлощадьПроемов_Окна", totalWindowArea))
                                parametersUpdated = true;

                            // Параметр для витражей
                            if (UpdateRoomParameter(room, "ПлощадьПроемов_Витражи", totalCurtainWallArea))
                                parametersUpdated = true;

                            // Параметр для разделителей
                            if (UpdateRoomParameter(room, "ПлощадьПроемов_РазделителиПомещений", totalSeparationLineArea))
                                parametersUpdated = true;

                            if (parametersUpdated)
                                updatedRooms++;

                            // Также обновляем параметр "Комментарии" для обратной совместимости
                            UpdateCommentsParameter(room, totalDoorArea, totalWindowArea, totalCurtainWallArea, totalSeparationLineArea);
                        }
                        else
                        {
                            // Если у помещения нет проемов, очищаем все параметры
                            ClearAllParameters(room);
                        }
                    }

                    trans.Commit();

                    // Статистика
                    int doorsInTwoRooms = doors.Count(d => d.FromRoom != null && d.ToRoom != null);
                    int doorsInOneRoom = doors.Count(d => (d.FromRoom != null && d.ToRoom == null) ||
                                                         (d.FromRoom == null && d.ToRoom != null));
                    int doorsWithoutRooms = doors.Count(d => d.FromRoom == null && d.ToRoom == null);

                    int windowsWithRoom = windows.Count(w => w.Room != null);
                    int windowsWithoutRoom = windows.Count(w => w.Room == null);

                    int curtainWallsWithRoom = curtainWalls.Count(cw => GetRoomContainingElement(cw, rooms) != null);
                    int curtainPanelsWithRoom = curtainPanels.Count(cp => GetRoomForElement(cp, rooms) != null);
                    int curtainFamiliesWithRoom = curtainFamilies.Count(cf => GetRoomForElement(cf, rooms) != null);

                    TaskDialog.Show("Результаты расчета",
                        $"ОБРАБОТАНО:\n" +
                        $"• Всего помещений: {rooms.Count}\n" +
                        $"• Помещений обновлено: {updatedRooms}\n" +
                        $"• Помещений без проемов: {rooms.Count - updatedRooms}\n\n" +
                        $"ДВЕРИ:\n" +
                        $"• Всего дверей: {doors.Count}\n" +
                        $"• Дверей с площадью: {processedDoors}\n" +
                        $"• Дверей между 2 помещениями: {doorsInTwoRooms}\n" +
                        $"• Дверей в 1 помещении: {doorsInOneRoom}\n" +
                        $"• Дверей без помещения: {doorsWithoutRooms}\n\n" +
                        $"ОКНА:\n" +
                        $"• Всего окон: {windows.Count}\n" +
                        $"• Окон с площадью: {processedWindows}\n" +
                        $"• Окон с помещением: {windowsWithRoom}\n" +
                        $"• Окон без помещения: {windowsWithoutRoom}\n\n" +
                        $"ВИТРАЖИ:\n" +
                        $"• Занавесных стен: {curtainWalls.Count}\n" +
                        $"• С площадью: {processedCurtainWalls}\n" +
                        $"• С помещением: {curtainWallsWithRoom}\n" +
                        $"• Панелей: {curtainPanels.Count}\n" +
                        $"• Панелей с площадью: {processedCurtainPanels}\n" +
                        $"• Панелей с помещением: {curtainPanelsWithRoom}\n" +
                        $"• Семейств-витражей: {curtainFamilies.Count}\n" +
                        $"• Семейств с площадью: {processedCurtainFamilies}\n" +
                        $"• Семейств с помещением: {curtainFamiliesWithRoom}\n\n" +
                        $"РАЗДЕЛИТЕЛИ ПОМЕЩЕНИЙ:\n" +
                        $"• Всего разделителей: {roomSeparationLines.Count}\n" +
                        $"• Обработано разделителей: {processedSeparationLines}\n\n" +
                        $"РЕЗУЛЬТАТ:\n" +
                        $"• Параметры обновлены: ПлощадьПроемов_Двери, ПлощадьПроемов_Окна,\n" +
                        $"  ПлощадьПроемов_Витражи, ПлощадьПроемов_РазделителиПомещений");
                }

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                // Пользователь отменил операцию
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка",
                    $"Произошла ошибка:\n{ex.Message}\n\n" +
                    $"StackTrace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        // Метод для получения длины кривой в метрах
        private double GetCurveLengthInMeters(Curve curve)
        {
            try
            {
                if (curve == null) return 0;

                // Получаем длину в внутренних единицах (футах)
                double lengthFeet = curve.Length;

                // Конвертируем футы в метры (1 фут = 0.3048 метра)
                double lengthMeters = UnitUtils.ConvertFromInternalUnits(lengthFeet, UnitTypeId.Meters);

                return lengthMeters;
            }
            catch
            {
                return 0;
            }
        }

        // Метод для получения высоты помещения в метрах
        private double? GetRoomHeightInMeters(Room room)
        {
            try
            {
                // Параметр "Высота помещения" (внутренние единицы - футы)
                Parameter heightParam = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
                if (heightParam != null && heightParam.HasValue)
                {
                    double heightFeet = heightParam.AsDouble();
                    // Конвертируем футы в метры
                    double heightMeters = UnitUtils.ConvertFromInternalUnits(heightFeet, UnitTypeId.Meters);
                    return heightMeters;
                }

                // Параметр "Высота" (общий)
                heightParam = room.LookupParameter("Высота");
                if (heightParam != null && heightParam.HasValue)
                {
                    double heightFeet = heightParam.AsDouble();
                    // Конвертируем футы в метры
                    double heightMeters = UnitUtils.ConvertFromInternalUnits(heightFeet, UnitTypeId.Meters);
                    return heightMeters;
                }

                // Параметр "Unbounded Height" (верхний offset)
                heightParam = room.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET);
                if (heightParam != null && heightParam.HasValue)
                {
                    double upperOffsetFeet = heightParam.AsDouble();

                    // Получаем нижний offset
                    Parameter lowerOffsetParam = room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET);
                    double lowerOffsetFeet = 0;
                    if (lowerOffsetParam != null && lowerOffsetParam.HasValue)
                    {
                        lowerOffsetFeet = lowerOffsetParam.AsDouble();
                    }

                    // Вычисляем высоту в футах
                    double heightFeet = upperOffsetFeet - lowerOffsetFeet;

                    // Конвертируем футы в метры
                    double heightMeters = UnitUtils.ConvertFromInternalUnits(heightFeet, UnitTypeId.Meters);
                    return heightMeters;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // Метод для проверки наличия необходимых параметров
        private List<string> CheckRequiredParameters(Document doc)
        {
            List<string> missingParameters = new List<string>();

            try
            {
                // Берем первое помещение для проверки параметров
                Room sampleRoom = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault() as Room;

                if (sampleRoom == null)
                {
                    // Если нет помещений, все параметры считаем отсутствующими
                    return requiredParameters;
                }

                // Проверяем каждый требуемый параметр
                foreach (string paramName in requiredParameters)
                {
                    Parameter param = sampleRoom.LookupParameter(paramName);
                    if (param == null)
                    {
                        missingParameters.Add(paramName);
                    }
                }
            }
            catch
            {
                // В случае ошибки считаем все параметры отсутствующими
                return requiredParameters;
            }

            return missingParameters;
        }

        // Метод для показа сообщения об отсутствующих параметрах
        private void ShowMissingParametersMessage(List<string> missingParameters, Document doc)
        {
            string missingParamsText = string.Join("\n• ", missingParameters);
            string projectName = doc.Title;

            string message = $"В проекте '{projectName}' отсутствуют необходимые параметры для работы плагина:\n\n" +
                           $"• {missingParamsText}\n\n" +
                           $"ПЕРЕД ИСПОЛЬЗОВАНИЕМ ПЛАГИНА ВЫПОЛНИТЕ СЛЕДУЮЩИЕ ДЕЙСТВИЯ:\n\n" +
                           $"1. Перейдите в 'Управление' → 'Параметры проекта'\n" +
                           $"2. Нажмите 'Добавить...' в разделе 'Параметры'\n" +
                           $"3. В открывшемся окне нажмите 'Создать...'\n" +
                           $"4. Настройте каждый параметр:\n" +
                           $"   • Тип параметра: 'Общий параметр'\n" +
                           $"   • Тип данных: 'Текст'\n" +
                           $"   • Группа параметров: 'Данные'\n" +
                           $"   • Установите флажок 'Экземпляр'\n" +
                           $"   • Привяжите к категории 'Помещения'\n\n" +
                           $"5. Создайте все {missingParameters.Count} параметров из списка выше\n" +
                           $"6. Сохраните изменения и перезапустите плагин\n\n" +
                           $"ПРИМЕЧАНИЕ:\n" +
                           $"• Если параметры уже существуют в шаблоне проекта,\n" +
                           $"  загрузите этот шаблон или перенесите параметры.\n" +
                           $"• После создания параметров необходимо перезагрузить плагин.";

            TaskDialog dialog = new TaskDialog("Отсутствуют необходимые параметры");
            dialog.MainInstruction = "Требуются общие параметры для работы плагина";
            dialog.MainContent = message;
            dialog.CommonButtons = TaskDialogCommonButtons.Ok;
            dialog.Show();
        }

        // Метод для обновления параметра помещения
        private bool UpdateRoomParameter(Room room, string parameterName, double areaValue)
        {
            try
            {
                Parameter param = room.LookupParameter(parameterName);
                if (param == null)
                {
                    // Этот случай не должен возникнуть, так как мы проверили параметры заранее
                    TaskDialog.Show("Ошибка",
                        $"Параметр '{parameterName}' не найден в помещении.\n" +
                        $"Проверьте, что параметры правильно созданы и привязаны к категории 'Помещения'.");
                    return false;
                }

                if (!param.IsReadOnly)
                {
                    // Если площадь равна 0, очищаем параметр
                    if (areaValue == 0)
                    {
                        if (param.HasValue)
                            param.SetValueString("");
                    }
                    else
                    {
                        // Форматируем значение площади
                        string formattedValue = areaValue.ToString("F3");
                        param.SetValueString(formattedValue);
                    }
                    return true;
                }
                else
                {
                    TaskDialog.Show("Внимание",
                        $"Параметр '{parameterName}' доступен только для чтения.\n" +
                        $"Убедитесь, что параметр имеет правильные настройки доступа.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка при обновлении параметра",
                    $"Не удалось обновить параметр '{parameterName}':\n{ex.Message}");
                return false;
            }
        }

        // Метод для обновления параметра "Комментарии" (для обратной совместимости)
        private void UpdateCommentsParameter(Room room, double doorArea, double windowArea,
                                           double curtainArea, double separationArea)
        {
            try
            {
                Parameter commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam != null && !commentsParam.IsReadOnly)
                {
                    // Формируем строку результата
                    string resultText = "";

                    if (doorArea > 0)
                    {
                        string doorAreaValue = doorArea.ToString("F3");
                        resultText += $"Площадь дверей: {doorAreaValue} м²";
                    }

                    if (windowArea > 0)
                    {
                        if (!string.IsNullOrEmpty(resultText))
                            resultText += ", ";
                        string windowAreaValue = windowArea.ToString("F3");
                        resultText += $"Площадь окон: {windowAreaValue} м²";
                    }

                    if (curtainArea > 0)
                    {
                        if (!string.IsNullOrEmpty(resultText))
                            resultText += ", ";
                        string curtainAreaValue = curtainArea.ToString("F3");
                        resultText += $"Площадь витражей: {curtainAreaValue} м²";
                    }

                    if (separationArea > 0)
                    {
                        if (!string.IsNullOrEmpty(resultText))
                            resultText += ", ";
                        string separationAreaValue = separationArea.ToString("F3");
                        resultText += $"Площадь разделителей: {separationAreaValue} м²";
                    }

                    commentsParam.Set(resultText);
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        // Метод для очистки всех параметров
        private void ClearAllParameters(Room room)
        {
            try
            {
                // Очищаем параметр "Комментарии"
                Parameter commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam != null && !commentsParam.IsReadOnly && commentsParam.HasValue)
                {
                    commentsParam.Set(string.Empty);
                }

                // Очищаем пользовательские параметры
                ClearParameterIfExists(room, "ПлощадьПроемов_Двери");
                ClearParameterIfExists(room, "ПлощадьПроемов_Окна");
                ClearParameterIfExists(room, "ПлощадьПроемов_Витражи");
                ClearParameterIfExists(room, "ПлощадьПроемов_РазделителиПомещений");
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        // Метод для очистки конкретного параметра
        private void ClearParameterIfExists(Room room, string parameterName)
        {
            try
            {
                Parameter param = room.LookupParameter(parameterName);
                if (param != null && !param.IsReadOnly && param.HasValue)
                {
                    param.SetValueString("");
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        // Метод для получения высоты этажа по умолчанию
        private double GetDefaultFloorHeight(Document doc)
        {
            try
            {
                // Получаем первый уровень для примера высоты
                Level firstLevel = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();

                if (firstLevel != null)
                {
                    // Предполагаемая высота этажа - 3 метра (конвертируем в футы)
                    return UnitUtils.ConvertToInternalUnits(3.0, UnitTypeId.Meters);
                }

                // Если уровней нет, используем стандартную высоту 3 метра
                return UnitUtils.ConvertToInternalUnits(3.0, UnitTypeId.Meters);
            }
            catch
            {
                return UnitUtils.ConvertToInternalUnits(3.0, UnitTypeId.Meters);
            }
        }

        // Метод для получения помещений, граничащих с разделителем
        private List<Room> GetRoomsAdjacentToSeparationLine(ModelCurve separationLine, List<Room> allRooms)
        {
            List<Room> adjacentRooms = new List<Room>();

            try
            {
                // Получаем кривую разделителя
                Curve curve = separationLine.GeometryCurve;
                if (curve == null) return adjacentRooms;

                // Берем несколько точек вдоль кривой
                int samplePoints = 5;
                for (int i = 0; i <= samplePoints; i++)
                {
                    double parameter = curve.IsBound ? curve.GetEndParameter(0) +
                        (curve.GetEndParameter(1) - curve.GetEndParameter(0)) * i / samplePoints : 0;

                    XYZ pointOnCurve = curve.Evaluate(parameter, false);

                    // Ищем помещение, содержащее эту точку
                    Room roomAtPoint = FindRoomAtPoint(pointOnCurve, allRooms);
                    if (roomAtPoint != null && !adjacentRooms.Contains(roomAtPoint))
                    {
                        adjacentRooms.Add(roomAtPoint);
                    }

                    // Также проверяем точку с небольшим смещением в обе стороны от кривой
                    if (curve is Line line)
                    {
                        XYZ direction = line.Direction;
                        XYZ normal = new XYZ(-direction.Y, direction.X, 0).Normalize();

                        // Смещение в 0.1 метра (конвертируем в футы)
                        double offset = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Meters);

                        // Точка с одной стороны от линии
                        XYZ offsetPoint1 = pointOnCurve + normal * offset;
                        Room room1 = FindRoomAtPoint(offsetPoint1, allRooms);
                        if (room1 != null && !adjacentRooms.Contains(room1))
                        {
                            adjacentRooms.Add(room1);
                        }

                        // Точка с другой стороны от линии
                        XYZ offsetPoint2 = pointOnCurve - normal * offset;
                        Room room2 = FindRoomAtPoint(offsetPoint2, allRooms);
                        if (room2 != null && !adjacentRooms.Contains(room2))
                        {
                            adjacentRooms.Add(room2);
                        }
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем пустой список
            }

            return adjacentRooms;
        }

        // Метод для проверки, является ли элемент занавесной стеной
        private bool IsCurtainWall(Element element)
        {
            try
            {
                // Проверяем, является ли элемент стеной
                if (element is Wall wall)
                {
                    // Проверяем через WallType.Kind
                    WallType wallType = wall.WallType;
                    if (wallType != null)
                    {
                        // В Revit 2022 есть свойство Kind
                        if (wallType.Kind == WallKind.Curtain)
                        {
                            return true;
                        }
                    }
                }

                // Дополнительная проверка по имени
                string elementName = element.Name?.ToLower() ?? "";
                if (elementName.Contains("curtain") || elementName.Contains("витраж") ||
                    elementName.Contains("занавес") || elementName.Contains("остекление"))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // Метод для проверки, является ли семейство витражем
        private bool IsCurtainFamily(FamilyInstance familyInstance)
        {
            try
            {
                // Проверяем категорию
                if (familyInstance.Category == null)
                    return false;

                string categoryName = familyInstance.Category.Name?.ToLower() ?? "";

                // Проверяем по имени категории
                if (categoryName.Contains("curtain") || categoryName.Contains("витраж") ||
                    categoryName.Contains("занавес") || categoryName.Contains("остекление") ||
                    categoryName.Contains("стекло") || categoryName.Contains("glazing"))
                {
                    return true;
                }

                // Проверяем по имени семейства
                Family family = familyInstance.Symbol?.Family;
                if (family != null)
                {
                    string familyName = family.Name?.ToLower() ?? "";
                    if (familyName.Contains("curtain") || familyName.Contains("витраж") ||
                        familyName.Contains("занавес") || familyName.Contains("остекление") ||
                        familyName.Contains("стекло") || familyName.Contains("glazing"))
                    {
                        return true;
                    }
                }

                // Проверяем по имени типа
                string typeName = familyInstance.Name?.ToLower() ?? "";
                if (typeName.Contains("curtain") || typeName.Contains("витраж") ||
                    typeName.Contains("занавес") || typeName.Contains("остекление") ||
                    typeName.Contains("стекло") || typeName.Contains("glazing"))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // Метод для определения помещения для элемента (общий)
        private Room GetRoomForElement(Element element, List<Room> rooms)
        {
            try
            {
                // Для FamilyInstance используем встроенное свойство Room
                if (element is FamilyInstance familyInstance)
                {
                    Room room = familyInstance.Room;
                    if (room != null)
                    {
                        return room;
                    }
                }

                // Для всех элементов используем поиск по точке
                return GetRoomContainingElement(element, rooms);
            }
            catch
            {
                return null;
            }
        }

        // Метод для определения помещения, в котором находится элемент
        private Room GetRoomContainingElement(Element element, List<Room> rooms)
        {
            try
            {
                // Получаем BoundingBox элемента
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null)
                {
                    // Для некоторых элементов можно попробовать получить расположение
                    Location location = element.Location;
                    if (location is LocationPoint locationPoint)
                    {
                        XYZ point = locationPoint.Point;
                        return FindRoomAtPoint(point, rooms);
                    }
                    return null;
                }

                // Центральная точка элемента
                XYZ center = (bbox.Min + bbox.Max) / 2.0;

                // Ищем помещение, содержащее эту точку
                return FindRoomAtPoint(center, rooms);
            }
            catch
            {
                return null;
            }
        }

        // Метод поиска помещения по точке
        private Room FindRoomAtPoint(XYZ point, List<Room> rooms)
        {
            foreach (Room room in rooms)
            {
                try
                {
                    if (room.IsPointInRoom(point))
                    {
                        return room;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }

        // Метод для получения площади элемента (общий)
        private double GetElementArea(Element element)
        {
            try
            {
                // Параметр "Площадь" (вычисленная площадь хоста)
                Parameter areaParam = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null && areaParam.HasValue)
                {
                    double area = areaParam.AsDouble();
                    area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                    if (area > 0 && area < 1000)
                    {
                        return area;
                    }
                }

                // Альтернативные параметры площади
                areaParam = element.LookupParameter("Area");
                if (areaParam == null || !areaParam.HasValue)
                    areaParam = element.LookupParameter("Площадь");
                if (areaParam == null || !areaParam.HasValue)
                    areaParam = element.LookupParameter("Площадь остекления");
                if (areaParam == null || !areaParam.HasValue)
                    areaParam = element.LookupParameter("Проем");

                if (areaParam != null && areaParam.HasValue)
                {
                    double area = areaParam.AsDouble();
                    area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                    if (area > 0 && area < 1000)
                    {
                        return area;
                    }
                }

                // Для стен - альтернативный расчет через длину и высоту
                if (element is Wall wall)
                {
                    return GetWallArea(wall);
                }

                // Для других элементов - расчет через BoundingBox
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    double width = bbox.Max.X - bbox.Min.X;
                    double height = bbox.Max.Z - bbox.Min.Z;
                    double depth = bbox.Max.Y - bbox.Min.Y;

                    // Берем максимальную площадь проекции
                    double areaXY = Math.Abs(width * height);
                    double areaXZ = Math.Abs(width * depth);
                    double areaYZ = Math.Abs(height * depth);

                    double area = Math.Max(areaXY, Math.Max(areaXZ, areaYZ));
                    area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                    if (area > 0 && area < 100)
                    {
                        return area;
                    }
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        // Метод для получения площади стены
        private double GetWallArea(Wall wall)
        {
            try
            {
                // Параметр "Площадь" (вычисленная площадь хоста)
                Parameter areaParam = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null && areaParam.HasValue)
                {
                    double area = areaParam.AsDouble();
                    area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                    if (area > 0 && area < 1000)
                    {
                        return area;
                    }
                }

                // Альтернативный расчет через длину и высоту
                double length = GetWallLength(wall);
                if (length > 0)
                {
                    BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
                    if (bbox != null)
                    {
                        double height = bbox.Max.Z - bbox.Min.Z;
                        if (height > 0)
                        {
                            double area = length * height;
                            area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                            if (area > 0 && area < 1000)
                            {
                                return area;
                            }
                        }
                    }
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        // Метод для получения длины стены
        private double GetWallLength(Wall wall)
        {
            try
            {
                // Получаем кривую стены
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve != null)
                {
                    Curve curve = locationCurve.Curve;
                    if (curve != null)
                    {
                        return curve.Length;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        // Метод для получения площади двери из параметров типа
        private double GetDoorArea(FamilyInstance door)
        {
            try
            {
                // Получаем тип двери
                ElementId doorTypeId = door.GetTypeId();
                if (doorTypeId == null || doorTypeId == ElementId.InvalidElementId)
                    return 0;

                Element doorType = door.Document.GetElement(doorTypeId);
                if (doorType == null) return 0;

                // Сначала попробуем найти параметр с готовой площадью
                double area = GetAreaFromDirectParameter(doorType, new[] { "Площадь", "Area", "Площадь проема", "Проем" });
                if (area > 0) return area;

                // Если нет параметра площади, вычисляем из ширины и высоты
                return GetAreaFromWidthHeight(doorType, "Ширина", "Высота", BuiltInParameter.DOOR_WIDTH, BuiltInParameter.DOOR_HEIGHT);
            }
            catch
            {
                return 0;
            }
        }

        // Метод для получения площади окна из параметров типа
        private double GetWindowArea(FamilyInstance window)
        {
            try
            {
                // Получаем тип окна
                ElementId windowTypeId = window.GetTypeId();
                if (windowTypeId == null || windowTypeId == ElementId.InvalidElementId)
                    return 0;

                Element windowType = window.Document.GetElement(windowTypeId);
                if (windowType == null) return 0;

                // Сначала попробуем найти параметр с готовой площадью
                double area = GetAreaFromDirectParameter(windowType, new[] { "Площадь", "Area", "Площадь проема", "Проем", "Площадь остекления" });
                if (area > 0) return area;

                // Если нет параметра площади, вычисляем из ширины и высоты
                return GetAreaFromWidthHeight(windowType, "Ширина", "Высота", BuiltInParameter.WINDOW_WIDTH, BuiltInParameter.WINDOW_HEIGHT);
            }
            catch
            {
                return 0;
            }
        }

        // Общий метод получения площади из готового параметра
        private double GetAreaFromDirectParameter(Element element, string[] paramNames)
        {
            foreach (string paramName in paramNames)
            {
                Parameter areaParam = element.LookupParameter(paramName);
                if (areaParam != null && areaParam.HasValue)
                {
                    double area = areaParam.AsDouble();
                    // Конвертируем из внутренних единиц (квадратные футы) в квадратные метры
                    area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                    // Проверяем на разумные значения
                    if (area > 0 && area < 100)
                    {
                        return area;
                    }
                }
            }

            return 0;
        }

        // Общий метод вычисления площади из ширины и высоты
        private double GetAreaFromWidthHeight(Element element, string widthParamName, string heightParamName,
                                            BuiltInParameter builtInWidthParam, BuiltInParameter builtInHeightParam)
        {
            // Ищем параметры ширины
            double width = 0;
            Parameter widthParam = element.LookupParameter(widthParamName);
            if (widthParam == null || !widthParam.HasValue)
                widthParam = element.get_Parameter(builtInWidthParam);
            if (widthParam == null || !widthParam.HasValue)
                widthParam = element.LookupParameter("Ширина проема");

            if (widthParam != null && widthParam.HasValue)
            {
                width = widthParam.AsDouble();
                // Конвертируем из внутренних единиц (футы) в метры
                width = UnitUtils.ConvertFromInternalUnits(width, UnitTypeId.Meters);
            }
            else
            {
                return 0; // Не можем вычислить без ширины
            }

            // Ищем параметры высоты
            double height = 0;
            Parameter heightParam = element.LookupParameter(heightParamName);
            if (heightParam == null || !heightParam.HasValue)
            {
                heightParam = element.LookupParameter("Высота проема");
                if (heightParam == null || !heightParam.HasValue)
                    heightParam = element.get_Parameter(builtInHeightParam);
            }

            if (heightParam != null && heightParam.HasValue)
            {
                height = heightParam.AsDouble();
                // Конвертируем из внутренних единиц (футы) в метры
                height = UnitUtils.ConvertFromInternalUnits(height, UnitTypeId.Meters);
            }
            else
            {
                return 0; // Не можем вычислить без высоты
            }

            // Вычисляем площадь
            double area = width * height;

            // Проверяем на разумные значения (например, не больше 100 м²)
            if (area > 0 && area < 100)
            {
                return area;
            }

            return 0;
        }

        // Вспомогательный метод для добавления площади в словарь
        private void AddAreaToRoom(Dictionary<ElementId, double> dictionary, ElementId roomId, double area)
        {
            if (dictionary.ContainsKey(roomId))
            {
                dictionary[roomId] += area;
            }
            else
            {
                dictionary[roomId] = area;
            }
        }
    }
}