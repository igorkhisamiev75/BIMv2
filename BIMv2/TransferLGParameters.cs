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
                    $"Выполнить передачу параметров с формообразующих элементов?\nТекущий вид: {activeView.Name}",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (dialogResult != TaskDialogResult.Yes)
                    return Result.Cancelled;

                // Запускаем транзакцию
                using (Transaction trans = new Transaction(doc, "Передача параметров LG"))
                {
                    trans.Start();

                    try
                    {
                        // Получаем все элементы на текущем виде
                        FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
                        ICollection<ElementId> elementIdsOnView = collector.WhereElementIsNotElementType().ToElementIds();

                        // Разделяем элементы на формообразующие и обычные
                        List<Element> formElements = new List<Element>();
                        List<Element> otherElements = new List<Element>();

                        foreach (ElementId id in elementIdsOnView)
                        {
                            Element elem = doc.GetElement(id);
                            if (elem == null) continue;

                            // Проверяем наличие параметров LG у элемента
                            if (HasLGParameters(elem))
                            {
                                formElements.Add(elem);
                            }
                            else
                            {
                                otherElements.Add(elem);
                            }
                        }

                        // Счетчики для статистики
                        int elementsWithLG = 0;
                        int elementsUpdated = 0;
                        int totalParametersTransferred = 0;

                        // Проверяем пересечения и передаем параметры
                        foreach (Element formElement in formElements)
                        {
                            elementsWithLG++;

                            // Получаем LG-параметры формообразующего элемента
                            LGParameters sourceParams = GetLGParameters(formElement);

                            // Пропускаем если нет значений для передачи
                            if (sourceParams.IsEmpty())
                                continue;

                            // Ищем пересекающиеся элементы
                            foreach (Element otherElement in otherElements)
                            {
                                // Проверяем пересечение
                                if (CheckElementsIntersection(formElement, otherElement, activeView))
                                {
                                    // Передаем параметры
                                    int transferredCount = TransferParameters(otherElement, sourceParams);

                                    if (transferredCount > 0)
                                    {
                                        elementsUpdated++;
                                        totalParametersTransferred += transferredCount;
                                    }
                                }
                            }
                        }

                        trans.Commit();

                        // Показываем результаты
                        TaskDialog.Show("Готово",
                            $"Передача параметров LG завершена!\n\n" +
                            $"Найдено формообразующих элементов: {elementsWithLG}\n" +
                            $"Обновлено элементов: {elementsUpdated}\n" +
                            $"Передано параметров: {totalParametersTransferred}");
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
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

        // Проверка наличия LG-параметров у элемента
        private bool HasLGParameters(Element element)
        {
            return element.LookupParameter("LG_Номер этажа") != null ||
                   element.LookupParameter("LG_Секция") != null ||
                   element.LookupParameter("LG_Номер корпуса") != null;
        }

        // Получение LG-параметров из элемента
        private LGParameters GetLGParameters(Element element)
        {
            LGParameters parameters = new LGParameters();

            Parameter floorParam = element.LookupParameter("LG_Номер этажа");
            Parameter sectionParam = element.LookupParameter("LG_Секция");
            Parameter buildingParam = element.LookupParameter("LG_Номер корпуса");

            if (floorParam != null)
                parameters.FloorNumber = GetParameterValue(floorParam);

            if (sectionParam != null)
                parameters.Section = GetParameterValue(sectionParam);

            if (buildingParam != null)
                parameters.BuildingNumber = GetParameterValue(buildingParam);

            return parameters;
        }

        // Получение строкового значения параметра
        private string GetParameterValue(Parameter parameter)
        {
            if (parameter == null)
                return string.Empty;

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString() ?? string.Empty;
                case StorageType.Integer:
                    return parameter.AsInteger().ToString();
                case StorageType.Double:
                    return parameter.AsValueString() ?? parameter.AsDouble().ToString();
                default:
                    return string.Empty;
            }
        }

        // Проверка пересечения элементов
        private bool CheckElementsIntersection(Element elem1, Element elem2, View view)
        {
            try
            {
                // Получаем ограничивающие рамки
                BoundingBoxXYZ bbox1 = elem1.get_BoundingBox(view);
                BoundingBoxXYZ bbox2 = elem2.get_BoundingBox(view);

                if (bbox1 == null || bbox2 == null)
                    return false;

                // Проверяем пересечение ограничивающих рамок
                return (bbox1.Min.X <= bbox2.Max.X && bbox1.Max.X >= bbox2.Min.X) &&
                       (bbox1.Min.Y <= bbox2.Max.Y && bbox1.Max.Y >= bbox2.Min.Y) &&
                       (bbox1.Min.Z <= bbox2.Max.Z && bbox1.Max.Z >= bbox2.Min.Z);
            }
            catch
            {
                return false;
            }
        }

        // Передача параметров на элемент
        private int TransferParameters(Element targetElement, LGParameters sourceParams)
        {
            int transferredCount = 0;

            // LG_Номер этажа
            if (!string.IsNullOrEmpty(sourceParams.FloorNumber))
            {
                Parameter param = targetElement.LookupParameter("LG_Номер этажа");
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
                {
                    param.Set(sourceParams.FloorNumber);
                    transferredCount++;
                }
            }

            // LG_Секция
            if (!string.IsNullOrEmpty(sourceParams.Section))
            {
                Parameter param = targetElement.LookupParameter("LG_Секция");
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
                {
                    param.Set(sourceParams.Section);
                    transferredCount++;
                }
            }

            // LG_Номер корпуса
            if (!string.IsNullOrEmpty(sourceParams.BuildingNumber))
            {
                Parameter param = targetElement.LookupParameter("LG_Номер корпуса");
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
                {
                    param.Set(sourceParams.BuildingNumber);
                    transferredCount++;
                }
            }

            return transferredCount;
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