#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Data;
using MessageBox = System.Windows.MessageBox;
#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateSingleSystemViews : IExternalCommand
    {
        private Document _doc;
        private UIDocument _uiDoc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

            try
            {
                // Показываем WPF окно выбора систем
                var systemsWindow = new SystemsSelectionWindow(_doc);

                if (systemsWindow.ShowDialog() != true)
                    return Result.Cancelled;

                var selectedSystems = systemsWindow.GetSelectedSystems();

                if (selectedSystems.Count == 0)
                {
                    MessageBox.Show("Не выбрано ни одной системы.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return Result.Cancelled;
                }

                // Создаем окно прогресса
                var progressWindow = new SystemProgressWindow();
                progressWindow.Show();
                progressWindow.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                int createdViewsCount = 0;
                int totalSystems = selectedSystems.Count;
                int currentSystem = 0;

                using (Transaction t = new Transaction(_doc, "Создание видов систем"))
                {
                    t.Start();

                    foreach (var systemInfo in selectedSystems)
                    {
                        currentSystem++;

                        if (progressWindow.CancelRequested)
                        {
                            t.RollBack();
                            MessageBox.Show("Операция отменена пользователем.", "Отменено",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            return Result.Cancelled;
                        }

                        progressWindow.StatusText = $"Создание вида системы {currentSystem} из {totalSystems}: {systemInfo.Name}";
                        progressWindow.ProgressValue = (int)((double)currentSystem / totalSystems * 100);
                        progressWindow.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                        if (CreateSystemView(systemInfo))
                        {
                            createdViewsCount++;
                        }
                    }

                    t.Commit();
                }

                progressWindow.Close();

                MessageBox.Show($"Создано {createdViewsCount} 3D видов систем", "Результат",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Создание 3D вида для одной системы
        /// </summary>
        private bool CreateSystemView(SystemInfo systemInfo)
        {
            try
            {
                // Получаем тип 3D вида
                ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                // Создаем 3D вид
                View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                // Формируем имя вида
                string viewName = systemInfo.Name;
                if (viewName.Length > 200)
                    viewName = viewName.Substring(0, 200);
                view.Name = viewName;

                // Настраиваем вид
                view.DisplayStyle = DisplayStyle.Shading;
                view.DetailLevel = ViewDetailLevel.Fine;
                view.Discipline = ViewDiscipline.Coordination;

                // Получаем все элементы выбранной системы
                var systemElements = GetSystemElements(systemInfo);

                if (systemElements.Count == 0)
                {
                    _doc.Delete(view.Id); // Удаляем вид, если нет элементов
                    return false;
                }

                // Устанавливаем секционную коробку ТОЛЬКО для элементов системы
                BoundingBoxXYZ sectionBox = CreateSectionBoxFromElements(systemElements);
                if (sectionBox != null)
                    view.SetSectionBox(sectionBox);

                // Создаем фильтр для отображения только выбранной системы
                CreateViewFilter(view, systemInfo, systemElements);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка создания вида системы {systemInfo.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получение всех элементов системы
        /// </summary>
        private List<Element> GetSystemElements(SystemInfo systemInfo)
        {
            var elements = new List<Element>();

            try
            {
                List<BuiltInCategory> categories = new List<BuiltInCategory>();

                if (systemInfo.CategoryName == "Воздуховоды")
                {
                    categories.Add(BuiltInCategory.OST_DuctCurves);
                    categories.Add(BuiltInCategory.OST_DuctFitting);
                    categories.Add(BuiltInCategory.OST_DuctAccessory);
                    categories.Add(BuiltInCategory.OST_DuctTerminal);
                    categories.Add(BuiltInCategory.OST_DuctInsulations);
                    categories.Add(BuiltInCategory.OST_DuctLinings);
                }
                else if (systemInfo.CategoryName == "Трубы")
                {
                    categories.Add(BuiltInCategory.OST_PipeCurves);
                    categories.Add(BuiltInCategory.OST_PipeFitting);
                    categories.Add(BuiltInCategory.OST_PipeAccessory);
                    categories.Add(BuiltInCategory.OST_PlumbingFixtures);
                    categories.Add(BuiltInCategory.OST_Sprinklers);
                    categories.Add(BuiltInCategory.OST_PipeInsulations);
                }

                foreach (var category in categories)
                {
                    var collector = new FilteredElementCollector(_doc)
                        .OfCategory(category)
                        .WhereElementIsNotElementType();

                    foreach (Element element in collector)
                    {
                        // Получаем имя системы элемента
                        string elementSystemName = GetSystemNameFromElement(element);

                        if (!string.IsNullOrEmpty(elementSystemName))
                        {
                            // Сравниваем имена систем (без учета регистра и пробелов)
                            string cleanSystemName = systemInfo.Name.Trim();
                            string cleanElementSystemName = elementSystemName.Trim();

                            if (string.Equals(cleanSystemName, cleanElementSystemName, StringComparison.OrdinalIgnoreCase))
                            {
                                elements.Add(element);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения элементов системы: {ex.Message}");
            }

            return elements;
        }

        /// <summary>
        /// Получение имени системы из элемента
        /// </summary>
        private string GetSystemNameFromElement(Element element)
        {
            try
            {
                // В первую очередь пробуем встроенные параметры системы
                Parameter param = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (param != null && param.HasValue)
                {
                    return param.AsString();
                }

                param = element.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
                if (param != null && param.HasValue)
                {
                    return param.AsString();
                }

                // Затем пробуем пользовательские параметры
                string[] customParamNames =
                {
                    "Имя системы",
                    "System Name",
                    "System Abbreviation",
                    "Система",
                    "Аббревиатура системы"
                };

                foreach (var paramName in customParamNames)
                {
                    param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        if (param.StorageType == StorageType.String)
                        {
                            string value = param.AsString();
                            if (!string.IsNullOrEmpty(value))
                            {
                                return value;
                            }
                        }
                    }
                }

                // Если у элемента нет параметра системы, проверяем связанные системы
                if (element is MEPCurve mepCurve)
                {
                    MEPSystem mepSystem = mepCurve.MEPSystem;
                    if (mepSystem != null)
                    {
                        param = mepSystem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                        if (param != null && param.HasValue)
                        {
                            return param.AsString();
                        }

                        param = mepSystem.LookupParameter("Имя");
                        if (param != null && param.HasValue)
                        {
                            return param.AsString();
                        }

                        return mepSystem.Name;
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Создание секционной коробки из элементов системы
        /// </summary>
        private BoundingBoxXYZ CreateSectionBoxFromElements(List<Element> elements)
        {
            try
            {
                if (elements.Count == 0)
                    return null;

                // Собираем все BoundingBox элементов системы
                List<BoundingBoxXYZ> boxes = new List<BoundingBoxXYZ>();
                foreach (var el in elements)
                {
                    var bb = el.get_BoundingBox(null);
                    if (bb != null)
                    {
                        boxes.Add(bb);
                    }
                    else
                    {
                        // Для элементов без BoundingBox получаем геометрию
                        Options options = new Options();
                        options.ComputeReferences = true;
                        options.DetailLevel = ViewDetailLevel.Medium;

                        GeometryElement geomElem = el.get_Geometry(options);
                        if (geomElem != null)
                        {
                            foreach (GeometryObject geomObj in geomElem)
                            {
                                if (geomObj is Solid solid && solid.Volume > 0)
                                {
                                    bb = solid.GetBoundingBox();
                                    if (bb != null) boxes.Add(bb);
                                }
                            }
                        }
                    }
                }

                if (boxes.Count == 0)
                    return null;

                // Находим общие границы всех элементов системы
                double minX = boxes.Min(b => b.Min.X);
                double minY = boxes.Min(b => b.Min.Y);
                double minZ = boxes.Min(b => b.Min.Z);
                double maxX = boxes.Max(b => b.Max.X);
                double maxY = boxes.Max(b => b.Max.Y);
                double maxZ = boxes.Max(b => b.Max.Z);

                // Добавляем отступы для лучшего обзора
                double sizeX = maxX - minX;
                double sizeY = maxY - minY;
                double sizeZ = maxZ - minZ;

                double offset = Math.Max(Math.Max(sizeX, sizeY), sizeZ) * 0.2;
                offset = Math.Max(offset, 2.0); // Минимальный отступ 2 метра

                return new BoundingBoxXYZ
                {
                    Min = new XYZ(minX - offset, minY - offset, minZ - offset),
                    Max = new XYZ(maxX + offset, maxY + offset, maxZ + offset)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка создания секционной коробки: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Создание фильтра для вида - ТОЛЬКО элементы выбранной системы
        /// </summary>
        private void CreateViewFilter(View3D view, SystemInfo systemInfo, List<Element> systemElements)
        {
            using (Transaction filterTransaction = new Transaction(_doc, "Создание фильтра системы"))
            {
                filterTransaction.Start();

                try
                {
                    // 1. Сначала СКРЫВАЕМ ВСЕ КАТЕГОРИИ
                    HideAllCategories(view);

                    // 2. Затем ПОКАЗЫВАЕМ только нужные категории для выбранной системы
                    ShowSystemCategories(view, systemInfo);

                    // 3. Создаем фильтр для ПОКАЗА только элементов выбранной системы
                    CreateSystemFilter(view, systemInfo);

                    filterTransaction.Commit();
                }
                catch (Exception ex)
                {
                    filterTransaction.RollBack();
                    Debug.WriteLine($"Ошибка создания фильтра: {ex.Message}");

                    // Альтернативный простой метод
                    using (Transaction altTransaction = new Transaction(_doc, "Простая фильтрация"))
                    {
                        altTransaction.Start();
                        SimpleFilterBySystem(view, systemInfo, systemElements);
                        altTransaction.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Скрыть все категории в виде
        /// </summary>
        private void HideAllCategories(View3D view)
        {
            try
            {
                // Получаем все категории документа
                Categories categories = _doc.Settings.Categories;

                foreach (Category category in categories)
                {
                    // Пропускаем внутренние и специальные категории
                    if (category.Id.IntegerValue < 0 ||
                        category.Id == new ElementId(BuiltInCategory.OST_Viewers) ||
                        category.Id == new ElementId(BuiltInCategory.OST_VolumeOfInterest))
                        continue;

                    if (view.CanCategoryBeHidden(category.Id))
                    {
                        try
                        {
                            view.SetCategoryHidden(category.Id, true);
                        }
                        catch
                        {
                            // Пропускаем ошибки для отдельных категорий
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при скрытии всех категорий: {ex.Message}");
            }
        }

        /// <summary>
        /// Показать категории для системы
        /// </summary>
        private void ShowSystemCategories(View3D view, SystemInfo systemInfo)
        {
            try
            {
                List<ElementId> systemCategoryIds = new List<ElementId>();

                if (systemInfo.CategoryName == "Воздуховоды")
                {
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_DuctCurves));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_DuctFitting));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_DuctAccessory));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_DuctTerminal));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_DuctInsulations));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_DuctLinings));
                }
                else if (systemInfo.CategoryName == "Трубы")
                {
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_PipeCurves));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_PipeFitting));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_PipeAccessory));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_PlumbingFixtures));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_Sprinklers));
                    systemCategoryIds.Add(new ElementId(BuiltInCategory.OST_PipeInsulations));
                }

                // Делаем категории видимыми
                foreach (ElementId categoryId in systemCategoryIds)
                {
                    if (view.CanCategoryBeHidden(categoryId))
                    {
                        view.SetCategoryHidden(categoryId, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при показе категорий системы: {ex.Message}");
            }
        }

        /// <summary>
        /// Создание фильтра для отображения только выбранной системы
        /// </summary>
        private void CreateSystemFilter(View3D view, SystemInfo systemInfo)
        {
            try
            {
                // Получаем ID категорий для фильтра
                List<ElementId> categoryIds = new List<ElementId>();

                if (systemInfo.CategoryName == "Воздуховоды")
                {
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_DuctCurves));
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_DuctFitting));
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_DuctAccessory));
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_DuctTerminal));
                }
                else if (systemInfo.CategoryName == "Трубы")
                {
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_PipeCurves));
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_PipeFitting));
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_PipeAccessory));
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_PlumbingFixtures));
                    categoryIds.Add(new ElementId(BuiltInCategory.OST_Sprinklers));
                }

                if (categoryIds.Count == 0)
                    return;

                // Создаем фильтр по имени системы
                FilterRule filterRule = null;

                // Пробуем создать фильтр по разным параметрам
                try
                {
                    // Основной параметр - имя системы
                    filterRule = ParameterFilterRuleFactory.CreateEqualsRule(
                        new ElementId(BuiltInParameter.RBS_SYSTEM_NAME_PARAM),
                        systemInfo.Name,
                        false);
                }
                catch
                {
                    try
                    {
                        // Альтернативный параметр - аббревиатура системы
                        filterRule = ParameterFilterRuleFactory.CreateEqualsRule(
                            new ElementId(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM),
                            systemInfo.Name,
                            false);
                    }
                    catch
                    {
                        // Если не получается, создаем фильтр по пустому правилу
                        filterRule = ParameterFilterRuleFactory.CreateEqualsRule(
                            new ElementId(BuiltInParameter.INVALID),
                            "",
                            false);
                    }
                }

                // Создаем элемент фильтра
                var elementFilter = new ElementParameterFilter(filterRule);

                // Создаем фильтр вида
                string filterName = $"Фильтр_Системы_{systemInfo.Name}";
                ParameterFilterElement viewFilter = null;

                // Проверяем, существует ли уже такой фильтр
                var existingFilter = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .FirstOrDefault(f => f.Name == filterName);

                if (existingFilter != null)
                {
                    viewFilter = existingFilter;
                }
                else
                {
                    viewFilter = ParameterFilterElement.Create(_doc, filterName, categoryIds, elementFilter);
                }

                // Применяем фильтр к виду
                if (!view.GetFilters().Contains(viewFilter.Id))
                {
                    view.AddFilter(viewFilter.Id);
                }

                // Устанавливаем: фильтр ВКЛЮЧЕН (показываем элементы по фильтру)
                view.SetFilterVisibility(viewFilter.Id, true);

                // Создаем обратный фильтр для скрытия других систем
                CreateInverseFilter(view, categoryIds, systemInfo.Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка создания фильтра системы: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Создание обратного фильтра для скрытия элементов других систем
        /// </summary>
        private void CreateInverseFilter(View3D view, List<ElementId> categoryIds, string systemName)
        {
            try
            {
                // Фильтр "НЕ равно" для скрытия других систем
                FilterRule inverseRule = ParameterFilterRuleFactory.CreateNotEqualsRule(
                    new ElementId(BuiltInParameter.RBS_SYSTEM_NAME_PARAM),
                    systemName,
                    false);

                var inverseFilter = new ElementParameterFilter(inverseRule);

                string inverseFilterName = $"Фильтр_Скрыть_Другие_{systemName}";
                ParameterFilterElement inverseViewFilter = null;

                // Проверяем существование
                var existingInverseFilter = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .FirstOrDefault(f => f.Name == inverseFilterName);

                if (existingInverseFilter != null)
                {
                    inverseViewFilter = existingInverseFilter;
                }
                else
                {
                    inverseViewFilter = ParameterFilterElement.Create(_doc, inverseFilterName, categoryIds, inverseFilter);
                }

                // Применяем обратный фильтр
                if (!view.GetFilters().Contains(inverseViewFilter.Id))
                {
                    view.AddFilter(inverseViewFilter.Id);
                }

                // Обратный фильтр ВЫКЛЮЧЕН (скрывает элементы)
                view.SetFilterVisibility(inverseViewFilter.Id, false);
            }
            catch
            {
                // Если не получается создать обратный фильтр, просто работаем с основным
            }
        }

        /// <summary>
        /// Простая фильтрация по системе
        /// </summary>
        private void SimpleFilterBySystem(View3D view, SystemInfo systemInfo, List<Element> systemElements)
        {
            try
            {
                // Скрываем все категории
                HideAllCategories(view);

                // Показываем только категории для выбранной системы
                ShowSystemCategories(view, systemInfo);

                // Для надежности получаем ID всех элементов системы
                List<ElementId> systemElementIds = systemElements.Select(e => e.Id).ToList();

                // Создаем фильтр по элементам
                var elementFilter = new ElementIdSetFilter(new HashSet<ElementId>(systemElementIds));

                // Создаем фильтр вида
                var viewFilter = ParameterFilterElement.Create(_doc,
                    $"Фильтр_По_Элементам_{systemInfo.Name}",
                    new List<ElementId>(), // Пустой список категорий
                    elementFilter);

                // Применяем фильтр
                if (!view.GetFilters().Contains(viewFilter.Id))
                {
                    view.AddFilter(viewFilter.Id);
                }

                view.SetFilterVisibility(viewFilter.Id, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка простой фильтрации: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Класс для хранения информации о системе
    /// </summary>
    public class SystemInfo
    {
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// WPF окно для выбора систем
    /// </summary>
    public partial class SystemsSelectionWindow : Window
    {
        private Document _doc;
        private ObservableCollection<SystemInfo> _ductSystems = new ObservableCollection<SystemInfo>();
        private ObservableCollection<SystemInfo> _pipeSystems = new ObservableCollection<SystemInfo>();

        public SystemsSelectionWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();
            LoadSystems();
        }

        private void InitializeComponent()
        {
            this.Title = "Выбор систем для создания 3D видов";
            this.Width = 600;
            this.Height = 500;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.CanResize;

            // Создаем TabControl для вкладок
            TabControl tabControl = new TabControl();
            tabControl.Margin = new Thickness(10);

            // Вкладка 1: Воздуховоды
            TabItem ductTab = CreateTab("Воздуховоды", _ductSystems);
            tabControl.Items.Add(ductTab);

            // Вкладка 2: Трубы
            TabItem pipeTab = CreateTab("Трубы", _pipeSystems);
            tabControl.Items.Add(pipeTab);

            // Кнопки OK и Отмена
            StackPanel buttonPanel = new StackPanel();
            buttonPanel.Orientation = Orientation.Horizontal;
            buttonPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            buttonPanel.Margin = new Thickness(0, 10, 0, 0);

            Button selectAllButton = new Button
            {
                Content = "Выбрать все",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(0, 0, 10, 0)
            };
            selectAllButton.Click += SelectAllButton_Click;

            Button deselectAllButton = new Button
            {
                Content = "Снять все",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(0, 0, 10, 0)
            };
            deselectAllButton.Click += DeselectAllButton_Click;

            Button okButton = new Button
            {
                Content = "Создать виды",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button
            {
                Content = "Отмена",
                Padding = new Thickness(15, 5, 15, 5),
                IsCancel = true
            };
            cancelButton.Click += CancelButton_Click;

            buttonPanel.Children.Add(selectAllButton);
            buttonPanel.Children.Add(deselectAllButton);
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            // Основной контейнер
            StackPanel mainPanel = new StackPanel();
            mainPanel.Margin = new Thickness(10);
            mainPanel.Children.Add(tabControl);
            mainPanel.Children.Add(buttonPanel);

            this.Content = mainPanel;
        }

        /// <summary>
        /// Создание вкладки с CheckBox списком
        /// </summary>
        private TabItem CreateTab(string tabName, ObservableCollection<SystemInfo> systemsCollection)
        {
            TabItem tab = new TabItem();
            tab.Header = tabName;

            ScrollViewer scrollViewer = new ScrollViewer();
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            ListView listView = new ListView();
            listView.ItemsSource = systemsCollection;

            // Настраиваем шаблон отображения с CheckBox
            GridView gridView = new GridView();

            GridViewColumn checkBoxColumn = new GridViewColumn();
            checkBoxColumn.Width = 40;

            // Создаем DataTemplate для CheckBox
            DataTemplate checkBoxTemplate = new DataTemplate();
            FrameworkElementFactory checkBoxFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkBoxFactory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected"));
            checkBoxFactory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBoxFactory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            checkBoxTemplate.VisualTree = checkBoxFactory;
            checkBoxColumn.CellTemplate = checkBoxTemplate;

            GridViewColumn nameColumn = new GridViewColumn();
            nameColumn.Header = "Имя системы";
            nameColumn.Width = 200;
            nameColumn.DisplayMemberBinding = new System.Windows.Data.Binding("Name");

            gridView.Columns.Add(checkBoxColumn);
            gridView.Columns.Add(nameColumn);

            listView.View = gridView;

            scrollViewer.Content = listView;
            tab.Content = scrollViewer;

            return tab;
        }

        /// <summary>
        /// Загрузка систем из документа
        /// </summary>
        private void LoadSystems()
        {
            // Загрузка систем воздуховодов
            LoadSystemNames("Воздуховоды", _ductSystems);

            // Загрузка систем труб
            LoadSystemNames("Трубы", _pipeSystems);
        }

        /// <summary>
        /// Загрузка имен систем
        /// </summary>
        private void LoadSystemNames(string categoryName, ObservableCollection<SystemInfo> collection)
        {
            try
            {
                var systemNames = new HashSet<string>();

                // Определяем категории для сбора
                List<BuiltInCategory> categories = new List<BuiltInCategory>();

                if (categoryName == "Воздуховоды")
                {
                    categories.Add(BuiltInCategory.OST_DuctCurves);
                    categories.Add(BuiltInCategory.OST_DuctFitting);
                }
                else if (categoryName == "Трубы")
                {
                    categories.Add(BuiltInCategory.OST_PipeCurves);
                    categories.Add(BuiltInCategory.OST_PipeFitting);
                }

                foreach (var category in categories)
                {
                    var collector = new FilteredElementCollector(_doc)
                        .OfCategory(category)
                        .WhereElementIsNotElementType();

                    foreach (Element element in collector)
                    {
                        // Получаем имя системы из элемента
                        string systemName = GetSystemNameFromElement(element);

                        if (!string.IsNullOrEmpty(systemName) && !systemNames.Contains(systemName))
                        {
                            systemNames.Add(systemName);
                            collection.Add(new SystemInfo
                            {
                                Name = systemName,
                                CategoryName = categoryName,
                                IsSelected = false
                            });
                        }
                    }
                }

                // Сортируем по имени
                var sortedList = collection.OrderBy(s => s.Name).ToList();
                collection.Clear();
                foreach (var item in sortedList)
                {
                    collection.Add(item);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки систем {categoryName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение имени системы из элемента
        /// </summary>
        private string GetSystemNameFromElement(Element element)
        {
            try
            {
                // Пробуем встроенные параметры
                Parameter param = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (param != null && param.HasValue)
                {
                    return param.AsString();
                }

                param = element.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
                if (param != null && param.HasValue)
                {
                    return param.AsString();
                }

                // Пробуем пользовательские параметры
                string[] customParamNames =
                {
                    "Имя системы",
                    "System Name",
                    "System Abbreviation"
                };

                foreach (var paramName in customParamNames)
                {
                    param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        if (param.StorageType == StorageType.String)
                        {
                            return param.AsString();
                        }
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Получение выбранных систем
        /// </summary>
        public List<SystemInfo> GetSelectedSystems()
        {
            var selectedSystems = new List<SystemInfo>();

            var allSystems = _ductSystems.Concat(_pipeSystems);

            foreach (var system in allSystems)
            {
                if (system.IsSelected)
                {
                    selectedSystems.Add(system);
                }
            }

            return selectedSystems;
        }

        #region Обработчики событий кнопок

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Находим активную вкладку
            var tabControl = FindTabControl();
            if (tabControl != null && tabControl.SelectedItem is TabItem selectedTab)
            {
                ObservableCollection<SystemInfo> currentCollection = null;

                if (selectedTab.Header.ToString() == "Воздуховоды")
                    currentCollection = _ductSystems;
                else if (selectedTab.Header.ToString() == "Трубы")
                    currentCollection = _pipeSystems;

                if (currentCollection != null)
                {
                    foreach (var system in currentCollection)
                    {
                        system.IsSelected = true;
                    }
                }
            }
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Находим активную вкладку
            var tabControl = FindTabControl();
            if (tabControl != null && tabControl.SelectedItem is TabItem selectedTab)
            {
                ObservableCollection<SystemInfo> currentCollection = null;

                if (selectedTab.Header.ToString() == "Воздуховоды")
                    currentCollection = _ductSystems;
                else if (selectedTab.Header.ToString() == "Трубы")
                    currentCollection = _pipeSystems;

                if (currentCollection != null)
                {
                    foreach (var system in currentCollection)
                    {
                        system.IsSelected = false;
                    }
                }
            }
        }

        private TabControl FindTabControl()
        {
            if (this.Content is StackPanel mainPanel)
            {
                foreach (var child in mainPanel.Children)
                {
                    if (child is TabControl tabControl)
                        return tabControl;
                }
            }
            return null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }

    /// <summary>
    /// WPF окно прогресса
    /// </summary>
    public class SystemProgressWindow : Window
    {
        public bool CancelRequested { get; private set; }
        private ProgressBar _progressBar;
        private TextBlock _statusLabel;
        private Button _cancelButton;

        public double ProgressValue
        {
            get => _progressBar.Value;
            set => _progressBar.Dispatcher.Invoke(() => _progressBar.Value = value);
        }

        public string StatusText
        {
            get => _statusLabel.Text;
            set => _statusLabel.Dispatcher.Invoke(() => _statusLabel.Text = value);
        }

        public SystemProgressWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Title = "Создание видов систем";
            this.Width = 400;
            this.Height = 150;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;
            this.Topmost = true;

            // Основной контейнер
            StackPanel mainPanel = new StackPanel();
            mainPanel.Margin = new Thickness(20);

            // Текст статуса
            _statusLabel = new TextBlock()
            {
                Text = "Подготовка...",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Прогресс-бар
            _progressBar = new ProgressBar()
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Кнопка отмены
            _cancelButton = new Button()
            {
                Content = "Отмена",
                Width = 80,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Padding = new Thickness(10, 5, 10, 5)
            };
            _cancelButton.Click += (s, e) =>
            {
                CancelRequested = true;
                _cancelButton.IsEnabled = false;
                _cancelButton.Content = "Отмена...";
            };

            mainPanel.Children.Add(_statusLabel);
            mainPanel.Children.Add(_progressBar);
            mainPanel.Children.Add(_cancelButton);

            this.Content = mainPanel;
        }
    }
}