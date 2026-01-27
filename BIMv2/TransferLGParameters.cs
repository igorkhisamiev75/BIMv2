using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                // Запрашиваем подтверждение у пользователя
                TaskDialogResult dialogResult = TaskDialog.Show("Передача параметров LG",
                    "Начать поиск формообразующих массовых элементов и элементов на текущем виде?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (dialogResult != TaskDialogResult.Yes)
                    return Result.Cancelled;

                // Создаем два списка
                List<Element> formElements = new List<Element>();
                List<Element> visibleElements = new List<Element>();

                // 1. Поиск формообразующих массовых элементов во всем проекте
                TaskDialog.Show("Информация", "Начинаю поиск массовых элементов во всем проекте...");

                // Фильтр для массовых элементов (OST_Mass)
                ElementCategoryFilter massFilter = new ElementCategoryFilter(BuiltInCategory.OST_Mass);

                FilteredElementCollector projectCollector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(massFilter);

                foreach (Element element in projectCollector)
                {
                    if (element == null) continue;

                    // Проверяем наличие параметров LG
                    Parameter floorParam = element.LookupParameter("LG_Номер этажа");
                    Parameter sectionParam = element.LookupParameter("LG_Секция");
                    Parameter buildingParam = element.LookupParameter("LG_Номер корпуса");

                    // Добавляем в список формообразующих, даже если нет параметров
                    // (чтобы передать параметры на элементы, которые пересекаются с массой)
                    formElements.Add(element);

                    // Логирование найденного массового элемента
                    Debug.WriteLine($"Найден массовый элемент: ID={element.Id}, Категория={element.Category?.Name}");
                }

                // 2. Поиск элементов, видимых на текущем виде
                TaskDialog.Show("Информация", "Начинаю поиск элементов на текущем виде...");

                View activeView = doc.ActiveView;
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc, activeView.Id)
                    .WhereElementIsNotElementType();

                foreach (Element element in viewCollector)
                {
                    if (element != null && element.Category != null)
                    {
                        // Исключаем массовые элементы из видимых (они уже в formElements)
                        if (element.Category.Id.IntegerValue != (int)BuiltInCategory.OST_Mass)
                        {
                            visibleElements.Add(element);
                        }
                    }
                }

                // 3. Выводим статистику по спискам
                string statistics = $"СТАТИСТИКА ПОИСКА ЭЛЕМЕНТОВ:\n\n" +
                                   $"Формообразующие массовые элементы:\n" +
                                   $"• Найдено масс: {formElements.Count}\n\n" +
                                   $"Элементы на текущем виде (исключая массы):\n" +
                                   $"• Текущий вид: {activeView.Name}\n" +
                                   $"• Видимых элементов: {visibleElements.Count}\n\n" +
                                   $"Хотите продолжить с передачей параметров?";

                dialogResult = TaskDialog.Show("Статистика поиска", statistics,
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (dialogResult != TaskDialogResult.Yes)
                    return Result.Cancelled;

                // 4. Выполняем передачу параметров
                return TransferParametersBetweenLists(doc, formElements, visibleElements, activeView);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Критическая ошибка", $"Ошибка при поиске элементов:\n{ex.Message}\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        private Result TransferParametersBetweenLists(Document doc, List<Element> formElements,
                                                     List<Element> visibleElements, View activeView)
        {
            using (Transaction trans = new Transaction(doc, "Передача параметров LG"))
            {
                trans.Start();

                try
                {
                    // Счетчики для статистики
                    int totalFormElements = formElements.Count;
                    int totalVisibleElements = visibleElements.Count;
                    int elementsProcessed = 0;
                    int parametersTransferred = 0;
                    int elementsWithTransferredParams = 0;

                    // Обрабатываем каждый формообразующий массовый элемент
                    for (int i = 0; i < formElements.Count; i++)
                    {
                        Element formElement = formElements[i];

                        // Обновляем прогресс
                        if ((i + 1) % 5 == 0 || i == formElements.Count - 1)
                        {
                            string status = $"Обработка массовых элементов...\n" +
                                          $"Обработано: {i + 1} из {formElements.Count}";
                            TaskDialog.Show("Прогресс", status, TaskDialogCommonButtons.Ok);
                        }

                        // Получаем параметры массового элемента
                        LGParameters sourceParams = GetLGParameters(formElement);

                        // Если нет параметров, пропускаем
                        if (sourceParams.IsEmpty())
                        {
                            Debug.WriteLine($"Массовый элемент ID={formElement.Id} не имеет LG параметров, пропускаем");
                            continue;
                        }

                        Debug.WriteLine($"Обработка массы ID={formElement.Id} с параметрами: " +
                                       $"Этаж={sourceParams.FloorNumber}, " +
                                       $"Секция={sourceParams.Section}, " +
                                       $"Корпус={sourceParams.BuildingNumber}");

                        // Проверяем пересечения с каждым целевым элементом на виде
                        int intersectedElements = 0;
                        foreach (Element targetElement in visibleElements)
                        {
                            try
                            {
                                // Проверяем пересечение
                                if (CheckElementsIntersection(formElement, targetElement, activeView))
                                {
                                    intersectedElements++;

                                    // Передаем параметры
                                    int transferredCount = TransferParametersToElement(targetElement, sourceParams);

                                    if (transferredCount > 0)
                                    {
                                        parametersTransferred += transferredCount;
                                        elementsWithTransferredParams++;

                                        Debug.WriteLine($"  Передано {transferredCount} параметров на элемент ID={targetElement.Id}, " +
                                                       $"Категория={targetElement.Category?.Name}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Ошибка при обработке элемента ID={targetElement.Id}: {ex.Message}");
                                continue;
                            }
                        }

                        Debug.WriteLine($"Масса ID={formElement.Id} пересекается с {intersectedElements} элементами");

                        elementsProcessed++;
                    }

                    trans.Commit();

                    // Выводим финальную статистику
                    string resultMessage = $"ПЕРЕДАЧА ПАРАМЕТРОВ ЗАВЕРШЕНА!\n\n" +
                                          $"ОБРАБОТАНО:\n" +
                                          $"• Массовых элементов: {elementsProcessed} из {totalFormElements}\n" +
                                          $"• Элементов на виде: {totalVisibleElements}\n\n" +
                                          $"РЕЗУЛЬТАТ:\n" +
                                          $"• Элементов с переданными параметрами: {elementsWithTransferredParams}\n" +
                                          $"• Всего передано параметров: {parametersTransferred}";

                    if (elementsProcessed == 0)
                    {
                        resultMessage += "\n\nВНИМАНИЕ: Ни один массовый элемент не имеет LG параметров для передачи!";
                    }

                    TaskDialog.Show("Готово", resultMessage);

                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Ошибка передачи",
                        $"Ошибка при передаче параметров:\n{ex.Message}\n\n{ex.StackTrace}");
                    return Result.Failed;
                }
            }
        }

        // Получение LG-параметров из элемента
        private LGParameters GetLGParameters(Element element)
        {
            LGParameters parameters = new LGParameters();

            parameters.FloorNumber = GetParameterValue(element, "LG_Номер этажа");
            parameters.Section = GetParameterValue(element, "LG_Секция");
            parameters.BuildingNumber = GetParameterValue(element, "LG_Номер корпуса");

            return parameters;
        }

        // Получение значения параметра по имени
        private string GetParameterValue(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param == null)
                return string.Empty;

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        string stringValue = param.AsString();
                        return string.IsNullOrEmpty(stringValue) ? string.Empty : stringValue.Trim();
                    case StorageType.Integer:
                        return param.AsInteger().ToString();
                    case StorageType.Double:
                        string valueString = param.AsValueString();
                        return string.IsNullOrEmpty(valueString) ?
                               param.AsDouble().ToString("F0") : valueString.Trim();
                    default:
                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        // Проверка пересечения элементов через BoundingBox
        private bool CheckElementsIntersection(Element elem1, Element elem2, View view)
        {
            try
            {
                BoundingBoxXYZ bbox1 = elem1.get_BoundingBox(view);
                BoundingBoxXYZ bbox2 = elem2.get_BoundingBox(view);

                if (bbox1 == null || bbox2 == null)
                    return false;

                // Простая проверка пересечения BoundingBox
                return (bbox1.Min.X <= bbox2.Max.X && bbox1.Max.X >= bbox2.Min.X) &&
                       (bbox1.Min.Y <= bbox2.Max.Y && bbox1.Max.Y >= bbox2.Min.Y) &&
                       (bbox1.Min.Z <= bbox2.Max.Z && bbox1.Max.Z >= bbox2.Min.Z);
            }
            catch
            {
                return false;
            }
        }

        // Передача параметров на целевой элемент
        private int TransferParametersToElement(Element targetElement, LGParameters sourceParams)
        {
            int transferredCount = 0;

            // LG_Номер этажа
            if (!string.IsNullOrEmpty(sourceParams.FloorNumber))
            {
                transferredCount += SetParameterValue(targetElement, "LG_Номер этажа", sourceParams.FloorNumber);
            }

            // LG_Секция
            if (!string.IsNullOrEmpty(sourceParams.Section))
            {
                transferredCount += SetParameterValue(targetElement, "LG_Секция", sourceParams.Section);
            }

            // LG_Номер корпуса
            if (!string.IsNullOrEmpty(sourceParams.BuildingNumber))
            {
                transferredCount += SetParameterValue(targetElement, "LG_Номер корпуса", sourceParams.BuildingNumber);
            }

            return transferredCount;
        }

        // Установка значения параметра
        private int SetParameterValue(Element element, string parameterName, string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            Parameter param = element.LookupParameter(parameterName);
            if (param != null && !param.IsReadOnly)
            {
                try
                {
                    if (param.StorageType == StorageType.String)
                    {
                        // Проверяем текущее значение
                        string currentValue = param.AsString() ?? string.Empty;
                        if (currentValue.Trim() != value.Trim())
                        {
                            param.Set(value);
                            return 1;
                        }
                    }
                    else if (param.StorageType == StorageType.Integer)
                    {
                        if (int.TryParse(value, out int intValue))
                        {
                            param.Set(intValue);
                            return 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка установки параметра {parameterName}='{value}' на элемент ID={element.Id}: {ex.Message}");
                    return 0;
                }
            }
            return 0;
        }

        // Класс для хранения LG-параметров
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