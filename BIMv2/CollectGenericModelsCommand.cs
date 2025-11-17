using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CollectGenericModelsCommand : IExternalCommand
    {
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            _doc = uiDoc.Document;

            try
            {
                // Используем более эффективный сборщик
                FilteredElementCollector collector = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsNotElementType();

                IList<Element> genericModels = collector.ToElements();

                StringBuilder sb = new StringBuilder();

                sb.AppendLine($"НАЙДЕНО ОБОБЩЕННЫХ МОДЕЛЕЙ: {genericModels.Count}");
                sb.AppendLine($"Дата сбора: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                sb.AppendLine("=" + new string('=', 100));

                int counter = 1;
                foreach (Element genericModel in genericModels)
                {
                    sb.AppendLine($"\n--- ОБОБЩЕННАЯ МОДЕЛЬ #{counter++} ---");
                    CollectElementInfo(genericModel, sb);
                    sb.AppendLine(new string('-', 80));
                }

                ShowInfoWindow(sb.ToString());

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}\n{ex.StackTrace}";
                TaskDialog.Show("Ошибка", message);
                return Result.Failed;
            }
        }

        private void CollectElementInfo(Element element, StringBuilder sb)
        {
            try
            {
                // БАЗОВАЯ ИНФОРМАЦИЯ
                sb.AppendLine($"ID: {element.Id.IntegerValue}");
                sb.AppendLine($"Уникальный ID: {element.UniqueId}");
                sb.AppendLine($"Имя: {element.Name ?? "Н/Д"}");
                sb.AppendLine($"Категория: {element.Category?.Name ?? "Н/Д"}");

                Element level = _doc.GetElement(element.LevelId);
                sb.AppendLine($"Уровень: {level?.Name ?? "Н/Д"}");

                // ГЕОМЕТРИЯ - только GeometryInstance
                CollectGeometryInfo(element, sb);

            }
            catch (Exception ex)
            {
                sb.AppendLine($"Ошибка при сборе информации об элементе: {ex.Message}");
            }
        }

        private void CollectGeometryInfo(Element element, StringBuilder sb)
        {
            try
            {
                sb.AppendLine($"\nГЕОМЕТРИЯ:");

                Options options = new Options
                {
                    DetailLevel = ViewDetailLevel.Fine,
                    ComputeReferences = true
                };

                GeometryElement geometry = element.get_Geometry(options);

                if (geometry == null)
                {
                    sb.AppendLine("  Геометрия: null");
                    return;
                }

                int instanceCount = 0;

                foreach (GeometryObject obj in geometry)
                {
                    if (obj is GeometryInstance geometryInstance)
                    {
                        instanceCount++;
                        sb.AppendLine($"\n  GeometryInstance #{instanceCount}:");
                        AnalyzeGeometryInstance(geometryInstance, "    ", sb);
                    }
                }

                if (instanceCount == 0)
                {
                    sb.AppendLine("  GeometryInstance не найдены");
                }
                else
                {
                    sb.AppendLine($"\n  ВСЕГО GeometryInstance: {instanceCount}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Ошибка анализа геометрии: {ex.Message}");
            }
        }

        private void AnalyzeGeometryInstance(GeometryInstance geometryInstance, string indent, StringBuilder sb)
        {
            try
            {
                // Основные свойства GeometryInstance
                sb.AppendLine($"{indent}СВОЙСТВА GEOMETRYINSTANCE:");

                // Получаем GeometryElement через GetSymbolGeometry()
                GeometryElement symbolGeometry = geometryInstance.GetSymbolGeometry();
                if (symbolGeometry != null)
                {
                    sb.AppendLine($"{indent}  SymbolGeometry найдена:");

                    // Пытаемся получить информацию о Symbol из GeometryElement
                    AnalyzeSymbolFromGeometry(symbolGeometry, indent + "    ", sb);
                }

                // Пытаемся получить Symbol через другие методы
                ElementId symbolId = TryGetSymbolByReflection(geometryInstance, indent + "  ", sb);

                // Если нашли SymbolId, получаем полную информацию о Symbol
                if (symbolId != null && symbolId != ElementId.InvalidElementId)
                {
                    ShowSymbolInfo(symbolId, indent + "  ", sb);
                }

                // Transform информация
                Transform transform = geometryInstance.Transform;
                sb.AppendLine($"{indent}  Transform:");
                sb.AppendLine($"{indent}    Origin: {transform.Origin}");
                sb.AppendLine($"{indent}    BasisX: {transform.BasisX}");
                sb.AppendLine($"{indent}    BasisY: {transform.BasisY}");
                sb.AppendLine($"{indent}    BasisZ: {transform.BasisZ}");

                // Анализ содержимого GeometryInstance
                AnalyzeGeometryInstanceContent(geometryInstance, indent + "  ", sb);

            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}Ошибка анализа GeometryInstance: {ex.Message}");
            }
        }

        private ElementId TryGetSymbolByReflection(GeometryInstance geometryInstance, string indent, StringBuilder sb)
        {
            ElementId foundSymbolId = null;

            try
            {
                sb.AppendLine($"{indent}Поиск Symbol через Reflection:");

                // Пытаемся получить доступ к внутренним свойствам через Reflection
                Type geometryInstanceType = geometryInstance.GetType();

                // Пробуем различные возможные имена свойств для Symbol
                string[] possiblePropertyNames = { "Symbol", "FamilySymbol", "SymbolId", "SymbolElement", "GetSymbolId" };

                foreach (string propName in possiblePropertyNames)
                {
                    try
                    {
                        var property = geometryInstanceType.GetProperty(propName);
                        if (property != null)
                        {
                            object value = property.GetValue(geometryInstance);
                            if (value != null)
                            {
                                sb.AppendLine($"{indent}  Найден параметр '{propName}': {value}");

                                // Если это ElementId, сохраняем его
                                if (value is ElementId elementId && elementId != ElementId.InvalidElementId)
                                {
                                    foundSymbolId = elementId;
                                    sb.AppendLine($"{indent}    ID Symbol: {elementId.IntegerValue}");

                                    Element symbolElement = _doc.GetElement(elementId);
                                    if (symbolElement != null)
                                    {
                                        sb.AppendLine($"{indent}    Элемент Symbol: {symbolElement.Name} (ID: {symbolElement.Id.IntegerValue})");
                                        sb.AppendLine($"{indent}    Тип Symbol: {symbolElement.GetType().Name}");
                                    }
                                }
                                else if (value is Element symbolElement)
                                {
                                    foundSymbolId = symbolElement.Id;
                                    sb.AppendLine($"{indent}    ID Symbol: {symbolElement.Id.IntegerValue}");
                                    sb.AppendLine($"{indent}    Элемент Symbol: {symbolElement.Name} (ID: {symbolElement.Id.IntegerValue})");
                                    sb.AppendLine($"{indent}    Тип Symbol: {symbolElement.GetType().Name}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"{indent}  Ошибка доступа к '{propName}': {ex.Message}");
                    }
                }

                // Пробуем методы
                string[] possibleMethodNames = { "GetSymbol", "GetSymbolId", "GetFamilySymbol" };
                foreach (string methodName in possibleMethodNames)
                {
                    try
                    {
                        var method = geometryInstanceType.GetMethod(methodName);
                        if (method != null && method.GetParameters().Length == 0)
                        {
                            object result = method.Invoke(geometryInstance, null);
                            if (result != null)
                            {
                                sb.AppendLine($"{indent}  Метод '{methodName}' вернул: {result}");

                                // Если это ElementId, сохраняем его
                                if (result is ElementId elementId && elementId != ElementId.InvalidElementId)
                                {
                                    foundSymbolId = elementId;
                                    sb.AppendLine($"{indent}    ID Symbol: {elementId.IntegerValue}");
                                }
                                else if (result is Element symbolElement)
                                {
                                    foundSymbolId = symbolElement.Id;
                                    sb.AppendLine($"{indent}    ID Symbol: {symbolElement.Id.IntegerValue}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Игнорируем ошибки методов
                    }
                }

                if (foundSymbolId == null)
                {
                    sb.AppendLine($"{indent}  Symbol не найден через Reflection");
                }

            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}Ошибка Reflection: {ex.Message}");
            }

            return foundSymbolId;
        }

        private void ShowSymbolInfo(ElementId symbolId, string indent, StringBuilder sb)
        {
            try
            {
                if (symbolId == null || symbolId == ElementId.InvalidElementId)
                    return;

                Element symbolElement = _doc.GetElement(symbolId);
                if (symbolElement == null)
                    return;

                sb.AppendLine($"{indent}ПОЛНАЯ ИНФОРМАЦИЯ О SYMBOL:");
                sb.AppendLine($"{indent}  ID Symbol: {symbolId.IntegerValue}");
                sb.AppendLine($"{indent}  Уникальный ID: {symbolElement.UniqueId}");
                sb.AppendLine($"{indent}  Имя: {symbolElement.Name ?? "Н/Д"}");
                sb.AppendLine($"{indent}  Категория: {symbolElement.Category?.Name ?? "Н/Д"}");
                sb.AppendLine($"{indent}  Тип: {symbolElement.GetType().Name}");

                // Если это FamilySymbol, показываем дополнительную информацию
                if (symbolElement is FamilySymbol familySymbol)
                {
                    sb.AppendLine($"{indent}  Имя семейства: {familySymbol.FamilyName}");
                    //sb.AppendLine($"{indent}  Является редактируемым: {familySymbol.IsEditable}");

                    // Параметры FamilySymbol
                    sb.AppendLine($"{indent}  Параметры FamilySymbol:");
                    foreach (Parameter param in familySymbol.Parameters)
                    {
                        try
                        {
                            if (param?.Definition != null)
                            {
                                string paramName = param.Definition.Name;
                                string paramValue = GetParameterValue(param);
                                sb.AppendLine($"{indent}    {paramName}: {paramValue}");
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"{indent}    Ошибка чтения параметра: {ex.Message}");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}Ошибка получения информации о Symbol: {ex.Message}");
            }
        }

        private void AnalyzeSymbolFromGeometry(GeometryElement symbolGeometry, string indent, StringBuilder sb)
        {
            try
            {
                sb.AppendLine($"{indent}Информация о Symbol через GeometryElement:");

                // Анализируем содержимое GeometryElement для поиска информации о Symbol
                int solidCount = 0;
                int meshCount = 0;
                int curveCount = 0;
                int totalObjects = 0;

                foreach (GeometryObject obj in symbolGeometry)
                {
                    totalObjects++;

                    if (obj is Solid solid)
                    {
                        solidCount++;
                        if (solid.Volume > 0.0001)
                        {
                            sb.AppendLine($"{indent}  Solid #{solidCount}: Объем={solid.Volume:F6}, Площадь={solid.SurfaceArea:F6}");
                        }
                    }
                    else if (obj is Mesh mesh)
                    {
                        meshCount++;
                        sb.AppendLine($"{indent}  Mesh #{meshCount}: Треугольников={mesh.NumTriangles}, Вершин={mesh.Vertices.Count}");
                    }
                    else if (obj is Curve curve)
                    {
                        curveCount++;
                        sb.AppendLine($"{indent}  Curve #{curveCount}: Длина={curve.Length:F6}, Тип={curve.GetType().Name}");
                    }
                }

                sb.AppendLine($"{indent}Всего объектов в SymbolGeometry: {totalObjects}");
                sb.AppendLine($"{indent}  - Solids: {solidCount}");
                sb.AppendLine($"{indent}  - Meshes: {meshCount}");
                sb.AppendLine($"{indent}  - Curves: {curveCount}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}Ошибка анализа Symbol из Geometry: {ex.Message}");
            }
        }

        private void AnalyzeGeometryInstanceContent(GeometryInstance geometryInstance, string indent, StringBuilder sb)
        {
            try
            {
                sb.AppendLine($"{indent}Содержимое GeometryInstance:");

                // Получаем GeometryElement для анализа
                GeometryElement geometryElement = geometryInstance.GetSymbolGeometry();
                if (geometryElement == null)
                {
                    geometryElement = geometryInstance.GetInstanceGeometry();
                }

                if (geometryElement != null)
                {
                    int totalObjects = 0;
                    Dictionary<string, int> typeCounts = new Dictionary<string, int>();

                    foreach (GeometryObject obj in geometryElement)
                    {
                        totalObjects++;
                        string typeName = obj.GetType().Name;

                        if (!typeCounts.ContainsKey(typeName))
                            typeCounts[typeName] = 0;
                        typeCounts[typeName]++;
                    }

                    sb.AppendLine($"{indent}  Всего геометрических объектов: {totalObjects}");
                    foreach (var kvp in typeCounts)
                    {
                        sb.AppendLine($"{indent}    - {kvp.Key}: {kvp.Value}");
                    }
                }
                else
                {
                    sb.AppendLine($"{indent}  GeometryElement не доступен");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}Ошибка анализа содержимого: {ex.Message}");
            }
        }

        private string GetParameterValue(Parameter param)
        {
            if (param == null || !param.HasValue)
                return "Нет значения";

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.Double:
                        double dVal = param.AsDouble();
                        return dVal.ToString("F6");

                    case StorageType.ElementId:
                        ElementId id = param.AsElementId();
                        if (id == ElementId.InvalidElementId)
                            return "Invalid";

                        Element elem = _doc.GetElement(id);
                        return elem != null ? $"{elem.Name} (ID:{id.IntegerValue})" : id.IntegerValue.ToString();

                    case StorageType.Integer:
                        return param.AsInteger().ToString();

                    case StorageType.String:
                        return $"\"{param.AsString() ?? "null"}\"";

                    case StorageType.None:
                        return "None";

                    default:
                        return "Unknown";
                }
            }
            catch
            {
                return "Ошибка чтения";
            }
        }

        private void ShowInfoWindow(string info)
        {
            try
            {
                // Создаем окно WPF
                var window = new System.Windows.Window
                {
                    Title = $"Информация об обобщенных моделях - GeometryInstance Symbol",
                    Width = 1300,
                    Height = 900,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    WindowState = System.Windows.WindowState.Maximized
                };

                // Создаем TextBox с явным указанием пространства имен WPF
                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = info,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                    FontSize = 14,
                    HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    IsReadOnly = true,
                    TextWrapping = System.Windows.TextWrapping.NoWrap
                };

                // Добавляем контекстное меню для копирования
                var contextMenu = new System.Windows.Controls.ContextMenu();
                var copyItem = new System.Windows.Controls.MenuItem { Header = "Копировать" };
                copyItem.Click += (s, e) => System.Windows.Clipboard.SetText(textBox.SelectedText.Length > 0 ? textBox.SelectedText : textBox.Text);
                contextMenu.Items.Add(copyItem);
                textBox.ContextMenu = contextMenu;

                window.Content = textBox;
                window.Show();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка окна", $"Не удалось создать окно: {ex.Message}");
            }
        }
    }
}