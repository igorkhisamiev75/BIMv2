//#region Namespace

//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;

//using System;
//using System.Collections.Generic;
//using System.Linq;

//#endregion

//namespace RevitAddIn2BIMRU.Commands.Parameters
//{
//    [Transaction(TransactionMode.Manual)]
//    public class RemoveDuplicateProjectParameters : IExternalCommand
//    {
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            UIApplication uiApp = commandData.Application;
//            UIDocument uiDoc = uiApp.ActiveUIDocument;
//            Document doc = uiDoc.Document;

//            try
//            {
//                // Получаем список дублированных параметров
//                List<DuplicateParameterInfo> duplicates = GetDuplicateParameters(doc);

//                if (duplicates.Count == 0)
//                {
//                    TaskDialog.Show("Проверка параметров", "Дублированные параметры проекта не обнаружены.");
//                    return Result.Succeeded;
//                }

//                // Показываем диалог с найденными дубликатами
//                TaskDialogResult dialogResult = ShowDuplicateParametersDialog(duplicates);

//                if (dialogResult == TaskDialogResult.Yes)
//                {
//                    // Удаляем дублированные параметры
//                    RemoveDuplicates(doc, duplicates);

//                    TaskDialog.Show("Успех", $"Удалено {duplicates.Count} дублированных параметров.");
//                }
//                else if (dialogResult == TaskDialogResult.No)
//                {
//                    // Показываем отчет без удаления
//                    ShowParametersReport(duplicates);
//                }

//                return Result.Succeeded;
//            }
//            catch (Exception ex)
//            {
//                message = ex.Message;
//                TaskDialog.Show("Ошибка", $"Не удалось удалить дублированные параметры:\n{ex.Message}");
//                return Result.Failed;
//            }
//        }

//        #region Вспомогательные методы

//        /// <summary>
//        /// Информация о дублированном параметре
//        /// </summary>
//        private class DuplicateParameterInfo
//        {
//            public string ParameterName { get; set; }
//            public int InstanceCount { get; set; }
//            public List<ElementId> CategoryIds { get; set; }
//            public List<string> CategoryNames { get; set; }
//            public Definition Definition { get; set; }
//            public ElementBinding Binding { get; set; }
//        }

//        /// <summary>
//        /// Получает список дублированных параметров проекта
//        /// </summary>
//        private List<DuplicateParameterInfo> GetDuplicateParameters(Document doc)
//        {
//            var duplicates = new List<DuplicateParameterInfo>();
//            var parameterGroups = new Dictionary<string, List<DuplicateParameterInfo>>();

//            BindingMap bindingMap = doc.ParameterBindings;

//            // Собираем все параметры проекта
//            foreach (Definition definition in bindingMap)
//            {
//                ElementBinding binding = bindingMap.get_Item(definition) as ElementBinding;

//                if (binding != null)
//                {
//                    string paramName = definition.Name.Trim();
//                    string key = paramName.ToLowerInvariant();

//                    var paramInfo = new DuplicateParameterInfo
//                    {
//                        ParameterName = paramName,
//                        InstanceCount = 1,
//                        CategoryIds = new List<ElementId>(),
//                        CategoryNames = new List<string>(),
//                        Definition = definition,
//                        Binding = binding
//                    };

//                    // Получаем категории параметра
//                    foreach (Category category in binding.Categories)
//                    {
//                        paramInfo.CategoryIds.Add(category.Id);
//                        paramInfo.CategoryNames.Add(category.Name);
//                    }

//                    // Группируем по имени
//                    if (!parameterGroups.ContainsKey(key))
//                    {
//                        parameterGroups[key] = new List<DuplicateParameterInfo>();
//                    }
//                    parameterGroups[key].Add(paramInfo);
//                }
//            }

//            // Находим группы с дубликатами
//            foreach (var group in parameterGroups)
//            {
//                if (group.Value.Count > 1)
//                {
//                    // Увеличиваем счетчик для каждого параметра в группе
//                    foreach (var paramInfo in group.Value)
//                    {
//                        paramInfo.InstanceCount = group.Value.Count;
//                        duplicates.Add(paramInfo);
//                    }
//                }
//            }

//            return duplicates.OrderBy(d => d.ParameterName).ToList();
//        }

//        /// <summary>
//        /// Показывает диалог с найденными дубликатами
//        /// </summary>
//        private TaskDialogResult ShowDuplicateParametersDialog(List<DuplicateParameterInfo> duplicates)
//        {
//            TaskDialog dialog = new TaskDialog("Дублированные параметры проекта");
//            dialog.MainInstruction = $"Найдено {duplicates.Count} дублированных параметров";

//            // Создаем список параметров для отображения
//            string parameterList = "";
//            int uniqueCount = duplicates.GroupBy(d => d.ParameterName.ToLowerInvariant()).Count();

//            foreach (var group in duplicates.GroupBy(d => d.ParameterName.ToLowerInvariant()))
//            {
//                var firstParam = group.First();
//                parameterList += $"\n• {firstParam.ParameterName} (копий: {firstParam.InstanceCount})";

//                if (firstParam.CategoryNames.Any())
//                {
//                    parameterList += $"\n  Категории: {string.Join(", ", firstParam.CategoryNames.Take(3))}";
//                    if (firstParam.CategoryNames.Count > 3)
//                        parameterList += "...";
//                }
//            }

//            dialog.MainContent = $"Найдено {uniqueCount} параметров с дубликатами:\n{parameterList}\n\nВыберите действие:";

//            dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel;
//            dialog.DefaultButton = TaskDialogResult.Yes;

//            dialog.VerificationText = "Показать подробный отчет перед удалением";
//            dialog.VerificationChecked = true;

//            dialog.FooterText = "Внимание: Удаление параметров может повлиять на проект.\nРекомендуется создать резервную копию.";

//            var result = dialog.Show();

//            // Если выбрана галочка, показываем подробный отчет
//            if (dialog.WasVerificationChecked() && result != TaskDialogResult.Cancel)
//            {
//                ShowParametersReport(duplicates);
//            }

//            return result;
//        }

//        /// <summary>
//        /// Показывает подробный отчет о параметрах
//        /// </summary>
//        private void ShowParametersReport(List<DuplicateParameterInfo> duplicates)
//        {
//            string report = "ДЕТАЛЬНЫЙ ОТЧЕТ ПО ДУБЛИРОВАННЫМ ПАРАМЕТРАМ\n";
//            report += "=============================================\n\n";

//            foreach (var group in duplicates.GroupBy(d => d.ParameterName.ToLowerInvariant()))
//            {
//                report += $"ПАРАМЕТР: {group.First().ParameterName}\n";
//                report += $"Количество копий: {group.Count()}\n";
//                report += "---------------------------------------------\n";

//                int counter = 1;
//                foreach (var param in group)
//                {
//                    report += $"\nКопия #{counter++}:\n";
//                    report += $"  GUID: {param.Definition?.Id.ToString() ?? "N/A"}\n";
//                    report += $"  Тип: {param.Definition?.ParameterType.ToString() ?? "N/A"}\n";
//                    report += $"  Вид: {param.Definition?.ParameterGroup.ToString() ?? "N/A"}\n";

//                    if (param.CategoryNames.Any())
//                    {
//                        report += $"  Категории ({param.CategoryNames.Count}):\n";
//                        foreach (string category in param.CategoryNames)
//                        {
//                            report += $"    - {category}\n";
//                        }
//                    }
//                    else
//                    {
//                        report += "  Категории: не назначены\n";
//                    }
//                }
//                report += "\n" + new string('=', 50) + "\n\n";
//            }

//            // Показываем отчет в TaskDialog
//            TaskDialog reportDialog = new TaskDialog("Отчет по дублированным параметрам");
//            reportDialog.MainInstruction = "Подробный отчет";
//            reportDialog.MainContent = report.Length > 2000 ? report.Substring(0, 2000) + "..." : report;
//            reportDialog.CommonButtons = TaskDialogCommonButtons.Ok;

//            reportDialog.Show();
//        }

//        /// <summary>
//        /// Удаляет дублированные параметры
//        /// </summary>
//        private void RemoveDuplicates(Document doc, List<DuplicateParameterInfo> duplicates)
//        {
//            using (Transaction trans = new Transaction(doc, "Удаление дублированных параметров"))
//            {
//                trans.Start();

//                try
//                {
//                    BindingMap bindingMap = doc.ParameterBindings;
//                    var processedGroups = new HashSet<string>();
//                    int removedCount = 0;

//                    foreach (var group in duplicates.GroupBy(d => d.ParameterName.ToLowerInvariant()))
//                    {
//                        string groupKey = group.Key;

//                        // Пропускаем уже обработанные группы
//                        if (processedGroups.Contains(groupKey))
//                            continue;

//                        // Оставляем первую копию, остальные удаляем
//                        bool keepFirst = true;

//                        foreach (var param in group)
//                        {
//                            if (keepFirst)
//                            {
//                                // Оставляем первую копию
//                                keepFirst = false;
//                                continue;
//                            }

//                            // Пытаемся удалить параметр
//                            if (bindingMap.Contains(param.Definition))
//                            {
//                                bool removed = bindingMap.Remove(param.Definition);
//                                if (removed)
//                                {
//                                    removedCount++;
//                                }
//                            }
//                        }

//                        processedGroups.Add(groupKey);
//                    }

//                    trans.Commit();

//                    // Обновляем интерфейс
//                    doc.Regenerate();
//                }
//                catch (Exception ex)
//                {
//                    trans.RollBack();
//                    throw new Exception($"Не удалось удалить параметры: {ex.Message}", ex);
//                }
//            }
//        }

//        #endregion
//    }
//}