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
                    "- Все виды кроме 3D вида\n" +
                    "- Все спецификации, легенды\n" +
                    "- Все листы\n" +
                    "- Вся арматура и армирование\n" +
                    "- Все группы и сборки\n\n" +
                    "Будет создана копия файла с префиксом '_почищено2bimRU'",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (confirm != TaskDialogResult.Yes)
                {
                    return Result.Cancelled;
                }

                // Очищаем файл
                CleanFile(doc, uiApp);

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

        private void CleanFile(Document doc, UIApplication uiApp)
        {
            // 1. Открываем 3D вид по умолчанию
            OpenDefault3DView(doc, uiApp);

            // 2. Закрываем все остальные виды
            CloseAllOtherViews(uiApp);

            // 3. Удаляем все виды, кроме активного
            DeleteAllViewsExceptActive(doc, uiApp);

            // 4. Удаляем все ведомости и спецификации, легенды
            DeleteAllSchedulesAndLegends(doc);

            // 5. Удаляем все листы
            DeleteAllSheets(doc);

            // 6. Открепляем все элементы
            UnpinAllElements(doc);

            // 7. Разгруппировываем все группы
            UngroupAllElements(doc);

            // 7.1 Разбираем все сборки
            DisassembleAllAssemblies(doc);

            // 8. Удаляем всю арматуру и армирование
            DeleteAllRebarAndAreaReinforcement(doc);

            // 9. Удаляем все неиспользуемые элементы циклически
            DeleteAllUnusedElements(doc);
        }

        private void OpenDefault3DView(Document doc, UIApplication uiApp)
        {
            try
            {
                // Ищем 3D вид
                var view3D = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate && v.CanBePrinted);

                if (view3D != null)
                {
                    uiApp.ActiveUIDocument.ActiveView = view3D;
                }
                else
                {
                    // Если нет 3D вида, создаем новый
                    using (Transaction trans = new Transaction(doc, "Создание 3D вида"))
                    {
                        trans.Start();
                        View3D newView3D = View3D.CreateIsometric(doc, new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).First().Id);
                        uiApp.ActiveUIDocument.ActiveView = newView3D;
                        trans.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при открытии 3D вида: {ex.Message}");
            }
        }

        private void CloseAllOtherViews(UIApplication uiApp)
        {
            try
            {
                var openUIViews = uiApp.ActiveUIDocument.GetOpenUIViews();
                var activeViewId = uiApp.ActiveUIDocument.ActiveView.Id;

                foreach (var uiView in openUIViews)
                {
                    if (uiView.ViewId != activeViewId)
                    {
                        uiView.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при закрытии видов: {ex.Message}");
            }
        }

        private void DeleteAllViewsExceptActive(Document doc, UIApplication uiApp)
        {
            using (Transaction trans = new Transaction(doc, "Удаление видов кроме активного"))
            {
                trans.Start();

                try
                {
                    var activeViewId = uiApp.ActiveUIDocument.ActiveView.Id;

                    var viewsToDelete = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .WhereElementIsNotElementType()
                        .Cast<View>()
                        .Where(v => v.Id != activeViewId &&
                                   !v.IsTemplate &&
                                   v.CanBePrinted)
                        .Select(v => v.Id)
                        .ToList();

                    DeleteElementsSafely(doc, viewsToDelete);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении видов: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteAllSchedulesAndLegends(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление спецификаций и легенд"))
            {
                trans.Start();

                try
                {
                    // Удаляем все спецификации
                    var schedules = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSchedule))
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    // Удаляем все легенды
                    var legends = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .WhereElementIsNotElementType()
                        .Cast<View>()
                        .Where(v => v.ViewType == ViewType.Legend)
                        .Select(v => v.Id)
                        .ToList();

                    List<ElementId> elementsToDelete = new List<ElementId>();
                    elementsToDelete.AddRange(schedules);
                    elementsToDelete.AddRange(legends);

                    DeleteElementsSafely(doc, elementsToDelete);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении спецификаций и легенд: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteAllSheets(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление листов"))
            {
                trans.Start();

                try
                {
                    var sheets = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    DeleteElementsSafely(doc, sheets);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении листов: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void UnpinAllElements(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Открепление элементов"))
            {
                trans.Start();

                try
                {
                    var pinnedElements = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .Where(e => e.Pinned)
                        .ToList();

                    foreach (Element element in pinnedElements)
                    {
                        try
                        {
                            element.Pinned = false;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка при откреплении элемента {element.Id}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при откреплении элементов: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void UngroupAllElements(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Разгруппировка элементов"))
            {
                trans.Start();

                try
                {
                    var groups = new FilteredElementCollector(doc)
                        .OfClass(typeof(Group))
                        .WhereElementIsNotElementType()
                        .Cast<Group>()
                        .ToList();

                    foreach (Group group in groups)
                    {
                        try
                        {
                            group.UngroupMembers();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка при разгруппировке группы {group.Id}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при разгруппировке: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DisassembleAllAssemblies(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Разборка сборок"))
            {
                trans.Start();

                try
                {
                    var assemblies = new FilteredElementCollector(doc)
                        .OfClass(typeof(AssemblyInstance))
                        .WhereElementIsNotElementType()
                        .Cast<AssemblyInstance>()
                        .ToList();

                    foreach (AssemblyInstance assembly in assemblies)
                    {
                        try
                        {
                            assembly.Disassemble();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка при разборке сборки {assembly.Id}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при разборке сборок: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteAllRebarAndAreaReinforcement(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Удаление арматуры"))
            {
                trans.Start();

                try
                {
                    List<ElementId> elementsToDelete = new List<ElementId>();

                    // Обычная арматура
                    var rebarElements = new FilteredElementCollector(doc)
                        .OfClass(typeof(Rebar))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(rebarElements);

                    // Системная арматура
                    var rebarInSystem = new FilteredElementCollector(doc)
                        .OfClass(typeof(RebarInSystem))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(rebarInSystem);

                    // Армирование по площади
                    var areaReinforcement = new FilteredElementCollector(doc)
                        .OfClass(typeof(AreaReinforcement))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(areaReinforcement);

                    // Хостинг арматуры по площади
                    var areaReinforcementHost = new FilteredElementCollector(doc)
                        .OfClass(typeof(PathReinforcement))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(areaReinforcementHost);

                    // Связанная арматура Fabric
                    var fabricReinforcement = new FilteredElementCollector(doc)
                        .OfClass(typeof(FabricSheet))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(fabricReinforcement);

                    var fabricAreas = new FilteredElementCollector(doc)
                        .OfClass(typeof(FabricArea))
                        .WhereElementIsNotElementType()
                        .ToElementIds();
                    elementsToDelete.AddRange(fabricAreas);

                    DeleteElementsSafely(doc, elementsToDelete);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении арматуры: {ex.Message}");
                }

                trans.Commit();
            }
        }

        private void DeleteAllUnusedElements(Document doc)
        {
            int previousCount = int.MaxValue;
            int currentCount = 0;
            int iteration = 0;
            const int maxIterations = 10;

            while (iteration < maxIterations)
            {
                iteration++;

                using (Transaction trans = new Transaction(doc, $"Удаление неиспользуемых элементов (итерация {iteration})"))
                {
                    trans.Start();

                    try
                    {
                        int deletedInThisIteration = 0;

                        // Удаляем неиспользуемые семейства
                        deletedInThisIteration += DeleteUnusedFamilies(doc);

                        // Удаляем неиспользуемые типы
                        deletedInThisIteration += DeleteUnusedTypes(doc);

                        // Удаляем неиспользуемые материалы
                        deletedInThisIteration += DeleteUnusedMaterials(doc);

                        trans.Commit();

                        if (deletedInThisIteration == 0)
                        {
                            break; // Больше нечего удалять
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка на итерации {iteration}: {ex.Message}");
                        trans.RollBack();
                        break;
                    }
                }
            }
        }

        private int DeleteUnusedFamilies(Document doc)
        {
            int deletedCount = 0;

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
                deletedCount = familiesToDelete.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении семейств: {ex.Message}");
            }

            return deletedCount;
        }

        private int DeleteUnusedTypes(Document doc)
        {
            int deletedCount = 0;

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
                deletedCount = typesToDelete.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении типов: {ex.Message}");
            }

            return deletedCount;
        }

        private int DeleteUnusedMaterials(Document doc)
        {
            int deletedCount = 0;

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
                deletedCount = materialsToDelete.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении материалов: {ex.Message}");
            }

            return deletedCount;
        }

        private void DeleteElementsSafely(Document doc, ICollection<ElementId> elementIds)
        {
            if (elementIds == null || elementIds.Count == 0)
                return;

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
                    }
                }
                catch
                {
                    // Игнорируем ошибки удаления
                }
            }
        }

        private string SaveCleanedCopy(Document doc)
        {
            string originalPath = doc.PathName;
            string directory = Path.GetDirectoryName(originalPath);
            string fileName = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            string cleanedFileName = $"{fileName}_почищено2bimRU{extension}";
            string cleanedFilePath = Path.Combine(directory, cleanedFileName);

            // Сохраняем копию
            SaveAsOptions options = new SaveAsOptions();
            options.OverwriteExistingFile = true;

            doc.SaveAs(cleanedFilePath, options);

            return cleanedFilePath;
        }
    }
}