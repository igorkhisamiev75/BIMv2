#region Namespace

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace RevitAddIn2BIMRU.Commands.Parameters
{
    [Transaction(TransactionMode.Manual)]
    public class CreateGridDimensions : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Получаем все оси в проекте
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> gridElements = collector
                    .OfClass(typeof(Grid))
                    .WhereElementIsNotElementType()
                    .ToElements();

                if (gridElements.Count == 0)
                {
                    TaskDialog.Show("Информация", "В проекте нет осей.");
                    return Result.Cancelled;
                }

                // Группируем оси по направлению
                var verticalGrids = new List<Grid>();
                var horizontalGrids = new List<Grid>();

                foreach (Element element in gridElements)
                {
                    Grid grid = element as Grid;
                    if (grid != null && grid.Curve is Line line)
                    {
                        XYZ direction = line.Direction;

                        // Проверяем направление оси (вертикальные - ось X, горизонтальные - ось Y)
                        if (Math.Abs(direction.X) > Math.Abs(direction.Y))
                        {
                            horizontalGrids.Add(grid);
                        }
                        else
                        {
                            verticalGrids.Add(grid);
                        }
                    }
                }

                // Сортируем оси по координатам
                verticalGrids = SortGridsByCoordinate(verticalGrids, true);
                horizontalGrids = SortGridsByCoordinate(horizontalGrids, false);

                using (Transaction trans = new Transaction(doc, "Создание размеров между осями"))
                {
                    trans.Start();

                    // Создаем размеры между вертикальными осями (слева)
                    if (verticalGrids.Count > 1)
                    {
                        CreateDimensionsForGrids(doc, verticalGrids, true, true);
                    }

                    // Создаем размеры между горизонтальными осями (снизу)
                    if (horizontalGrids.Count > 1)
                    {
                        CreateDimensionsForGrids(doc, horizontalGrids, false, false);
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Успех", $"Созданы размеры между осями:\nВертикальных осей: {verticalGrids.Count}\nГоризонтальных осей: {horizontalGrids.Count}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Не удалось создать размеры: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Сортирует оси по координатам
        /// </summary>
        private List<Grid> SortGridsByCoordinate(List<Grid> grids, bool isVertical)
        {
            return grids.OrderBy(g =>
            {
                Line line = g.Curve as Line;
                if (line != null)
                {
                    XYZ origin = line.Origin;
                    // Для вертикальных осей сортируем по X, для горизонтальных - по Y
                    return isVertical ? origin.X : origin.Y;
                }
                return 0;
            }).ToList();
        }

        /// <summary>
        /// Создает размеры для осей в указанном направлении
        /// </summary>
        /// <param name="doc">Документ</param>
        /// <param name="grids">Список осей</param>
        /// <param name="isVertical">true - вертикальные оси, false - горизонтальные</param>
        /// <param name="placeLeft">true - размещать слева, false - размещать снизу</param>
        private void CreateDimensionsForGrids(Document doc, List<Grid> grids, bool isVertical, bool placeLeft)
        {
            if (grids.Count < 2) return;

            // Выбираем активный вид (план)
            View activeView = doc.ActiveView;
            if (!(activeView is ViewPlan))
            {
                TaskDialog.Show("Ошибка", "Команда должна выполняться в плане.");
                return;
            }

            // Находим минимальную и максимальную координаты для определения положения размеров
            double minCoord = double.MaxValue;
            double maxCoord = double.MinValue;

            foreach (Grid grid in grids)
            {
                if (grid.Curve is Line line)
                {
                    XYZ origin = line.Origin;
                    double coord = isVertical ? origin.X : origin.Y;

                    if (coord < minCoord) minCoord = coord;
                    if (coord > maxCoord) maxCoord = coord;
                }
            }

            // Определяем положение для размеров
            // Для вертикальных осей (placeLeft = true) - слева (меньшая X координата)
            // Для горизонтальных осей (placeLeft = false) - снизу (меньшая Y координата)
            double dimensionPosition = placeLeft ? (minCoord - 3.0) : (minCoord - 3.0);

            // Создаем размеры для пар осей
            for (int i = 0; i < grids.Count - 1; i++)
            {
                CreateDimensionBetweenTwoGrids(doc, activeView, grids[i], grids[i + 1],
                    isVertical, dimensionPosition);
            }

            // Создаем общий размер между крайними осями
            if (grids.Count >= 2)
            {
                CreateDimensionBetweenTwoGrids(doc, activeView, grids.First(), grids.Last(),
                    isVertical, dimensionPosition - 1.5); // Смещаем общий размер дальше
            }
        }

        /// <summary>
        /// Создает размер между двумя осями
        /// </summary>
        private void CreateDimensionBetweenTwoGrids(Document doc, View view, Grid grid1, Grid grid2,
            bool isVertical, double dimensionPosition)
        {
            try
            {
                if (!(grid1.Curve is Line line1) || !(grid2.Curve is Line line2))
                    return;

                // Получаем точки пересечения линии размеров с осями
                XYZ p1 = GetIntersectionPoint(line1, isVertical, dimensionPosition);
                XYZ p2 = GetIntersectionPoint(line2, isVertical, dimensionPosition);

                if (p1 == null || p2 == null)
                    return;

                // Создаем линию для размера
                Line dimensionLine = Line.CreateBound(p1, p2);

                // Создаем ссылки на оси
                ReferenceArray refArray = new ReferenceArray();
                refArray.Append(new Reference(grid1));
                refArray.Append(new Reference(grid2));

                // Создаем размер
                Dimension newDimension = doc.Create.NewDimension(view, dimensionLine, refArray);

                // Проверяем, что размер создан успешно
                if (newDimension == null)
                {
                    throw new Exception("Не удалось создать размер между осями.");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Предупреждение", $"Не удалось создать размер между осями {grid1.Name} и {grid2.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает точку пересечения линии размера с осью
        /// </summary>
        private XYZ GetIntersectionPoint(Line gridLine, bool isVertical, double dimensionPosition)
        {
            XYZ origin = gridLine.Origin;
            XYZ direction = gridLine.Direction;

            // Для вертикальных осей (ось направлена вверх-вниз)
            if (isVertical)
            {
                // Линия размера горизонтальная (Y = dimensionPosition)
                // Уравнение: origin.Y + t * direction.Y = dimensionPosition
                if (Math.Abs(direction.Y) > 0.001)
                {
                    double t = (dimensionPosition - origin.Y) / direction.Y;
                    return origin + direction * t;
                }
                else
                {
                    // Если ось строго вертикальна, просто используем origin
                    return new XYZ(origin.X, dimensionPosition, origin.Z);
                }
            }
            // Для горизонтальных осей (ось направлена влево-вправо)
            else
            {
                // Линия размера вертикальная (X = dimensionPosition)
                // Уравнение: origin.X + t * direction.X = dimensionPosition
                if (Math.Abs(direction.X) > 0.001)
                {
                    double t = (dimensionPosition - origin.X) / direction.X;
                    return origin + direction * t;
                }
                else
                {
                    // Если ось строго горизонтальна, просто используем origin
                    return new XYZ(dimensionPosition, origin.Y, origin.Z);
                }
            }
        }

        /// <summary>
        /// Альтернативный метод для создания размеров (более простой)
        /// </summary>
        private void CreateSimpleDimensions(Document doc, List<Grid> grids, bool isVertical)
        {
            View view = doc.ActiveView;
            if (!(view is ViewPlan)) return;

            // Получаем границы видового экрана
            BoundingBoxXYZ bbox = view.get_BoundingBox(view);
            if (bbox == null) return;

            // Определяем положение для размеров
            // Для вертикальных осей - слева (минимальная X)
            // Для горизонтальных осей - снизу (минимальная Y)
            double dimensionCoord = isVertical ?
                bbox.Min.X - 3.0 :  // Слева от вида
                bbox.Min.Y - 3.0;   // Снизу от вида

            // Получаем уровень плана
            Level level = (view as ViewPlan).GenLevel;
            if (level == null) return;

            // Создаем размеры между всеми осями
            for (int i = 0; i < grids.Count - 1; i++)
            {
                try
                {
                    Grid grid1 = grids[i];
                    Grid grid2 = grids[i + 1];

                    // Создаем линию размера
                    XYZ startPoint, endPoint;

                    if (isVertical)
                    {
                        // Вертикальные оси - горизонтальная линия размера
                        startPoint = new XYZ(dimensionCoord, 0, level.Elevation);
                        endPoint = new XYZ(dimensionCoord, 0, level.Elevation);
                    }
                    else
                    {
                        // Горизонтальные оси - вертикальная линия размера
                        startPoint = new XYZ(0, dimensionCoord, level.Elevation);
                        endPoint = new XYZ(0, dimensionCoord, level.Elevation);
                    }

                    // Настраиваем точки в зависимости от положения осей
                    if (grid1.Curve is Line line1 && grid2.Curve is Line line2)
                    {
                        double coord1 = isVertical ? line1.Origin.Y : line1.Origin.X;
                        double coord2 = isVertical ? line2.Origin.Y : line2.Origin.X;

                        if (isVertical)
                        {
                            startPoint = new XYZ(dimensionCoord, coord1, level.Elevation);
                            endPoint = new XYZ(dimensionCoord, coord2, level.Elevation);
                        }
                        else
                        {
                            startPoint = new XYZ(coord1, dimensionCoord, level.Elevation);
                            endPoint = new XYZ(coord2, dimensionCoord, level.Elevation);
                        }
                    }

                    Line dimensionLine = Line.CreateBound(startPoint, endPoint);

                    // Создаем ссылки
                    ReferenceArray refArray = new ReferenceArray();
                    refArray.Append(new Reference(grid1));
                    refArray.Append(new Reference(grid2));

                    // Создаем размер
                    doc.Create.NewDimension(view, dimensionLine, refArray);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Предупреждение", $"Ошибка при создании размера: {ex.Message}");
                }
            }
        }
    }
}