using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CleanRevitFile : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Проверяем, что файл сохранен
                if (string.IsNullOrEmpty(doc.PathName))
                {
                    TaskDialog.Show("Ошибка", "Файл должен быть сохранен перед очисткой");
                    return Result.Failed;
                }

                // Запрашиваем подтверждение
                TaskDialogResult confirm = TaskDialog.Show("Очистка файла",
                    "Вы уверены, что хотите очистить файл?\n\nБудут удалены:\n" +
                    "- Все неиспользуемые элементы\n" +
                    "- Все виды, спецификации, легенды\n" +
                    "- Все листы\n" +
                    "- Вся арматура\n\n" +
                    "Будет создана копия файла с префиксом '_очищенно'",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (confirm != TaskDialogResult.Yes)
                {
                    return Result.Cancelled;
                }

                // Очищаем файл
                CleanFile(doc);

                // Сохраняем копию
                string cleanedFilePath = SaveCleanedCopy(doc);

                TaskDialog.Show("Успех",
                    $"Файл успешно очищен и сохранен как:\n{cleanedFilePath}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Не удалось очистить файл: {ex.Message}");
                return Result.Failed;
            }
        }

        private void CleanFile(Document doc)
        {
            // 1. Удаляем все виды, спецификации, легенды, листы
            DeleteAllViewsAndSheets(doc);

            // 2. Удаляем всю арматуру
            DeleteAllRebar(doc);

            // 3. Удаляем другие неиспользуемые элементы
            DeleteUnusedElements(doc);
        }

        private void DeleteAllRebar(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление арматуры"))
            {
                trans.Start();

                try
                {
                    // Находим всю арматуру
                    var rebarElements = new FilteredElementCollector(doc)
                        .OfClass(typeof(Rebar))
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    // Находим хостинг арматуры
                    var rebarHostElements = new FilteredElementCollector(doc)
                        .OfClass(typeof(RebarInSystem))
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    List<ElementId> elementsToDelete = new List<ElementId>();
                    elementsToDelete.AddRange(rebarElements);
                    elementsToDelete.AddRange(rebarHostElements);

                    // Удаляем поэлементно с обработкой ошибок
                    DeleteElementsSafely(doc, elementsToDelete);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Предупреждение", $"Не удалось удалить всю арматуру: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteAllViewsAndSheets(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление видов и листов"))
            {
                trans.Start();

                try
                {
                    List<ElementId> elementsToDelete = new List<ElementId>();

                    // Удаляем все листы
                    var sheets = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(sheets);

                    // Удаляем все спецификации и расписания
                    var schedules = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSchedule))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(schedules);

                    // Удаляем все легенды
                    var legends = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .WhereElementIsNotElementType()
                        .Cast<View>()
                        .Where(v => v.ViewType == ViewType.Legend)
                        .Select(v => v.Id)
                        .ToList();
                    elementsToDelete.AddRange(legends);

                    // Удаляем все 3D виды
                    var view3Ds = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .WhereElementIsNotElementType()
                        .Cast<View>()
                        .Where(v => v.ViewType == ViewType.ThreeD)
                        .Select(v => v.Id)
                        .ToList();
                    elementsToDelete.AddRange(view3Ds);

                    // Удаляем все планы этажей
                    var floorPlans = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .WhereElementIsNotElementType()
                        .Cast<View>()
                        .Where(v => v.ViewType == ViewType.FloorPlan)
                        .Select(v => v.Id)
                        .ToList();
                    elementsToDelete.AddRange(floorPlans);

                    // Удаляем все разрезы
                    var sections = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .WhereElementIsNotElementType()
                        .Cast<View>()
                        .Where(v => v.ViewType == ViewType.Section)
                        .Select(v => v.Id)
                        .ToList();
                    elementsToDelete.AddRange(sections);

                    // Удаляем все фасады
                    var elevations = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .WhereElementIsNotElementType()
                        .Cast<View>()
                        .Where(v => v.ViewType == ViewType.Elevation)
                        .Select(v => v.Id)
                        .ToList();
                    elementsToDelete.AddRange(elevations);

                    // Удаляем поэлементно с обработкой ошибок
                    DeleteElementsSafely(doc, elementsToDelete);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Предупреждение", $"Не удалось удалить все виды и листы: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteElementsSafely(Document doc, ICollection<ElementId> elementIds)
        {
            if (elementIds == null || elementIds.Count == 0)
                return;

            List<ElementId> successfullyDeleted = new List<ElementId>();
            List<ElementId> failedToDelete = new List<ElementId>();

            foreach (ElementId elementId in elementIds)
            {
                try
                {
                    // Проверяем, существует ли элемент
                    Element element = doc.GetElement(elementId);
                    if (element != null)
                    {
                        // Пытаемся удалить элемент
                        doc.Delete(elementId);
                        successfullyDeleted.Add(elementId);
                    }
                    else
                    {
                        failedToDelete.Add(elementId);
                    }
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                {
                    // Элемент нельзя удалить
                    failedToDelete.Add(elementId);
                    System.Diagnostics.Debug.WriteLine($"Не удалось удалить элемент {elementId}: {ex.Message}");
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                {
                    // Операция недопустима для этого элемента
                    failedToDelete.Add(elementId);
                    System.Diagnostics.Debug.WriteLine($"Не удалось удалить элемент {elementId}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Другие ошибки
                    failedToDelete.Add(elementId);
                    System.Diagnostics.Debug.WriteLine($"Не удалось удалить элемент {elementId}: {ex.Message}");
                }
            }

            // Логируем результаты
            if (failedToDelete.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось удалить {failedToDelete.Count} элементов");
            }
        }

        private void DeleteUnusedElements(Document doc)
        {
            // Удаляем неиспользуемые семейства
            DeleteUnusedFamilies(doc);

            // Удаляем неиспользуемые типы
            DeleteUnusedTypes(doc);

            // Удаляем неиспользуемые материалы
            DeleteUnusedMaterials(doc);
        }

        private void DeleteUnusedFamilies(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление неиспользуемых семейств"))
            {
                trans.Start();

                try
                {
                    var allFamilies = new FilteredElementCollector(doc)
                        .OfClass(typeof(Family))
                        .Cast<Family>()
                        .ToList();

                    List<ElementId> familiesToDelete = new List<ElementId>();

                    foreach (Family family in allFamilies)
                    {
                        try
                        {
                            // Пропускаем системные семейства
                            if (family.IsInPlace)
                                continue;

                            // Проверяем, есть ли экземпляры этого семейства
                            var familyInstances = new FilteredElementCollector(doc)
                                .WhereElementIsNotElementType()
                                .OfCategoryId(family.FamilyCategoryId)
                                .Where(e => e.GetTypeId() != ElementId.InvalidElementId)
                                .Select(e => doc.GetElement(e.GetTypeId()) as ElementType)
                                .Where(t => t != null && t.FamilyName == family.Name)
                                .Any();

                            if (!familyInstances)
                            {
                                familiesToDelete.Add(family.Id);
                            }
                        }
                        catch
                        {
                            // Пропускаем проблемные семейства
                        }
                    }

                    DeleteElementsSafely(doc, familiesToDelete);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении семейств: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteUnusedTypes(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление неиспользуемых типов"))
            {
                trans.Start();

                try
                {
                    var allTypes = new FilteredElementCollector(doc)
                        .OfClass(typeof(ElementType))
                        .Cast<ElementType>()
                        .ToList();

                    List<ElementId> typesToDelete = new List<ElementId>();

                    foreach (ElementType type in allTypes)
                    {
                        try
                        {
                            // Пропускаем системные типы
                            if (type.FamilyName == "Системное семейство" || type.Name.Contains("Система"))
                                continue;

                            // Проверяем, используется ли тип
                            var instances = new FilteredElementCollector(doc)
                                .WhereElementIsNotElementType()
                                .Where(e => e.GetTypeId() == type.Id)
                                .Take(1)
                                .Any();

                            if (!instances)
                            {
                                typesToDelete.Add(type.Id);
                            }
                        }
                        catch
                        {
                            // Пропускаем проблемные типы
                        }
                    }

                    DeleteElementsSafely(doc, typesToDelete);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении типов: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteUnusedMaterials(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление неиспользуемых материалов"))
            {
                trans.Start();

                try
                {
                    var allMaterials = new FilteredElementCollector(doc)
                        .OfClass(typeof(Material))
                        .Cast<Material>()
                        .ToList();

                    List<ElementId> materialsToDelete = new List<ElementId>();

                    foreach (Material material in allMaterials)
                    {
                        try
                        {
                            // Пропускаем базовые материалы
                            if (material.Name == "По умолчанию" || material.Name == "Default")
                                continue;

                            // Проверяем, используется ли материал
                            var dependentElements = material.GetDependentElements(new ElementClassFilter(typeof(Element)));
                            if (dependentElements.Count == 0)
                            {
                                materialsToDelete.Add(material.Id);
                            }
                        }
                        catch
                        {
                            // Пропускаем проблемные материалы
                        }
                    }

                    DeleteElementsSafely(doc, materialsToDelete);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении материалов: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private string SaveCleanedCopy(Document doc)
        {
            string originalPath = doc.PathName;
            string directory = Path.GetDirectoryName(originalPath);
            string fileName = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            string cleanedFileName = $"{fileName}_очищенно{extension}";
            string cleanedFilePath = Path.Combine(directory, cleanedFileName);

            // Сохраняем копию
            SaveAsOptions options = new SaveAsOptions();
            options.OverwriteExistingFile = true;

            doc.SaveAs(cleanedFilePath, options);

            return cleanedFilePath;
        }
    }
}