#region Namespace
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

// WPF пространства имен
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;
using CheckBox = System.Windows.Controls.CheckBox;
#endregion

namespace RevitAddIn2BIMRU.Commands.BIM
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateViewsForDuctSystems : IExternalCommand
    {
        private Document _doc;
        private UIDocument _uiDoc;

        // Список возможных имен параметра "Система воздуховодов" для разных версий Revit
        private readonly List<string> _ductSystemParamNames = new List<string>
        {
            "Система воздуховодов",
            "Duct System",
            "Система",
            "System",
            "RBS_DUCT_SYSTEM_PARAM",
            "ДУС Система",
            "Система ДУС",
            "System Type",
            "Тип системы"
        };

        // Список возможных имен параметра "Имя системы" для разных версий Revit
        private readonly List<string> _systemNameParamNames = new List<string>
        {
            "Имя системы",
            "System Name",
            "Имя",
            "Name",
            "RBS_SYSTEM_NAME_PARAM",
            "Наименование системы",
            "System Abbreviation",
            "Аббревиатура системы"
        };

        // Список категорий для фильтрации
        private readonly List<BuiltInCategory> _relevantCategories = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_DuctSystem,           // Система воздуховодов
            BuiltInCategory.OST_DuctCurves,           // Воздуховоды
            BuiltInCategory.OST_DuctFitting,          // Соединительные детали воздуховодов
            BuiltInCategory.OST_DuctAccessory,        // Арматура воздуховодов
            BuiltInCategory.OST_DuctTerminal,         // Воздухораспределители
            BuiltInCategory.OST_MechanicalEquipment,  // Оборудование
            BuiltInCategory.OST_DuctInsulations       // Изоляция воздуховодов
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

            try
            {
                // 1. Собираем все системы воздуховодов в проекте
                var allSystems = CollectDuctSystems();

                if (allSystems.Count == 0)
                {
                    MessageBox.Show("В проекте не найдено систем воздуховодов.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return Result.Cancelled;
                }

                // 2. Получаем уникальные имена систем
                var systemNames = ExtractSystemNames(allSystems);

                if (systemNames.Count == 0)
                {
                    MessageBox.Show("Не найдено систем с заданными именами.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return Result.Cancelled;
                }

                // 3. Показываем WPF форму для выбора систем
                var selectionForm = new SystemSelectionForm(systemNames);

                if (selectionForm.ShowDialog() != true)
                    return Result.Cancelled;

                var selectedSystemNames = selectionForm.GetSelectedSystemNames();

                if (selectedSystemNames.Count == 0)
                {
                    MessageBox.Show("Не выбрано ни одной системы.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return Result.Cancelled;
                }

                // 4. Создаем 3D виды для выбранных систем
                int createdViewsCount = CreateViewsForSelectedSystems(selectedSystemNames, allSystems);

                // 5. Показываем результат
                MessageBox.Show($"Создано {createdViewsCount} 3D видов для выбранных систем.",
                    "Результат", MessageBoxButton.OK, MessageBoxImage.Information);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Собирает все системы воздуховодов в проекте
        /// </summary>
        private List<MEPSystem> CollectDuctSystems()
        {
            var systems = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_DuctSystem)
                .WhereElementIsNotElementType()
                .Cast<MEPSystem>()
                .ToList();

            TaskDialog.Show("Информация", $"Найдено систем воздуховодов: {systems.Count}");
            return systems;
        }

        /// <summary>
        /// Извлекает уникальные имена систем из коллекции
        /// </summary>
        private List<string> ExtractSystemNames(List<MEPSystem> systems)
        {
            var names = new List<string>();

            foreach (var system in systems)
            {
                try
                {
                    string systemName = GetSystemName(system);

                    if (!string.IsNullOrEmpty(systemName) && !names.Contains(systemName))
                    {
                        names.Add(systemName);
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Внимание", $"Ошибка при получении имени системы {system.Id}: {ex.Message}");
                }
            }

            TaskDialog.Show("Информация", $"Найдено уникальных имен систем: {names.Count}");
            return names.OrderBy(n => n).ToList();
        }

        /// <summary>
        /// Получает имя системы
        /// </summary>
        private string GetSystemName(MEPSystem system)
        {
            try
            {
                // Пробуем получить имя через все возможные параметры
                foreach (string paramName in _systemNameParamNames)
                {
                    Parameter nameParam = system.LookupParameter(paramName);
                    if (nameParam != null && nameParam.HasValue)
                    {
                        if (nameParam.StorageType == StorageType.String)
                        {
                            string name = nameParam.AsString();
                            if (!string.IsNullOrEmpty(name))
                                return name.Trim();
                        }
                        else if (nameParam.StorageType == StorageType.ElementId)
                        {
                            ElementId elementId = nameParam.AsElementId();
                            if (elementId != null && elementId != ElementId.InvalidElementId)
                            {
                                Element nameElement = _doc.GetElement(elementId);
                                if (nameElement != null && !string.IsNullOrEmpty(nameElement.Name))
                                    return nameElement.Name.Trim();
                            }
                        }
                    }
                }

                // В крайнем случае возвращаем имя самого элемента
                return (system.Name ?? $"Система_{system.Id}").Trim();
            }
            catch
            {
                return (system.Name ?? $"Система_{system.Id}").Trim();
            }
        }

        /// <summary>
        /// Создает 3D виды для выбранных систем
        /// </summary>
        private int CreateViewsForSelectedSystems(List<string> selectedSystemNames, List<MEPSystem> allSystems)
        {
            int createdViewsCount = 0;

            // Получаем тип 3D вида
            ViewFamilyType view3DType = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

            if (view3DType == null)
            {
                MessageBox.Show("Не найден тип 3D вида в шаблоне проекта.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return 0;
            }

            using (Transaction trans = new Transaction(_doc, "Создание 3D видов для систем"))
            {
                trans.Start();

                foreach (string systemName in selectedSystemNames)
                {
                    try
                    {
                        TaskDialog.Show("Обработка", $"Обработка системы: {systemName}");

                        // Находим все системы с данным именем
                        var systemsWithName = FindSystemsByName(systemName, allSystems);

                        if (systemsWithName.Count == 0)
                        {
                            TaskDialog.Show("Внимание", $"Не найдено систем с именем: {systemName}");
                            continue;
                        }

                        // Получаем все элементы из выбранных систем
                        var allElementsInSystems = GetAllElementsFromSystems(systemsWithName);

                        TaskDialog.Show("Информация", $"Для системы '{systemName}' найдено элементов: {allElementsInSystems.Count}");

                        if (allElementsInSystems.Count == 0)
                        {
                            TaskDialog.Show("Внимание", $"Система '{systemName}' не содержит элементов.");
                            continue;
                        }

                        // Создаем 3D вид
                        View3D view3D = Create3DView(systemName, view3DType);

                        if (view3D == null)
                            continue;

                        // Применяем фильтр для отображения только элементов этой системы
                        ApplySystemFilter(view3D, allElementsInSystems, systemName);

                        createdViewsCount++;

                        TaskDialog.Show("Успех", $"Создан вид для системы: {systemName}");
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Ошибка", $"Не удалось создать вид для системы '{systemName}': {ex.Message}");
                    }
                }

                trans.Commit();
            }

            return createdViewsCount;
        }

        /// <summary>
        /// Находит все системы с заданным именем
        /// </summary>
        private List<MEPSystem> FindSystemsByName(string systemName, List<MEPSystem> allSystems)
        {
            var result = new List<MEPSystem>();

            foreach (var system in allSystems)
            {
                try
                {
                    string currentSystemName = GetSystemName(system);

                    if (string.Equals(currentSystemName, systemName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(system);
                    }
                }
                catch
                {
                    // Пропускаем системы с ошибками
                }
            }

            return result;
        }

        /// <summary>
        /// Получает все элементы из систем воздуховодов
        /// </summary>
        private List<Element> GetAllElementsFromSystems(List<MEPSystem> systems)
        {
            var allElements = new List<Element>();

            if (systems.Count == 0)
                return allElements;

            // Собираем ID всех систем
            var systemIds = systems.Select(s => s.Id).ToList();
            var systemNames = systems.Select(s => GetSystemName(s)).ToList();

            // Метод 1: Ищем элементы через параметр системы
            var elementsByParameter = FindElementsBySystemParameter(systemIds, systemNames);
            allElements.AddRange(elementsByParameter);

            // Метод 2: Ищем через связанные элементы для MechanicalSystem
            var elementsByConnectors = FindElementsBySystemConnectors(systems);
            allElements.AddRange(elementsByConnectors);

            // Метод 3: Ищем изоляцию через хост-элементы
            var insulationElements = FindInsulationElements(elementsByParameter.Union(elementsByConnectors).ToList());
            allElements.AddRange(insulationElements);

            // Удаляем дубликаты
            return allElements.Distinct().ToList();
        }

        /// <summary>
        /// Находит элементы через параметр системы
        /// </summary>
        private List<Element> FindElementsBySystemParameter(List<ElementId> systemIds, List<string> systemNames)
        {
            var elements = new List<Element>();

            // Собираем все элементы из релевантных категорий
            var collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(new ElementMulticategoryFilter(_relevantCategories.Select(c => new ElementId(c)).ToList()));

            var allRelevantElements = collector.ToElements();

            TaskDialog.Show("Информация", $"Найдено релевантных элементов: {allRelevantElements.Count()}");

            foreach (var element in allRelevantElements)
            {
                try
                {
                    bool isInSystem = false;

                    // Проверяем параметр системы по ID
                    Parameter systemParam = FindDuctSystemParameter(element);

                    if (systemParam != null && systemParam.HasValue)
                    {
                        if (systemParam.StorageType == StorageType.ElementId)
                        {
                            ElementId elementSystemId = systemParam.AsElementId();
                            if (elementSystemId != null && systemIds.Contains(elementSystemId))
                            {
                                isInSystem = true;
                            }
                        }
                    }

                    // Если не нашли по ID, проверяем по имени системы
                    if (!isInSystem)
                    {
                        foreach (string paramName in _systemNameParamNames)
                        {
                            Parameter nameParam = element.LookupParameter(paramName);
                            if (nameParam != null && nameParam.HasValue && nameParam.StorageType == StorageType.String)
                            {
                                string elementSystemName = nameParam.AsString();
                                if (!string.IsNullOrEmpty(elementSystemName) &&
                                    systemNames.Any(sysName => string.Equals(sysName, elementSystemName.Trim(), StringComparison.OrdinalIgnoreCase)))
                                {
                                    isInSystem = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (isInSystem)
                    {
                        elements.Add(element);
                    }
                }
                catch
                {
                    // Пропускаем элементы с ошибками
                }
            }

            TaskDialog.Show("Информация", $"Найдено элементов через параметры: {elements.Count}");
            return elements;
        }

        /// <summary>
        /// Находит элементы через коннекторы системы (для MechanicalSystem)
        /// </summary>
        private List<Element> FindElementsBySystemConnectors(List<MEPSystem> systems)
        {
            var elements = new List<Element>();

            foreach (var system in systems)
            {
                try
                {
                    // Для механических систем используем поиск через коннекторы
                    if (system is MechanicalSystem mechSystem)
                    {
                        // Получаем базовую точку системы (обычно это первая точка системы)
                        var basePoint = GetSystemBasePoint(mechSystem);

                        if (basePoint != null)
                        {
                            // Ищем элементы в радиусе от базовой точки
                            var nearbyElements = FindNearbyElements(basePoint, 10.0); // 10 метров
                            elements.AddRange(nearbyElements);
                        }
                    }
                }
                catch
                {
                    // Пропускаем системы с ошибками
                }
            }

            return elements.Distinct().ToList();
        }

        /// <summary>
        /// Получает базовую точку системы
        /// </summary>
        private XYZ GetSystemBasePoint(MechanicalSystem system)
        {
            try
            {
                // Пробуем получить точку из параметров системы
                Parameter basePointParam = system.LookupParameter("Base Point");
                if (basePointParam != null && basePointParam.HasValue)
                {
                    // Логика получения точки из параметра
                    return GetPointFromParameter(basePointParam);
                }

                // Альтернативный способ: получаем точку из расположения системы
                Location location = system.Location;
                if (location is LocationPoint locationPoint)
                {
                    return locationPoint.Point;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получает точку из параметра
        /// </summary>
        private XYZ GetPointFromParameter(Parameter param)
        {
            try
            {
                if (param.StorageType == StorageType.String)
                {
                    string pointString = param.AsString();
                    // Парсим строку в формате "X,Y,Z"
                    var coordinates = pointString.Split(',');
                    if (coordinates.Length == 3)
                    {
                        if (double.TryParse(coordinates[0], out double x) &&
                            double.TryParse(coordinates[1], out double y) &&
                            double.TryParse(coordinates[2], out double z))
                        {
                            return new XYZ(x, y, z);
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Находит элементы в радиусе от точки
        /// </summary>
        private List<Element> FindNearbyElements(XYZ point, double radius)
        {
            var elements = new List<Element>();

            try
            {
                // Создаем BoundingBox вокруг точки
                BoundingBoxXYZ bbox = new BoundingBoxXYZ
                {
                    Min = new XYZ(point.X - radius, point.Y - radius, point.Z - radius),
                    Max = new XYZ(point.X + radius, point.Y + radius, point.Z + radius)
                };

                // Создаем Outline на основе BoundingBox
                Outline outline = new Outline(bbox.Min, bbox.Max);

                // Создаем BoundingBoxIntersectsFilter
                BoundingBoxIntersectsFilter bboxFilter = new BoundingBoxIntersectsFilter(outline);

                // Ищем элементы в BoundingBox
                var collector = new FilteredElementCollector(_doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(new ElementMulticategoryFilter(_relevantCategories.Select(c => new ElementId(c)).ToList()))
                    .WherePasses(bboxFilter);

                return collector.ToElements().ToList();
            }
            catch
            {
                return elements;
            }
        }

        /// <summary>
        /// Находит изоляцию через хост-элементы
        /// </summary>
        private List<Element> FindInsulationElements(List<Element> hostElements)
        {
            var insulationElements = new List<Element>();

            if (hostElements.Count == 0)
                return insulationElements;

            // Собираем ID всех хостов
            var hostIds = hostElements.Select(he => he.Id).ToList();

            // Собираем все элементы изоляции
            var insulationCollector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_DuctInsulations)
                .WhereElementIsNotElementType();

            var allInsulations = insulationCollector.ToElements();

            foreach (Element insulation in allInsulations)
            {
                try
                {
                    if (insulation is InsulationLiningBase insulationBase)
                    {
                        ElementId hostId = insulationBase.HostElementId;
                        if (hostId != null && hostId != ElementId.InvalidElementId && hostIds.Contains(hostId))
                        {
                            insulationElements.Add(insulation);
                        }
                    }
                }
                catch
                {
                    // Пропускаем изоляцию с ошибками
                }
            }

            return insulationElements;
        }

        /// <summary>
        /// Находит параметр системы по всем возможным именам
        /// </summary>
        private Parameter FindDuctSystemParameter(Element element)
        {
            foreach (string paramName in _ductSystemParamNames)
            {
                Parameter param = element.LookupParameter(paramName);
                if (param != null && param.HasValue)
                {
                    return param;
                }
            }

            return null;
        }

        /// <summary>
        /// Создает 3D вид с именем системы
        /// </summary>
        private View3D Create3DView(string systemName, ViewFamilyType view3DType)
        {
            try
            {
                // Создаем изометрический 3D вид
                View3D view3D = View3D.CreateIsometric(_doc, view3DType.Id);

                // Задаем имя вида
                string viewName = $"3D_Система_{systemName}";

                // Ограничиваем длину имени
                if (viewName.Length > 200)
                    viewName = viewName.Substring(0, 200);

                view3D.Name = viewName;

                // Настраиваем отображение
                view3D.DisplayStyle = DisplayStyle.Shading;
                view3D.DetailLevel = ViewDetailLevel.Medium;
                view3D.Discipline = ViewDiscipline.Mechanical;

                TaskDialog.Show("Создание вида", $"Создан 3D вид: {viewName}");
                return view3D;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось создать 3D вид для системы '{systemName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Применяет фильтр к виду для отображения только элементов выбранной системы
        /// </summary>
        private void ApplySystemFilter(View3D view, List<Element> elementsToShow, string systemName)
        {
            try
            {
                List<ElementId> elementIdsToShow = elementsToShow.Select(e => e.Id).Distinct().ToList();

                if (elementIdsToShow.Count == 0)
                {
                    CreateEmptyFilter(view, systemName);
                    return;
                }

                // Получаем BoundingBox всех элементов системы
                BoundingBoxXYZ sectionBox = CalculateSectionBox(elementIdsToShow);
                if (sectionBox != null)
                {
                    view.SetSectionBox(sectionBox);
                }

                // Создаем фильтр для скрытия всех элементов, кроме выбранных
                CreateExclusionFilter(view, elementIdsToShow, systemName);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось применить фильтр для системы '{systemName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Рассчитывает секционную коробку на основе элементов
        /// </summary>
        private BoundingBoxXYZ CalculateSectionBox(List<ElementId> elementIds)
        {
            try
            {
                List<BoundingBoxXYZ> boundingBoxes = new List<BoundingBoxXYZ>();

                foreach (ElementId elementId in elementIds)
                {
                    Element element = _doc.GetElement(elementId);
                    if (element != null)
                    {
                        BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            boundingBoxes.Add(bbox);
                        }
                    }
                }

                if (boundingBoxes.Count == 0)
                    return null;

                // Находим общие границы
                double minX = boundingBoxes.Min(b => b.Min.X);
                double minY = boundingBoxes.Min(b => b.Min.Y);
                double minZ = boundingBoxes.Min(b => b.Min.Z);
                double maxX = boundingBoxes.Max(b => b.Max.X);
                double maxY = boundingBoxes.Max(b => b.Max.Y);
                double maxZ = boundingBoxes.Max(b => b.Max.Z);

                // Добавляем отступ 20%
                double offsetX = (maxX - minX) * 0.2;
                double offsetY = (maxY - minY) * 0.2;
                double offsetZ = (maxZ - minZ) * 0.2;

                offsetX = Math.Max(offsetX, 1.0);
                offsetY = Math.Max(offsetY, 1.0);
                offsetZ = Math.Max(offsetZ, 1.0);

                return new BoundingBoxXYZ
                {
                    Min = new XYZ(minX - offsetX, minY - offsetY, minZ - offsetZ),
                    Max = new XYZ(maxX + offsetX, maxY + offsetY, maxZ + offsetZ)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Создает фильтр исключения для скрытия всех элементов, кроме указанных
        /// </summary>
        private void CreateExclusionFilter(View3D view, List<ElementId> elementsToShow, string systemName)
        {
            try
            {
                // Получаем все элементы в виде
                var allElementsInView = new FilteredElementCollector(_doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds()
                    .ToList();

                TaskDialog.Show("Информация", $"Всего элементов в виде: {allElementsInView.Count}, элементов для показа: {elementsToShow.Count}");

                // Создаем список элементов для скрытия
                var elementsToHide = new List<ElementId>();

                foreach (ElementId elementId in allElementsInView)
                {
                    if (!elementsToShow.Contains(elementId))
                    {
                        elementsToHide.Add(elementId);
                    }
                }

                // Скрываем элементы
                if (elementsToHide.Count > 0)
                {
                    for (int i = 0; i < elementsToHide.Count; i += 100)
                    {
                        var batch = elementsToHide.Skip(i).Take(100).ToList();
                        view.HideElements(batch);
                    }

                    TaskDialog.Show("Информация", $"Скрыто элементов: {elementsToHide.Count}");
                }

                // Создаем и применяем графические настройки
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(new Autodesk.Revit.DB.Color(255, 165, 0)); // Оранжевый
                ogs.SetProjectionLineWeight(4);
                ogs.SetSurfaceTransparency(0);

                int appliedCount = 0;
                foreach (ElementId elementId in elementsToShow)
                {
                    try
                    {
                        view.SetElementOverrides(elementId, ogs);
                        appliedCount++;
                    }
                    catch
                    {
                        // Игнорируем элементы, которые не могут быть изменены
                    }
                }

                TaskDialog.Show("Информация", $"Применены графические настройки к {appliedCount} элементам");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Внимание", $"Не удалось создать фильтр для системы '{systemName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Создает пустой фильтр
        /// </summary>
        private void CreateEmptyFilter(View3D view, string systemName)
        {
            try
            {
                var allElementsInView = new FilteredElementCollector(_doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds()
                    .ToList();

                for (int i = 0; i < allElementsInView.Count; i += 100)
                {
                    var batch = allElementsInView.Skip(i).Take(100).ToList();
                    view.HideElements(batch);
                }

                TaskDialog.Show("Внимание", $"Система '{systemName}' не содержит элементов. Создан пустой вид.");
            }
            catch
            {
                // Игнорируем ошибки
            }
        }
    }

    // WPF форма для выбора систем
    public partial class SystemSelectionForm : Window
    {
        private List<string> _systemNames;
        private List<CheckBox> _checkBoxes = new List<CheckBox>();
        private WrapPanel _checkBoxPanel;

        public SystemSelectionForm(List<string> systemNames)
        {
            _systemNames = systemNames;
            InitializeComponent();
            LoadSystemNames();
        }

        private void InitializeComponent()
        {
            this.Width = 500;
            this.Height = 600;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.CanResize;
            this.Title = "Выбор систем воздуховодов";

            var mainGrid = new System.Windows.Controls.Grid();
            mainGrid.Margin = new Thickness(10);

            // Определяем строки
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Список
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Кнопки выбора
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Основные кнопки

            // Заголовок
            var lblTitle = new Label
            {
                Content = "Выберите системы для создания 3D видов:",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            System.Windows.Controls.Grid.SetRow(lblTitle, 0);

            // ScrollViewer со списком CheckBox
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(5)
            };

            // Добавим панель для чекбоксов
            _checkBoxPanel = new WrapPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(5)
            };

            stackPanel.Children.Add(_checkBoxPanel);
            scrollViewer.Content = stackPanel;
            System.Windows.Controls.Grid.SetRow(scrollViewer, 1);

            // Кнопки выбора
            var selectionButtonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var btnSelectAll = new Button
            {
                Content = "Выбрать все",
                Width = 100,
                Margin = new Thickness(5)
            };
            btnSelectAll.Click += BtnSelectAll_Click;

            var btnDeselectAll = new Button
            {
                Content = "Снять все",
                Width = 100,
                Margin = new Thickness(5)
            };
            btnDeselectAll.Click += BtnDeselectAll_Click;

            var btnInvertSelection = new Button
            {
                Content = "Инвертировать",
                Width = 100,
                Margin = new Thickness(5)
            };
            btnInvertSelection.Click += BtnInvertSelection_Click;

            selectionButtonsPanel.Children.Add(btnSelectAll);
            selectionButtonsPanel.Children.Add(btnDeselectAll);
            selectionButtonsPanel.Children.Add(btnInvertSelection);
            System.Windows.Controls.Grid.SetRow(selectionButtonsPanel, 2);

            // Основные кнопки
            var mainButtonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnOK = new Button
            {
                Content = "Создать виды",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5),
                Background = Brushes.LightGreen
            };
            btnOK.Click += BtnOK_Click;

            var btnCancel = new Button
            {
                Content = "Отмена",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5)
            };
            btnCancel.Click += BtnCancel_Click;

            mainButtonsPanel.Children.Add(btnOK);
            mainButtonsPanel.Children.Add(btnCancel);
            System.Windows.Controls.Grid.SetRow(mainButtonsPanel, 3);

            // Добавляем элементы в основной Grid
            mainGrid.Children.Add(lblTitle);
            mainGrid.Children.Add(scrollViewer);
            mainGrid.Children.Add(selectionButtonsPanel);
            mainGrid.Children.Add(mainButtonsPanel);

            this.Content = mainGrid;
        }

        private void LoadSystemNames()
        {
            if (_checkBoxPanel != null)
            {
                _checkBoxPanel.Children.Clear();
                _checkBoxes.Clear();

                foreach (string systemName in _systemNames)
                {
                    var checkBox = new CheckBox
                    {
                        Content = systemName,
                        Margin = new Thickness(2),
                        IsChecked = false,
                        Tag = systemName
                    };

                    _checkBoxPanel.Children.Add(checkBox);
                    _checkBoxes.Add(checkBox);
                }
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in _checkBoxes)
            {
                checkBox.IsChecked = true;
            }
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in _checkBoxes)
            {
                checkBox.IsChecked = false;
            }
        }

        private void BtnInvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in _checkBoxes)
            {
                checkBox.IsChecked = !checkBox.IsChecked;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Возвращает список выбранных имен систем
        /// </summary>
        public List<string> GetSelectedSystemNames()
        {
            var selectedNames = new List<string>();

            foreach (var checkBox in _checkBoxes)
            {
                if (checkBox.IsChecked == true && checkBox.Tag is string systemName)
                {
                    selectedNames.Add(systemName);
                }
            }

            return selectedNames;
        }
    }
}