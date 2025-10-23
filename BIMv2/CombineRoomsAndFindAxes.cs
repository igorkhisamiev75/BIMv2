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
        private const double OFFSET_DISTANCE = 0.3; // 300 мм в метрах

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
                        .Where(g => !IsExcludedGrid(g, doc)) // Фильтруем исключенные оси
                        .ToList();

                    // Разделяем оси на вертикальные и горизонтальные
                    var (verticalGrids, horizontalGrids) = ClassifyGrids(allGrids);

                    int processedCount = 0;

                    foreach (var apartmentGroup in roomsByApartment)
                    {
                        string apartmentNumber = apartmentGroup.Key;
                        var rooms = apartmentGroup.ToList();

                        // Получаем объединенный контур всех помещений квартиры с отступом 300 мм
                        List<XYZ> expandedBoundingPoints = GetExpandedBoundingPoints(rooms);

                        if (expandedBoundingPoints.Count == 0) continue;

                        // Находим ближайшие оси для угловых точек
                        var (closestVerticalAxes, closestHorizontalAxes) = FindClosestAxes(
                            expandedBoundingPoints, verticalGrids, horizontalGrids);

                        // Ограничиваем количество осей до 2 и сортируем
                        var sortedVerticalAxes = SortAndLimitAxes(closestVerticalAxes, true); // true - вертикальные
                        var sortedHorizontalAxes = SortAndLimitAxes(closestHorizontalAxes, false); // false - горизонтальные

                        // Форматируем результат с использованием LG_BS параметров
                        string axesInfo = FormatAxesInfo(sortedVerticalAxes, sortedHorizontalAxes);

                        // Записываем результат в параметр ADSK_Примечание для каждого помещения квартиры
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

        private List<XYZ> GetExpandedBoundingPoints(List<Room> rooms)
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

            // Находим bounding box всех точек
            BoundingBoxXYZ bbox = CalculateBoundingBox(allPoints);

            // Расширяем bounding box на 300 мм
            bbox.Min = new XYZ(bbox.Min.X - OFFSET_DISTANCE, bbox.Min.Y - OFFSET_DISTANCE, bbox.Min.Z);
            bbox.Max = new XYZ(bbox.Max.X + OFFSET_DISTANCE, bbox.Max.Y + OFFSET_DISTANCE, bbox.Max.Z);

            // Возвращаем угловые точки расширенного bounding box
            return GetBoundingBoxCorners(bbox);
        }

        private BoundingBoxXYZ CalculateBoundingBox(List<XYZ> points)
        {
            if (points.Count == 0) return null;

            double minX = points[0].X, minY = points[0].Y, minZ = points[0].Z;
            double maxX = points[0].X, maxY = points[0].Y, maxZ = points[0].Z;

            foreach (XYZ point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                minZ = Math.Min(minZ, point.Z);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
                maxZ = Math.Max(maxZ, point.Z);
            }

            BoundingBoxXYZ bbox = new BoundingBoxXYZ();
            bbox.Min = new XYZ(minX, minY, minZ);
            bbox.Max = new XYZ(maxX, maxY, maxZ);

            return bbox;
        }

        private List<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ bbox)
        {
            // Возвращаем 4 угловые точки в 2D плоскости (Z=0)
            return new List<XYZ>
            {
                new XYZ(bbox.Min.X, bbox.Min.Y, 0), // Левый нижний
                new XYZ(bbox.Max.X, bbox.Min.Y, 0), // Правый нижний
                new XYZ(bbox.Max.X, bbox.Max.Y, 0), // Правый верхний
                new XYZ(bbox.Min.X, bbox.Max.Y, 0)  // Левый верхний
            };
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

        private (List<Grid> vertical, List<Grid> horizontal) FindClosestAxes(
            List<XYZ> boundingPoints, List<Grid> verticalGrids, List<Grid> horizontalGrids)
        {
            HashSet<Grid> closestVertical = new HashSet<Grid>();
            HashSet<Grid> closestHorizontal = new HashSet<Grid>();

            foreach (XYZ point in boundingPoints)
            {
                // Находим ближайшую вертикальную ось для текущей точки
                Grid closestVerticalGrid = FindClosestGridToPoint(point, verticalGrids);
                if (closestVerticalGrid != null)
                {
                    closestVertical.Add(closestVerticalGrid);
                }

                // Находим ближайшую горизонтальную ось для текущей точки
                Grid closestHorizontalGrid = FindClosestGridToPoint(point, horizontalGrids);
                if (closestHorizontalGrid != null)
                {
                    closestHorizontal.Add(closestHorizontalGrid);
                }
            }

            return (closestVertical.ToList(), closestHorizontal.ToList());
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

        private List<Grid> SortAndLimitAxes(List<Grid> axes, bool isVertical)
        {
            if (axes.Count == 0) return axes;

            // Сортируем оси по координатам
            if (isVertical)
            {
                // Вертикальные оси: слева направо по X координате
                axes.Sort((a, b) =>
                {
                    double aX = GetGridBasePoint(a).X;
                    double bX = GetGridBasePoint(b).X;
                    return aX.CompareTo(bX);
                });
            }
            else
            {
                // Горизонтальные оси: снизу вверх по Y координате
                axes.Sort((a, b) =>
                {
                    double aY = GetGridBasePoint(a).Y;
                    double bY = GetGridBasePoint(b).Y;
                    return aY.CompareTo(bY);
                });
            }

            // Ограничиваем количество осей до 2
            return axes.Take(2).ToList();
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

            List<string> verticalNames = verticalAxes.Select(g => GetGridDisplayName(g)).ToList();
            List<string> horizontalNames = horizontalAxes.Select(g => GetGridDisplayName(g)).ToList();

            string result = "";

            if (verticalNames.Count > 0)
            {
                result += $" {string.Join("/", verticalNames)}";
            }

            if (horizontalNames.Count > 0)
            {
                if (!string.IsNullOrEmpty(result))
                    result += ";";
                result += $" {string.Join("/", horizontalNames)}";
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