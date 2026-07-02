using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class ExportPlansToPDF : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. Получаем выбранные элементы
                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                if (selectedIds == null || selectedIds.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "Пожалуйста, выберите один или несколько планов этажей в дереве проекта перед запуском команды.");
                    return Result.Failed;
                }

                // 2. Фильтруем только ViewPlan (планы этажей)
                List<ViewPlan> viewPlans = new List<ViewPlan>();
                foreach (ElementId id in selectedIds)
                {
                    Element elem = doc.GetElement(id);
                    if (elem is ViewPlan viewPlan)
                    {
                        viewPlans.Add(viewPlan);
                    }
                }

                if (viewPlans.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "Среди выбранных элементов нет ни одного плана этажа (ViewPlan).");
                    return Result.Failed;
                }

                // 3. Подтверждение количества экспортируемых видов (опционально)
                string confirmMsg = $"Будет экспортировано {viewPlans.Count} планов в отдельные PDF-файлы. Продолжить?";
                if (TaskDialog.Show("Подтверждение", confirmMsg, TaskDialogCommonButtons.Yes) != TaskDialogResult.Yes)
                {
                    return Result.Cancelled;
                }

                // 4. Выбор папки для сохранения
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Выберите папку для сохранения PDF-файлов";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                        return Result.Cancelled;

                    string outputPath = folderDialog.SelectedPath;

                    // 5. Экспорт каждого вида в отдельный PDF
                    int exportedCount = 0;
                    foreach (ViewPlan viewPlan in viewPlans)
                    {
                        try
                        {
                            // Очищаем имя от недопустимых символов
                            string cleanName = string.Join("_", viewPlan.Name.Split(Path.GetInvalidFileNameChars()));
                            if (string.IsNullOrWhiteSpace(cleanName))
                            {
                                cleanName = "UnnamedView";
                            }

                            // Настройки экспорта
                            PDFExportOptions options = new PDFExportOptions
                            {
                                FileName = cleanName,
                                Combine = false,          // Для отдельного файла каждого вида
                                ExportQuality = PDFExportQualityType.DPI300
                            };

                            // Экспортируем вид
                            List<ElementId> viewsToExport = new List<ElementId> { viewPlan.Id };
                            doc.Export(outputPath, viewsToExport, options);
                            exportedCount++;
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Ошибка", $"Не удалось экспортировать вид '{viewPlan.Name}': {ex.Message}");
                            // Продолжаем с остальными
                        }
                    }

                    // 6. Итог
                    TaskDialog.Show("Успешно", $"Экспортировано {exportedCount} из {viewPlans.Count} планов в папку:\n{outputPath}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка экспорта", $"Не удалось выполнить экспорт: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}