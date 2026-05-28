using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace ABIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class DimensionCurtainGridOnSection : IExternalCommand
    {
        private Document _doc;
        private ViewSection _view;
        private const double OFFSET_MM = 500; // Отступ в миллиметрах
        private const double MIN_DIMENSION_MM = 10; // Минимальная длина размера в миллиметрах

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            _doc = uidoc.Document;

            if (!(uidoc.ActiveView is ViewSection section))
            {
                TaskDialog.Show("Ошибка", "Команда работает только в разрезе.");
                return Result.Failed;
            }

            _view = section;

            Reference pick;
            try
            {
                pick = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new CurtainWallFilter(),
                    "Выберите витражную стену");
            }
            catch
            {
                return Result.Cancelled;
            }

            if (pick == null)
                return Result.Cancelled;

            Wall wall = _doc.GetElement(pick) as Wall;
            if (wall == null || wall.CurtainGrid == null)
            {
                TaskDialog.Show("Ошибка", "Выбранный элемент не является витражной стеной.");
                return Result.Failed;
            }

            int createdCount = 0;

            using (Transaction t = new Transaction(_doc, "Dimension Curtain Grid"))
            {
                t.Start();

                createdCount = CreateDimensions(wall);

                t.Commit();
            }

            TaskDialog.Show("Результат",
                $"Создано размеров: {createdCount}");

            return Result.Succeeded;
        }

        private int CreateDimensions(Wall wall)
        {
            CurtainGrid grid = wall.CurtainGrid;

            // Получаем U линии (вертикальные)
            ICollection<ElementId> uGridLineIds = grid.GetUGridLineIds();

            if (uGridLineIds.Count < 2)
            {
                TaskDialog.Show("Ошибка", $"Найдено вертикальных линий: {uGridLineIds.Count}. Нужно минимум 2.");
                return 0;
            }

            // Получаем все CurtainGridLine по ID
            List<CurtainGridLine> verticalLines = new List<CurtainGridLine>();
            foreach (ElementId id in uGridLineIds)
            {
                CurtainGridLine gridLine = _doc.GetElement(id) as CurtainGridLine;
                if (gridLine != null)
                {
                    verticalLines.Add(gridLine);
                }
            }

            // Создаем отдельные размеры между каждой парой линий
            return CreateDimensionsBetweenLines(verticalLines, true);
        }

        private int CreateDimensionsBetweenLines(List<CurtainGridLine> lines, bool horizontalDimension)
        {
            if (lines.Count < 2)
            {
                return 0;
            }

            // Сортируем линии по их положению
            var sortedLines = lines
                .OrderBy(l => GetLinePosition(l))
                .ToList();

            // Убираем установку DetailLevel - он будет взят из вида автоматически
            Options opt = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                View = _view
                // DetailLevel убираем, так как он конфликтует с View
            };

            // Собираем данные о каждой линии
            List<LineData> lineDataList = new List<LineData>();

            foreach (var gridLine in sortedLines)
            {
                try
                {
                    GeometryElement geo = gridLine.get_Geometry(opt);
                    if (geo == null) continue;

                    foreach (GeometryObject obj in geo)
                    {
                        if (obj is Line line && line.Reference != null)
                        {
                            XYZ midPoint = line.Evaluate(0.5, true);

                            lineDataList.Add(new LineData
                            {
                                Reference = line.Reference,
                                Position = midPoint,
                                GridLine = gridLine
                            });
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Пропускаем проблемные линии
                }
            }

            if (lineDataList.Count < 2)
            {
                TaskDialog.Show("Ошибка",
                    $"Не удалось получить ссылки на линии. Получено данных: {lineDataList.Count}");
                return 0;
            }

            // Направление размера (вдоль чего мерим)
            XYZ dimDirection = horizontalDimension ? _view.RightDirection : _view.UpDirection;

            // Направление смещения (перпендикулярно размеру)
            XYZ offsetDirection = horizontalDimension ? _view.UpDirection : _view.RightDirection;

            double offset = UnitUtils.ConvertToInternalUnits(OFFSET_MM, UnitTypeId.Millimeters);
            double minDimension = UnitUtils.ConvertToInternalUnits(MIN_DIMENSION_MM, UnitTypeId.Millimeters);

            int createdCount = 0;
            int skippedCount = 0;

            // Создаем размеры между каждой парой соседних линий
            for (int i = 0; i < lineDataList.Count - 1; i++)
            {
                LineData leftLine = lineDataList[i];
                LineData rightLine = lineDataList[i + 1];

                // Вычисляем позиции линий вдоль направления размера
                double leftPos = (leftLine.Position - _view.Origin).DotProduct(dimDirection);
                double rightPos = (rightLine.Position - _view.Origin).DotProduct(dimDirection);

                // Проверяем расстояние между линиями
                double distance = Math.Abs(rightPos - leftPos);
                double distanceMM = distance * 304.8;

                if (distance < minDimension)
                {
                    skippedCount++;
                    continue;
                }

                ReferenceArray refArray = new ReferenceArray();
                refArray.Append(leftLine.Reference);
                refArray.Append(rightLine.Reference);

                // Создаем линию размера
                XYZ startPoint, endPoint;

                if (horizontalDimension)
                {
                    // Горизонтальный размер - линия идет горизонтально
                    // Используем среднюю Z координату между линиями
                    double midZ = (leftLine.Position.Z + rightLine.Position.Z) / 2;

                    startPoint = new XYZ(leftLine.Position.X, leftLine.Position.Y, midZ + offset);
                    endPoint = new XYZ(rightLine.Position.X, rightLine.Position.Y, midZ + offset);
                }
                else
                {
                    // Вертикальный размер - линия идет вертикально
                    // Используем среднюю X координату между линиями
                    double midX = (leftLine.Position.X + rightLine.Position.X) / 2;

                    startPoint = new XYZ(midX + offset, leftLine.Position.Y, leftLine.Position.Z);
                    endPoint = new XYZ(midX + offset, rightLine.Position.Y, rightLine.Position.Z);
                }

                // Проверяем длину линии размера
                double lineLength = startPoint.DistanceTo(endPoint);
                double lineLengthMM = lineLength * 304.8;

                if (lineLength < minDimension)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    Line dimLine = Line.CreateBound(startPoint, endPoint);
                    Dimension dimension = _doc.Create.NewDimension(_view, dimLine, refArray);

                    if (dimension != null)
                    {
                        createdCount++;
                    }
                }
                catch (Exception ex)
                {
                    // Пропускаем ошибки создания размера
                }
            }

            return createdCount;
        }

        private double GetLinePosition(CurtainGridLine gridLine)
        {
            LocationCurve location = gridLine.Location as LocationCurve;
            if (location == null) return 0;

            Curve curve = location.Curve;
            if (curve == null) return 0;

            // Берем среднюю точку линии
            XYZ midPoint = curve.Evaluate(0.5, true);

            // Возвращаем координату X для сортировки слева направо
            return midPoint.X;
        }

        private class LineData
        {
            public Reference Reference { get; set; }
            public XYZ Position { get; set; }
            public CurtainGridLine GridLine { get; set; }
        }

        private class CurtainWallFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Wall w && w.CurtainGrid != null;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}