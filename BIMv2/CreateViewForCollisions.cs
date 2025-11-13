#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
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
        private List<ElementId> _allElementsToColor = new List<ElementId>(); // Все элементы для раскраски

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;
            Stopwatch timer = Stopwatch.StartNew();

            ProgressForm2 progressForm = null;

            try
            {
                ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                ElementId solidFillPatternId = GetSolidFillPatternId();

                OpenFileDialog ofd = new OpenFileDialog
                {
                    Title = "Выберите XML отчет Navisworks",
                    Filter = "XML files|*.xml"
                };
                if (ofd.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                XmlDocument xml = new XmlDocument();
                xml.Load(ofd.FileName);

                var clashResults = xml.SelectNodes("//clashresult");
                if (clashResults == null || clashResults.Count == 0)
                {
                    TaskDialog.Show("Информация", "В отчёте не найдено коллизий.");
                    return Result.Cancelled;
                }

                progressForm = new ProgressForm2();
                progressForm.Show();
                progressForm.ProgressBar.Maximum = clashResults.Count;
                progressForm.StatusLabel.Text = "Начало обработки...";
                Application.DoEvents();

                int viewCount = 0;
                int processed = 0;
                StringBuilder log = new StringBuilder();

                using (Transaction t = new Transaction(_doc, "Создание видов коллизий"))
                {
                    t.Start();

                    foreach (XmlNode clash in clashResults)
                    {
                        processed++;

                        if (progressForm.CancelRequested)
                        {
                            progressForm.StatusLabel.Text = "Отмена пользователем...";
                            Application.DoEvents();
                            break;
                        }

                        progressForm.StatusLabel.Text = $"Обработка {processed} из {clashResults.Count}";
                        progressForm.ProgressBar.Value = processed;
                        Application.DoEvents();

                        string name = clash.Attributes["name"]?.Value ?? "Без_имени";

                        // Получаем все ID из отчета
                        List<int> allIds = GetAllIdsFromClashNode(clash);
                        log.AppendLine($"🔍 {name}: Найдено ID в отчете: [{string.Join(",", allIds)}]");

                        if (allIds.Count == 0)
                        {
                            log.AppendLine($"⚠️ {name}: не найдено ID");
                            continue;
                        }

                        // Сбрасываем список элементов для раскраски для каждого конфликта
                        _allElementsToColor.Clear();

                        // Ищем элементы по новой логике
                        List<Element> foundElements = FindElementsWithLogic(allIds, log, name);

                        if (foundElements.Count == 0)
                        {
                            log.AppendLine($"⚠️ {name}: не найдено элементов");
                            continue;
                        }

                        // Создаем вид
                        if (CreateCollisionView(name, foundElements, viewFamilyType, solidFillPatternId, log))
                        {
                            viewCount++;
                        }
                    }

                    t.Commit();
                }

                timer.Stop();
                progressForm.Close();

                ShowFinalResults(viewCount, timer.Elapsed, log);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                if (progressForm != null)
                    progressForm.Close();

                TaskDialog.Show("Ошибка", ex.Message + "\n" + ex.StackTrace);
                return Result.Failed;
            }
        }

        #region 🔍 Логика поиска элементов

        private List<Element> FindElementsWithLogic(List<int> allIds, StringBuilder log, string clashName)
        {
            List<Element> foundElements = new List<Element>();
            List<int> remainingIds = new List<int>(allIds);

            // ЭТАП 1: Поиск по прямому ElementId
            foreach (int id in allIds.ToList())
            {
                Element element = FindElementByDirectId(id);
                if (element != null)
                {
                    if (!foundElements.Any(e => e.Id.IntegerValue == element.Id.IntegerValue))
                    {
                        foundElements.Add(element);
                        _allElementsToColor.Add(element.Id); // Добавляем для раскраски
                        remainingIds.Remove(id);
                        log.AppendLine($"✅ {clashName}: найден элемент по прямому ID {id} -> {element.Id.IntegerValue} ({element.Category?.Name})");
                    }
                }
            }

            // ЭТАП 2: Для оставшихся ID ищем как ID геометрии
            foreach (int geometryId in remainingIds.ToList())
            {
                List<Element> elementsByGeometry = FindElementsByGeometryId(geometryId);
                if (elementsByGeometry.Count > 0)
                {
                    foreach (Element element in elementsByGeometry)
                    {
                        if (!foundElements.Any(e => e.Id.IntegerValue == element.Id.IntegerValue))
                        {
                            foundElements.Add(element);
                            _allElementsToColor.Add(element.Id); // Добавляем для раскраски
                            log.AppendLine($"✅ {clashName}: найден элемент по ID геометрии {geometryId} -> {element.Id.IntegerValue} ({element.Category?.Name})");
                        }
                    }
                    remainingIds.Remove(geometryId);
                }
            }

            // ЭТАП 3: Поиск по GeometryInstance Symbol ID
            foreach (int symbolId in remainingIds.ToList())
            {
                List<Element> symbolElements = FindElementsBySymbolId(symbolId);
                if (symbolElements.Count > 0)
                {
                    // Для Symbol ID получаем ВСЕ экземпляры этого семейства
                    List<Element> allInstances = GetAllInstancesForSymbol(symbolElements, log, clashName);

                    // ВАЖНО: добавляем ВСЕ экземпляры в foundElements и для раскраски
                    foreach (Element element in allInstances)
                    {
                        if (!foundElements.Any(e => e.Id.IntegerValue == element.Id.IntegerValue))
                        {
                            foundElements.Add(element);
                            log.AppendLine($"✅ {clashName}: найден элемент по Symbol ID {symbolId} -> {element.Id.IntegerValue} ({element.Category?.Name}) [экземпляр]");
                        }

                        // Добавляем ВСЕ экземпляры в список для раскраски
                        if (!_allElementsToColor.Contains(element.Id))
                        {
                            _allElementsToColor.Add(element.Id);
                            log.AppendLine($"🎨 {clashName}: добавлен для раскраски элемент по Symbol ID {symbolId} -> {element.Id.IntegerValue} ({element.Category?.Name})");
                        }
                    }

                    if (allInstances.Count > 0)
                    {
                        log.AppendLine($"📦 {clashName}: для Symbol ID {symbolId} найдено {allInstances.Count} экземпляров, раскрашено {allInstances.Count}");
                    }
                    else
                    {
                        log.AppendLine($"⚠️ {clashName}: для Symbol ID {symbolId} не найдено экземпляров, но найден символ");
                        // Если не найдено экземпляров, добавляем сам символ для раскраски
                        foreach (Element symbolElement in symbolElements)
                        {
                            if (!_allElementsToColor.Contains(symbolElement.Id))
                            {
                                _allElementsToColor.Add(symbolElement.Id);
                                log.AppendLine($"🎨 {clashName}: добавлен САМ Symbol элемент {symbolElement.Id} для раскраски");
                            }
                        }
                    }

                    remainingIds.Remove(symbolId);
                }
            }

            // ЭТАП 4: Если остались ненайденные ID - логируем
            if (remainingIds.Count > 0)
            {
                log.AppendLine($"ℹ️ {clashName}: не найдены элементы для ID: [{string.Join(",", remainingIds)}] (возможно в связанных моделях)");
            }

            log.AppendLine($"📊 {clashName}: итого найдено {foundElements.Count} элементов, для раскраски {_allElementsToColor.Count} элементов");

            return foundElements;
        }

        private List<Element> GetAllInstancesForSymbol(List<Element> symbolElements, StringBuilder log, string clashName)
        {
            List<Element> allInstances = new List<Element>();

            foreach (Element symbolElement in symbolElements)
            {
                if (symbolElement is FamilySymbol familySymbol)
                {
                    // Получаем все экземпляры этого семейства
                    List<Element> instances = GetInstancesOfFamilySymbol(familySymbol);
                    allInstances.AddRange(instances);

                    log.AppendLine($"📦 {clashName}: для символа {familySymbol.Name} найдено {instances.Count} экземпляров");

                    // Отладочная информация
                    foreach (Element instance in instances.Take(10)) // Ограничиваем вывод чтобы лог не был слишком большим
                    {
                        log.AppendLine($"   📍 Экземпляр: {instance.Id} ({instance.Category?.Name})");
                    }
                    if (instances.Count > 10)
                    {
                        log.AppendLine($"   ... и еще {instances.Count - 10} экземпляров");
                    }
                }
                else if (symbolElement is FamilyInstance familyInstance)
                {
                    // Если это экземпляр, получаем его символ и все экземпляры
                    FamilySymbol symbol = familyInstance.Symbol;
                    List<Element> instances = GetInstancesOfFamilySymbol(symbol);
                    allInstances.AddRange(instances);

                    log.AppendLine($"📦 {clashName}: для символа {symbol.Name} найдено {instances.Count} экземпляров");

                    // Отладочная информация
                    foreach (Element instance in instances.Take(10))
                    {
                        log.AppendLine($"   📍 Экземпляр: {instance.Id} ({instance.Category?.Name})");
                    }
                    if (instances.Count > 10)
                    {
                        log.AppendLine($"   ... и еще {instances.Count - 10} экземпляров");
                    }
                }
            }

            var distinctInstances = allInstances.Distinct(new ElementIdComparer()).ToList();
            log.AppendLine($"📦 {clashName}: всего уникальных экземпляров: {distinctInstances.Count}");

            return distinctInstances;
        }

        private List<Element> GetInstancesOfFamilySymbol(FamilySymbol familySymbol)
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.Id == familySymbol.Id)
                    .Cast<Element>()
                    .ToList();
            }
            catch
            {
                return new List<Element>();
            }
        }

        private Element FindElementByDirectId(int elementId)
        {
            try
            {
                ElementId id = new ElementId(elementId);
                Element element = _doc.GetElement(id);
                return element;
            }
            catch
            {
                return null;
            }
        }

        private List<Element> FindElementsByGeometryId(int geometryId)
        {
            List<Element> foundElements = new List<Element>();

            // Используем FilteredElementCollector для поиска по всем элементам
            var allElements = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element element in allElements)
            {
                try
                {
                    if (HasGeometryWithId(element, geometryId))
                    {
                        foundElements.Add(element);
                    }
                }
                catch
                {
                    // Игнорируем ошибки при проверке геометрии
                }
            }

            return foundElements;
        }

        private List<Element> FindElementsBySymbolId(int symbolId)
        {
            List<Element> foundElements = new List<Element>();

            // Используем FilteredElementCollector для поиска по всем элементам
            var allElements = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element element in allElements)
            {
                try
                {
                    if (HasSymbolWithId(element, symbolId))
                    {
                        foundElements.Add(element);
                    }
                }
                catch
                {
                    // Игнорируем ошибки при проверке символов
                }
            }

            return foundElements;
        }

        private bool HasGeometryWithId(Element element, int geometryId)
        {
            try
            {
                Options geomOptions = new Options
                {
                    ComputeReferences = true,
                    DetailLevel = ViewDetailLevel.Fine,
                    IncludeNonVisibleObjects = true
                };

                GeometryElement geometry = element.get_Geometry(geomOptions);
                if (geometry == null)
                    return false;

                return CheckGeometryForId(geometry, geometryId);
            }
            catch
            {
                return false;
            }
        }

        private bool HasSymbolWithId(Element element, int symbolId)
        {
            try
            {
                // Проверяем для FamilyInstance - ID символа
                if (element is FamilyInstance familyInstance)
                {
                    ElementId symbolElementId = familyInstance.Symbol.Id;
                    if (symbolElementId.IntegerValue == symbolId)
                        return true;
                }

                // Проверяем для FamilySymbol - прямой ID
                if (element is FamilySymbol familySymbol)
                {
                    if (familySymbol.Id.IntegerValue == symbolId)
                        return true;
                }

                // Также проверяем геометрию символа для FamilyInstance
                if (element is FamilyInstance familyInstance2)
                {
                    Options geomOptions = new Options
                    {
                        ComputeReferences = true,
                        DetailLevel = ViewDetailLevel.Fine,
                        IncludeNonVisibleObjects = true
                    };

                    GeometryElement symbolGeometry = familyInstance2.Symbol.get_Geometry(geomOptions);
                    if (symbolGeometry != null && CheckGeometryForId(symbolGeometry, symbolId))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool CheckGeometryForId(GeometryElement geometry, int targetId)
        {
            foreach (GeometryObject geomObj in geometry)
            {
                // Проверяем ID самого GeometryObject
                if (geomObj.Id == targetId)
                    return true;

                // Если это GeometryInstance, проверяем все виды геометрии
                if (geomObj is GeometryInstance geometryInstance)
                {
                    // Проверяем instance geometry (геометрию размещенного элемента)
                    GeometryElement instanceGeometry = geometryInstance.GetInstanceGeometry();
                    if (instanceGeometry != null && CheckGeometryForId(instanceGeometry, targetId))
                        return true;

                    // Проверяем symbol geometry (геометрию семейства)
                    GeometryElement symbolGeometry = geometryInstance.GetSymbolGeometry();
                    if (symbolGeometry != null && CheckGeometryForId(symbolGeometry, targetId))
                        return true;
                }

                // Если это Solid, проверяем его Faces и Edges
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face.Id == targetId)
                            return true;
                    }

                    foreach (Edge edge in solid.Edges)
                    {
                        if (edge.Id == targetId)
                            return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region 🎨 Создание видов

        private bool CreateCollisionView(string clashName, List<Element> elements, ViewFamilyType viewFamilyType, ElementId solidFillPatternId, StringBuilder log)
        {
            try
            {
                string collisionStatus = elements.Count == 1 ? "частичная_1элемент" : "коллизия";
                string categoriesInfo = GetCategoriesInfo(elements);

                View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                string idsString = string.Join("_", elements.Select(e => e.Id.IntegerValue));
                string viewName = $"{clashName}_{collisionStatus}_{categoriesInfo}_{idsString}";
                if (viewName.Length > 200)
                    viewName = viewName.Substring(0, 200);

                view.Name = viewName;
                ConfigureView(view); // Настройка стиля вида

                // Используем bounding box ВСЕХ найденных элементов
                BoundingBoxXYZ sectionBox = CreateSectionBoxFromElements(elements);
                if (sectionBox != null)
                    view.SetSectionBox(sectionBox);

                // Применяем графические настройки ко ВСЕМ элементам для раскраски
                ApplyCustomOverrides(view, _allElementsToColor, solidFillPatternId, log);

                _createdViews.Add(view);
                log.AppendLine($"✅ {clashName}: создан вид '{viewName}' для {elements.Count} элементов (раскрашено {_allElementsToColor.Count})");

                return true;
            }
            catch (Exception ex)
            {
                log.AppendLine($"❌ {clashName}: ошибка создания вида: {ex.Message}");
                return false;
            }
        }

        private void ConfigureView(View3D view)
        {
            // Настройка стиля отображения - Тонированный для отображения заливки
            view.DisplayStyle = DisplayStyle.Shading;

            // Дополнительные настройки
            view.Discipline = ViewDiscipline.Coordination;
            view.DetailLevel = ViewDetailLevel.Fine;
        }

        private void ApplyCustomOverrides(View3D view, List<ElementId> elementIds, ElementId solidFillPatternId, StringBuilder log)
        {
            if (elementIds.Count == 0) return;

            log.AppendLine($"🎨 Начало раскраски: всего элементов {elementIds.Count}");

            // Цвета для проблемных элементов
            Color[] problemColors = {
                new Color(0, 255, 0),   // Зеленый
                new Color(255, 165, 0)  // Оранжевый
            };

            int colorIndex = 0;
            int coloredCount = 0;
            int notFoundCount = 0;
            int errorCount = 0;

            foreach (ElementId id in elementIds)
            {
                Element el = _doc.GetElement(id);
                if (el == null)
                {
                    log.AppendLine($"⚠️ Элемент {id} не найден в документе");
                    notFoundCount++;
                    continue;
                }

                Color color = problemColors[colorIndex % problemColors.Length];

                // Настройки для проблемных элементов - цветная заливка
                OverrideGraphicSettings problemOgs = new OverrideGraphicSettings()
                    .SetProjectionLineColor(color) // Цвет контура
                    .SetProjectionLineWeight(4) // Толщина линии
                    .SetSurfaceForegroundPatternColor(color) // Цвет заливки
                    .SetSurfaceForegroundPatternId(solidFillPatternId) // Сплошная заливка
                    .SetSurfaceTransparency(0); // Полностью непрозрачно

                try
                {
                    view.SetElementOverrides(id, problemOgs);
                    coloredCount++;

                    string elementType = el is FamilyInstance ? "FamilyInstance" :
                                       el is FamilySymbol ? "FamilySymbol" :
                                       el.GetType().Name;

                    log.AppendLine($"🎨 Раскрашен элемент {id} ({el.Category?.Name}) [{elementType}] -> {GetColorName(color)}");
                }
                catch (Exception ex)
                {
                    log.AppendLine($"❌ Ошибка раскраски элемента {id}: {ex.Message}");
                    errorCount++;
                }

                colorIndex++;
            }

            log.AppendLine($"🎨 Итоги раскраски: успешно {coloredCount}, не найдено {notFoundCount}, ошибок {errorCount}, всего {elementIds.Count}");

            // Делаем все остальные элементы в виде полупрозрачными серыми
            //ApplyTransparencyToAllOtherElements(view, elementIds, solidFillPatternId);
        }

        private string GetColorName(Color color)
        {
            if (color.Red == 0 && color.Green == 255 && color.Blue == 0) return "Зеленый";
            if (color.Red == 255 && color.Green == 165 && color.Blue == 0) return "Оранжевый";
            return "Неизвестный";
        }

        private void ApplyTransparencyToAllOtherElements(View3D view, List<ElementId> excludedElementIds, ElementId solidFillPatternId)
        {
            try
            {
                // Настройки для всех остальных элементов - прозрачные серые
                OverrideGraphicSettings transparentOgs = new OverrideGraphicSettings()
                    .SetProjectionLineColor(new Color(150, 150, 150)) // Серый контур
                    .SetProjectionLineWeight(1) // Тонкие линии
                    .SetSurfaceForegroundPatternColor(new Color(200, 200, 200)) // Светло-серая заливка
                    .SetSurfaceForegroundPatternId(solidFillPatternId)
                    .SetSurfaceTransparency(70); // 70% прозрачность

                // Получаем все элементы в секционной коробке
                var allElementsInView = new FilteredElementCollector(_doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                int transparentCount = 0;
                foreach (ElementId elementId in allElementsInView)
                {
                    // Пропускаем наши целевые элементы
                    if (excludedElementIds.Contains(elementId))
                        continue;

                    // Применяем прозрачность к остальным элементам
                    view.SetElementOverrides(elementId, transparentOgs);
                    transparentCount++;
                }

                Debug.WriteLine($"Применена прозрачность к {transparentCount} элементам");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при применении прозрачности: {ex.Message}");
            }
        }

        // Остальные методы остаются без изменений...
        private BoundingBoxXYZ CreateSectionBoxFromElements(List<Element> elements)
        {
            try
            {
                List<BoundingBoxXYZ> allBoundingBoxes = new List<BoundingBoxXYZ>();

                foreach (Element element in elements)
                {
                    // Получаем bounding box элемента
                    List<BoundingBoxXYZ> elementBboxes = GetElementBoundingBoxes(element);
                    if (elementBboxes != null && elementBboxes.Count > 0)
                    {
                        allBoundingBoxes.AddRange(elementBboxes);
                    }
                }

                if (allBoundingBoxes.Count == 0)
                    return null;

                // Объединяем все bounding boxes
                return CombineBoundingBoxes(allBoundingBoxes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при создании SectionBox: {ex.Message}");
                return null;
            }
        }

        private List<BoundingBoxXYZ> GetElementBoundingBoxes(Element element)
        {
            List<BoundingBoxXYZ> boundingBoxes = new List<BoundingBoxXYZ>();

            try
            {
                // Для обычных элементов используем стандартный подход
                if (!(element is FamilySymbol))
                {
                    BoundingBoxXYZ bbox = GetSingleElementBoundingBox(element);
                    if (bbox != null)
                        boundingBoxes.Add(bbox);
                }
                else
                {
                    // Для FamilySymbol получаем bounding box всех его экземпляров
                    boundingBoxes.AddRange(GetAllInstancesBoundingBoxes(element as FamilySymbol));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при получении BoundingBox для элемента {element.Id}: {ex.Message}");
            }

            return boundingBoxes;
        }

        private BoundingBoxXYZ GetSingleElementBoundingBox(Element element)
        {
            try
            {
                // Пробуем получить bounding box элемента
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox != null) return bbox;

                // Если не получилось, пробуем через активный вид
                bbox = element.get_BoundingBox(_doc.ActiveView);
                if (bbox != null) return bbox;

                // Если все еще не получилось, используем геометрию элемента
                return GetBoundingBoxFromElementGeometry(element);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при получении BoundingBox для элемента {element.Id}: {ex.Message}");
                return null;
            }
        }

        private List<BoundingBoxXYZ> GetAllInstancesBoundingBoxes(FamilySymbol familySymbol)
        {
            List<BoundingBoxXYZ> allBoundingBoxes = new List<BoundingBoxXYZ>();

            try
            {
                // Получаем все экземпляры этого символа
                var instances = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.Id == familySymbol.Id)
                    .ToList();

                foreach (FamilyInstance instance in instances)
                {
                    BoundingBoxXYZ bbox = GetSingleElementBoundingBox(instance);
                    if (bbox != null)
                    {
                        allBoundingBoxes.Add(bbox);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при получении BoundingBox для экземпляров символа {familySymbol.Name}: {ex.Message}");
            }

            return allBoundingBoxes;
        }

        private BoundingBoxXYZ GetBoundingBoxFromElementGeometry(Element element)
        {
            try
            {
                Options options = new Options
                {
                    ComputeReferences = true,
                    DetailLevel = ViewDetailLevel.Fine,
                    IncludeNonVisibleObjects = true
                };

                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null)
                    return null;

                List<BoundingBoxXYZ> allBoxes = new List<BoundingBoxXYZ>();

                foreach (GeometryObject geomObj in geometry)
                {
                    BoundingBoxXYZ bbox = null;

                    // Для GeometryInstance используем instance geometry с трансформацией
                    if (geomObj is GeometryInstance geometryInstance)
                    {
                        GeometryElement instanceGeometry = geometryInstance.GetInstanceGeometry();
                        if (instanceGeometry != null)
                        {
                            bbox = GetBoundingBoxFromGeometryElement(instanceGeometry, geometryInstance.Transform);
                        }
                    }
                    // Для Solid получаем bounding box напрямую
                    else if (geomObj is Solid solid && solid.Volume > 0.001)
                    {
                        bbox = solid.GetBoundingBox();
                    }
                    // Для Mesh вычисляем bounding box по вершинам
                    else if (geomObj is Mesh mesh && mesh.Vertices.Count > 0)
                    {
                        bbox = GetMeshBoundingBox(mesh, Transform.Identity);
                    }

                    if (bbox != null)
                    {
                        allBoxes.Add(bbox);
                    }
                }

                return allBoxes.Count > 0 ? CombineBoundingBoxes(allBoxes) : null;
            }
            catch
            {
                return null;
            }
        }

        private BoundingBoxXYZ GetBoundingBoxFromGeometryElement(GeometryElement geometry, Transform transform)
        {
            List<BoundingBoxXYZ> boxes = new List<BoundingBoxXYZ>();

            foreach (GeometryObject geomObj in geometry)
            {
                BoundingBoxXYZ bbox = null;

                if (geomObj is Solid solid && solid.Volume > 0.001)
                {
                    bbox = solid.GetBoundingBox();
                }
                else if (geomObj is Mesh mesh && mesh.Vertices.Count > 0)
                {
                    bbox = GetMeshBoundingBox(mesh, Transform.Identity);
                }
                else if (geomObj is GeometryInstance nestedInstance)
                {
                    // Рекурсивно обрабатываем вложенные GeometryInstance
                    GeometryElement nestedInstanceGeometry = nestedInstance.GetInstanceGeometry();
                    if (nestedInstanceGeometry != null)
                    {
                        Transform combinedTransform = transform.Multiply(nestedInstance.Transform);
                        bbox = GetBoundingBoxFromGeometryElement(nestedInstanceGeometry, combinedTransform);
                    }
                }

                if (bbox != null)
                {
                    // Применяем трансформацию к bounding box
                    if (!transform.IsIdentity)
                    {
                        bbox = TransformBoundingBox(bbox, transform);
                    }
                    boxes.Add(bbox);
                }
            }

            return boxes.Count > 0 ? CombineBoundingBoxes(boxes) : null;
        }

        private BoundingBoxXYZ GetMeshBoundingBox(Mesh mesh, Transform transform)
        {
            if (mesh.Vertices.Count == 0)
                return null;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (XYZ vertex in mesh.Vertices)
            {
                XYZ transformedVertex = transform.OfPoint(vertex);
                minX = Math.Min(minX, transformedVertex.X);
                minY = Math.Min(minY, transformedVertex.Y);
                minZ = Math.Min(minZ, transformedVertex.Z);
                maxX = Math.Max(maxX, transformedVertex.X);
                maxY = Math.Max(maxY, transformedVertex.Y);
                maxZ = Math.Max(maxZ, transformedVertex.Z);
            }

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }

        private BoundingBoxXYZ TransformBoundingBox(BoundingBoxXYZ bbox, Transform transform)
        {
            if (transform.IsIdentity)
                return bbox;

            XYZ min = transform.OfPoint(bbox.Min);
            XYZ max = transform.OfPoint(bbox.Max);

            return new BoundingBoxXYZ
            {
                Min = new XYZ(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z)),
                Max = new XYZ(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z))
            };
        }

        private BoundingBoxXYZ CombineBoundingBoxes(List<BoundingBoxXYZ> boundingBoxes)
        {
            if (boundingBoxes.Count == 0)
                return null;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (BoundingBoxXYZ bbox in boundingBoxes)
            {
                if (bbox == null) continue;

                minX = Math.Min(minX, bbox.Min.X);
                minY = Math.Min(minY, bbox.Min.Y);
                minZ = Math.Min(minZ, bbox.Min.Z);
                maxX = Math.Max(maxX, bbox.Max.X);
                maxY = Math.Max(maxY, bbox.Max.Y);
                maxZ = Math.Max(maxZ, bbox.Max.Z);
            }

            double padding = Math.Max(Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)) * 0.2, 1.0);

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX - padding, minY - padding, minZ - padding),
                Max = new XYZ(maxX + padding, maxY + padding, maxZ + padding)
            };
        }

        #endregion

        #region 🔧 Вспомогательные методы

        private List<int> GetAllIdsFromClashNode(XmlNode clashNode)
        {
            List<int> ids = new List<int>();

            try
            {
                var valueNodes = clashNode.SelectNodes(".//value");
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка извлечения ID: {ex.Message}");
            }

            return ids;
        }

        private void ShowFinalResults(int viewCount, TimeSpan elapsed, StringBuilder log)
        {
            string resultMessage = $"Создано {viewCount} 3D видов\nВремя: {elapsed:mm\\:ss}";

            if (viewCount > 0)
            {
                DialogResult openViewResult = MessageBox.Show(
                    $"{resultMessage}\n\nХотите открыть один из созданных видов?",
                    "Создание видов завершено",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (openViewResult == DialogResult.Yes)
                {
                    OpenCreatedView();
                }
            }

            // Всегда показываем детальный лог
            string detailedLog = resultMessage + "\n\nДетали обработки:\n" + log.ToString();
            TaskDialog.Show("Результат", detailedLog);
        }

        private void OpenCreatedView()
        {
            try
            {
                if (_createdViews == null || _createdViews.Count == 0) return;

                if (_createdViews.Count == 1)
                {
                    _uiDoc.ActiveView = _createdViews[0];
                    return;
                }

                using (System.Windows.Forms.Form selectionForm = new System.Windows.Forms.Form())
                {
                    selectionForm.Text = "Выберите вид для открытия";
                    selectionForm.Size = new System.Drawing.Size(400, 300);
                    selectionForm.StartPosition = FormStartPosition.CenterScreen;

                    ListBox listBox = new ListBox() { Dock = DockStyle.Fill };
                    foreach (View3D view in _createdViews)
                        listBox.Items.Add(view.Name);
                    listBox.SelectedIndex = 0;

                    Button okButton = new Button() { Text = "Открыть", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 30 };
                    Button cancelButton = new Button() { Text = "Отмена", DialogResult = DialogResult.Cancel, Dock = DockStyle.Bottom, Height = 30 };

                    selectionForm.Controls.Add(listBox);
                    selectionForm.Controls.Add(okButton);
                    selectionForm.Controls.Add(cancelButton);

                    if (selectionForm.ShowDialog() == DialogResult.OK && listBox.SelectedIndex >= 0)
                    {
                        _uiDoc.ActiveView = _createdViews[listBox.SelectedIndex];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка открытия вида: {ex.Message}");
            }
        }

        private ElementId GetSolidFillPatternId()
        {
            foreach (FillPatternElement fpe in new FilteredElementCollector(_doc).OfClass(typeof(FillPatternElement)))
                if (fpe.GetFillPattern().IsSolidFill)
                    return fpe.Id;
            return ElementId.InvalidElementId;
        }

        private string GetCategoriesInfo(List<Element> elements)
        {
            var categories = elements.Where(e => e?.Category != null)
                .Select(e => e.Category.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (categories.Count == 0) return "Unknown";
            if (categories.Count == 1) return categories[0];
            if (categories.Count == 2) return $"{categories[0]}_vs_{categories[1]}";
            return string.Join("_", categories.Take(3)) + "_etc";
        }

        // Компаратор для сравнения элементов по ID
        private class ElementIdComparer : IEqualityComparer<Element>
        {
            public bool Equals(Element x, Element y)
            {
                return x?.Id.IntegerValue == y?.Id.IntegerValue;
            }

            public int GetHashCode(Element obj)
            {
                return obj.Id.IntegerValue.GetHashCode();
            }
        }

        #endregion
    }
}