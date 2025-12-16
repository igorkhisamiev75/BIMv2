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
                using (Transaction trans = new Transaction(doc, "Расчет площадей проемов"))
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

                    // 4. Собираем все витражи (занавесные стены и оконные системы)
                    List<Wall> curtainWalls = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Walls)
                        .WhereElementIsNotElementType()
                        .Cast<Wall>()
                        .Where(w => w.WallType.Kind == WallKind.Curtain)
                        .ToList();

                    // 5. Собираем также занавесные системы из панелей
                    List<FamilyInstance> curtainPanels = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .ToList();

                    if (doors.Count == 0 && windows.Count == 0 && curtainWalls.Count == 0 && curtainPanels.Count == 0)
                    {
                        TaskDialog.Show("Информация", "В проекте не найдено дверей, окон и витражей.");
                        return Result.Cancelled;
                    }

                    int processedDoors = 0;
                    int processedWindows = 0;
                    int processedCurtainWalls = 0;
                    int processedCurtainPanels = 0;

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
                    foreach (Wall curtainWall in curtainWalls)
                    {
                        // Получаем помещение, в котором находится витраж
                        Room curtainWallRoom = GetRoomContainingElement(curtainWall, rooms);

                        if (curtainWallRoom != null)
                        {
                            // Получаем площадь витража
                            double curtainWallArea = GetCurtainWallArea(curtainWall);

                            if (curtainWallArea > 0)
                            {
                                processedCurtainWalls++;
                                AddAreaToRoom(roomCurtainWallAreas, curtainWallRoom.Id, curtainWallArea);
                            }
                        }
                    }

                    // Обрабатываем занавесные панели
                    foreach (FamilyInstance curtainPanel in curtainPanels)
                    {
                        // Получаем помещение, в котором находится панель
                        Room panelRoom = curtainPanel.Room;

                        if (panelRoom == null)
                        {
                            // Если у панели нет комнаты, ищем через расположение
                            panelRoom = GetRoomContainingElement(curtainPanel, rooms);
                        }

                        if (panelRoom != null)
                        {
                            // Получаем площадь панели
                            double panelArea = GetCurtainPanelArea(curtainPanel);

                            if (panelArea > 0)
                            {
                                processedCurtainPanels++;
                                AddAreaToRoom(roomCurtainWallAreas, panelRoom.Id, panelArea);
                            }
                        }
                    }

                    // 6. Записываем результаты в параметр "Комментарии" помещений
                    int updatedRooms = 0;

                    foreach (Room room in rooms)
                    {
                        bool hasDoorArea = roomDoorAreas.ContainsKey(room.Id);
                        bool hasWindowArea = roomWindowAreas.ContainsKey(room.Id);
                        bool hasCurtainWallArea = roomCurtainWallAreas.ContainsKey(room.Id);

                        if (hasDoorArea || hasWindowArea || hasCurtainWallArea)
                        {
                            double totalDoorArea = hasDoorArea ? roomDoorAreas[room.Id] : 0;
                            double totalWindowArea = hasWindowArea ? roomWindowAreas[room.Id] : 0;
                            double totalCurtainWallArea = hasCurtainWallArea ? roomCurtainWallAreas[room.Id] : 0;

                            // Получаем параметр "Комментарии"
                            Parameter commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);

                            if (commentsParam != null && !commentsParam.IsReadOnly)
                            {
                                // Формируем строку результата
                                string resultText = "";

                                if (hasDoorArea)
                                {
                                    string doorAreaValue = totalDoorArea.ToString("F3");
                                    resultText += $"Площадь дверей: {doorAreaValue} м²";
                                }

                                if (hasWindowArea)
                                {
                                    if (!string.IsNullOrEmpty(resultText))
                                        resultText += ", ";
                                    string windowAreaValue = totalWindowArea.ToString("F3");
                                    resultText += $"Площадь окон: {windowAreaValue} м²";
                                }

                                if (hasCurtainWallArea)
                                {
                                    if (!string.IsNullOrEmpty(resultText))
                                        resultText += ", ";
                                    string curtainAreaValue = totalCurtainWallArea.ToString("F3");
                                    resultText += $"Площадь витражей: {curtainAreaValue} м²";
                                }

                                commentsParam.Set(resultText);
                                updatedRooms++;
                            }
                        }
                        else
                        {
                            // Если у помещения нет проемов, очищаем параметр
                            Parameter commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            if (commentsParam != null && !commentsParam.IsReadOnly && commentsParam.HasValue)
                            {
                                commentsParam.Set(string.Empty);
                            }
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
                    int curtainPanelsWithRoom = curtainPanels.Count(cp => cp.Room != null || GetRoomContainingElement(cp, rooms) != null);

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
                        $"• Панелей с помещением: {curtainPanelsWithRoom}\n\n" +
                        $"Примечание: Двери между двумя помещениями учитываются полной площадью в каждом из помещений.");
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

        // Метод для определения помещения, в котором находится элемент
        private Room GetRoomContainingElement(Element element, List<Room> rooms)
        {
            // Получаем BoundingBox элемента
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox == null) return null;

            // Центральная точка элемента
            XYZ center = (bbox.Min + bbox.Max) / 2.0;

            // Ищем помещение, содержащее эту точку
            foreach (Room room in rooms)
            {
                if (room.IsPointInRoom(center))
                {
                    return room;
                }
            }

            return null;
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

        // Метод для получения площади занавесной стены (витража)
        private double GetCurtainWallArea(Wall curtainWall)
        {
            try
            {
                // Параметр "Площадь"
                Parameter areaParam = curtainWall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null && areaParam.HasValue)
                {
                    double area = areaParam.AsDouble();
                    area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                    if (area > 0 && area < 1000)
                    {
                        return area;
                    }
                }

                // Альтернативный расчет через BoundingBox
                BoundingBoxXYZ bbox = curtainWall.get_BoundingBox(null);
                if (bbox != null)
                {
                    double height = bbox.Max.Z - bbox.Min.Z;
                    double length = GetWallLength(curtainWall);

                    if (length > 0 && height > 0)
                    {
                        double area = length * height;
                        area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                        if (area > 0 && area < 1000)
                        {
                            return area;
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

        // Метод для получения площади занавесной панели
        private double GetCurtainPanelArea(FamilyInstance curtainPanel)
        {
            try
            {
                // Параметр "Площадь"
                Parameter areaParam = curtainPanel.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam == null || !areaParam.HasValue)
                {
                    areaParam = curtainPanel.LookupParameter("Area");
                }
                if (areaParam == null || !areaParam.HasValue)
                {
                    areaParam = curtainPanel.LookupParameter("Площадь");
                }

                if (areaParam != null && areaParam.HasValue)
                {
                    double area = areaParam.AsDouble();
                    area = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);

                    if (area > 0 && area < 100)
                    {
                        return area;
                    }
                }

                // Альтернативный расчет через BoundingBox
                BoundingBoxXYZ bbox = curtainPanel.get_BoundingBox(null);
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