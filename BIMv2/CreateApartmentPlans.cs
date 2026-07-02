using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using View = Autodesk.Revit.DB.View;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateApartmentPlans : IExternalCommand
    {
        private const double Eps = 0.01; // допуск 1 см

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            Autodesk.Revit.DB.View activeView = doc.ActiveView;

            if (!(activeView is ViewPlan))
            {
                TaskDialog.Show("Ошибка", "Активный вид должен быть планом этажа");
                return Result.Failed;
            }

            ViewPlan activeViewPlan = activeView as ViewPlan;

            // Выбор шаблона
            ElementId templateId = SelectTemplate(doc);
            if (templateId == ElementId.InvalidElementId && !ShowContinueWithoutTemplate())
                return Result.Cancelled;

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
                            ViewPlan newView = CreateViewCopy(doc, activeViewPlan, apartmentNumber, templateId);

                            CurveLoop apartmentBoundary = GetMergedApartmentBoundary(rooms);

                            if (apartmentBoundary != null && apartmentBoundary.Any())
                            {
                                ApplyCropToView(newView, apartmentBoundary);
                                HideInternalGrids(doc, newView, apartmentBoundary);
                                // НОВЫЙ ВЫЗОВ: добавить текстовую аннотацию
                                AddApartmentLabel(doc, newView, rooms, apartmentBoundary);
                            }
                            else
                            {
                                TaskDialog.Show("Предупреждение", $"Для квартиры {apartmentNumber} не удалось построить контур");
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

        // ======================================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ======================================================================

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

        // ======================================================================
        // ДИАЛОГ ВЫБОРА ШАБЛОНА И СОЗДАНИЕ ВИДА
        // ======================================================================

        private ElementId SelectTemplate(Document doc)
        {
            List<Autodesk.Revit.DB.View> templates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<Autodesk.Revit.DB.View>()
                .Where(v => v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                .ToList();

            if (templates.Count == 0)
            {
                TaskDialog.Show("Информация", "В проекте нет шаблонов для планов этажей. Планы будут созданы без шаблона.");
                return ElementId.InvalidElementId;
            }

            using (System.Windows.Forms.Form dialog = new System.Windows.Forms.Form())
            {
                dialog.Text = "Выбор шаблона для новых видов";
                dialog.Width = 900;
                dialog.Height = 550;
                dialog.StartPosition = FormStartPosition.CenterScreen;

                System.Windows.Forms.ComboBox comboBox = new System.Windows.Forms.ComboBox();
                comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                comboBox.Width = 300;
                comboBox.Left = 20;
                comboBox.Top = 20;

                comboBox.Items.Add("(Без шаблона)");
                foreach (var vt in templates)
                {
                    comboBox.Items.Add(vt.Name);
                }
                comboBox.SelectedIndex = 0;

                Button btnOk = new Button();
                btnOk.Text = "OK";
                btnOk.Width = 80;
                btnOk.Left = 80;
                btnOk.Top = 60;
                btnOk.DialogResult = DialogResult.OK;

                Button btnCancel = new Button();
                btnCancel.Text = "Отмена";
                btnCancel.Width = 80;
                btnCancel.Left = 180;
                btnCancel.Top = 60;
                btnCancel.DialogResult = DialogResult.Cancel;

                dialog.Controls.Add(comboBox);
                dialog.Controls.Add(btnOk);
                dialog.Controls.Add(btnCancel);
                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancel;

                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.Cancel)
                    return ElementId.InvalidElementId;

                int idx = comboBox.SelectedIndex;
                if (idx == 0)
                    return ElementId.InvalidElementId;
                else
                    return templates[idx - 1].Id;
            }
        }

        private bool ShowContinueWithoutTemplate()
        {
            TaskDialog taskDialog = new TaskDialog("Подтверждение");
            taskDialog.MainInstruction = "Вы не выбрали шаблон. Продолжить без шаблона?";
            taskDialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            return taskDialog.Show() == TaskDialogResult.Yes;
        }

        private ViewPlan CreateViewCopy(Document doc, ViewPlan sourceView, string apartmentNumber, ElementId templateId)
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

            string viewName = $"{sourceView.Name}_кв_{apartmentNumber}";
            if (templateId != ElementId.InvalidElementId)
            {
                Autodesk.Revit.DB.View template = doc.GetElement(templateId) as View;
                if (template != null)
                    viewName += $"_{template.Name}";
            }

            newView.Name = viewName;
            newView.Scale = sourceView.Scale;

            if (templateId != ElementId.InvalidElementId)
                newView.ViewTemplateId = templateId;
            else
                newView.ViewTemplateId = ElementId.InvalidElementId;

            return newView;
        }

        // ======================================================================
        // ОБЪЕДИНЕНИЕ ПОМЕЩЕНИЙ ЧЕРЕЗ БУЛЕВУ ОПЕРАЦИЮ UNION
        // ======================================================================

        private CurveLoop GetMergedApartmentBoundary(List<Room> rooms)
        {
            if (rooms == null || rooms.Count == 0)
                return null;

            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;

            List<Solid> solids = new List<Solid>();
            double offsetDistance = 1.2;

            foreach (Room room in rooms)
            {
                IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
                if (boundaries == null || boundaries.Count == 0)
                    continue;

                IList<BoundarySegment> outerBoundary = boundaries[0];
                CurveLoop loop = new CurveLoop();

                foreach (BoundarySegment seg in outerBoundary)
                {
                    Curve curve = seg.GetCurve();
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    XYZ p0Flat = new XYZ(p0.X, p0.Y, 0);
                    XYZ p1Flat = new XYZ(p1.X, p1.Y, 0);

                    if (curve is Line)
                        loop.Append(Line.CreateBound(p0Flat, p1Flat));
                    else if (curve is Arc arc)
                    {
                        XYZ center = new XYZ(arc.Center.X, arc.Center.Y, 0);
                        XYZ xDir = new XYZ(arc.XDirection.X, arc.XDirection.Y, 0);
                        XYZ yDir = new XYZ(arc.YDirection.X, arc.YDirection.Y, 0);
                        double startParam = arc.GetEndParameter(0);
                        double endParam = arc.GetEndParameter(1);
                        loop.Append(Arc.Create(center, arc.Radius, startParam, endParam, xDir, yDir));
                    }
                }

                if (loop.Any() && !PointsAreEqual(loop.First().GetEndPoint(0), loop.Last().GetEndPoint(1)))
                {
                    Line closing = Line.CreateBound(loop.Last().GetEndPoint(1), loop.First().GetEndPoint(0));
                    loop.Append(closing);
                }

                if (!loop.Any())
                    continue;

                CurveLoop offsetLoop = null;
                try
                {
                    XYZ normal = new XYZ(0, 0, 1);
                    offsetLoop = CurveLoop.CreateViaOffset(loop, offsetDistance, normal);
                }
                catch
                {
                    offsetLoop = loop;
                }

                if (offsetLoop == null || !offsetLoop.Any())
                    continue;

                try
                {
                    Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                        new List<CurveLoop> { offsetLoop },
                        XYZ.BasisZ,
                        0.1
                    );
                    solids.Add(solid);
                }
                catch { }
            }

            if (solids.Count == 0)
                return null;

            Solid unionSolid = solids[0];
            for (int i = 1; i < solids.Count; i++)
            {
                try
                {
                    unionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(
                        unionSolid,
                        solids[i],
                        BooleanOperationsType.Union
                    );
                }
                catch { }
            }

            Face topFace = null;
            double maxArea = 0;
            foreach (Face face in unionSolid.Faces)
            {
                PlanarFace planarFace = face as PlanarFace;
                if (planarFace != null && Math.Abs(planarFace.FaceNormal.Z) > 0.99)
                {
                    double area = planarFace.Area;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        topFace = planarFace;
                    }
                }
            }

            if (topFace == null)
                return null;

            IList<CurveLoop> edgeLoops = topFace.GetEdgesAsCurveLoops();
            if (edgeLoops == null || edgeLoops.Count == 0)
                return null;

            CurveLoop outerLoop = null;
            double maxLoopArea = 0;
            foreach (CurveLoop candidate in edgeLoops)
            {
                CurveLoop flatLoop = new CurveLoop();
                foreach (Curve curve in candidate)
                {
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    XYZ p0Flat = new XYZ(p0.X, p0.Y, 0);
                    XYZ p1Flat = new XYZ(p1.X, p1.Y, 0);

                    if (curve is Line)
                        flatLoop.Append(Line.CreateBound(p0Flat, p1Flat));
                    else if (curve is Arc arc)
                    {
                        XYZ center = new XYZ(arc.Center.X, arc.Center.Y, 0);
                        XYZ xDir = new XYZ(arc.XDirection.X, arc.XDirection.Y, 0);
                        XYZ yDir = new XYZ(arc.YDirection.X, arc.YDirection.Y, 0);
                        double startParam = arc.GetEndParameter(0);
                        double endParam = arc.GetEndParameter(1);
                        flatLoop.Append(Arc.Create(center, arc.Radius, startParam, endParam, xDir, yDir));
                    }
                }
                double area = GetCurveLoopArea(flatLoop);
                if (area > maxLoopArea)
                {
                    maxLoopArea = area;
                    outerLoop = flatLoop;
                }
            }

            if (outerLoop == null || !outerLoop.Any())
                return null;

            return outerLoop;
        }

        // ======================================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ РАБОТЫ С КРИВЫМИ
        // ======================================================================

        private bool PointsAreEqual(XYZ a, XYZ b)
        {
            return Math.Abs(a.X - b.X) < Eps && Math.Abs(a.Y - b.Y) < Eps && Math.Abs(a.Z - b.Z) < Eps;
        }

        private double GetCurveLoopArea(CurveLoop loop)
        {
            if (loop == null || !loop.Any()) return 0;
            List<XYZ> pts = new List<XYZ>();
            foreach (Curve c in loop)
            {
                pts.Add(c.GetEndPoint(0));
                if (!(c is Line))
                {
                    double step = 1.0 / 10;
                    for (int i = 1; i < 10; i++)
                    {
                        double param = c.GetEndParameter(0) + step * i * (c.GetEndParameter(1) - c.GetEndParameter(0));
                        pts.Add(c.Evaluate(param, true));
                    }
                }
            }
            pts.Add(loop.Last().GetEndPoint(1));
            var distinct = pts.Distinct(new XYZComparer()).ToList();
            if (distinct.Count < 3) return 0;
            double area = 0;
            for (int i = 0; i < distinct.Count - 1; i++)
                area += distinct[i].X * distinct[i + 1].Y - distinct[i + 1].X * distinct[i].Y;
            return Math.Abs(area) / 2.0;
        }

        private class XYZComparer : IEqualityComparer<XYZ>
        {
            private double eps = 0.01;
            public bool Equals(XYZ a, XYZ b)
            {
                return Math.Abs(a.X - b.X) < eps && Math.Abs(a.Y - b.Y) < eps && Math.Abs(a.Z - b.Z) < eps;
            }
            public int GetHashCode(XYZ obj)
            {
                int x = (int)Math.Round(obj.X / eps);
                int y = (int)Math.Round(obj.Y / eps);
                int z = (int)Math.Round(obj.Z / eps);
                return x ^ y ^ z;
            }
        }

        // ======================================================================
        // ПРИМЕНЕНИЕ ОБРЕЗКИ К ВИДУ
        // ======================================================================

        private void ApplyCropToView(ViewPlan view, CurveLoop cropBoundary)
        {
            if (cropBoundary == null || !cropBoundary.Any())
                return;

            view.CropBoxActive = true;
            view.CropBoxVisible = true;

            ViewCropRegionShapeManager cropManager = view.GetCropRegionShapeManager();

            if (cropManager.CanHaveShape)
            {
                try
                {
                    cropManager.SetCropShape(cropBoundary);
                    view.CropBoxVisible = false;
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Ошибка обрезки",
                        $"Не удалось установить форму обрезки: {ex.Message}\n" +
                        $"Будет использована прямоугольная обрезка.");

                    BoundingBoxXYZ bounds = GetBoundsFromCurveLoop(cropBoundary);
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
                BoundingBoxXYZ bounds = GetBoundsFromCurveLoop(cropBoundary);
                if (bounds != null)
                {
                    view.CropBox = bounds;
                    view.CropBoxActive = true;
                    view.CropBoxVisible = false;
                }
            }
        }

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

        // ======================================================================
        // СКРЫТИЕ ВНУТРЕННИХ ОСЕЙ НА ВИДЕ
        // ======================================================================

        private void HideInternalGrids(Document doc, ViewPlan view, CurveLoop apartmentBoundary)
        {
            try
            {
                // Собираем все оси, видимые на данном плане этажа
                List<Grid> visibleGrids = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Grids)
                    .WhereElementIsNotElementType()
                    .Cast<Grid>()
                    .ToList();

                List<Grid> intersectingVerticals = new List<Grid>();
                List<Grid> intersectingHorizontals = new List<Grid>();

                foreach (Grid grid in visibleGrids)
                {
                    Line gridLine = grid.Curve as Line;
                    if (gridLine == null) continue;

                    XYZ gP0 = gridLine.GetEndPoint(0);
                    XYZ gP1 = gridLine.GetEndPoint(1);
                    XYZ gDir = (gP1 - gP0).Normalize();

                    XYZ gP0Flat = new XYZ(gP0.X, gP0.Y, 0);
                    XYZ flatDir = new XYZ(gDir.X, gDir.Y, 0).Normalize();

                    // Создаем искусственную длинную линию для проверки пересечения (5000 футов в обе стороны)
                    Line extendedGridLine = Line.CreateBound(gP0Flat - flatDir * 5000, gP0Flat + flatDir * 5000);

                    bool intersects = false;
                    foreach (Curve bCurve in apartmentBoundary)
                    {
                        if (extendedGridLine.Intersect(bCurve) == SetComparisonResult.Overlap)
                        {
                            intersects = true;
                            break;
                        }
                    }

                    // Если ось пересекает контур квартиры, распределяем её по направлению
                    if (intersects)
                    {
                        // Если координата Y меняется сильнее, чем X — ось преимущественно вертикальная
                        if (Math.Abs(flatDir.Y) > Math.Abs(flatDir.X))
                        {
                            intersectingVerticals.Add(grid);
                        }
                        else // Иначе преимущественно горизонтальная
                        {
                            intersectingHorizontals.Add(grid);
                        }
                    }
                }

                List<ElementId> gridsToHide = new List<ElementId>();

                // Обрабатываем вертикальные оси (ищем самую левую и самую правую)
                if (intersectingVerticals.Count > 2)
                {
                    // Сортируем по координате X
                    var sortedVerts = intersectingVerticals.OrderBy(g => g.Curve.GetEndPoint(0).X).ToList();

                    // Пропускаем первую и последнюю, остальные скрываем
                    for (int i = 1; i < sortedVerts.Count - 1; i++)
                    {
                        gridsToHide.Add(sortedVerts[i].Id);
                    }
                }

                // Обрабатываем горизонтальные оси (ищем самую нижнюю и самую верхнюю)
                if (intersectingHorizontals.Count > 2)
                {
                    // Сортируем по координате Y
                    var sortedHoriz = intersectingHorizontals.OrderBy(g => g.Curve.GetEndPoint(0).Y).ToList();

                    // Пропускаем первую и последнюю, остальные скрываем
                    for (int i = 1; i < sortedHoriz.Count - 1; i++)
                    {
                        gridsToHide.Add(sortedHoriz[i].Id);
                    }
                }

                // Скрываем элементы на текущем виде, если список не пуст
                if (gridsToHide.Count > 0)
                {
                    view.HideElements(gridsToHide);
                }
            }
            catch (Exception ex)
            {
                // Не прерываем общий процесс, если на каком-то из видов скрытие сбойнуло
                System.Diagnostics.Debug.WriteLine($"Ошибка при скрытии внутренних осей: {ex.Message}");
            }
        }

        // ======================================================================
        // НОВЫЙ МЕТОД: ДОБАВЛЕНИЕ ТЕКСТОВОЙ АННОТАЦИИ НАД ПЛАНОМ
        // ======================================================================

        private void AddApartmentLabel(Document doc, ViewPlan view, List<Room> rooms, CurveLoop boundary)
        {
            if (rooms == null || rooms.Count == 0 || boundary == null || !boundary.Any())
                return;

            Room room = rooms[0];

            string корпус = GetParameterValue(room, "LG_Номер корпуса");
            string секция = GetParameterValue(room, "LG_Секция");
            string этаж = GetParameterValue(room, "LG_Номер этажа");
            string квартира = GetParameterValue(room, "LG_МаркаКвартирыФРС");

            // Если ни один параметр не заполнен – ничего не создаём
            if (string.IsNullOrEmpty(корпус) && string.IsNullOrEmpty(секция) &&
                string.IsNullOrEmpty(этаж) && string.IsNullOrEmpty(квартира))
                return;

            // Формируем текст, заменяя null на пустую строку
            string text = $"Корпус {корпус ?? ""}, секция {секция ?? ""}, этаж {этаж ?? ""}, кв. {квартира ?? ""}";

            // Находим центр и верхнюю границу контура
            BoundingBoxXYZ bbox = GetBoundsFromCurveLoop(boundary);
            if (bbox == null)
                return;

            double centerX = (bbox.Min.X + bbox.Max.X) / 2.0;
            double topY = bbox.Max.Y + 2.0; // отступ 2 фута вверх
            XYZ position = new XYZ(centerX, topY, 0);

            // Получаем первый попавшийся тип текстовой заметки
            TextNoteType textType = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault();

            if (textType == null)
                return;

            try
            {
                TextNote.Create(doc, view.Id, position, text, textType.Id);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                System.Diagnostics.Debug.WriteLine($"Не удалось создать текстовую аннотацию: {ex.Message}");
            }
        }
    }
}