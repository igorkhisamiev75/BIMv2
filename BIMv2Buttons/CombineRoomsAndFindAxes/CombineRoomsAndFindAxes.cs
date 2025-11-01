using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CombineRoomsAndFindAxes : IExternalCommand
    {
        private const double OFFSET_DISTANCE = 0.90; // 300 мм в метрах

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

                    // Получаем все оси в проекте и фильтруем исключенные
                    List<Grid> allGrids = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Grids)
                        .WhereElementIsNotElementType()
                        .Cast<Grid>()
                        .Where(g => !IsExcludedGrid(g, doc))
                        .ToList();

                    // Разделяем оси на вертикальные и горизонтальные
                    var (verticalGrids, horizontalGrids) = ClassifyGrids(allGrids);

                    int processedCount = 0;

                    foreach (var apartmentGroup in roomsByApartment)
                    {
                        string apartmentNumber = apartmentGroup.Key;
                        var rooms = apartmentGroup.ToList();

                        // Для каждой квартиры собираем все найденные оси из всех помещений
                        List<Grid> allVerticalAxesForApartment = new List<Grid>();
                        List<Grid> allHorizontalAxesForApartment = new List<Grid>();

                        foreach (Room room in rooms)
                        {
                            // Получаем крайние точки для текущего помещения с отступом
                            List<XYZ> roomExtremePoints = GetMinMaxPointsWithOffset(new List<Room> { room });

                            if (roomExtremePoints.Count < 2) continue;

                            // Находим ближайшие оси для этого помещения
                            var (roomVerticalAxes, roomHorizontalAxes) = FindClosestAxesForTwoPoints(
                                roomExtremePoints, verticalGrids, horizontalGrids);

                            // Добавляем найденные оси в общую коллекцию для квартиры
                            allVerticalAxesForApartment.AddRange(roomVerticalAxes);
                            allHorizontalAxesForApartment.AddRange(roomHorizontalAxes);
                        }

                        // Убираем дубликаты осей
                        allVerticalAxesForApartment = allVerticalAxesForApartment.Distinct().ToList();
                        allHorizontalAxesForApartment = allHorizontalAxesForApartment.Distinct().ToList();

                        // Находим крайние оси для всей квартиры
                        var extremeVerticalAxes = GetExtremeAxes(allVerticalAxesForApartment, true); // true - вертикальные
                        var extremeHorizontalAxes = GetExtremeAxes(allHorizontalAxesForApartment, false); // false - горизонтальные

                        // Форматируем результат
                        string axesInfo = FormatAxesInfo(extremeVerticalAxes, extremeHorizontalAxes);

                        // Записываем результат в параметр ADSK_Примечание для каждого помещения квартиры
                        foreach (Room room in rooms)
                        {
                            SetParameterValue(room, "ADSK_Примечание", axesInfo);
                        }

                        processedCount++;
                    }

                    trans.Commit();
                    TaskDialog.Show("Успех", $"Обработка завершена!!! Обработано квартир: {processedCount}");
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

        private List<Grid> GetExtremeAxes(List<Grid> axes, bool isVertical)
        {
            if (axes.Count == 0) return new List<Grid>();
            if (axes.Count == 1) return axes;

            // Для вертикальных осей ищем минимальную и максимальную по X координате
            if (isVertical)
            {
                Grid minAxis = axes[0];
                Grid maxAxis = axes[0];
                double minX = GetGridBasePoint(axes[0]).X;
                double maxX = minX;

                foreach (Grid axis in axes)
                {
                    double currentX = GetGridBasePoint(axis).X;
                    if (currentX < minX)
                    {
                        minX = currentX;
                        minAxis = axis;
                    }
                    if (currentX > maxX)
                    {
                        maxX = currentX;
                        maxAxis = axis;
                    }
                }

                return new List<Grid> { minAxis, maxAxis };
            }
            // Для горизонтальных осей ищем минимальную и максимальную по Y координате
            else
            {
                Grid minAxis = axes[0];
                Grid maxAxis = axes[0];
                double minY = GetGridBasePoint(axes[0]).Y;
                double maxY = minY;

                foreach (Grid axis in axes)
                {
                    double currentY = GetGridBasePoint(axis).Y;
                    if (currentY < minY)
                    {
                        minY = currentY;
                        minAxis = axis;
                    }
                    if (currentY > maxY)
                    {
                        maxY = currentY;
                        maxAxis = axis;
                    }
                }

                return new List<Grid> { minAxis, maxAxis };
            }
        }

        private List<XYZ> GetMinMaxPointsWithOffset(List<Room> rooms)
        {
            List<XYZ> allPoints = new List<XYZ>();

            foreach (Room room in rooms)
            {
                try
                {
                    // Получаем границы помещения
                    SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
                    {
                        SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center
                    };

                    IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                    if (boundaries == null) continue;

                    foreach (IList<BoundarySegment> boundary in boundaries)
                    {
                        foreach (BoundarySegment segment in boundary)
                        {
                            Curve curve = segment.GetCurve();
                            if (curve != null && curve.Length > 0.001)
                            {
                                allPoints.Add(curve.GetEndPoint(0));
                                allPoints.Add(curve.GetEndPoint(1));
                            }
                        }
                    }
                }
                catch
                {
                    // Пропускаем помещения с ошибками границ
                    continue;
                }
            }

            if (allPoints.Count == 0) return allPoints;

            // Находим минимальные и максимальные точки
            double minX = allPoints[0].X, maxX = allPoints[0].X;
            double minY = allPoints[0].Y, maxY = allPoints[0].Y;

            foreach (XYZ point in allPoints)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            // Применяем отступ 300 мм
            minX -= OFFSET_DISTANCE;
            maxX += OFFSET_DISTANCE;
            minY -= OFFSET_DISTANCE;
            maxY += OFFSET_DISTANCE;

            // Возвращаем только 2 точки: минимум и максимум по XY
            return new List<XYZ>
            {
                new XYZ(minX, minY, 0), // Минимум XY (левый нижний)
                new XYZ(maxX, maxY, 0)  // Максимум XY (правый верхний)
            };
        }

        private (List<Grid> vertical, List<Grid> horizontal) FindClosestAxesForTwoPoints(
            List<XYZ> extremePoints, List<Grid> verticalGrids, List<Grid> horizontalGrids)
        {
            if (extremePoints.Count < 2)
                return (new List<Grid>(), new List<Grid>());

            XYZ minPoint = extremePoints[0]; // Минимум XY
            XYZ maxPoint = extremePoints[1]; // Максимум XY

            HashSet<Grid> closestVertical = new HashSet<Grid>();
            HashSet<Grid> closestHorizontal = new HashSet<Grid>();

            // Для точки минимума XY ищем ближайшие оси
            Grid minVerticalGrid = FindClosestGridToPoint(minPoint, verticalGrids);
            Grid minHorizontalGrid = FindClosestGridToPoint(minPoint, horizontalGrids);

            // Для точки максимума XY ищем ближайшие оси
            Grid maxVerticalGrid = FindClosestGridToPoint(maxPoint, verticalGrids);
            Grid maxHorizontalGrid = FindClosestGridToPoint(maxPoint, horizontalGrids);

            // Добавляем найденные оси
            if (minVerticalGrid != null) closestVertical.Add(minVerticalGrid);
            if (maxVerticalGrid != null) closestVertical.Add(maxVerticalGrid);
            if (minHorizontalGrid != null) closestHorizontal.Add(minHorizontalGrid);
            if (maxHorizontalGrid != null) closestHorizontal.Add(maxHorizontalGrid);

            return (closestVertical.ToList(), closestHorizontal.ToList());
        }

        private bool IsExcludedGrid(Grid grid, Document doc)
        {
            // Получаем имя типа оси
            string gridTypeName = GetGridTypeName(grid, doc);

            // Проверяем точное соответствие исключаемому названию типа
            string excludedTypeName = "LG_Од. Круг двойной - с чертой";

            if (!string.IsNullOrEmpty(gridTypeName) && gridTypeName.Equals(excludedTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private string GetGridTypeName(Grid grid, Document doc)
        {
            try
            {
                // Получаем ID типа оси
                ElementId typeId = grid.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    // Получаем элемент типа оси
                    Element gridType = doc.GetElement(typeId);
                    if (gridType != null)
                    {
                        // Получаем параметр Name типа оси
                        Parameter nameParam = gridType.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM);
                        if (nameParam != null && nameParam.HasValue)
                        {
                            return nameParam.AsString();
                        }
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем null
            }

            return null;
        }

        private (List<Grid> vertical, List<Grid> horizontal) ClassifyGrids(List<Grid> allGrids)
        {
            List<Grid> verticalGrids = new List<Grid>();
            List<Grid> horizontalGrids = new List<Grid>();

            foreach (Grid grid in allGrids)
            {
                Curve curve = grid.Curve;
                if (curve == null) continue;

                XYZ direction = GetCurveDirection(curve);

                // Классифицируем оси по направлению
                if (IsVerticalGrid(direction))
                {
                    verticalGrids.Add(grid);
                }
                else if (IsHorizontalGrid(direction))
                {
                    horizontalGrids.Add(grid);
                }
            }

            return (verticalGrids, horizontalGrids);
        }

        private XYZ GetCurveDirection(Curve curve)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            return (end - start).Normalize();
        }

        private bool IsVerticalGrid(XYZ direction)
        {
            // Вертикальные оси: преимущественно по Y
            return Math.Abs(direction.Y) > Math.Abs(direction.X);
        }

        private bool IsHorizontalGrid(XYZ direction)
        {
            // Горизонтальные оси: преимущественно по X
            return Math.Abs(direction.X) > Math.Abs(direction.Y);
        }

        private Grid FindClosestGridToPoint(XYZ point, List<Grid> grids)
        {
            if (grids.Count == 0) return null;

            Grid closestGrid = null;
            double minDistance = double.MaxValue;

            foreach (Grid grid in grids)
            {
                Curve curve = grid.Curve;
                if (curve == null) continue;

                double distance = GetDistanceFromPointToCurve(point, curve);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestGrid = grid;
                }
            }

            return closestGrid;
        }

        private double GetDistanceFromPointToCurve(XYZ point, Curve curve)
        {
            try
            {
                IntersectionResult result = curve.Project(point);
                if (result != null)
                {
                    return point.DistanceTo(result.XYZPoint);
                }
            }
            catch
            {
                // В случае ошибки возвращаем большое расстояние
            }

            return double.MaxValue;
        }

        private XYZ GetGridBasePoint(Grid grid)
        {
            // Возвращаем начальную точку оси для сортировки
            Curve curve = grid.Curve;
            return curve?.GetEndPoint(0) ?? XYZ.Zero;
        }

        private string FormatAxesInfo(List<Grid> verticalAxes, List<Grid> horizontalAxes)
        {
            if (verticalAxes.Count == 0 && horizontalAxes.Count == 0)
                return "Оси не найдены";

            // Сортируем оси для правильного отображения
            var sortedVertical = verticalAxes.OrderBy(g => GetGridBasePoint(g).X).ToList();
            var sortedHorizontal = horizontalAxes.OrderBy(g => GetGridBasePoint(g).Y).ToList();

            List<string> verticalNames = sortedVertical.Select(g => GetGridDisplayName(g)).ToList();
            List<string> horizontalNames = sortedHorizontal.Select(g => GetGridDisplayName(g)).ToList();

            string result = "";

            if (verticalNames.Count > 0)
            {
                result += $"{string.Join("/", verticalNames)}";
            }

            if (horizontalNames.Count > 0)
            {
                if (!string.IsNullOrEmpty(result))
                    result += ";";
                result += $"{string.Join("/", horizontalNames)}";
            }

            return result;
        }

        private string GetGridDisplayName(Grid grid)
        {
            // Получаем значение параметра LG_BS_Ось индекс
            string axisIndex = GetGridParameterValue(grid, "LG_BS_Ось индекс");

            // Получаем значение параметра LG_BS_Имя оси
            string axisName = GetGridParameterValue(grid, "LG_BS_Имя оси");

            // Формируем строку в формате "индекс-имя"
            if (!string.IsNullOrEmpty(axisIndex) && !string.IsNullOrEmpty(axisName))
            {
                return $"{axisIndex}-{axisName}";
            }
            else if (!string.IsNullOrEmpty(axisIndex))
            {
                return axisIndex;
            }
            else if (!string.IsNullOrEmpty(axisName))
            {
                return axisName;
            }
            else
            {
                // Если оба параметра пустые, используем стандартное имя
                return GetGridName(grid);
            }
        }

        private string GetGridParameterValue(Grid grid, string parameterName)
        {
            Parameter param = grid.LookupParameter(parameterName);
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