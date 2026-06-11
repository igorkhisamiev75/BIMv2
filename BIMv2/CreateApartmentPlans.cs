using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateApartmentPlans : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = doc.ActiveView;

            if (!(activeView is ViewPlan))
            {
                TaskDialog.Show("Ошибка", "Активный вид должен быть планом этажа");
                return Result.Failed;
            }

            ViewPlan activeViewPlan = activeView as ViewPlan;

            try
            {
                ICollection<Element> allRooms = GetAllRoomsInView(doc, activeViewPlan);

                if (allRooms.Count == 0)
                {
                    TaskDialog.Show("Информация", "На данном виде не найдены помещения");
                    return Result.Failed;
                }

                var roomsByApartment = GroupRoomsByApartmentNumber(doc, allRooms);

                if (roomsByApartment.Count == 0)
                {
                    TaskDialog.Show("Информация", "Не найдены помещения с заполненным параметром LG_МаркаКвартирыФРС");
                    return Result.Failed;
                }

                using (Transaction t = new Transaction(doc, "Создание планов квартир"))
                {
                    t.Start();

                    int createdCount = 0;

                    foreach (var apartmentGroup in roomsByApartment)
                    {
                        string apartmentNumber = apartmentGroup.Key;
                        List<Room> rooms = apartmentGroup.Value;

                        if (string.IsNullOrEmpty(apartmentNumber))
                            continue;

                        try
                        {
                            ViewPlan newView = CreateViewCopy(doc, activeViewPlan, apartmentNumber);

                            // Получаем объединенный контур квартиры
                            CurveLoop apartmentBoundary = GetMergedApartmentBoundary(rooms);

                            if (apartmentBoundary != null && apartmentBoundary.Any())
                            {
                                ApplyCropToView(newView, apartmentBoundary);
                            }

                            createdCount++;
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Предупреждение",
                                $"Не удалось создать план для квартиры {apartmentNumber}: {ex.Message}");
                        }
                    }

                    t.Commit();

                    TaskDialog.Show("Готово", $"Создано планов квартир: {createdCount} из {roomsByApartment.Count}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось выполнить операцию: {ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private ICollection<Element> GetAllRoomsInView(Document doc, ViewPlan view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .ToElements();
        }

        private string GetParameterValue(Element element, string paramName)
        {
            foreach (Parameter param in element.Parameters)
            {
                if (param.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    if (param.HasValue)
                        return param.AsString();
                }
            }
            return null;
        }

        private Dictionary<string, List<Room>> GroupRoomsByApartmentNumber(Document doc, ICollection<Element> rooms)
        {
            var groups = new Dictionary<string, List<Room>>();

            foreach (Element element in rooms)
            {
                Room room = element as Room;
                if (room == null) continue;

                string apartmentNumber = GetParameterValue(room, "LG_МаркаКвартирыФРС");

                if (string.IsNullOrEmpty(apartmentNumber))
                    apartmentNumber = GetParameterValue(room, "Марка квартиры ФРС");

                if (string.IsNullOrEmpty(apartmentNumber))
                    continue;

                if (!groups.ContainsKey(apartmentNumber))
                    groups[apartmentNumber] = new List<Room>();

                groups[apartmentNumber].Add(room);
            }

            return groups;
        }

        private ViewPlan CreateViewCopy(Document doc, ViewPlan sourceView, string apartmentNumber)
        {
            ElementId levelId = sourceView.GenLevel.Id;

            ViewFamilyType viewFamilyType = null;
            if (sourceView.GetTypeId() != ElementId.InvalidElementId)
            {
                viewFamilyType = doc.GetElement(sourceView.GetTypeId()) as ViewFamilyType;
            }

            if (viewFamilyType == null)
            {
                viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);
            }

            ViewPlan newView = ViewPlan.Create(doc, viewFamilyType.Id, levelId);
            newView.Name = $"{sourceView.Name}_{apartmentNumber}";
            newView.Scale = sourceView.Scale;

            if (sourceView.ViewTemplateId != ElementId.InvalidElementId)
                newView.ViewTemplateId = sourceView.ViewTemplateId;

            return newView;
        }

        /// <summary>
        /// Получает объединенный контур квартиры из всех помещений
        /// </summary>
        private CurveLoop GetMergedApartmentBoundary(List<Room> rooms)
        {
            if (rooms.Count == 0) return null;

            // Собираем все точки границ помещений
            List<XYZ> allPoints = new List<XYZ>();

            foreach (Room room in rooms)
            {
                SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;

                IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                if (boundaries == null) continue;

                foreach (IList<BoundarySegment> boundary in boundaries)
                {
                    foreach (BoundarySegment segment in boundary)
                    {
                        Curve curve = segment.GetCurve();
                        allPoints.Add(curve.GetEndPoint(0));
                        allPoints.Add(curve.GetEndPoint(1));
                    }
                }
            }

            if (allPoints.Count < 3)
                return null;

            // Удаляем дубликаты точек с округлением
            var distinctPoints = allPoints
                .Select(p => new XYZ(Math.Round(p.X, 4), Math.Round(p.Y, 4), 0))
                .GroupBy(p => new { p.X, p.Y })
                .Select(g => g.First())
                .ToList();

            // Находим минимальные и максимальные координаты для создания bounding box
            double minX = distinctPoints.Min(p => p.X);
            double minY = distinctPoints.Min(p => p.Y);
            double maxX = distinctPoints.Max(p => p.X);
            double maxY = distinctPoints.Max(p => p.Y);

            // Создаем прямоугольный контур с отступом
            // Это гарантированно создаст непрерывный CurveLoop
            double offset = 0.3; // Отступ 30 см

            CurveLoop boundaryLoop = new CurveLoop();

            // Создаем 4 точки прямоугольника
            XYZ p1 = new XYZ(minX - offset, minY - offset, 0);
            XYZ p2 = new XYZ(maxX + offset, minY - offset, 0);
            XYZ p3 = new XYZ(maxX + offset, maxY + offset, 0);
            XYZ p4 = new XYZ(minX - offset, maxY + offset, 0);

            // Создаем линии (последовательно, конец одной = начало следующей)
            Line line1 = Line.CreateBound(p1, p2);
            Line line2 = Line.CreateBound(p2, p3);
            Line line3 = Line.CreateBound(p3, p4);
            Line line4 = Line.CreateBound(p4, p1);

            boundaryLoop.Append(line1);
            boundaryLoop.Append(line2);
            boundaryLoop.Append(line3);
            boundaryLoop.Append(line4);

            return boundaryLoop;
        }

        /// <summary>
        /// Применяет обрезку к виду
        /// </summary>
        private void ApplyCropToView(ViewPlan view, CurveLoop cropBoundary)
        {
            if (cropBoundary == null || !cropBoundary.Any())
                return;

            // Активируем обрезку
            view.CropBoxActive = true;
            view.CropBoxVisible = true;

            // Получаем менеджер формы обрезки
            ViewCropRegionShapeManager cropManager = view.GetCropRegionShapeManager();

            // Проверяем поддержку произвольной формы
            if (cropManager.CanHaveShape)
            {
                try
                {
                    // Пробуем установить форму обрезки
                    cropManager.SetCropShape(cropBoundary);
                    view.CropBoxVisible = false;
                }
                catch (Exception ex)
                {
                    // Если не удалось установить сложную форму, используем прямоугольную обрезку
                    TaskDialog.Show("Информация",
                        $"Не удалось установить сложную форму обрезки для квартиры, используется прямоугольная обрезка: {ex.Message}");

                    // Устанавливаем прямоугольную обрезку через CropBox
                    BoundingBoxXYZ bounds = view.CropBox;
                    if (bounds != null)
                    {
                        view.CropBox = bounds;
                        view.CropBoxActive = true;
                        view.CropBoxVisible = false;
                    }
                }
            }
            else
            {
                // Если вид не поддерживает сложную форму, используем прямоугольную обрезку
                TaskDialog.Show("Информация", "Вид не поддерживает непрямоугольную форму обрезки, используется прямоугольная обрезка");

                // Устанавливаем прямоугольную обрезку через габариты
                BoundingBoxXYZ bounds = GetBoundsFromCurveLoop(cropBoundary);
                if (bounds != null)
                {
                    view.CropBox = bounds;
                    view.CropBoxActive = true;
                    view.CropBoxVisible = false;
                }
            }
        }

        /// <summary>
        /// Получает BoundingBox из CurveLoop
        /// </summary>
        private BoundingBoxXYZ GetBoundsFromCurveLoop(CurveLoop loop)
        {
            if (loop == null || !loop.Any())
                return null;

            List<XYZ> points = new List<XYZ>();
            foreach (Curve curve in loop)
            {
                points.Add(curve.GetEndPoint(0));
                points.Add(curve.GetEndPoint(1));
            }

            if (points.Count == 0)
                return null;

            double minX = points.Min(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxX = points.Max(p => p.X);
            double maxY = points.Max(p => p.Y);

            BoundingBoxXYZ bounds = new BoundingBoxXYZ();
            bounds.Min = new XYZ(minX, minY, -100);
            bounds.Max = new XYZ(maxX, maxY, 100);

            return bounds;
        }
    }
}