using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CleanRevitFile : IExternalCommand
    {
        private int _totalElementsDeleted = 0;
        private long _originalFileSize = 0;
        private Stopwatch _stopwatch;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            _stopwatch = Stopwatch.StartNew();

            try
            {
                // Проверяем, что файл сохранен
                if (string.IsNullOrEmpty(doc.PathName))
                {
                    TaskDialog.Show("Ошибка", "Файл должен быть сохранен перед очисткой");
                    return Result.Failed;
                }

                // Получаем исходный размер файла
                _originalFileSize = new FileInfo(doc.PathName).Length;

                // Запрашиваем подтверждение
                TaskDialogResult confirm = TaskDialog.Show("Очистка файла",
                    "Вы уверены, что хотите очистить файл?\n\nБудут удалены:\n" +
                    "- Все виды кроме 3D вида\n" +
                    "- Все спецификации, легенды\n" +
                    "- Все листы\n" +
                    "- Вся арматура и армирование\n" +
                    "- Все сборки\n\n" +
                    "Файл будет очищен без сохранения!",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (confirm != TaskDialogResult.Yes)
                {
                    return Result.Cancelled;
                }

                // 1. Сначала открываем 3D вид
                OpenDefault3DView(uiDoc);

                // 2. Очищаем текущий документ (минимальный набор операций)
                CleanCurrentDocumentMinimal(doc);

                // 3. Показываем результаты
                ShowResults();

                TaskDialog.Show("Готово", "Очистка завершена успешно!\n\nМожно продолжать работать!");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка",
                    $"Не удалось очистить файл: {ex.Message}\n\n" +
                    "Можно продолжать работать!");
                return Result.Failed;
            }
            finally
            {
                _stopwatch.Stop();
            }
        }

        private void OpenDefault3DView(UIDocument uiDoc)
        {
            try
            {
                Document doc = uiDoc.Document;

                // Ищем существующий 3D вид
                View3D view3D = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate && v.CanBePrinted);

                if (view3D != null)
                {
                    // Активируем найденный 3D вид
                    uiDoc.ActiveView = view3D;
                }

                // Закрываем все другие открытые виды
                CloseAllOtherViews(uiDoc);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при открытии 3D вида: {ex.Message}");
            }
        }

        private void CloseAllOtherViews(UIDocument uiDoc)
        {
            try
            {
                var openUIViews = uiDoc.GetOpenUIViews();
                var activeViewId = uiDoc.ActiveView.Id;

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

        private void CleanCurrentDocumentMinimal(Document doc)
        {
            _totalElementsDeleted = 0;

            // 1. Удаляем все листы (безопасно)
            _totalElementsDeleted += DeleteElementsMinimal<ViewSheet>(doc, "Удаление листов");

            // 2. Удаляем все спецификации (безопасно)
            _totalElementsDeleted += DeleteElementsMinimal<ViewSchedule>(doc, "Удаление спецификаций");

            // 3. Удаляем все легенды (безопасно)
            _totalElementsDeleted += DeleteLegendsMinimal(doc);

            // 4. Удаляем все виды кроме активного 3D (безопасно)
            _totalElementsDeleted += DeleteViewsExceptActiveMinimal(doc);

            // 5. Удаляем все сборки (безопасно)
            _totalElementsDeleted += DeleteElementsMinimal<AssemblyInstance>(doc, "Удаление сборок");

            // 6. Удаляем всю арматуру (безопасно)
            _totalElementsDeleted += DeleteRebarMinimal(doc);
        }

        private int DeleteElementsMinimal<T>(Document doc, string transactionName) where T : Element
        {
            int deletedCount = 0;

            try
            {
                // Получаем элементы для удаления
                var elements = new FilteredElementCollector(doc)
                    .OfClass(typeof(T))
                    .WhereElementIsNotElementType()
                    .ToList();

                if (elements.Count == 0)
                    return 0;

                using (Transaction trans = new Transaction(doc, transactionName))
                {
                    trans.Start();

                    try
                    {
                        // Удаляем элементы по одному с обработкой ошибок
                        foreach (Element element in elements)
                        {
                            try
                            {
                                // Пропускаем если элемент уже удален или невалиден
                                if (element == null || !element.IsValidObject)
                                    continue;

                                doc.Delete(element.Id);
                                deletedCount++;
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException)
                            {
                                // Игнорируем ошибки связанные с группами и зависимостями
                                continue;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении элемента {element?.Id}: {ex.Message}");
                                continue;
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка в транзакции {transactionName}: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Общая ошибка при {transactionName}: {ex.Message}");
            }

            return deletedCount;
        }

        private int DeleteLegendsMinimal(Document doc)
        {
            int deletedCount = 0;

            try
            {
                var legends = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .WhereElementIsNotElementType()
                    .Cast<View>()
                    .Where(v => v.ViewType == ViewType.Legend)
                    .ToList();

                if (legends.Count == 0)
                    return 0;

                using (Transaction trans = new Transaction(doc, "Удаление легенд"))
                {
                    trans.Start();

                    try
                    {
                        foreach (View legend in legends)
                        {
                            try
                            {
                                if (legend.IsValidObject)
                                {
                                    doc.Delete(legend.Id);
                                    deletedCount++;
                                }
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException)
                            {
                                continue;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении легенды {legend.Id}: {ex.Message}");
                                continue;
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при удалении легенд: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Общая ошибка при удалении легенд: {ex.Message}");
            }

            return deletedCount;
        }

        private int DeleteViewsExceptActiveMinimal(Document doc)
        {
            int deletedCount = 0;

            try
            {
                // Получаем активный вид
                View activeView = doc.ActiveView;
                ElementId activeViewId = activeView?.Id;

                if (activeViewId == null)
                    return 0;

                var viewsToDelete = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .WhereElementIsNotElementType()
                    .Cast<View>()
                    .Where(v => v.Id != activeViewId &&
                               !v.IsTemplate &&
                               v.CanBePrinted)
                    .ToList();

                if (viewsToDelete.Count == 0)
                    return 0;

                using (Transaction trans = new Transaction(doc, "Удаление видов кроме активного"))
                {
                    trans.Start();

                    try
                    {
                        foreach (View view in viewsToDelete)
                        {
                            try
                            {
                                if (view.IsValidObject)
                                {
                                    doc.Delete(view.Id);
                                    deletedCount++;
                                }
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException)
                            {
                                continue;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении вида {view.Id}: {ex.Message}");
                                continue;
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при удалении видов: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Общая ошибка при удалении видов: {ex.Message}");
            }

            return deletedCount;
        }

        private int DeleteRebarMinimal(Document doc)
        {
            int deletedCount = 0;

            try
            {
                // Список классов арматуры для удаления
                var rebarClasses = new Type[]
                {
                    typeof(Rebar),
                    typeof(RebarInSystem),
                    typeof(AreaReinforcement),
                    typeof(PathReinforcement),
                    typeof(FabricSheet),
                    typeof(FabricArea)
                };

                foreach (Type rebarType in rebarClasses)
                {
                    try
                    {
                        var rebarElements = new FilteredElementCollector(doc)
                            .OfClass(rebarType)
                            .WhereElementIsNotElementType()
                            .ToList();

                        if (rebarElements.Count == 0)
                            continue;

                        using (Transaction trans = new Transaction(doc, $"Удаление {rebarType.Name}"))
                        {
                            trans.Start();

                            try
                            {
                                foreach (Element element in rebarElements)
                                {
                                    try
                                    {
                                        if (element.IsValidObject)
                                        {
                                            doc.Delete(element.Id);
                                            deletedCount++;
                                        }
                                    }
                                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                                    {
                                        continue;
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Ошибка при удалении {rebarType.Name} {element.Id}: {ex.Message}");
                                        continue;
                                    }
                                }

                                trans.Commit();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении {rebarType.Name}: {ex.Message}");
                                trans.RollBack();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при обработке {rebarType.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Общая ошибка при удалении арматуры: {ex.Message}");
            }

            return deletedCount;
        }

        private void ShowResults()
        {
            _stopwatch.Stop();
            TimeSpan timeElapsed = _stopwatch.Elapsed;

            string resultsMessage = $@"ОЧИСТКА ЗАВЕРШЕНА УСПЕШНО!

📊 РЕЗУЛЬТАТЫ:
• Удалено элементов: {_totalElementsDeleted:N0}
• Время выполнения: {timeElapsed:mm\\:ss} мин.

✅ Основные ненужные элементы удалены!

МОЖНО ПРОДОЛЖАТЬ РАБОТУ!";

            TaskDialog.Show("Результаты очистки", resultsMessage);
        }
    }
}