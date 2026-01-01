#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Microsoft.Win32;
using WpfLabel = System.Windows.Controls.Label;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfButton = System.Windows.Controls.Button;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfSlider = System.Windows.Controls.Slider;
using WpfBorder = System.Windows.Controls.Border;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfGrid = System.Windows.Controls.Grid;
using WpfColumnDefinition = System.Windows.Controls.ColumnDefinition;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateViewForCollisions : IExternalCommand
    {
        private Document _doc;
        private UIDocument _uiDoc;
        private List<View3D> _createdViews = new List<View3D>();

        // База данных: FamilySymbolId -> List<Element> (обобщенные модели с этим Symbol)
        private Dictionary<int, List<Element>> _symbolToElementsMap = new Dictionary<int, List<Element>>();
        // База данных: ElementId -> FamilySymbolId (для быстрого поиска)
        private Dictionary<int, int> _elementToSymbolMap = new Dictionary<int, int>();

        // Настройки цветов и прозрачности
        private Color _collision1Color = Colors.Orange;
        private Color _collision2Color = Colors.Green;
        private int _transparency = 80;
        private string _reportFilePath = string.Empty;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

            try
            {
                // Показываем WPF форму настроек
                var settingsForm = new CollisionSettingsWpfWindow();

                // Устанавливаем начальные значения
                settingsForm.Collision1Color = _collision1Color;
                settingsForm.Collision2Color = _collision2Color;
                settingsForm.Transparency = _transparency;

                if (settingsForm.ShowDialog() != true)
                    return Result.Cancelled;

                // Получаем настройки из формы
                _collision1Color = settingsForm.Collision1Color;
                _collision2Color = settingsForm.Collision2Color;
                _transparency = settingsForm.Transparency;
                _reportFilePath = settingsForm.ReportFilePath;

                if (string.IsNullOrEmpty(_reportFilePath))
                {
                    MessageBox.Show("Файл отчета не выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return Result.Cancelled;
                }

                ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                ElementId solidFillPatternId = GetSolidFillPatternId();

                // Загружаем отчет
                XmlDocument xml = new XmlDocument();
                xml.Load(_reportFilePath);
                var clashResults = xml.SelectNodes("//clashresult");

                if (clashResults == null || clashResults.Count == 0)
                {
                    MessageBox.Show("В отчёте не найдено коллизий.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return Result.Cancelled;
                }

                // Создаем и показываем WPF форму прогресса
                var progressForm = new ProgressWpfWindow();
                progressForm.Show();

                // Обновляем интерфейс
                progressForm.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                // 2. Создаем базу данных обобщенных моделей, дверей, окон, импостов витража, панелей витража и соединительных деталей
                progressForm.StatusText = "Построение базы данных элементов...";
                progressForm.ProgressValue = 10;
                progressForm.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                BuildExtendedElementsDatabase();

                int viewCount = 0;
                int totalClashes = clashResults.Count;
                int currentClash = 0;

                using (Transaction t = new Transaction(_doc, "Создание видов коллизий"))
                {
                    t.Start();

                    // 3. Проверяем каждый конфликт из отчета
                    foreach (XmlNode clash in clashResults)
                    {
                        if (progressForm.CancelRequested)
                        {
                            t.RollBack();
                            MessageBox.Show("Операция отменена пользователем.", "Отменено",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            return Result.Cancelled;
                        }

                        currentClash++;
                        string name = clash.Attributes["name"]?.Value ?? "Без_имени";

                        progressForm.StatusText = $"Обработка коллизии {currentClash} из {totalClashes}: {name}";
                        progressForm.ProgressValue = 10 + (int)((double)currentClash / totalClashes * 80);
                        progressForm.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                        // Ищем элементы для этой коллизии с разделением на две группы
                        List<Element> collision1Elements;
                        List<Element> collision2Elements;
                        List<Element> allCollisionElements = FindElementsForCollision(clash, out collision1Elements, out collision2Elements);

                        if (allCollisionElements.Count > 0)
                        {
                            Debug.WriteLine($"Найдено элементов: {allCollisionElements.Count}");
                            Debug.WriteLine($"Коллизия 1: {collision1Elements.Count} элементов");
                            Debug.WriteLine($"Коллизия 2: {collision2Elements.Count} элементов");

                            // Получаем ID для имени вида
                            List<int> collision1Ids, collision2Ids;
                            GetCollisionIdsFromXml(clash, out collision1Ids, out collision2Ids);
                            List<int> allReportIds = collision1Ids.Concat(collision2Ids).ToList();

                            if (CreateCollisionView(name, allCollisionElements, collision1Elements, collision2Elements, allReportIds, viewFamilyType, solidFillPatternId))
                            {
                                viewCount++;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Элементы не найдены");
                        }
                    }

                    t.Commit();
                }

                progressForm.StatusText = "Завершено!";
                progressForm.ProgressValue = 100;
                progressForm.Close();

                MessageBox.Show($"Создано {viewCount} 3D видов для коллизий", "Результат",
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
        /// 2. Создаем базу данных обобщенных моделей, дверей, окон, импостов витража, панелей витража и соединительных деталей
        /// </summary>
        private void BuildExtendedElementsDatabase()
        {
            _symbolToElementsMap.Clear();
            _elementToSymbolMap.Clear();

            // Собираем обобщенные модели
            var genericModels = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType()
                .ToList();

            // Собираем двери
            var doors = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToList();

            // Собираем окна
            var windows = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType()
                .ToList();

            // Собираем импосты витража (стоечные профили витражей)
            var curtainWallMullions = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_CurtainWallMullions)
                .WhereElementIsNotElementType()
                .ToList();

            // Собираем панели витража
            var curtainPanels = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                .WhereElementIsNotElementType()
                .ToList();

            // Собираем соединительные детали воздуховодов
            var ductFittings = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_DuctFitting)
                .WhereElementIsNotElementType()
                .ToList();

            // Собираем соединительные детали труб
            var pipeFittings = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_PipeFitting)
                .WhereElementIsNotElementType()
                .ToList();

            Debug.WriteLine($"=== ПОСТРОЕНИЕ БАЗЫ ДАННЫХ ===");
            Debug.WriteLine($"Найдено обобщенных моделей: {genericModels.Count}");
            Debug.WriteLine($"Найдено дверей: {doors.Count}");
            Debug.WriteLine($"Найдено окон: {windows.Count}");
            Debug.WriteLine($"Найдено импостов витража: {curtainWallMullions.Count}");
            Debug.WriteLine($"Найдено панелей витража: {curtainPanels.Count}");
            Debug.WriteLine($"Найдено соединительных деталей воздуховодов: {ductFittings.Count}");
            Debug.WriteLine($"Найдено соединительных деталей труб: {pipeFittings.Count}");

            // Обрабатываем обобщенные модели
            ProcessElementsCollection(genericModels, "Обобщенная модель");

            // Обрабатываем двери
            ProcessElementsCollection(doors, "Дверь");

            // Обрабатываем окна
            ProcessElementsCollection(windows, "Окно");

            // Обрабатываем импосты витража
            ProcessElementsCollection(curtainWallMullions, "Импост витража");

            // Обрабатываем панели витража
            ProcessElementsCollection(curtainPanels, "Панель витража");

            // Обрабатываем соединительные детали воздуховодов
            ProcessElementsCollection(ductFittings, "Соединительная деталь воздуховода");

            // Обрабатываем соединительные детали труб
            ProcessElementsCollection(pipeFittings, "Соединительная деталь трубы");

            Debug.WriteLine($"База построена: {_symbolToElementsMap.Count} FamilySymbolId, {_elementToSymbolMap.Count} элементов");
        }

        /// <summary>
        /// Обрабатывает коллекцию элементов и добавляет их в базу данных
        /// </summary>
        private void ProcessElementsCollection(List<Element> elements, string elementType)
        {
            foreach (Element element in elements)
            {
                try
                {
                    // Получаем FamilySymbolId из геометрии элемента
                    int? familySymbolId = GetFamilySymbolIdFromGeometry(element);

                    if (familySymbolId.HasValue)
                    {
                        // Сохраняем в базу: ElementId -> FamilySymbolId
                        _elementToSymbolMap[element.Id.IntegerValue] = familySymbolId.Value;

                        // Сохраняем в базу: FamilySymbolId -> List<Element>
                        if (!_symbolToElementsMap.ContainsKey(familySymbolId.Value))
                            _symbolToElementsMap[familySymbolId.Value] = new List<Element>();

                        _symbolToElementsMap[familySymbolId.Value].Add(element);

                        Debug.WriteLine($"{elementType} {element.Id} -> FamilySymbolId {familySymbolId.Value}");
                    }
                    else
                    {
                        Debug.WriteLine($"{elementType} {element.Id}: FamilySymbolId не найден");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка {elementType.ToLower()} {element.Id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Получаем FamilySymbolId из геометрии элемента
        /// </summary>
        private int? GetFamilySymbolIdFromGeometry(Element element)
        {
            try
            {
                Options options = new Options
                {
                    ComputeReferences = true,
                    DetailLevel = ViewDetailLevel.Medium,
                    IncludeNonVisibleObjects = true
                };

                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null)
                    return null;

                // Ищем GeometryInstance в геометрии
                foreach (GeometryObject geomObj in geometry)
                {
                    if (geomObj is GeometryInstance geometryInstance)
                    {
                        // Пытаемся получить FamilySymbolId через Reflection
                        ElementId symbolId = FindSymbolIdByReflection(geometryInstance);
                        if (symbolId != null && symbolId != ElementId.InvalidElementId)
                        {
                            return symbolId.IntegerValue;
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
        /// Поиск SymbolId через Reflection
        /// </summary>
        private ElementId FindSymbolIdByReflection(GeometryInstance geometryInstance)
        {
            try
            {
                Type geometryInstanceType = geometryInstance.GetType();

                string[] possiblePropertyNames = {
                    "Symbol", "FamilySymbol", "SymbolId", "SymbolElement",
                    "GetSymbolId", "SymbolReference", "FamilySymbolId"
                };

                foreach (string propName in possiblePropertyNames)
                {
                    try
                    {
                        var property = geometryInstanceType.GetProperty(propName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (property != null)
                        {
                            object value = property.GetValue(geometryInstance);
                            if (value is ElementId elementId && elementId != ElementId.InvalidElementId)
                            {
                                return elementId;
                            }
                            else if (value is Element symbolElement)
                            {
                                return symbolElement.Id;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                // Пробуем методы
                string[] possibleMethodNames = {
                    "GetSymbol", "GetSymbolId", "GetFamilySymbol"
                };

                foreach (string methodName in possibleMethodNames)
                {
                    try
                    {
                        var method = geometryInstanceType.GetMethod(methodName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                            null, Type.EmptyTypes, null);

                        if (method != null)
                        {
                            object result = method.Invoke(geometryInstance, null);
                            if (result is ElementId elementId && elementId != ElementId.InvalidElementId)
                            {
                                return elementId;
                            }
                            else if (result is Element symbolElement)
                            {
                                return symbolElement.Id;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
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
        /// Получает ID элементов из XML с разделением на две коллизии
        /// </summary>
        private void GetCollisionIdsFromXml(XmlNode clashNode, out List<int> collision1Ids, out List<int> collision2Ids)
        {
            collision1Ids = new List<int>();
            collision2Ids = new List<int>();

            // Ищем элементы clashobject в XML
            var clashObjects = clashNode.SelectNodes(".//clashobject");

            if (clashObjects != null && clashObjects.Count >= 2)
            {
                // Первый clashobject - первая коллизия
                ParseClashObjectIds(clashObjects[0], collision1Ids);

                // Второй clashobject - вторая коллизия  
                ParseClashObjectIds(clashObjects[1], collision2Ids);
            }
            else
            {
                // Альтернативный поиск по различным структурам XML
                TryAlternativeXmlParsing(clashNode, collision1Ids, collision2Ids);
            }

            Debug.WriteLine($"XML parsing: Collision1 IDs: [{string.Join(", ", collision1Ids)}], Collision2 IDs: [{string.Join(", ", collision2Ids)}]");
        }

        /// <summary>
        /// Парсит ID из clashobject узла
        /// </summary>
        private void ParseClashObjectIds(XmlNode clashObjectNode, List<int> ids)
        {
            if (clashObjectNode == null) return;

            // Ищем значения в различных возможных структурах
            var valueNodes = clashObjectNode.SelectNodes(".//value");
            if (valueNodes != null)
            {
                foreach (XmlNode valueNode in valueNodes)
                {
                    string valueText = valueNode.InnerText.Trim();
                    if (int.TryParse(valueText, out int id) && id > 0)
                    {
                        ids.Add(id);
                    }
                }
            }

            // Альтернативный поиск по атрибутам
            var attributes = clashObjectNode.Attributes;
            if (attributes != null)
            {
                foreach (XmlAttribute attr in attributes)
                {
                    if (int.TryParse(attr.Value, out int id) && id > 0)
                    {
                        ids.Add(id);
                    }
                }
            }
        }

        /// <summary>
        /// Альтернативные методы парсинга XML структуры
        /// </summary>
        private void TryAlternativeXmlParsing(XmlNode clashNode, List<int> collision1Ids, List<int> collision2Ids)
        {
            // Попробуем найти по различным XPath выражениям
            string[] possibleXPaths = {
                ".//clashobject1", ".//clashobject2",
                ".//object1", ".//object2",
                ".//item1", ".//item2",
                ".//leftobject", ".//rightobject",
                ".//first", ".//second"
            };

            for (int i = 0; i < possibleXPaths.Length; i += 2)
            {
                var node1 = clashNode.SelectSingleNode(possibleXPaths[i]);
                var node2 = clashNode.SelectSingleNode(possibleXPaths[i + 1]);

                if (node1 != null && node2 != null)
                {
                    ParseClashObjectIds(node1, collision1Ids);
                    ParseClashObjectIds(node2, collision2Ids);
                    return;
                }
            }

            // Если не нашли разделение, пробуем разделить пополам все найденные ID
            var allIds = GetAllIdsFromClashNode(clashNode);
            int half = allIds.Count / 2;
            collision1Ids.AddRange(allIds.Take(half));
            collision2Ids.AddRange(allIds.Skip(half));
        }

        /// <summary>
        /// Получает все ID из узла коллизии
        /// </summary>
        private List<int> GetAllIdsFromClashNode(XmlNode clashNode)
        {
            List<int> ids = new List<int>();
            var valueNodes = clashNode.SelectNodes(".//value");
            if (valueNodes != null)
            {
                foreach (XmlNode valueNode in valueNodes)
                {
                    string valueText = valueNode.InnerText.Trim();
                    if (int.TryParse(valueText, out int id) && id > 0)
                        ids.Add(id);
                }
            }
            return ids;
        }

        /// <summary>
        /// 3. Поиск элементов для коллизии с правильным разделением
        /// </summary>
        private List<Element> FindElementsForCollision(XmlNode clashNode, out List<Element> collision1Elements, out List<Element> collision2Elements)
        {
            // Получаем ID с разделением из XML
            List<int> collision1Ids, collision2Ids;
            GetCollisionIdsFromXml(clashNode, out collision1Ids, out collision2Ids);

            var allCollisionElements = new List<Element>();
            collision1Elements = new List<Element>();
            collision2Elements = new List<Element>();

            Debug.WriteLine($"Поиск элементов: Коллизия1 IDs: {collision1Ids.Count}, Коллизия2 IDs: {collision2Ids.Count}");

            // Ищем элементы для первой коллизии
            foreach (int reportId in collision1Ids)
            {
                var elements = FindElementsById(reportId);
                foreach (var element in elements)
                {
                    if (!allCollisionElements.Any(e => e.Id == element.Id))
                    {
                        allCollisionElements.Add(element);
                        collision1Elements.Add(element);
                    }
                }
            }

            // Ищем элементы для второй коллизии
            foreach (int reportId in collision2Ids)
            {
                var elements = FindElementsById(reportId);
                foreach (var element in elements)
                {
                    if (!allCollisionElements.Any(e => e.Id == element.Id))
                    {
                        allCollisionElements.Add(element);
                        collision2Elements.Add(element);
                    }
                }
            }

            Debug.WriteLine($"Найдено элементов: Всего {allCollisionElements.Count}, Коллизия1: {collision1Elements.Count}, Коллизия2: {collision2Elements.Count}");

            return allCollisionElements;
        }

        /// <summary>
        /// Поиск элементов по ID (в базе данных или напрямую)
        /// </summary>
        private List<Element> FindElementsById(int reportId)
        {
            var result = new List<Element>();

            Debug.WriteLine($"  Поиск ID {reportId}:");

            // Сначала ищем в базе по FamilySymbolId
            if (_symbolToElementsMap.ContainsKey(reportId))
            {
                var elements = _symbolToElementsMap[reportId];
                result.AddRange(elements);
                Debug.WriteLine($"    Найден как FamilySymbolId -> {elements.Count} элементов");
            }

            // Если не найден как FamilySymbolId, ищем напрямую как ElementId
            if (result.Count == 0)
            {
                Element directElement = _doc.GetElement(new ElementId(reportId));
                if (directElement != null)
                {
                    result.Add(directElement);
                    Debug.WriteLine($"    Найден как прямой ElementId");
                }
            }

            if (result.Count == 0)
            {
                Debug.WriteLine($"    Не найден в базе (возможно, в связанной модели)");
            }

            return result;
        }

        /// <summary>
        /// Создание 3D вида для коллизии
        /// </summary>
        private bool CreateCollisionView(string clashName, List<Element> allElements, List<Element> collision1Elements, List<Element> collision2Elements, List<int> reportIds, ViewFamilyType viewFamilyType, ElementId solidFillPatternId)
        {
            try
            {
                View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                // Формируем имя вида с ID
                string ids = string.Join("_", reportIds.Take(3));
                string collisionInfo = "Коллизия ";

                string viewName = $"{clashName}_{collisionInfo}_{ids}";
                if (viewName.Length > 200)
                    viewName = viewName.Substring(0, 200);

                view.Name = viewName;
                view.DisplayStyle = DisplayStyle.Shading;
                view.DetailLevel = ViewDetailLevel.Fine;
                view.Discipline = ViewDiscipline.Coordination;

                // Устанавливаем секционную коробку
                BoundingBoxXYZ sectionBox = CreateSectionBoxFromElements(allElements);
                if (sectionBox != null)
                    view.SetSectionBox(sectionBox);

                // Применяем графические настройки с разными цветами
                ApplyColorOverrides(view, collision1Elements, collision2Elements, solidFillPatternId);

                _createdViews.Add(view);

                Debug.WriteLine($"Создан вид: {viewName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка создания вида: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Применение графических настроек с разными цветами для двух коллизий
        /// </summary>
        private void ApplyColorOverrides(View3D view, List<Element> collision1Elements, List<Element> collision2Elements, ElementId solidFillPatternId)
        {
            // Конвертируем WPF цвета в Revit цвета
            byte r1 = _collision1Color.R;
            byte g1 = _collision1Color.G;
            byte b1 = _collision1Color.B;

            byte r2 = _collision2Color.R;
            byte g2 = _collision2Color.G;
            byte b2 = _collision2Color.B;

            // Настройки для элементов первой коллизии
            OverrideGraphicSettings collision1Ogs = new OverrideGraphicSettings();
            collision1Ogs.SetProjectionLineColor(new Autodesk.Revit.DB.Color(r1, g1, b1));
            collision1Ogs.SetProjectionLineWeight(5);
            collision1Ogs.SetSurfaceForegroundPatternId(solidFillPatternId);
            collision1Ogs.SetSurfaceForegroundPatternColor(new Autodesk.Revit.DB.Color(r1, g1, b1));
            collision1Ogs.SetSurfaceTransparency(0);

            // Настройки для элементов второй коллизии
            OverrideGraphicSettings collision2Ogs = new OverrideGraphicSettings();
            collision2Ogs.SetProjectionLineColor(new Autodesk.Revit.DB.Color(r2, g2, b2));
            collision2Ogs.SetProjectionLineWeight(5);
            collision2Ogs.SetSurfaceForegroundPatternId(solidFillPatternId);
            collision2Ogs.SetSurfaceForegroundPatternColor(new Autodesk.Revit.DB.Color(r2, g2, b2));
            collision2Ogs.SetSurfaceTransparency(0);

            // Настройки для остальных элементов (прозрачность)
            OverrideGraphicSettings transparentOgs = new OverrideGraphicSettings();
            transparentOgs.SetProjectionLineColor(new Autodesk.Revit.DB.Color(128, 128, 128));
            transparentOgs.SetProjectionLineWeight(1);
            transparentOgs.SetSurfaceForegroundPatternId(solidFillPatternId);
            transparentOgs.SetSurfaceForegroundPatternColor(new Autodesk.Revit.DB.Color(200, 200, 200));
            transparentOgs.SetSurfaceTransparency((byte)_transparency);

            // Получаем все элементы в секционной коробке
            var allElementsInView = new FilteredElementCollector(_doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            // Применяем настройки ко всем элементам в виде
            foreach (Element element in allElementsInView)
            {
                try
                {
                    // Если элемент из первой коллизии
                    if (collision1Elements.Any(e => e.Id == element.Id))
                    {
                        view.SetElementOverrides(element.Id, collision1Ogs);
                    }
                    // Если элемент из второй коллизии
                    else if (collision2Elements.Any(e => e.Id == element.Id))
                    {
                        view.SetElementOverrides(element.Id, collision2Ogs);
                    }
                    // Остальные элементы - прозрачность
                    else
                    {
                        view.SetElementOverrides(element.Id, transparentOgs);
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }

            Debug.WriteLine($"Раскрашено: {collision1Elements.Count} элементов цветом 1, {collision2Elements.Count} элементов цветом 2, {allElementsInView.Count - collision1Elements.Count - collision2Elements.Count} прозрачных");
        }

        private BoundingBoxXYZ CreateSectionBoxFromElements(List<Element> elements)
        {
            List<BoundingBoxXYZ> boxes = new List<BoundingBoxXYZ>();
            foreach (var el in elements)
            {
                var bb = el.get_BoundingBox(null);
                if (bb != null)
                    boxes.Add(bb);
            }
            if (boxes.Count == 0) return null;

            double minX = boxes.Min(b => b.Min.X);
            double minY = boxes.Min(b => b.Min.Y);
            double minZ = boxes.Min(b => b.Min.Z);
            double maxX = boxes.Max(b => b.Max.X);
            double maxY = boxes.Max(b => b.Max.Y);
            double maxZ = boxes.Max(b => b.Max.Z);

            double offset = Math.Max((maxX - minX) * 0.2, 1.0);
            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX - offset, minY - offset, minZ - offset),
                Max = new XYZ(maxX + offset, maxY + offset, maxZ + offset)
            };
        }

        private ElementId GetSolidFillPatternId()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill)?.Id
                ?? ElementId.InvalidElementId;
        }
    }

    // WPF окно настроек
    public partial class CollisionSettingsWpfWindow : Window
    {
        public Color Collision1Color { get; set; }
        public Color Collision2Color { get; set; }
        public int Transparency { get; set; }
        public string ReportFilePath { get; set; }

        private WpfLabel _lblTransparency;

        public CollisionSettingsWpfWindow()
        {
            InitializeComponent();

            // Устанавливаем значения по умолчанию
            Collision1Color = Colors.Orange;
            Collision2Color = Colors.Green;
            Transparency = 80;
            ReportFilePath = string.Empty;

            // Обновляем визуальные элементы
            UpdateColorDisplays();
            _lblTransparency.Content = $"Прозрачность: {100 - Transparency}%";
        }

        private void InitializeComponent()
        {
            this.Title = "Настройки коллизий";
            this.Width = 400;
            this.Height = 300;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;

            // Основной контейнер
            WpfStackPanel mainPanel = new WpfStackPanel();
            mainPanel.Margin = new Thickness(20);
            // Используем Margin вместо Spacing для .NET Framework 3.5
            // mainPanel.Spacing = 10; // Только для .NET Framework 4.0+

            // 1. Выбор файла отчета
            WpfGrid reportGrid = new WpfGrid();
            reportGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            reportGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            reportGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            reportGrid.Margin = new Thickness(0, 0, 0, 10); // Заменяем Spacing

            WpfLabel lblReport = new WpfLabel() { Content = "Отчет:", VerticalAlignment = VerticalAlignment.Center };
            WpfGrid.SetColumn(lblReport, 0);

            WpfTextBox txtReportPath = new WpfTextBox()
            {
                IsReadOnly = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            };
            txtReportPath.TextChanged += (s, e) => ReportFilePath = txtReportPath.Text;
            WpfGrid.SetColumn(txtReportPath, 1);

            WpfButton btnBrowse = new WpfButton()
            {
                Content = "Обзор...",
                Padding = new Thickness(10, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            btnBrowse.Click += (s, e) =>
            {
                // Используем WPF OpenFileDialog
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Выберите XML отчет Navisworks",
                    Filter = "XML files|*.xml",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    ReportFilePath = openFileDialog.FileName;
                    txtReportPath.Text = System.IO.Path.GetFileName(ReportFilePath);
                }
            };
            WpfGrid.SetColumn(btnBrowse, 2);

            reportGrid.Children.Add(lblReport);
            reportGrid.Children.Add(txtReportPath);
            reportGrid.Children.Add(btnBrowse);

            // 2. Настройка цвета коллизии 1
            WpfGrid color1Grid = new WpfGrid();
            color1Grid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            color1Grid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            color1Grid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            color1Grid.Margin = new Thickness(0, 0, 0, 10); // Заменяем Spacing

            WpfLabel lblColor1 = new WpfLabel() { Content = "Коллизия 1:", VerticalAlignment = VerticalAlignment.Center };
            WpfGrid.SetColumn(lblColor1, 0);

            WpfBorder color1Display = new WpfBorder()
            {
                Height = 25,
                CornerRadius = new CornerRadius(3),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Collision1Color)
            };
            color1Display.Tag = "color1";
            WpfGrid.SetColumn(color1Display, 1);

            WpfButton btnColor1 = new WpfButton()
            {
                Content = "Выбрать...",
                Padding = new Thickness(10, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            btnColor1.Tag = "color1";
            btnColor1.Click += ColorButton_Click;
            WpfGrid.SetColumn(btnColor1, 2);

            color1Grid.Children.Add(lblColor1);
            color1Grid.Children.Add(color1Display);
            color1Grid.Children.Add(btnColor1);

            // 3. Настройка цвета коллизии 2
            WpfGrid color2Grid = new WpfGrid();
            color2Grid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            color2Grid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            color2Grid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            color2Grid.Margin = new Thickness(0, 0, 0, 10); // Заменяем Spacing

            WpfLabel lblColor2 = new WpfLabel() { Content = "Коллизия 2:", VerticalAlignment = VerticalAlignment.Center };
            WpfGrid.SetColumn(lblColor2, 0);

            WpfBorder color2Display = new WpfBorder()
            {
                Height = 25,
                CornerRadius = new CornerRadius(3),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Collision2Color)
            };
            color2Display.Tag = "color2";
            WpfGrid.SetColumn(color2Display, 1);

            WpfButton btnColor2 = new WpfButton()
            {
                Content = "Выбрать...",
                Padding = new Thickness(10, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            btnColor2.Tag = "color2";
            btnColor2.Click += ColorButton_Click;
            WpfGrid.SetColumn(btnColor2, 2);

            color2Grid.Children.Add(lblColor2);
            color2Grid.Children.Add(color2Display);
            color2Grid.Children.Add(btnColor2);

            // 4. Настройка прозрачности
            WpfGrid transparencyGrid = new WpfGrid();
            transparencyGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            transparencyGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            transparencyGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            transparencyGrid.Margin = new Thickness(0, 0, 0, 10); // Заменяем Spacing

            _lblTransparency = new WpfLabel() { Content = "Прозрачность:", VerticalAlignment = VerticalAlignment.Center };
            WpfGrid.SetColumn(_lblTransparency, 0);

            WpfSlider transparencySlider = new WpfSlider()
            {
                Minimum = 0,
                Maximum = 100,
                Value = Transparency,
                Margin = new Thickness(5, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            transparencySlider.ValueChanged += (s, e) =>
            {
                Transparency = (int)transparencySlider.Value;
                _lblTransparency.Content = $"Прозрачность: {100 - Transparency}%";
            };
            WpfGrid.SetColumn(transparencySlider, 1);

            WpfButton btnTransparency = new WpfButton()
            {
                Content = "Сброс",
                Padding = new Thickness(10, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            btnTransparency.Click += (s, e) =>
            {
                Transparency = 80;
                transparencySlider.Value = Transparency;
                _lblTransparency.Content = $"Прозрачность: {100 - Transparency}%";
            };
            WpfGrid.SetColumn(btnTransparency, 2);

            transparencyGrid.Children.Add(_lblTransparency);
            transparencyGrid.Children.Add(transparencySlider);
            transparencyGrid.Children.Add(btnTransparency);

            // 5. Кнопки OK и Отмена
            WpfGrid buttonGrid = new WpfGrid();
            buttonGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            buttonGrid.ColumnDefinitions.Add(new WpfColumnDefinition() { Width = GridLength.Auto });
            buttonGrid.Margin = new Thickness(0, 20, 0, 0);

            WpfButton btnOK = new WpfButton()
            {
                Content = "OK",
                Padding = new Thickness(20, 5, 20, 5),
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            btnOK.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(ReportFilePath))
                {
                    MessageBox.Show("Пожалуйста, выберите файл отчета с коллизиями.", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DialogResult = true;
                Close();
            };
            WpfGrid.SetColumn(btnOK, 1);

            WpfButton btnCancel = new WpfButton()
            {
                Content = "Отмена",
                Padding = new Thickness(20, 5, 20, 5),
                IsCancel = true
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };
            WpfGrid.SetColumn(btnCancel, 2);

            buttonGrid.Children.Add(btnOK);
            buttonGrid.Children.Add(btnCancel);

            // Добавляем все элементы в основной панель
            mainPanel.Children.Add(reportGrid);
            mainPanel.Children.Add(color1Grid);
            mainPanel.Children.Add(color2Grid);
            mainPanel.Children.Add(transparencyGrid);
            mainPanel.Children.Add(buttonGrid);

            this.Content = mainPanel;
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            string colorType = ((WpfButton)sender).Tag as string;

            // Используем WinForms ColorDialog для выбора цвета
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorType == "color1")
                colorDialog.Color = System.Drawing.Color.FromArgb(Collision1Color.R, Collision1Color.G, Collision1Color.B);
            else
                colorDialog.Color = System.Drawing.Color.FromArgb(Collision2Color.R, Collision2Color.G, Collision2Color.B);

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                if (colorType == "color1")
                    Collision1Color = newColor;
                else
                    Collision2Color = newColor;

                UpdateColorDisplays();
            }
        }

        private void UpdateColorDisplays()
        {
            // Находим элементы Border по тегам и обновляем их цвет
            var mainPanel = this.Content as WpfStackPanel;
            if (mainPanel != null)
            {
                foreach (var grid in mainPanel.Children.OfType<WpfGrid>())
                {
                    foreach (var border in grid.Children.OfType<WpfBorder>())
                    {
                        if (border.Tag as string == "color1")
                            border.Background = new SolidColorBrush(Collision1Color);
                        else if (border.Tag as string == "color2")
                            border.Background = new SolidColorBrush(Collision2Color);
                    }
                }
            }
        }
    }

    // WPF окно прогресса
    public class ProgressWpfWindow : Window
    {
        public bool CancelRequested { get; private set; }
        private WpfProgressBar _progressBar;
        private WpfLabel _statusLabel;
        private WpfButton _cancelButton;

        public double ProgressValue
        {
            get => _progressBar.Value;
            set => _progressBar.Dispatcher.Invoke(() => _progressBar.Value = value);
        }

        public string StatusText
        {
            get => _statusLabel.Content as string;
            set => _statusLabel.Dispatcher.Invoke(() => _statusLabel.Content = value);
        }

        public ProgressWpfWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Title = "Обработка коллизий";
            this.Width = 400;
            this.Height = 150;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;
            this.Topmost = true;

            // Основной контейнер
            WpfStackPanel mainPanel = new WpfStackPanel();
            mainPanel.Margin = new Thickness(20);
            // Используем Margin вместо Spacing для .NET Framework 3.5
            // mainPanel.Spacing = 15; // Только для .NET Framework 4.0+

            // Текст статуса
            _statusLabel = new WpfLabel()
            {
                Content = "Подготовка...",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 15) // Заменяем Spacing
            };

            // Прогресс-бар
            _progressBar = new WpfProgressBar()
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Margin = new Thickness(0, 0, 0, 15) // Заменяем Spacing
            };

            // Кнопка отмены
            _cancelButton = new WpfButton()
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