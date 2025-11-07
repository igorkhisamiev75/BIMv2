#region Namespace

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateOpeningsForMEP : IExternalCommand
    {
        private Document _doc;
        private double _tolerance = 0.05; // 50 мм по умолчанию
        private int _openingCounter = 1;
        private Stopwatch _stopwatch;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _stopwatch = Stopwatch.StartNew();

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _doc = uiDoc.Document;

            // 1. Выводим форму для указания допуска через TaskDialog
            var toleranceResult = GetToleranceFromUser();
            if (toleranceResult.Item1 == Result.Cancelled)
            {
                return Result.Cancelled;
            }
            _tolerance = toleranceResult.Item2;

            // Выбор связанной модели
            var linkInstance = SelectLinkInstance();
            if (linkInstance == null)
            {
                TaskDialog.Show("Ошибка", "Связанная модель не выбрана");
                return Result.Cancelled;
            }

            using (var transaction = new Transaction(_doc))
            {
                transaction.Start("Создание отверстий для МЭП");

                try
                {
                    // 2. Собираем все воздуховоды, трубы, лотки
                    var mepElements = GetAllMEPElements(_doc);

                    // Получаем стены из связанной модели
                    var linkedWalls = GetWallsFromLink(linkInstance);

                    // Показываем информацию о найденных элементах
                    TaskDialog.Show("Информация",
                        $"Найдено МЭП элементов: {mepElements.Count}\n" +
                        $"Найдено стен в связанной модели: {linkedWalls.Count}\n" +
                        $"Допуск: {_tolerance * 1000} мм");

                    // 3. Находим пересечения и создаем отверстия
                    CreateOpeningsAtIntersections(mepElements, linkedWalls, linkInstance);

                    transaction.Commit();

                    _stopwatch.Stop();
                    // 4. Выводим результат
                    TaskDialog.Show("Завершено",
                        $"Расставлено отверстий: {_openingCounter - 1}\n" +
                        $"Время выполнения: {_stopwatch.Elapsed.TotalSeconds:F2} секунд");

                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                    return Result.Failed;
                }
            }
        }

        private (Result, double) GetToleranceFromUser()
        {
            // Создаем диалог для ввода допуска
            var dialog = new TaskDialog("Настройка отверстий для МЭП систем")
            {
                MainInstruction = "Введите допуск для отверстий",
                MainContent = "Допуск будет добавлен к размерам МЭП элементов для создания отверстий",
                CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
            };

            // Добавляем поле ввода - используем только доступные CommandLink
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "50 мм (стандартный)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "25 мм (минимальный)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "100 мм (увеличенный)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Указать свой размер...");

            var result = dialog.Show();

            switch (result)
            {
                case TaskDialogResult.CommandLink1:
                    return (Result.Succeeded, 0.05); // 50 мм
                case TaskDialogResult.CommandLink2:
                    return (Result.Succeeded, 0.025); // 25 мм
                case TaskDialogResult.CommandLink3:
                    return (Result.Succeeded, 0.1); // 100 мм
                case TaskDialogResult.CommandLink4:
                    return GetCustomTolerance();
                case TaskDialogResult.Cancel:
                default:
                    return (Result.Cancelled, 0.0);
            }
        }

        private (Result, double) GetCustomTolerance()
        {
            // Диалог для ввода произвольного значения - используем два диалога если нужно больше опций
            var dialog = new TaskDialog("Укажите допуск")
            {
                MainInstruction = "Введите значение допуска в миллиметрах",
                MainContent = "Значение от 10 до 200 мм",
                AllowCancellation = true
            };

            // Используем только доступные CommandLink (1-4)
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "10 мм");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "20 мм");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "75 мм");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "150 мм и более...");

            var result = dialog.Show();

            switch (result)
            {
                case TaskDialogResult.CommandLink1:
                    return (Result.Succeeded, 0.01);
                case TaskDialogResult.CommandLink2:
                    return (Result.Succeeded, 0.02);
                case TaskDialogResult.CommandLink3:
                    return (Result.Succeeded, 0.075);
                case TaskDialogResult.CommandLink4:
                    return GetLargeTolerance();
                case TaskDialogResult.Cancel:
                default:
                    return (Result.Cancelled, 0.0);
            }
        }

        private (Result, double) GetLargeTolerance()
        {
            var dialog = new TaskDialog("Большие допуски")
            {
                MainInstruction = "Выберите большой допуск",
                MainContent = "Для крупных инженерных систем",
                AllowCancellation = true
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "150 мм");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "200 мм (максимальный)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Вернуться к предыдущему выбору");

            var result = dialog.Show();

            switch (result)
            {
                case TaskDialogResult.CommandLink1:
                    return (Result.Succeeded, 0.15);
                case TaskDialogResult.CommandLink2:
                    return (Result.Succeeded, 0.2);
                case TaskDialogResult.CommandLink3:
                    return GetCustomTolerance(); // Возвращаемся назад
                case TaskDialogResult.Cancel:
                default:
                    return (Result.Cancelled, 0.0);
            }
        }

        private RevitLinkInstance SelectLinkInstance()
        {
            var linkedInstances = FindLinkedInstances().ToList();

            if (linkedInstances.Count == 0)
            {
                TaskDialog.Show("Ошибка", "Не найдено связанных моделей в проекте");
                return null;
            }

            if (linkedInstances.Count == 1)
            {
                var link = linkedInstances.First();
                var linkDoc = link.GetLinkDocument();
                TaskDialog.Show("Информация", $"Выбрана связанная модель: {linkDoc?.Title ?? "Неизвестно"}");
                return link;
            }

            // Если несколько связанных моделей, предлагаем выбрать
            return SelectLinkFromMultiple(linkedInstances);
        }

        private RevitLinkInstance SelectLinkFromMultiple(List<RevitLinkInstance> linkedInstances)
        {
            // Если моделей больше 4, разбиваем на несколько диалогов
            if (linkedInstances.Count > 4)
            {
                return SelectLinkFromMany(linkedInstances);
            }

            var dialog = new TaskDialog("Выбор связанной модели")
            {
                MainInstruction = "Выберите связанную модель для поиска стен",
                MainContent = "Будут использованы стены из выбранной модели",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };

            // Добавляем кнопки для каждой связанной модели (максимум 4)
            for (int i = 0; i < Math.Min(linkedInstances.Count, 4); i++)
            {
                var link = linkedInstances[i];
                var linkDoc = link.GetLinkDocument();
                var linkName = linkDoc?.Title ?? "Неизвестная модель";
                dialog.AddCommandLink((TaskDialogCommandLinkId)(i + 1), linkName);
            }

            var result = dialog.Show();

            if (result >= TaskDialogResult.CommandLink1 &&
                result <= (TaskDialogResult)Math.Min(linkedInstances.Count, 4))
            {
                int selectedIndex = (int)result - (int)TaskDialogResult.CommandLink1;
                return linkedInstances[selectedIndex];
            }

            return null;
        }

        private RevitLinkInstance SelectLinkFromMany(List<RevitLinkInstance> linkedInstances)
        {
            // Для большого количества моделей используем несколько диалогов
            int currentIndex = 0;
            const int pageSize = 4; // Максимум 4 на страницу

            while (currentIndex < linkedInstances.Count)
            {
                var pageInstances = linkedInstances
                    .Skip(currentIndex)
                    .Take(pageSize)
                    .ToList();

                var dialog = new TaskDialog("Выбор связанной модели")
                {
                    MainInstruction = $"Выберите связанную модель ({currentIndex + 1}-{currentIndex + pageInstances.Count} из {linkedInstances.Count})",
                    MainContent = "Будут использованы стены из выбранной модели",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };

                // Добавляем кнопки для текущей страницы
                for (int i = 0; i < pageInstances.Count; i++)
                {
                    var link = pageInstances[i];
                    var linkDoc = link.GetLinkDocument();
                    var linkName = linkDoc?.Title ?? "Неизвестная модель";
                    dialog.AddCommandLink((TaskDialogCommandLinkId)(i + 1), linkName);
                }

                // Добавляем кнопку для следующей страницы если есть
                if (currentIndex + pageSize < linkedInstances.Count)
                {
                    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Следующая страница...");
                }

                var result = dialog.Show();

                if (result >= TaskDialogResult.CommandLink1 &&
                    result <= (TaskDialogResult)pageInstances.Count)
                {
                    int selectedIndex = (int)result - (int)TaskDialogResult.CommandLink1;
                    return pageInstances[selectedIndex];
                }
                else if (result == TaskDialogResult.CommandLink4 && currentIndex + pageSize < linkedInstances.Count)
                {
                    currentIndex += pageSize;
                    continue;
                }

                break;
            }

            return null;
        }

        // Остальные методы остаются без изменений...
        private IList<Element> GetAllMEPElements(Document doc)
        {
            var elements = new List<Element>();

            // Воздуховоды
            var ducts = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DuctCurves)
                .WhereElementIsNotElementType()
                .ToElements();
            elements.AddRange(ducts);

            // Трубы
            var pipes = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsNotElementType()
                .ToElements();
            elements.AddRange(pipes);

            // Кабельные лотки
            var cableTrays = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CableTray)
                .WhereElementIsNotElementType()
                .ToElements();
            elements.AddRange(cableTrays);

            // Лотки
            var conduits = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Conduit)
                .WhereElementIsNotElementType()
                .ToElements();
            elements.AddRange(conduits);

            return elements;
        }

        private IList<Element> GetWallsFromLink(RevitLinkInstance linkInstance)
        {
            var linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null) return new List<Element>();

            return new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .ToElements();
        }

        private void CreateOpeningsAtIntersections(IList<Element> mepElements, IList<Element> walls, RevitLinkInstance linkInstance)
        {
            var openingSymbol = GetOpeningSymbol();
            if (openingSymbol == null)
            {
                throw new Exception("Не найдено семейство 'ОМ_Отверстие_Экз'");
            }

            // Активируем семейство, если оно не активировано
            if (!openingSymbol.IsActive)
            {
                openingSymbol.Activate();
                _doc.Regenerate();
            }

            // Получаем уровень по умолчанию
            Level defaultLevel = GetDefaultLevel();

            foreach (var mepElement in mepElements)
            {
                try
                {
                    var mepBoundingBox = mepElement.get_BoundingBox(null);
                    if (mepBoundingBox == null) continue;

                    var intersectingWalls = new List<Element>();

                    foreach (var wall in walls)
                    {
                        try
                        {
                            if (CheckIntersection(mepElement, wall, linkInstance))
                            {
                                intersectingWalls.Add(wall);
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (intersectingWalls.Count > 0)
                    {
                        CreateOpeningsForMEPElement(mepElement, intersectingWalls, linkInstance, openingSymbol, defaultLevel);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        private Level GetDefaultLevel()
        {
            // Получаем первый доступный уровень
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();
        }

        private void CreateOpeningsForMEPElement(Element mepElement, List<Element> intersectingWalls, RevitLinkInstance linkInstance, FamilySymbol openingSymbol, Level level)
        {
            LocationCurve mepLocation = mepElement.Location as LocationCurve;
            if (mepLocation == null) return;

            var mepCurve = mepLocation.Curve;
            var transform = linkInstance.GetTotalTransform();

            for (int i = 0; i < intersectingWalls.Count; i++)
            {
                try
                {
                    var wall = intersectingWalls[i];

                    double parameter = (double)(i + 1) / (intersectingWalls.Count + 1);
                    var intersectionPoint = mepCurve.Evaluate(parameter, true);

                    var transformedPoint = transform.OfPoint(intersectionPoint);

                    var (width, height) = GetOpeningDimensions(mepElement);
                    var wallThickness = GetWallThickness(wall);

                    // Создаем семейство
                    FamilyInstance openingInstance = _doc.Create.NewFamilyInstance(
                        transformedPoint, openingSymbol, level,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    if (openingInstance != null)
                    {
                        SetOpeningParameters(openingInstance, width, height, wallThickness, mepElement, wall);
                        _openingCounter++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при создании отверстия {_openingCounter}: {ex.Message}");
                    continue;
                }
            }
        }

        private bool CheckIntersection(Element mepElement, Element wall, RevitLinkInstance linkInstance)
        {
            try
            {
                var mepBoundingBox = mepElement.get_BoundingBox(null);
                var wallBoundingBox = wall.get_BoundingBox(null);

                if (mepBoundingBox == null || wallBoundingBox == null) return false;

                var transform = linkInstance.GetTotalTransform();

                var wallMin = transform.OfPoint(wallBoundingBox.Min);
                var wallMax = transform.OfPoint(wallBoundingBox.Max);

                var outline1 = new Outline(mepBoundingBox.Min, mepBoundingBox.Max);
                var outline2 = new Outline(wallMin, wallMax);

                return outline1.Intersects(outline2, 0);
            }
            catch
            {
                return false;
            }
        }

        private (double width, double height) GetOpeningDimensions(Element mepElement)
        {
            double width = 0.1;
            double height = 0.1;

            try
            {
                switch (mepElement.Category.Id.IntegerValue)
                {
                    case (int)BuiltInCategory.OST_DuctCurves:
                        var duct = mepElement as Duct;
                        if (duct != null)
                        {
                            var connectors = duct.ConnectorManager.Connectors;
                            foreach (Connector connector in connectors)
                            {
                                if (connector.Shape == ConnectorProfileType.Rectangular)
                                {
                                    width = connector.Width + _tolerance;
                                    height = connector.Height + _tolerance;
                                    break;
                                }
                                else if (connector.Shape == ConnectorProfileType.Round || connector.Shape == ConnectorProfileType.Oval)
                                {
                                    width = connector.Radius * 2 + _tolerance;
                                    height = connector.Radius * 2 + _tolerance;
                                    break;
                                }
                            }

                            if (width <= 0.1 || height <= 0.1)
                            {
                                var widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                                var heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                                var diameterParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

                                if (widthParam != null && heightParam != null && widthParam.HasValue && heightParam.HasValue)
                                {
                                    width = widthParam.AsDouble() + _tolerance;
                                    height = heightParam.AsDouble() + _tolerance;
                                }
                                else if (diameterParam != null && diameterParam.HasValue)
                                {
                                    width = diameterParam.AsDouble() + _tolerance;
                                    height = diameterParam.AsDouble() + _tolerance;
                                }
                            }
                        }
                        break;

                    case (int)BuiltInCategory.OST_PipeCurves:
                        var pipe = mepElement as Pipe;
                        if (pipe != null)
                        {
                            var diameterParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER);
                            if (diameterParam != null && diameterParam.HasValue)
                            {
                                width = diameterParam.AsDouble() + _tolerance;
                                height = diameterParam.AsDouble() + _tolerance;
                            }
                            else
                            {
                                var diameterParam2 = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                                if (diameterParam2 != null && diameterParam2.HasValue)
                                {
                                    width = diameterParam2.AsDouble() + _tolerance;
                                    height = diameterParam2.AsDouble() + _tolerance;
                                }
                            }
                        }
                        break;

                    case (int)BuiltInCategory.OST_CableTray:
                        var cableTray = mepElement as CableTray;
                        if (cableTray != null)
                        {
                            var widthParam = cableTray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                            var heightParam = cableTray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);

                            if (widthParam != null && widthParam.HasValue)
                                width = widthParam.AsDouble() + _tolerance;
                            if (heightParam != null && heightParam.HasValue)
                                height = heightParam.AsDouble() + _tolerance;
                        }
                        break;

                    case (int)BuiltInCategory.OST_Conduit:
                        var conduit = mepElement as Conduit;
                        if (conduit != null)
                        {
                            var diameterParam = conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                            if (diameterParam != null && diameterParam.HasValue)
                            {
                                width = diameterParam.AsDouble() + _tolerance;
                                height = diameterParam.AsDouble() + _tolerance;
                            }
                        }
                        break;
                }
            }
            catch
            {
                // В случае ошибки используем минимальные размеры
            }

            if (width < 0.05) width = 0.05;
            if (height < 0.05) height = 0.05;

            return (width, height);
        }

        private double GetWallThickness(Element wall)
        {
            try
            {
                var wallElement = wall as Wall;
                if (wallElement != null)
                {
                    return wallElement.Width;
                }

                var thicknessParam = wall.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
                if (thicknessParam != null && thicknessParam.HasValue)
                {
                    return thicknessParam.AsDouble();
                }
            }
            catch
            {
                // В случае ошибки используем толщину по умолчанию
            }

            return 0.3;
        }

        private void SetOpeningParameters(FamilyInstance opening, double width, double height, double depth, Element mepElement, Element wall)
        {
            try
            {
                var widthParam = opening.LookupParameter("ADSK_Размер_Ширина");
                if (widthParam != null && !widthParam.IsReadOnly)
                    widthParam.Set(width);

                var heightParam = opening.LookupParameter("ADSK_Размер_Высота");
                if (heightParam != null && !heightParam.IsReadOnly)
                    heightParam.Set(height);

                var depthParam = opening.LookupParameter("ADSK_Размер_Глубина");
                if (depthParam != null && !depthParam.IsReadOnly)
                    depthParam.Set(depth);

                var commentsParam = opening.LookupParameter("Комментарии");
                if (commentsParam != null && !commentsParam.IsReadOnly)
                    commentsParam.Set($"Отверстие №{_openingCounter} для {GetMEPTypeName(mepElement)}");

                SetAdditionalParameters(opening, mepElement, wall);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при установке параметров: {ex.Message}");
            }
        }

        private string GetMEPTypeName(Element mepElement)
        {
            switch (mepElement.Category.Id.IntegerValue)
            {
                case (int)BuiltInCategory.OST_DuctCurves:
                    return "Воздуховод";
                case (int)BuiltInCategory.OST_PipeCurves:
                    return "Труба";
                case (int)BuiltInCategory.OST_CableTray:
                    return "Кабельный лоток";
                case (int)BuiltInCategory.OST_Conduit:
                    return "Лоток";
                default:
                    return "МЭП элемент";
            }
        }

        private void SetAdditionalParameters(FamilyInstance opening, Element mepElement, Element wall)
        {
            try
            {
                var mepIdParam = opening.LookupParameter("ADSK_ID_MEP_Элемента");
                if (mepIdParam != null && !mepIdParam.IsReadOnly)
                    mepIdParam.Set(mepElement.Id.IntegerValue.ToString());

                var wallIdParam = opening.LookupParameter("ADSK_ID_Стены");
                if (wallIdParam != null && !wallIdParam.IsReadOnly)
                    wallIdParam.Set(wall.Id.IntegerValue.ToString());

                var mepTypeParam = opening.LookupParameter("ADSK_Тип_MEP_Элемента");
                if (mepTypeParam != null && !mepTypeParam.IsReadOnly)
                    mepTypeParam.Set(GetMEPTypeName(mepElement));

                var levelParam = opening.LookupParameter("Уровень");
                if (levelParam != null && !levelParam.IsReadOnly)
                {
                    var mepLevel = mepElement.LevelId;
                    if (mepLevel != ElementId.InvalidElementId)
                    {
                        var level = _doc.GetElement(mepLevel) as Level;
                        levelParam.Set(level?.Name ?? "");
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки дополнительных параметров
            }
        }

        private FamilySymbol GetOpeningSymbol()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .OfType<Family>()
                .FirstOrDefault(f => f.Name.Equals("ОМ_Отверстие_Экз"))
                ?.GetFamilySymbolIds()
                .Select(id => _doc.GetElement(id))
                .OfType<FamilySymbol>()
                .FirstOrDefault();
        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }
    }
}