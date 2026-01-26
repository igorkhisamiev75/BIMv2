#region Namespaces
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using OperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;
#endregion

namespace DoorAreaCalculator
{
    [Transaction(TransactionMode.Manual)]
    public class CalculateOpeningAreas : IExternalCommand
    {
        // Имена параметров для помещений
        private const string PARAM_DOOR_AREA = "ПлощадьПроемов_Двери";
        private const string PARAM_WINDOW_AREA = "ПлощадьПроемов_Окна";
        private const string PARAM_CURTAIN_AREA = "ПлощадьПроемов_Витражи";
        private const string PARAM_PARTITION_AREA = "ПлощадьПроемов_РазделителиПомещений";

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

                    // Проверяем наличие необходимых параметров и устанавливаем 0
                    foreach (Room room in rooms)
                    {
                        InitializeRoomParameters(room);
                    }

                    // Словари для хранения суммарных площадей по помещениям
                    Dictionary<ElementId, double> roomDoorAreas = new Dictionary<ElementId, double>();
                    Dictionary<ElementId, double> roomWindowAreas = new Dictionary<ElementId, double>();
                    Dictionary<ElementId, double> roomCurtainWallAreas = new Dictionary<ElementId, double>();
                    Dictionary<ElementId, double> roomPartitionAreas = new Dictionary<ElementId, double>();

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

                    // 4. Собираем все витражи (занавесные стены)
                    List<Wall> curtainWalls = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Walls)
                        .WhereElementIsNotElementType()
                        .OfClass(typeof(Wall))
                        .Cast<Wall>()
                        .Where(w => w.WallType.Kind == WallKind.Curtain)
                        .ToList();

                    // 5. Собираем занавесные панели
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

                                // Также добавляем в разделители помещений
                                AddAreaToRoom(roomPartitionAreas, fromRoom.Id, doorArea);
                                AddAreaToRoom(roomPartitionAreas, toRoom.Id, doorArea);
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

                    // 6. Анализируем границы помещений для поиска разделителей
                    AnalyzeRoomBoundaries(rooms, roomPartitionAreas, doors, doc);

                    // 7. Записываем результаты в параметры помещений и в комментарии
                    int roomsWithDoors = 0;
                    int roomsWithWindows = 0;
                    int roomsWithCurtains = 0;
                    int roomsWithPartitions = 0;

                    foreach (Room room in rooms)
                    {
                        bool hasDoorArea = roomDoorAreas.ContainsKey(room.Id);
                        bool hasWindowArea = roomWindowAreas.ContainsKey(room.Id);
                        bool hasCurtainWallArea = roomCurtainWallAreas.ContainsKey(room.Id);
                        bool hasPartitionArea = roomPartitionAreas.ContainsKey(room.Id);

                        double totalDoorArea = hasDoorArea ? roomDoorAreas[room.Id] : 0;
                        double totalWindowArea = hasWindowArea ? roomWindowAreas[room.Id] : 0;
                        double totalCurtainWallArea = hasCurtainWallArea ? roomCurtainWallAreas[room.Id] : 0;
                        double totalPartitionArea = hasPartitionArea ? roomPartitionAreas[room.Id] : 0;

                        // Записываем площадь дверей
                        if (SetRoomParameter(room, PARAM_DOOR_AREA, totalDoorArea) && hasDoorArea)
                        {
                            roomsWithDoors++;
                        }

                        // Записываем площадь окон
                        if (SetRoomParameter(room, PARAM_WINDOW_AREA, totalWindowArea) && hasWindowArea)
                        {
                            roomsWithWindows++;
                        }

                        // Записываем площадь витражей
                        if (SetRoomParameter(room, PARAM_CURTAIN_AREA, totalCurtainWallArea) && hasCurtainWallArea)
                        {
                            roomsWithCurtains++;
                        }

                        // Записываем площадь разделителей помещений
                        if (SetRoomParameter(room, PARAM_PARTITION_AREA, totalPartitionArea) && hasPartitionArea)
                        {
                            roomsWithPartitions++;
                        }

                        // Также записываем в комментарии для проверки
                        UpdateRoomComments(room, totalDoorArea, totalWindowArea, totalCurtainWallArea, totalPartitionArea);
                    }

                    trans.Commit();

                    // Статистика
                    int doorsInTwoRooms = doors.Count(d => d.FromRoom != null && d.ToRoom != null);
                    int doorsInOneRoom = doors.Count(d => (d.FromRoom != null && d.ToRoom == null) ||
                                                         (d.FromRoom == null && d.ToRoom != null));
                    int doorsWithoutRooms = doors.Count(d => d.FromRoom == null && d.ToRoom == null);

                    int windowsWithRoom = windows.Count(w => w.Room != null);
                    int windowsWithoutRoom = windows.Count(w => w.Room == null);

                    TaskDialog.Show("Результаты расчета",
                        $"ОБРАБОТАНО:\n" +
                        $"• Всего помещений: {rooms.Count}\n" +
                        $"• Помещений с дверями: {roomsWithDoors}\n" +
                        $"• Помещений с окнами: {roomsWithWindows}\n" +
                        $"• Помещений с витражами: {roomsWithCurtains}\n" +
                        $"• Помещений с разделителями: {roomsWithPartitions}\n\n" +
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
                        $"• Панелей: {curtainPanels.Count}\n" +
                        $"• Панелей с площадью: {processedCurtainPanels}\n\n" +
                        $"Площади записаны в параметры помещений и в комментарии для проверки.");
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

        // Метод для анализа границ помещений
        private void AnalyzeRoomBoundaries(List<Room> rooms, Dictionary<ElementId, double> roomPartitionAreas, List<FamilyInstance> doors, Document doc)
        {
            try
            {
                // Создаем словарь для хранения соседних помещений и их общих границ
                Dictionary<string, List<Room>> roomPairs = new Dictionary<string, List<Room>>();
                Dictionary<string, List<Curve>> sharedSegments = new Dictionary<string, List<Curve>>();

                // Собираем границы всех помещений
                foreach (Room room in rooms)
                {
                    // Получаем границы помещения
                    SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                    options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center;

                    IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                    if (boundaries == null) continue;

                    // Анализируем каждый сегмент границы
                    foreach (IList<BoundarySegment> boundaryLoop in boundaries)
                    {
                        foreach (BoundarySegment segment in boundaryLoop)
                        {
                            Element boundaryElement = doc.GetElement(segment.ElementId);
                            if (boundaryElement == null) continue;

                            Curve curve = segment.GetCurve();
                            if (curve == null) continue;

                            // Ищем соседнее помещение для этого сегмента
                            Room adjacentRoom = FindAdjacentRoomForSegment(room, curve, rooms);

                            if (adjacentRoom != null)
                            {
                                // Создаем ключ для пары помещений (упорядоченный)
                                string roomPairKey = GetRoomPairKey(room, adjacentRoom);

                                if (!roomPairs.ContainsKey(roomPairKey))
                                {
                                    roomPairs[roomPairKey] = new List<Room> { room, adjacentRoom };
                                    sharedSegments[roomPairKey] = new List<Curve>();
                                }

                                sharedSegments[roomPairKey].Add(curve);
                            }
                        }
                    }
                }

                // Теперь анализируем каждую пару соседних помещений
                foreach (var roomPair in roomPairs)
                {
                    List<Room> adjacentRooms = roomPair.Value;
                    if (adjacentRooms.Count != 2) continue;

                    Room room1 = adjacentRooms[0];
                    Room room2 = adjacentRooms[1];

                    // Находим двери между этими помещениями
                    double totalDoorArea = FindDoorsBetweenRooms(room1, room2, doors);

                    if (totalDoorArea > 0)
                    {
                        // Распределяем площадь между обоими помещениями
                        AddAreaToRoom(roomPartitionAreas, room1.Id, totalDoorArea);
                        AddAreaToRoom(roomPartitionAreas, room2.Id, totalDoorArea);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка анализа границ: {ex.Message}");
            }
        }

        // Метод для поиска соседнего помещения для сегмента границы
        private Room FindAdjacentRoomForSegment(Room currentRoom, Curve segmentCurve, List<Room> allRooms)
        {
            try
            {
                // Получаем середину сегмента
                XYZ midPoint = segmentCurve.Evaluate(0.5, true);

                // Ищем нормаль к сегменту
                XYZ tangent = segmentCurve.ComputeDerivatives(0.5, true).BasisX;
                if (tangent == null || tangent.IsZeroLength()) return null;

                // Перпендикуляр к касательной в плоскости XY
                XYZ normal = new XYZ(-tangent.Y, tangent.X, 0).Normalize();
                if (normal == null || normal.IsZeroLength()) return null;

                // Проверяем точки с обеих сторон сегмента
                XYZ testPoint1 = midPoint + normal * 0.1;
                XYZ testPoint2 = midPoint - normal * 0.1;

                // Определяем, с какой стороны текущее помещение
                bool roomIsOnSide1 = currentRoom.IsPointInRoom(testPoint1);
                bool roomIsOnSide2 = currentRoom.IsPointInRoom(testPoint2);

                // Ищем соседнее помещение на противоположной стороне
                XYZ searchPoint = roomIsOnSide1 ? testPoint2 : testPoint1;

                foreach (Room otherRoom in allRooms)
                {
                    if (otherRoom.Id == currentRoom.Id) continue;

                    if (otherRoom.IsPointInRoom(searchPoint))
                    {
                        return otherRoom;
                    }
                }
            }
            catch { }

            return null;
        }

        // Метод для создания ключа для пары помещений
        private string GetRoomPairKey(Room room1, Room room2)
        {
            // Упорядочиваем ID помещений для создания уникального ключа
            ElementId id1 = room1.Id;
            ElementId id2 = room2.Id;

            if (id1.IntegerValue < id2.IntegerValue)
            {
                return $"{id1.IntegerValue}_{id2.IntegerValue}";
            }
            else
            {
                return $"{id2.IntegerValue}_{id1.IntegerValue}";
            }
        }

        // Метод для поиска дверей между двумя помещениями
        private double FindDoorsBetweenRooms(Room room1, Room room2, List<FamilyInstance> doors)
        {
            double totalArea = 0;

            try
            {
                foreach (FamilyInstance door in doors)
                {
                    Room fromRoom = door.FromRoom;
                    Room toRoom = door.ToRoom;

                    if (fromRoom != null && toRoom != null)
                    {
                        // Проверяем, соединяет ли дверь эти два помещения
                        if ((fromRoom.Id == room1.Id && toRoom.Id == room2.Id) ||
                            (fromRoom.Id == room2.Id && toRoom.Id == room1.Id))
                        {
                            double doorArea = GetDoorArea(door);
                            if (doorArea > 0)
                            {
                                totalArea += doorArea;
                            }
                        }
                    }
                }
            }
            catch { }

            return totalArea;
        }

        // Метод для обновления комментариев помещения
        private void UpdateRoomComments(Room room, double doorArea, double windowArea, double curtainArea, double partitionArea)
        {
            try
            {
                Parameter commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam != null && !commentsParam.IsReadOnly)
                {
                    string resultText = "";

                    if (doorArea > 0)
                    {
                        string doorAreaValue = doorArea.ToString("F3");
                        resultText += $"Двери: {doorAreaValue} м²";
                    }

                    if (windowArea > 0)
                    {
                        if (!string.IsNullOrEmpty(resultText))
                            resultText += ", ";
                        string windowAreaValue = windowArea.ToString("F3");
                        resultText += $"Окна: {windowAreaValue} м²";
                    }

                    if (curtainArea > 0)
                    {
                        if (!string.IsNullOrEmpty(resultText))
                            resultText += ", ";
                        string curtainAreaValue = curtainArea.ToString("F3");
                        resultText += $"Витражи: {curtainAreaValue} м²";
                    }

                    if (partitionArea > 0)
                    {
                        if (!string.IsNullOrEmpty(resultText))
                            resultText += ", ";
                        string partitionAreaValue = partitionArea.ToString("F3");
                        resultText += $"Разделители: {partitionAreaValue} м²";
                    }

                    if (string.IsNullOrEmpty(resultText))
                    {
                        resultText = "Нет проемов";
                    }

                    commentsParam.Set(resultText);
                }
            }
            catch { }
        }

        // Метод для инициализации параметров помещения (установка 0)
        private void InitializeRoomParameters(Room room)
        {
            SetRoomParameter(room, PARAM_DOOR_AREA, 0);
            SetRoomParameter(room, PARAM_WINDOW_AREA, 0);
            SetRoomParameter(room, PARAM_CURTAIN_AREA, 0);
            SetRoomParameter(room, PARAM_PARTITION_AREA, 0);
        }

        // Метод для записи значения в параметр помещения
        private bool SetRoomParameter(Room room, string paramName, double value)
        {
            try
            {
                // Ищем параметр по имени
                Parameter param = room.LookupParameter(paramName);

                if (param == null)
                {
                    // Пытаемся найти среди всех параметров (включая shared parameters)
                    foreach (Parameter p in room.Parameters)
                    {
                        if (p.Definition.Name == paramName)
                        {
                            param = p;
                            break;
                        }
                    }
                }

                if (param != null && !param.IsReadOnly)
                {
                    // Для параметров площади обычно используется тип Double
                    if (param.StorageType == StorageType.Double)
                    {
                        // Конвертируем в квадратные футы (внутренние единицы Revit)
                        double valueInInternalUnits = UnitUtils.ConvertToInternalUnits(value, UnitTypeId.SquareMeters);
                        param.Set(valueInInternalUnits);
                        return true;
                    }
                    else if (param.StorageType == StorageType.String)
                    {
                        param.Set(value.ToString("F3"));
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка записи параметра {paramName}: {ex.Message}");
                return false;
            }
        }

        // Метод для определения помещения, в котором находится элемент
        private Room GetRoomContainingElement(Element element, List<Room> rooms)
        {
            try
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
            catch
            {
                return null;
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