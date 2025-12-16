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
    public class CalculateDoorAreas : IExternalCommand
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
                using (Transaction trans = new Transaction(doc, "Расчет площадей дверей"))
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

                    // Словарь для хранения суммарных площадей дверей по помещениям
                    Dictionary<ElementId, double> roomDoorAreas = new Dictionary<ElementId, double>();

                    // 2. Собираем все двери
                    List<FamilyInstance> doors = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .ToList();

                    if (doors.Count == 0)
                    {
                        TaskDialog.Show("Информация", "В проекте не найдено дверей.");
                        return Result.Cancelled;
                    }

                    int processedDoors = 0;

                    foreach (FamilyInstance door in doors)
                    {
                        // 3. Определяем в каких помещениях находится дверь
                        Room fromRoom = door.FromRoom;
                        Room toRoom = door.ToRoom;

                        // 4. Получаем площадь проема из параметров типа
                        double doorArea = GetDoorArea(door);

                        if (doorArea > 0)
                        {
                            processedDoors++;

                            // Распределяем площадь между помещениями
                            if (fromRoom != null && toRoom != null)
                            {
                                // Дверь между двумя помещениями - полная площадь для каждого помещения
                                AddDoorAreaToRoom(roomDoorAreas, fromRoom.Id, doorArea);
                                AddDoorAreaToRoom(roomDoorAreas, toRoom.Id, doorArea);
                            }
                            else if (fromRoom != null)
                            {
                                // Дверь только в одном помещении
                                AddDoorAreaToRoom(roomDoorAreas, fromRoom.Id, doorArea);
                            }
                            else if (toRoom != null)
                            {
                                // Дверь только в одном помещении
                                AddDoorAreaToRoom(roomDoorAreas, toRoom.Id, doorArea);
                            }
                        }
                    }

                    // 5. Записываем результаты в параметр "Комментарии" помещений
                    int updatedRooms = 0;

                    foreach (Room room in rooms)
                    {
                        if (roomDoorAreas.ContainsKey(room.Id))
                        {
                            double totalDoorArea = roomDoorAreas[room.Id];

                            // Получаем параметр "Комментарии"
                            Parameter commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);

                            if (commentsParam != null && !commentsParam.IsReadOnly)
                            {
                                // Форматируем значение площади (в квадратных метрах)
                                string areaValue = totalDoorArea.ToString("F3");
                                commentsParam.Set($"Площадь дверей: {areaValue} м²");
                                updatedRooms++;
                            }
                        }
                        else
                        {
                            // Если у помещения нет дверей, очищаем параметр
                            Parameter commentsParam = room.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            if (commentsParam != null && !commentsParam.IsReadOnly && commentsParam.HasValue)
                            {
                                commentsParam.Set(string.Empty);
                            }
                        }
                    }

                    trans.Commit();

                    // Статистика по дверям
                    int doorsInTwoRooms = doors.Count(d => d.FromRoom != null && d.ToRoom != null);
                    int doorsInOneRoom = doors.Count(d => (d.FromRoom != null && d.ToRoom == null) ||
                                                         (d.FromRoom == null && d.ToRoom != null));
                    int doorsWithoutRooms = doors.Count(d => d.FromRoom == null && d.ToRoom == null);

                    TaskDialog.Show("Результаты расчета",
                        $"ОБРАБОТАНО:\n" +
                        $"• Всего помещений: {rooms.Count}\n" +
                        $"• Помещений с дверьми: {updatedRooms}\n" +
                        $"• Помещений без дверей: {rooms.Count - updatedRooms}\n\n" +
                        $"ДВЕРИ:\n" +
                        $"• Всего дверей: {doors.Count}\n" +
                        $"• Дверей с площадью: {processedDoors}\n" +
                        $"• Дверей между 2 помещениями: {doorsInTwoRooms}\n" +
                        $"• Дверей в 1 помещении: {doorsInOneRoom}\n" +
                        $"• Дверей без помещения: {doorsWithoutRooms}\n\n" +
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
                double area = GetAreaFromDirectParameter(doorType);
                if (area > 0) return area;

                // Если нет параметра площади, вычисляем из ширины и высоты
                return GetAreaFromWidthHeight(doorType);
            }
            catch
            {
                return 0;
            }
        }

        // Метод получения площади из готового параметра
        private double GetAreaFromDirectParameter(Element doorType)
        {
            // Ищем готовый параметр с площадью
            Parameter areaParam = doorType.LookupParameter("Площадь");
            if (areaParam == null || !areaParam.HasValue)
                areaParam = doorType.LookupParameter("Area");
            if (areaParam == null || !areaParam.HasValue)
                areaParam = doorType.LookupParameter("Площадь проема");
            if (areaParam == null || !areaParam.HasValue)
                areaParam = doorType.LookupParameter("Проем");

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

            return 0;
        }

        // Метод вычисления площади из ширины и высоты
        private double GetAreaFromWidthHeight(Element doorType)
        {
            // Ищем параметры ширины
            double width = 0;
            Parameter widthParam = doorType.LookupParameter("Ширина");
            if (widthParam == null || !widthParam.HasValue)
                widthParam = doorType.get_Parameter(BuiltInParameter.DOOR_WIDTH);
            if (widthParam == null || !widthParam.HasValue)
                widthParam = doorType.LookupParameter("Ширина проема");

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
            Parameter heightParam = doorType.LookupParameter("Высота");
            if (heightParam == null || !heightParam.HasValue)
            {
                heightParam = doorType.LookupParameter("Высота проема");
                if (heightParam == null || !heightParam.HasValue)
                    heightParam = doorType.get_Parameter(BuiltInParameter.DOOR_HEIGHT);
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
        private void AddDoorAreaToRoom(Dictionary<ElementId, double> dictionary, ElementId roomId, double area)
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