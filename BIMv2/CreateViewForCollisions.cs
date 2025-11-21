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
using System.IO;
using System.Text;
using System.Reflection;
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
        private System.Drawing.Color _collision1Color = System.Drawing.Color.Orange;
        private System.Drawing.Color _collision2Color = System.Drawing.Color.Green;
        private int _transparency = 80;
        private string _reportFilePath = string.Empty;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

            try
            {
                // Показываем форму настроек
                using (var settingsForm = new CollisionSettingsForm())
                {
                    // Устанавливаем начальные значения
                    settingsForm.Collision1Color = _collision1Color;
                    settingsForm.Collision2Color = _collision2Color;
                    settingsForm.Transparency = _transparency;

                    if (settingsForm.ShowDialog() != DialogResult.OK)
                        return Result.Cancelled;

                    // Получаем настройки из формы через свойства
                    _collision1Color = settingsForm.Collision1Color;
                    _collision2Color = settingsForm.Collision2Color;
                    _transparency = settingsForm.Transparency;
                    _reportFilePath = settingsForm.ReportFilePath;

                    if (string.IsNullOrEmpty(_reportFilePath))
                    {
                        TaskDialog.Show("Ошибка", "Файл отчета не выбран.");
                        return Result.Cancelled;
                    }

                    ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                    ElementId solidFillPatternId = GetSolidFillPatternId();

                // 1. Открываем отчет с коллизиями
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

                // 2. Создаем базу данных обобщенных моделей, дверей, окон, импостов витража, панелей витража и соединительных деталей
                BuildExtendedElementsDatabase();

                int viewCount = 0;

                        using (Transaction t = new Transaction(_doc, "Создание видов коллизий"))
                        {
                            t.Start();

                    // 3. Проверяем каждый конфликт из отчета
                    foreach (XmlNode clash in clashResults)
                    {
                        string name = clash.Attributes["name"]?.Value ?? "Без_имени";
                        List<int> reportIds = GetAllIdsFromClashNode(clash);

                        if (reportIds.Count == 0)
                            continue;

                        Debug.WriteLine($"\n=== ОБРАБОТКА КОЛЛИЗИИ: {name} ===");
                        Debug.WriteLine($"ID в отчете: [{string.Join(", ", reportIds)}]");

                        // Ищем элементы для этой коллизии
                        List<Element> collisionElements = FindElementsForCollision(reportIds);

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

                TaskDialog.Show("Результат", $"Создано {viewCount} 3D видов для коллизий");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
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
        /// 3. Поиск элементов для коллизии
        /// </summary>
        private List<Element> FindElementsForCollision(List<int> reportIds)
        {
            var collisionElements = new List<Element>();
            var elementsToHighlight = new List<Element>(); // Элементы для оранжевой заливки

            foreach (int reportId in reportIds)
            {
                Debug.WriteLine($"Проверка ID {reportId}:");

                bool found = false;

                // Сначала ищем в базе по FamilySymbolId
                if (_symbolToElementsMap.ContainsKey(reportId))
                {
                    var elements = _symbolToElementsMap[reportId];
                    foreach (Element element in elements)
                    {
                        if (!collisionElements.Any(e => e.Id == element.Id))
                        {
                            collisionElements.Add(element);
                            elementsToHighlight.Add(element); // Этот элемент из отчета - оранжевый
                        }
                    }
                    Debug.WriteLine($"  Найден как FamilySymbolId -> {elements.Count} элементов");
                    found = true;
                }

                // Если не найден как FamilySymbolId, ищем напрямую как ElementId
                if (!found)
                {
                    Element directElement = _doc.GetElement(new ElementId(reportId));
                    if (directElement != null)
                    {
                        if (!collisionElements.Any(e => e.Id == directElement.Id))
                        {
                            collisionElements.Add(directElement);
                            elementsToHighlight.Add(directElement); // Этот элемент из отчета - оранжевый
                        }
                        Debug.WriteLine($"  Найден как прямой ElementId");
                        found = true;
                    }
                }

                if (!found)
                {
                    Debug.WriteLine($"  Не найден в базе");
                }
            }

            // Сохраняем элементы для раскраски
            _elementsToHighlight = elementsToHighlight;

            return collisionElements;
        }

        private List<Element> _elementsToHighlight = new List<Element>();

        /// <summary>
        /// Создание 3D вида для коллизии
        /// </summary>
        private bool CreateCollisionView(string clashName, List<Element> elements, List<int> reportIds,
            ViewFamilyType viewFamilyType, ElementId solidFillPatternId)
        {
            try
            {
                View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                // Формируем имя вида
                string categories = string.Join("_", elements
                    .Where(e => e.Category != null)
                    .Select(e => e.Category.Name)
                    .Distinct()
                    .Take(2));

                string ids = string.Join("_", reportIds.Take(3));
                string viewName = $"{clashName}_{categories}_{ids}";
                if (viewName.Length > 200)
                    viewName = viewName.Substring(0, 200);

                view.Name = viewName;
                view.DisplayStyle = DisplayStyle.Shading;
                view.DetailLevel = ViewDetailLevel.Fine;
                view.Discipline = ViewDiscipline.Coordination;

                // Устанавливаем секционную коробку
                BoundingBoxXYZ sectionBox = CreateSectionBoxFromElements(elements);
                if (sectionBox != null)
                    view.SetSectionBox(sectionBox);

                // Применяем графические настройки
                ApplyColorOverrides(view, elements, solidFillPatternId);

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
        /// Применение графических настроек
        /// </summary>
        private void ApplyColorOverrides(View3D view, List<Element> elements, ElementId solidFillPatternId)
        {
            // Настройки для элементов из отчета (оранжевая заливка)
            OverrideGraphicSettings highlightOgs = new OverrideGraphicSettings()
                .SetProjectionLineColor(new Color(255, 165, 0)) // Оранжевый контур
                .SetProjectionLineWeight(5)
                .SetSurfaceForegroundPatternId(solidFillPatternId)
                .SetSurfaceForegroundPatternColor(new Color(255, 165, 0)) // Оранжевая заливка
                .SetSurfaceTransparency(0); // Непрозрачно

            // Настройки для остальных элементов (прозрачность 20%)
            OverrideGraphicSettings transparentOgs = new OverrideGraphicSettings()
                .SetProjectionLineColor(new Color(128, 128, 128)) // Серый контур
                .SetProjectionLineWeight(1)
                .SetSurfaceForegroundPatternId(solidFillPatternId)
                .SetSurfaceForegroundPatternColor(new Color(200, 200, 200)) // Светло-серая заливка
                .SetSurfaceTransparency(80); // 80% прозрачность = 20% видимости

            // Получаем все элементы в секционной коробке
            var allElementsInView = new FilteredElementCollector(_doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            // Применяем настройки ко всем элементам в виде
            foreach (Element element in allElementsInView)
            {
                try
                {
                    // Если элемент из отчета - оранжевая заливка
                    if (_elementsToHighlight.Any(e => e.Id == element.Id))
                    {
                        view.SetElementOverrides(element.Id, highlightOgs);
                    }
                    // Остальные элементы - прозрачность 20%
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

            Debug.WriteLine($"Раскрашено элементов: {_elementsToHighlight.Count} оранжевых, {allElementsInView.Count - _elementsToHighlight.Count} прозрачных");
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

        private void InitializeComponent()
        {
            this.progressBar = new ProgressBar();
            this.labelStatus = new Label();
            this.buttonCancel = new Button();

            this.SuspendLayout();

            // progressBar
            this.progressBar.Location = new System.Drawing.Point(15, 30);
            this.progressBar.Size = new System.Drawing.Size(350, 23);
            this.progressBar.Style = ProgressBarStyle.Continuous;

            // labelStatus
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(15, 10);
            this.labelStatus.Text = "Подготовка...";

            // buttonCancel
            this.buttonCancel.Location = new System.Drawing.Point(290, 60);
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.Text = "Отмена";
            this.buttonCancel.Click += (s, e) => { CancelRequested = true; };

            // ProgressForm
            this.ClientSize = new System.Drawing.Size(380, 95);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Обработка коллизий";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private ProgressBar progressBar;
        private Label labelStatus;
        private Button buttonCancel;
    }
}

