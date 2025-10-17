using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using Grid = Autodesk.Revit.DB.Grid;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CombineRoomsAndFindAxes : IExternalCommand
    {
        private const double ACCURACY = 0.1; // 100 мм в метрах
        private const double MAX_DISTANCE = 2.0; // 2 метра максимальное расстояние для поиска осей

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;

            using (Transaction trans = new Transaction(doc, "Объединение помещений и поиск осей"))
            {
                trans.Start();

                try
                {
                    // Получаем все помещения в проекте
                    List<Room> allRooms = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .Where(r => r.Area > 0)
                        .ToList();

                    // Группируем помещения по номеру квартиры
                    var roomsByApartment = allRooms
                        .Where(r => !string.IsNullOrEmpty(GetParameterValue(r, "ADSK_Номер квартиры")))
                        .GroupBy(r => GetParameterValue(r, "ADSK_Номер квартиры"))
                        .Where(g => !string.IsNullOrEmpty(g.Key));

                    // Получаем все оси в проекте
                    List<Grid> allGrids = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Grids)
                        .WhereElementIsNotElementType()
                        .Cast<Grid>()
                        .ToList();

                    // Разделяем оси на вертикальные и горизонтальные
                    var (verticalGrids, horizontalGrids) = ClassifyGrids(allGrids);

                    int processedCount = 0;

                    foreach (var apartmentGroup in roomsByApartment)
                    {
                        string apartmentNumber = apartmentGroup.Key;
                        var rooms = apartmentGroup.ToList();

                        // Получаем solids для всех помещений квартиры
                        List<Solid> roomSolids = GetRoomSolids(rooms);

                        if (roomSolids.Count == 0) continue;

                        // Находим ограничивающие оси по логике Python скрипта
                        var (verticalAxes, horizontalAxes) = FindBoundingAxesAdvanced(
                            roomSolids, verticalGrids, horizontalGrids);

                        // Форматируем результат как в Python скрипте
                        string axesInfo = FormatAxesInfo(verticalAxes, horizontalAxes);

                        // Записываем результат в параметр ADSK_Примечание
                        foreach (Room room in rooms)
                        {
                            SetParameterValue(room, "ADSK_Примечание", axesInfo);
                        }

                        processedCount++;
                    }

                    trans.Commit();
                    TaskDialog.Show("Успех", $"Обработка завершена! Обработано квартир: {processedCount}");
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}\n\n{ex.StackTrace}");
                    return Result.Failed;
                }
            }
        }

        private (List<Grid> vertical, List<Grid> horizontal) ClassifyGrids(List<Grid> allGrids)
        {
            List<Grid> verticalGrids = new List<Grid>();
            List<Grid> horizontalGrids = new List<Grid>();

            foreach (Grid grid in allGrids)
            {
                Curve curve = grid.Curve;
                if (curve == null) continue;

                XYZ direction = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                direction = direction.Normalize();

                // Логика классификации осей из Python скрипта
                if (direction.X != 0 && Math.Abs(direction.Y) < 0.01 && Math.Abs(direction.Z) < 0.01)
                {
                    horizontalGrids.Add(grid);
                }
                else if (direction.Y != 0 && Math.Abs(direction.X) < 0.01 && Math.Abs(direction.Z) < 0.01)
                {
                    verticalGrids.Add(grid);
                }
                else if (Math.Abs(Math.Tan(direction.Y / direction.X)) <= 1 && Math.Abs(direction.Z) < 0.01)
                {
                    horizontalGrids.Add(grid);
                }
                else if ((Math.Abs(direction.Y) < 0.01 || Math.Abs(Math.Tan(direction.Y / direction.X)) > 1) && Math.Abs(direction.Z) < 0.01)
                {
                    verticalGrids.Add(grid);
                }
            }

            // Сортируем оси по расстоянию от начальной точки
            XYZ startPoint = new XYZ(-80, -40, 0); // Точка как в Python скрипте (в метрах)
            verticalGrids.Sort((g1, g2) => SortGrids(g1, g2, startPoint));
            horizontalGrids.Sort((g1, g2) => SortGrids(g1, g2, startPoint));

            return (verticalGrids, horizontalGrids);
        }

        private int SortGrids(Grid grid1, Grid grid2, XYZ startPoint)
        {
            double distance1 = GetDistanceFromPoint(grid1, startPoint);
            double distance2 = GetDistanceFromPoint(grid2, startPoint);
            return distance1.CompareTo(distance2);
        }

        private double GetDistanceFromPoint(Grid grid, XYZ point)
        {
            Curve curve = grid.Curve;
            if (curve == null) return double.MaxValue;

            Line line = curve as Line;
            if (line != null)
            {
                line = line.Clone() as Line;
                line.MakeUnbound();
                return line.Distance(point);
            }

            return curve.GetEndPoint(0).DistanceTo(point);
        }

        private List<Solid> GetRoomSolids(List<Room> rooms)
        {
            Document document = rooms[0].Document;
            List<Solid> solids = new List<Solid>();
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(document, options);

            foreach (Room room in rooms)
            {
                try
                {
                    SpatialElementGeometryResults results = calculator.CalculateSpatialElementGeometry(room);
                    Solid solid = results.GetGeometry();
                    if (solid != null && solid.Volume > 0)
                    {
                        solids.Add(solid);
                    }
                }
                catch
                {
                    // Пропускаем помещения с ошибками
                    continue;
                }
            }

            return solids;
        }

        private (List<Grid> vertical, List<Grid> horizontal) FindBoundingAxesAdvanced(
            List<Solid> roomSolids, List<Grid> verticalGrids, List<Grid> horizontalGrids)
        {
            List<Grid> foundVertical = new List<Grid>();
            List<Grid> foundHorizontal = new List<Grid>();

            SolidCurveIntersectionOptions intersectionOptions = new SolidCurveIntersectionOptions();

            foreach (Solid solid in roomSolids)
            {
                XYZ centroid = solid.ComputeCentroid();

                // Создаем трансформации для смещения кривых (как в Python скрипте)
                Transform[] transformsVertical = new Transform[]
                {
                    Transform.CreateTranslation(new XYZ(0, 0, centroid.Z)),
                    Transform.CreateTranslation(new XYZ(0, ACCURACY, 0)),
                    Transform.CreateTranslation(new XYZ(0, -ACCURACY, 0))
                };

                Transform[] transformsHorizontal = new Transform[]
                {
                    Transform.CreateTranslation(new XYZ(0, 0, centroid.Z)),
                    Transform.CreateTranslation(new XYZ(ACCURACY, 0, 0)),
                    Transform.CreateTranslation(new XYZ(-ACCURACY, 0, 0))
                };

                // Проверяем вертикальные оси
                foreach (Grid grid in verticalGrids)
                {
                    if (foundVertical.Contains(grid)) continue;

                    Curve gridCurve = grid.Curve;
                    if (gridCurve == null) continue;

                    bool intersects = false;
                    foreach (Transform transform in transformsHorizontal)
                    {
                        Curve transformedCurve = gridCurve.CreateTransformed(transform);

                        // Проверяем расстояние до центроида
                        if (transformedCurve.Distance(centroid) > MAX_DISTANCE)
                            continue;

                        // Проверяем пересечение с solid
                        SolidCurveIntersection intersection = solid.IntersectWithCurve(
                            transformedCurve, intersectionOptions);

                        if (intersection != null && intersection.SegmentCount > 0)
                        {
                            intersects = true;
                            break;
                        }
                    }

                    if (intersects)
                    {
                        foundVertical.Add(grid);
                    }
                }

                // Проверяем горизонтальные оси
                foreach (Grid grid in horizontalGrids)
                {
                    if (foundHorizontal.Contains(grid)) continue;

                    Curve gridCurve = grid.Curve;
                    if (gridCurve == null) continue;

                    bool intersects = false;
                    foreach (Transform transform in transformsVertical)
                    {
                        Curve transformedCurve = gridCurve.CreateTransformed(transform);

                        // Проверяем расстояние до центроида
                        if (transformedCurve.Distance(centroid) > MAX_DISTANCE)
                            continue;

                        // Проверяем пересечение с solid
                        SolidCurveIntersection intersection = solid.IntersectWithCurve(
                            transformedCurve, intersectionOptions);

                        if (intersection != null && intersection.SegmentCount > 0)
                        {
                            intersects = true;
                            break;
                        }
                    }

                    if (intersects)
                    {
                        foundHorizontal.Add(grid);
                    }
                }
            }

            // Удаляем дубликаты
            foundVertical = foundVertical.Distinct().ToList();
            foundHorizontal = foundHorizontal.Distinct().ToList();

            // Сортируем результаты
            XYZ startPoint = new XYZ(-80, -40, 0);
            foundVertical.Sort((g1, g2) => SortGrids(g1, g2, startPoint));
            foundHorizontal.Sort((g1, g2) => SortGrids(g1, g2, startPoint));

            return (foundVertical, foundHorizontal);
        }

        private string FormatAxesInfo(List<Grid> verticalAxes, List<Grid> horizontalAxes)
        {
            if (verticalAxes.Count == 0 && horizontalAxes.Count == 0)
                return "Оси не найдены";

            string verticalInfo = "Вертикальные: ";
            string horizontalInfo = "Горизонтальные: ";

            if (verticalAxes.Count > 0)
            {
                verticalInfo += $"{GetGridName(verticalAxes.First())}/{GetGridName(verticalAxes.Last())}";
            }
            else
            {
                verticalInfo += "не найдены";
            }

            if (horizontalAxes.Count > 0)
            {
                horizontalInfo += $"{GetGridName(horizontalAxes.First())}/{GetGridName(horizontalAxes.Last())}";
            }
            else
            {
                horizontalInfo += "не найдены";
            }

            return $"{verticalInfo}; {horizontalInfo}";
        }

        private string GetGridName(Grid grid)
        {
            Parameter nameParam = grid.get_Parameter(BuiltInParameter.DATUM_TEXT);
            return nameParam?.AsString() ?? "Без имени";
        }

        // Вспомогательные методы для работы с параметрами
        private string GetParameterValue(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param != null && param.HasValue)
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        return param.AsString();
                    case StorageType.Integer:
                        return param.AsInteger().ToString();
                    case StorageType.Double:
                        return param.AsValueString();
                    case StorageType.ElementId:
                        return param.AsElementId().ToString();
                    default:
                        return null;
                }
            }
            return null;
        }

        private void SetParameterValue(Element element, string parameterName, string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            Parameter param = element.LookupParameter(parameterName);
            if (param != null && !param.IsReadOnly)
            {
                try
                {
                    param.Set(value);
                }
                catch
                {
                    // Игнорируем ошибки установки значения
                }
            }
        }
    }
}