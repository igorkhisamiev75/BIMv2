using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TransferLGParameters : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Получаем текущий вид
                View activeView = doc.ActiveView;

                // Запрашиваем подтверждение у пользователя
                TaskDialogResult dialogResult = TaskDialog.Show("Передача параметров LG",
                    $"Выполнить передачу параметров LG с формообразующих элементов?\nТекущий вид: {activeView.Name}",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (dialogResult != TaskDialogResult.Yes)
                    return Result.Cancelled;

                // Счетчики для статистики
                int elementsProcessed = 0;
                int elementsUpdated = 0;
                int totalParametersTransferred = 0;

                // Список для отладки
                List<string> debugInfo = new List<string>();

                // Запускаем транзакцию
                using (Transaction trans = new Transaction(doc, "Передача параметров LG"))
                {
                    trans.Start();

                    try
                    {
                        debugInfo.Add($"=== НАЧАЛО ОБРАБОТКИ ===");
                        debugInfo.Add($"Активный вид: {activeView.Name}");

                        // 1. Собираем все элементы на текущем виде (стены, окна, двери)
                        FilteredElementCollector viewCollector = new FilteredElementCollector(doc, activeView.Id);

                        List<Element> wallsOnView = viewCollector
                            .WhereElementIsNotElementType()
                            .OfCategory(BuiltInCategory.OST_Walls)
                            .ToElements()
                            .Cast<Element>()
                            .ToList();

                        List<Element> windowsOnView = viewCollector
                            .WhereElementIsNotElementType()
                            .OfCategory(BuiltInCategory.OST_Windows)
                            .ToElements()
                            .Cast<Element>()
                            .ToList();

                        List<Element> doorsOnView = viewCollector
                            .WhereElementIsNotElementType()
                            .OfCategory(BuiltInCategory.OST_Doors)
                            .ToElements()
                            .Cast<Element>()
                            .ToList();

                        // Объединяем все элементы в один список
                        List<Element> elementsOnView = new List<Element>();
                        elementsOnView.AddRange(wallsOnView);
                        elementsOnView.AddRange(windowsOnView);
                        elementsOnView.AddRange(doorsOnView);

                        debugInfo.Add($"");
                        debugInfo.Add($"Элементов на виде:");
                        debugInfo.Add($"  Стены: {wallsOnView.Count}");
                        debugInfo.Add($"  Окна: {windowsOnView.Count}");
                        debugInfo.Add($"  Двери: {doorsOnView.Count}");
                        debugInfo.Add($"  Всего: {elementsOnView.Count}");

                        // 2. Собираем все формообразующие в проекте (массы)
                        FilteredElementCollector massCollector = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Mass)
                            .WhereElementIsNotElementType();

                        List<Element> masses = massCollector.ToElements().Cast<Element>().ToList();
                        debugInfo.Add($"");
                        debugInfo.Add($"Найдено масс в проекте: {masses.Count}");

                        // 3. ЗАРАНЕЕ извлекаем BoundingBox масс
                        Dictionary<Element, BoundingBoxXYZ> massBoundingBoxes = new Dictionary<Element, BoundingBoxXYZ>();
                        Dictionary<Element, LGParameters> massParameters = new Dictionary<Element, LGParameters>();

                        foreach (Element mass in masses)
                        {
                            // Получаем параметры массы
                            LGParameters massParams = GetLGParameters(mass);
                            if (massParams.IsEmpty())
                            {
                                debugInfo.Add($"Масса {mass.Id} пропущена - нет LG параметров");
                                continue;
                            }

                            massParameters[mass] = massParams;

                            // Получаем BoundingBox массы
                            BoundingBoxXYZ massBBox = GetElementBoundingBox(mass);
                            if (massBBox != null)
                            {
                                massBoundingBoxes[mass] = massBBox;
                                debugInfo.Add($"Масса {mass.Id}: Этаж='{massParams.FloorNumber}', Z({massBBox.Min.Z:F2}-{massBBox.Max.Z:F2})");
                            }
                            else
                            {
                                debugInfo.Add($"Масса {mass.Id}: НЕ удалось получить BoundingBox");
                            }
                        }

                        debugInfo.Add($"Масс с параметрами: {massParameters.Count}");

                        // Если нет масс, выходим
                        if (massParameters.Count == 0)
                        {
                            TaskDialog.Show("Ошибка", "Не найдено масс с заполненными LG параметрами.");
                            return Result.Failed;
                        }

                        debugInfo.Add($"");
                        debugInfo.Add($"=== НАЧАЛО ПРОВЕРКИ ПЕРЕСЕЧЕНИЙ ===");

                        // 4. Обрабатываем все элементы одинаково
                        foreach (Element element in elementsOnView)
                        {
                            elementsProcessed++;

                            bool elementUpdated = false;
                            int paramsForThisElement = 0;
                            string categoryName = GetElementCategoryName(element);

                            debugInfo.Add($"");
                            debugInfo.Add($"[{categoryName} {element.Id}] Обработка...");

                            // Получаем BoundingBox элемента
                            BoundingBoxXYZ elementBBox = GetElementBoundingBox(element);

                            if (elementBBox == null)
                            {
                                debugInfo.Add($"  ⚠ Нет BoundingBox элемента");
                                continue;
                            }

                            debugInfo.Add($"  BoundingBox элемента: Z({elementBBox.Min.Z:F2}-{elementBBox.Max.Z:F2})");

                            // Ищем массы, которые пересекаются с элементом
                            foreach (var massEntry in massBoundingBoxes)
                            {
                                Element mass = massEntry.Key;
                                BoundingBoxXYZ massBBox = massEntry.Value;

                                if (!massParameters.ContainsKey(mass))
                                    continue;

                                LGParameters sourceParams = massParameters[mass];

                                // ПРОВЕРКА ПЕРЕСЕЧЕНИЯ ПО ВСЕМ ОСЯМ
                                bool intersects = CheckBoundingBoxIntersection(elementBBox, massBBox);

                                if (intersects)
                                {
                                    debugInfo.Add($"  ✓ Найдено пересечение с массой {mass.Id}");
                                    debugInfo.Add($"    Высота массы: Z({massBBox.Min.Z:F2}-{massBBox.Max.Z:F2})");
                                    debugInfo.Add($"    Параметры массы: Этаж='{sourceParams.FloorNumber}', Секция='{sourceParams.Section}', Корпус='{sourceParams.BuildingNumber}'");

                                    // Передаем параметры
                                    int transferredCount = TransferParameters(element, sourceParams);

                                    if (transferredCount > 0)
                                    {
                                        paramsForThisElement += transferredCount;
                                        elementUpdated = true;
                                        debugInfo.Add($"    ✓ Передано {transferredCount} параметров");
                                    }
                                    else
                                    {
                                        debugInfo.Add($"    ⚠ Параметры не переданы (возможно, уже установлены)");
                                    }

                                    break; // Нашли пересечение, выходим
                                }
                            }

                            if (!elementUpdated)
                            {
                                debugInfo.Add($"  ✗ Не найдено пересечений с массами");
                            }
                            else
                            {
                                elementsUpdated++;
                                totalParametersTransferred += paramsForThisElement;
                            }
                        }

                        trans.Commit();

                        debugInfo.Add($"");
                        debugInfo.Add($"=== РЕЗУЛЬТАТЫ ===");
                        debugInfo.Add($"Обработано элементов на виде: {elementsProcessed}");
                        debugInfo.Add($"Обновлено элементов: {elementsUpdated}");
                        debugInfo.Add($"Передано параметров: {totalParametersTransferred}");

                        // Выводим отладочную информацию
                        string debugMessage = string.Join("\n", debugInfo);
                        DebugLog(debugMessage);

                        // Показываем результаты
                        TaskDialog.Show("Готово",
                            $"Передача параметров LG завершена!\n\n" +
                            $"Обработано элементов на виде: {elementsProcessed}\n" +
                            $"Обновлено элементов: {elementsUpdated}\n" +
                            $"Передано параметров: {totalParametersTransferred}");
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        debugInfo.Add($"");
                        debugInfo.Add($"=== ОШИБКА ===");
                        debugInfo.Add($"{ex.Message}");
                        debugInfo.Add($"{ex.StackTrace}");

                        string errorMessage = string.Join("\n", debugInfo);
                        DebugLog(errorMessage);

                        TaskDialog.Show("Ошибка", $"Произошла ошибка:\n{ex.Message}");
                        return Result.Failed;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        // Получение BoundingBox элемента (универсальный метод)
        private BoundingBoxXYZ GetElementBoundingBox(Element element)
        {
            try
            {
                // Пробуем получить BoundingBox в глобальных координатах
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);

                if (bbox != null)
                {
                    // Проверяем, что BoundingBox валидный
                    if (bbox.Min != null && bbox.Max != null &&
                        bbox.Min.X <= bbox.Max.X && bbox.Min.Y <= bbox.Max.Y && bbox.Min.Z <= bbox.Max.Z)
                    {
                        return bbox;
                    }
                }

                // Если get_BoundingBox не сработал, пробуем через геометрию
                return GetBoundingBoxFromGeometry(element);
            }
            catch (Exception ex)
            {
                DebugLog($"Ошибка получения BoundingBox для элемента {element.Id}: {ex.Message}");
                return null;
            }
        }

        // Получение BoundingBox через геометрию (запасной метод)
        private BoundingBoxXYZ GetBoundingBoxFromGeometry(Element element)
        {
            try
            {
                Options options = new Options();
                options.ComputeReferences = false;
                options.DetailLevel = ViewDetailLevel.Medium;
                options.IncludeNonVisibleObjects = false;

                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null)
                    return null;

                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                bool hasGeometry = false;

                foreach (GeometryObject geomObj in geometry)
                {
                    if (geomObj is Solid solid && solid != null)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            Mesh mesh = face.Triangulate();
                            foreach (XYZ vertex in mesh.Vertices)
                            {
                                minX = Math.Min(minX, vertex.X);
                                minY = Math.Min(minY, vertex.Y);
                                minZ = Math.Min(minZ, vertex.Z);
                                maxX = Math.Max(maxX, vertex.X);
                                maxY = Math.Max(maxY, vertex.Y);
                                maxZ = Math.Max(maxZ, vertex.Z);
                                hasGeometry = true;
                            }
                        }
                    }
                    else if (geomObj is GeometryInstance geomInst)
                    {
                        GeometryElement instGeom = geomInst.GetInstanceGeometry();
                        Transform transform = geomInst.Transform;

                        foreach (GeometryObject instObj in instGeom)
                        {
                            if (instObj is Solid instSolid && instSolid != null)
                            {
                                foreach (Face face in instSolid.Faces)
                                {
                                    Mesh mesh = face.Triangulate();
                                    foreach (XYZ vertex in mesh.Vertices)
                                    {
                                        XYZ transformedVertex = transform.OfPoint(vertex);
                                        minX = Math.Min(minX, transformedVertex.X);
                                        minY = Math.Min(minY, transformedVertex.Y);
                                        minZ = Math.Min(minZ, transformedVertex.Z);
                                        maxX = Math.Max(maxX, transformedVertex.X);
                                        maxY = Math.Max(maxY, transformedVertex.Y);
                                        maxZ = Math.Max(maxZ, transformedVertex.Z);
                                        hasGeometry = true;
                                    }
                                }
                            }
                        }
                    }
                }

                if (hasGeometry)
                {
                    return new BoundingBoxXYZ
                    {
                        Min = new XYZ(minX, minY, minZ),
                        Max = new XYZ(maxX, maxY, maxZ)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                DebugLog($"Ошибка получения BoundingBox из геометрии для элемента {element.Id}: {ex.Message}");
                return null;
            }
        }

        // Проверка пересечения двух BoundingBox
        private bool CheckBoundingBoxIntersection(BoundingBoxXYZ bbox1, BoundingBoxXYZ bbox2)
        {
            if (bbox1 == null || bbox2 == null)
                return false;

            // Проверяем пересечение по всем осям
            bool intersectX = bbox1.Max.X >= bbox2.Min.X && bbox1.Min.X <= bbox2.Max.X;
            bool intersectY = bbox1.Max.Y >= bbox2.Min.Y && bbox1.Min.Y <= bbox2.Max.Y;
            bool intersectZ = bbox1.Max.Z >= bbox2.Min.Z && bbox1.Min.Z <= bbox2.Max.Z;

            // Все три оси должны пересекаться
            return intersectX && intersectY && intersectZ;
        }

        // Получение имени категории элемента
        private string GetElementCategoryName(Element element)
        {
            if (element == null || element.Category == null)
                return "Без категории";

            return element.Category.Name;
        }

        // Получение LG-параметров из элемента
        private LGParameters GetLGParameters(Element element)
        {
            LGParameters parameters = new LGParameters();

            Parameter floorParam = element.LookupParameter("LG_Номер этажа");
            Parameter sectionParam = element.LookupParameter("LG_Секция");
            Parameter buildingParam = element.LookupParameter("LG_Номер корпуса");

            if (floorParam != null)
                parameters.FloorNumber = GetStringParameterValue(floorParam);

            if (sectionParam != null)
                parameters.Section = GetStringParameterValue(sectionParam);

            if (buildingParam != null)
                parameters.BuildingNumber = GetStringParameterValue(buildingParam);

            return parameters;
        }

        // Получение строкового значения параметра
        private string GetStringParameterValue(Parameter parameter)
        {
            try
            {
                if (parameter == null || parameter.StorageType != StorageType.String)
                    return string.Empty;

                string value = parameter.AsString();
                return !string.IsNullOrWhiteSpace(value) ? value.Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Передача параметров на элемент
        private int TransferParameters(Element targetElement, LGParameters sourceParams)
        {
            int transferredCount = 0;

            try
            {
                // LG_Номер этажа
                if (!string.IsNullOrEmpty(sourceParams.FloorNumber))
                {
                    Parameter param = targetElement.LookupParameter("LG_Номер этажа");
                    if (param != null && param.StorageType == StorageType.String && !param.IsReadOnly)
                    {
                        string currentValue = GetStringParameterValue(param);
                        if (currentValue != sourceParams.FloorNumber)
                        {
                            param.Set(sourceParams.FloorNumber);
                            transferredCount++;
                        }
                    }
                }

                // LG_Секция
                if (!string.IsNullOrEmpty(sourceParams.Section))
                {
                    Parameter param = targetElement.LookupParameter("LG_Секция");
                    if (param != null && param.StorageType == StorageType.String && !param.IsReadOnly)
                    {
                        string currentValue = GetStringParameterValue(param);
                        if (currentValue != sourceParams.Section)
                        {
                            param.Set(sourceParams.Section);
                            transferredCount++;
                        }
                    }
                }

                // LG_Номер корпуса
                if (!string.IsNullOrEmpty(sourceParams.BuildingNumber))
                {
                    Parameter param = targetElement.LookupParameter("LG_Номер корпуса");
                    if (param != null && param.StorageType == StorageType.String && !param.IsReadOnly)
                    {
                        string currentValue = GetStringParameterValue(param);
                        if (currentValue != sourceParams.BuildingNumber)
                        {
                            param.Set(sourceParams.BuildingNumber);
                            transferredCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Ошибка передачи параметров для элемента {targetElement.Id}: {ex.Message}");
            }

            return transferredCount;
        }

        // Логирование для отладки
        private void DebugLog(string message)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(message);
#endif
        }

        // Вспомогательный класс для хранения LG-параметров
        private class LGParameters
        {
            public string FloorNumber { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
            public string BuildingNumber { get; set; } = string.Empty;

            public bool IsEmpty()
            {
                return string.IsNullOrEmpty(FloorNumber) &&
                       string.IsNullOrEmpty(Section) &&
                       string.IsNullOrEmpty(BuildingNumber);
            }
        }
    }
}