//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Windows;
//using System.Windows.Controls;

//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    public class CollectGenericModelsCommand : IExternalCommand
//    {
//        private Document _doc;

//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            UIApplication uiApp = commandData.Application;
//            UIDocument uiDoc = uiApp.ActiveUIDocument;
//            _doc = uiDoc.Document;

//            try
//            {
//                FilteredElementCollector collector = new FilteredElementCollector(_doc);
//                ICollection<Element> genericModels = collector.OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsNotElementType().ToElements();

//                StringBuilder sb = new StringBuilder();

//                sb.AppendLine($"НАЙДЕНО ОБОБЩЕННЫХ МОДЕЛЕЙ: {genericModels.Count}");
//                sb.AppendLine("=" + new string('=', 80));

//                int counter = 1;
//                foreach (Element genericModel in genericModels)
//                {
//                    sb.AppendLine($"\n--- ОБОБЩЕННАЯ МОДЕЛЬ #{counter++} ---");
//                    CollectElementInfo(genericModel, sb);
//                    sb.AppendLine(new string('-', 50));
//                }

//                ShowInfoWindow(sb.ToString());

//                return Result.Succeeded;
//            }
//            catch (Exception ex)
//            {
//                message = $"Ошибка: {ex.Message}";
//                return Result.Failed;
//            }
//        }

//        private void CollectElementInfo(Element element, StringBuilder sb)
//        {
//            // Базовая информация об элементе
//            sb.AppendLine($"ID: {element.Id.IntegerValue}");
//            sb.AppendLine($"Имя: {element.Name}");
//            sb.AppendLine($"Категория: {element.Category?.Name ?? "Н/Д"}");

//            // Параметры Element
//            sb.AppendLine($"\nПАРАМЕТРЫ ELEMENT:");
//            foreach (Parameter param in element.Parameters)
//            {
//                try
//                {
//                    if (param.Definition != null)
//                    {
//                        string paramName = param.Definition.Name;
//                        string paramValue = GetParameterValue(param);
//                        string storageType = param.StorageType.ToString();

//                        sb.AppendLine($"  {paramName}: {paramValue} ({storageType})");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    sb.AppendLine($"  Ошибка чтения параметра: {ex.Message}");
//                }
//            }

//            // Детальная информация о геометрии с разными Options
//            CollectGeometryInfoWithVariants(element, sb);
//        }

//        private void CollectGeometryInfoWithVariants(Element element, StringBuilder sb)
//        {
//            sb.AppendLine($"\nГЕОМЕТРИЯ С РАЗНЫМИ НАСТРОЙКАМИ:");

//            // Вариант 1: Fine detail level
//            Options optionsFine = new Options();
//            optionsFine.DetailLevel = ViewDetailLevel.Fine;
//            optionsFine.ComputeReferences = true;
//            GeometryElement geometryFine = element.get_Geometry(optionsFine);
//            sb.AppendLine($"\n*** ViewDetailLevel.Fine ***");
//            AnalyzeGeometryElement(geometryFine, "  ", sb);

//            // Вариант 2: Medium detail level
//            Options optionsMedium = new Options();
//            optionsMedium.DetailLevel = ViewDetailLevel.Medium;
//            optionsMedium.ComputeReferences = true;
//            GeometryElement geometryMedium = element.get_Geometry(optionsMedium);
//            sb.AppendLine($"\n*** ViewDetailLevel.Medium ***");
//            AnalyzeGeometryElement(geometryMedium, "  ", sb);

//            // Вариант 3: Coarse detail level
//            Options optionsCoarse = new Options();
//            optionsCoarse.DetailLevel = ViewDetailLevel.Coarse;
//            optionsCoarse.ComputeReferences = true;
//            GeometryElement geometryCoarse = element.get_Geometry(optionsCoarse);
//            sb.AppendLine($"\n*** ViewDetailLevel.Coarse ***");
//            AnalyzeGeometryElement(geometryCoarse, "  ", sb);

//            // Вариант 4: Без вычисления ссылок
//            Options optionsNoRefs = new Options();
//            optionsNoRefs.DetailLevel = ViewDetailLevel.Fine;
//            optionsNoRefs.ComputeReferences = false;
//            GeometryElement geometryNoRefs = element.get_Geometry(optionsNoRefs);
//            sb.AppendLine($"\n*** Fine без ComputeReferences ***");
//            AnalyzeGeometryElement(geometryNoRefs, "  ", sb);
//        }

//        private void AnalyzeGeometryElement(GeometryElement geometry, string indent, StringBuilder sb)
//        {
//            if (geometry == null)
//            {
//                sb.AppendLine($"{indent}Геометрия: null");
//                return;
//            }

//            int totalObjects = 0;
//            int instanceCount = 0;
//            int solidCount = 0;
//            int meshCount = 0;
//            int curveCount = 0;
//            int otherCount = 0;

//            foreach (GeometryObject obj in geometry)
//            {
//                totalObjects++;

//                if (obj is GeometryInstance geometryInstance)
//                {
//                    instanceCount++;
//                    sb.AppendLine($"{indent}GeometryInstance #{instanceCount}:");
//                    AnalyzeGeometryInstance(geometryInstance, indent + "  ", sb);
//                }
//                else if (obj is Solid solid)
//                {
//                    solidCount++;
//                    sb.AppendLine($"{indent}Solid #{solidCount}:");
//                    sb.AppendLine($"{indent}  Объем: {(solid.Volume > 0 ? solid.Volume.ToString("F6") : "0")}");
//                    sb.AppendLine($"{indent}  Площадь: {solid.SurfaceArea:F6}");
//                    sb.AppendLine($"{indent}  Граней: {solid.Faces.Size}");
//                    sb.AppendLine($"{indent}  Ребёр: {solid.Edges.Size}");
//                }
//                else if (obj is Mesh mesh)
//                {
//                    meshCount++;
//                    sb.AppendLine($"{indent}Mesh #{meshCount}:");
//                    sb.AppendLine($"{indent}  Треугольников: {mesh.NumTriangles}");
//                    sb.AppendLine($"{indent}  Вершин: {mesh.Vertices.Count}");
//                }
//                else if (obj is Curve curve)
//                {
//                    curveCount++;
//                    sb.AppendLine($"{indent}Curve #{curveCount}:");
//                    sb.AppendLine($"{indent}  Длина: {curve.Length:F6}");
//                    sb.AppendLine($"{indent}  Тип: {curve.GetType().Name}");
//                }
//                else if (obj is Point point)
//                {
//                    otherCount++;
//                    sb.AppendLine($"{indent}Point #{otherCount}: {point.Coordinate}");
//                }
//                else if (obj is PolyLine polyLine)
//                {
//                    otherCount++;
//                    sb.AppendLine($"{indent}PolyLine #{otherCount}:");
//                    sb.AppendLine($"{indent}  Количество точек: {polyLine.Coordinates.Count}");
//                }
//                else
//                {
//                    otherCount++;
//                    sb.AppendLine($"{indent}{obj.GetType().Name} #{otherCount}");
//                }
//            }

//            sb.AppendLine($"{indent}ВСЕГО ОБЪЕКТОВ: {totalObjects}");
//            sb.AppendLine($"{indent}  - GeometryInstance: {instanceCount}");
//            sb.AppendLine($"{indent}  - Solids: {solidCount}");
//            sb.AppendLine($"{indent}  - Meshes: {meshCount}");
//            sb.AppendLine($"{indent}  - Curves: {curveCount}");
//            sb.AppendLine($"{indent}  - Other: {otherCount}");
//        }

//        private void AnalyzeGeometryInstance(GeometryInstance geometryInstance, string indent, StringBuilder sb)
//        {
//            sb.AppendLine($"{indent}Transform: {geometryInstance.Transform}");
//            sb.AppendLine($"{indent}IsSymbolGeometry: {geometryInstance.IsSymbolGeometry}");

//            // SymbolGeometry
//            GeometryElement symbolGeometry = geometryInstance.GetSymbolGeometry();
//            if (symbolGeometry != null)
//            {
//                sb.AppendLine($"{indent}SymbolGeometry объектов: {GetGeometryCount(symbolGeometry)}");

//                // Детальный анализ SymbolGeometry
//                int symbolSolids = 0;
//                int symbolMeshes = 0;
//                int symbolCurves = 0;
//                int symbolInstances = 0;

//                foreach (GeometryObject symbolObj in symbolGeometry)
//                {
//                    if (symbolObj is Solid solid && solid.Volume > 0)
//                    {
//                        symbolSolids++;
//                    }
//                    else if (symbolObj is Mesh)
//                    {
//                        symbolMeshes++;
//                    }
//                    else if (symbolObj is Curve)
//                    {
//                        symbolCurves++;
//                    }
//                    else if (symbolObj is GeometryInstance)
//                    {
//                        symbolInstances++;
//                    }
//                }

//                sb.AppendLine($"{indent}SymbolGeometry состав:");
//                sb.AppendLine($"{indent}  Solids: {symbolSolids}, Meshes: {symbolMeshes}");
//                sb.AppendLine($"{indent}  Curves: {symbolCurves}, Instances: {symbolInstances}");
//            }

//            // Transformed Geometry
//            GeometryElement transformedGeometry = geometryInstance.GetSymbolGeometry(geometryInstance.Transform);
//            if (transformedGeometry != null)
//            {
//                sb.AppendLine($"{indent}Transformed Geometry объектов: {GetGeometryCount(transformedGeometry)}");
//            }

//            // Получаем информацию о Symbol через родительский элемент
//            GetSymbolInfoFromInstance(geometryInstance, indent, sb);
//        }

//        private void GetSymbolInfoFromInstance(GeometryInstance geometryInstance, string indent, StringBuilder sb)
//        {
//            try
//            {
//                // Получаем GeometryObject из GeometryInstance для получения дополнительной информации
//                sb.AppendLine($"{indent}*** ИНФОРМАЦИЯ О SYMBOL ***");

//                // Анализируем геометрию символа более детально
//                GeometryElement symbolGeom = geometryInstance.SymbolGeometry;
//                if (symbolGeom != null)
//                {
//                    // Ищем вложенные элементы, которые могут содержать информацию о Symbol
//                    foreach (GeometryObject geomObj in symbolGeom)
//                    {
//                        if (geomObj is GeometryInstance nestedInstance)
//                        {
//                            sb.AppendLine($"{indent}Вложенный GeometryInstance:");
//                            sb.AppendLine($"{indent}  Transform: {nestedInstance.Transform}");
//                            sb.AppendLine($"{indent}  IsSymbolGeometry: {nestedInstance.IsSymbolGeometry}");

//                            GeometryElement nestedSymbolGeom = nestedInstance.GetSymbolGeometry();
//                            if (nestedSymbolGeom != null)
//                            {
//                                sb.AppendLine($"{indent}  Вложенных объектов: {GetGeometryCount(nestedSymbolGeom)}");
//                            }
//                        }
//                    }
//                }

//                // Информация о трансформации
//                Transform transform = geometryInstance.Transform;
//                sb.AppendLine($"{indent}Transform Matrix:");
//                sb.AppendLine($"{indent}  Origin: {transform.Origin}");
//                sb.AppendLine($"{indent}  BasisX: {transform.BasisX}");
//                sb.AppendLine($"{indent}  BasisY: {transform.BasisY}");
//                sb.AppendLine($"{indent}  BasisZ: {transform.BasisZ}");
//                sb.AppendLine($"{indent}  Determinant: {transform.Determinant}");

//            }
//            catch (Exception ex)
//            {
//                sb.AppendLine($"{indent}Ошибка анализа Symbol: {ex.Message}");
//            }
//        }

//        private string GetParameterValue(Parameter param)
//        {
//            if (!param.HasValue) return "Нет значения";

//            try
//            {
//                switch (param.StorageType)
//                {
//                    case StorageType.Double:
//                        return param.AsDouble().ToString("F6");
//                    case StorageType.ElementId:
//                        ElementId id = param.AsElementId();
//                        if (id == ElementId.InvalidElementId)
//                            return "Invalid";

//                        Element elem = _doc.GetElement(id);
//                        return elem != null ? $"{elem.Name} (ID: {id.IntegerValue})" : id.IntegerValue.ToString();
//                    case StorageType.Integer:
//                        return param.AsInteger().ToString();
//                    case StorageType.String:
//                        return param.AsString() ?? "null";
//                    case StorageType.None:
//                        return "None";
//                    default:
//                        return "Unknown";
//                }
//            }
//            catch
//            {
//                return "Ошибка чтения";
//            }
//        }

//        private int GetGeometryCount(GeometryElement geometry)
//        {
//            if (geometry == null) return 0;

//            int count = 0;
//            foreach (GeometryObject obj in geometry)
//            {
//                count++;
//                if (obj is GeometryInstance instance)
//                {
//                    GeometryElement symbolGeometry = instance.GetSymbolGeometry();
//                    if (symbolGeometry != null)
//                    {
//                        foreach (GeometryObject symbolObj in symbolGeometry)
//                        {
//                            count++;
//                        }
//                    }
//                }
//            }
//            return count;
//        }

//        private void ShowInfoWindow(string info)
//        {
//            Window window = new Window
//            {
//                Title = "Информация об обобщенных моделях - ПАРАМЕТРЫ И ГЕОМЕТРИЯ",
//                Width = 1200,
//                Height = 800,
//                WindowStartupLocation = WindowStartupLocation.CenterScreen
//            };

//            System.Windows.Controls.TextBox textBox = new System.Windows.Controls.TextBox
//            {
//                Text = info,
//                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
//                FontSize = 8,
//                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
//                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
//                IsReadOnly = true,
//                TextWrapping = TextWrapping.NoWrap
//            };

//            window.Content = textBox;
//            window.Show();
//        }
//    }
//}