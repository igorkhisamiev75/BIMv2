#region Namespaces

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using static RevitAddIn2BIMRU.Commands.AN.MarkElementsByGrid;

#endregion

namespace RevitAddIn2BIMRU.Commands.AN
{
    [Transaction(TransactionMode.Manual)]
    public class MarkElementsByGrid : IExternalCommand
    {
        private Document _doc;
        private UIDocument _uiDoc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

            if (_uiDoc == null || _doc == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось получить доступ к документу Revit");
                return Result.Failed;
            }

            try
            {
                // Выбор элементов
                TaskDialog.Show("Выбор элементов",
                    "Выберите элементы для маркировки по сетке X-Y");

                IList<Reference> references;
                try
                {
                    references = _uiDoc.Selection.PickObjects(
                        ObjectType.Element,
                        "Выберите элементы для маркировки");
                }
                catch
                {
                    return Result.Cancelled;
                }

                if (references == null || references.Count == 0)
                    return Result.Cancelled;

                List<Element> selectedElements = new List<Element>();
                foreach (Reference reference in references)
                {
                    if (reference == null) continue;

                    Element element = _doc.GetElement(reference);
                    if (element != null && element.IsValidObject)
                    {
                        selectedElements.Add(element);
                    }
                }

                if (selectedElements.Count == 0)
                {
                    TaskDialog.Show("Ошибка",
                        "Не выбрано ни одного валидного элемента");
                    return Result.Cancelled;
                }

                // Показываем WPF окно
                var dialog = new GridMarkingWindow(selectedElements.Count);
                if (dialog.ShowDialog() != true)
                    return Result.Cancelled;

                // Маркируем по сетке
                using (Transaction tx = new Transaction(_doc))
                {
                    tx.Start("Маркировка элементов по сетке");

                    int markedCount = MarkByGrid(selectedElements, dialog.Settings);

                    tx.Commit();

                    if (markedCount > 0)
                    {
                        TaskDialog.Show("Готово",
                            $"Успешно промаркировано {markedCount} элементов\n" +
                            $"Параметр: {dialog.Settings.ParameterName}\n" +
                            $"Порядок: {GetOrderDescription(dialog.Settings)}");
                    }
                    else
                    {
                        TaskDialog.Show("Предупреждение",
                            "Ни один элемент не был промаркирован. " +
                            "Проверьте наличие и доступность параметра.");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка",
                    $"Произошла ошибка: {ex.Message}\n" +
                    $"StackTrace: {ex.StackTrace}");
                return Result.Failed;
            }
        }

        private bool HasPositionParameter(Element element, string parameterName)
        {
            if (element == null || !element.IsValidObject)
                return false;

            if (string.IsNullOrEmpty(parameterName))
                return false;

            Parameter param = element.LookupParameter(parameterName);
            return param != null && !param.IsReadOnly;
        }

        /// <summary>
        /// Маркировка элементов по сетке X-Y (одна непрерывная последовательность)
        /// </summary>
        private int MarkByGrid(List<Element> elements, GridMarkingSettings settings)
        {
            int markedCount = 0;
            int currentNumber = settings.StartNumber;

            // Фильтруем элементы, у которых есть нужный параметр
            var elementsWithParameter = new List<Element>();
            foreach (var element in elements)
            {
                if (element != null && element.IsValidObject &&
                    HasPositionParameter(element, settings.ParameterName))
                {
                    elementsWithParameter.Add(element);
                }
            }

            if (elementsWithParameter.Count == 0)
            {
                TaskDialog.Show("Ошибка",
                    $"Ни у одного из выбранных элементов нет параметра '{settings.ParameterName}' " +
                    "или параметр недоступен для записи");
                return 0;
            }

            // Получаем элементы с их координатами
            var elementsWithCoords = new List<ElementWithCoords>();
            foreach (var element in elementsWithParameter)
            {
                if (element != null && element.IsValidObject)
                {
                    XYZ center = GetElementCenter(element);
                    if (center != null)
                    {
                        elementsWithCoords.Add(new ElementWithCoords
                        {
                            Element = element,
                            Center = center
                        });
                    }
                }
            }

            if (elementsWithCoords.Count == 0)
            {
                TaskDialog.Show("Ошибка",
                    "Не удалось определить координаты элементов");
                return 0;
            }

            // Сортируем все элементы в одну последовательность
            var sortedElements = SortElementsInOneSequence(elementsWithCoords, settings);

            // Маркируем элементы в полученном порядке
            foreach (var item in sortedElements)
            {
                if (item != null && item.Element != null && item.Element.IsValidObject)
                {
                    if (SetPositionParameter(item.Element, currentNumber, settings.ParameterName))
                    {
                        markedCount++;
                        currentNumber++;
                    }
                }
            }

            return markedCount;
        }

        /// <summary>
        /// Сортировка элементов в одну последовательность на основе координат
        /// </summary>
        private List<ElementWithCoords> SortElementsInOneSequence(List<ElementWithCoords> elements, GridMarkingSettings settings)
        {
            if (elements == null || elements.Count == 0)
                return new List<ElementWithCoords>();

            // Основная логика: сначала сортируем по Y (строчкам), потом по X внутри строк

            // Группируем элементы по строкам (Y-координата) с учетом допуска
            var groupedByRow = elements
                .Where(item => item != null && item.Center != null)
                .GroupBy(item => GetRowKey(item.Center.Y, settings.RowTolerance))
                .ToList();

            // Определяем порядок сортировки строк
            var sortedRows = settings.YDirection == SortDirection.Ascending
                ? groupedByRow.OrderBy(group => group.Average(x => x.Center.Y)).ToList()
                : groupedByRow.OrderByDescending(group => group.Average(x => x.Center.Y)).ToList();

            // Собираем все элементы в одну последовательность
            var result = new List<ElementWithCoords>();

            foreach (var rowGroup in sortedRows)
            {
                if (rowGroup == null) continue;

                // Сортируем элементы внутри строки по X
                var sortedInRow = settings.XDirection == SortDirection.Ascending
                    ? rowGroup.OrderBy(x => x.Center.X).ToList()
                    : rowGroup.OrderByDescending(x => x.Center.X).ToList();

                result.AddRange(sortedInRow);
            }

            return result;
        }

        /// <summary>
        /// Определяет ключ группы для строки на основе Y-координаты
        /// </summary>
        private double GetRowKey(double y, double tolerance)
        {
            if (tolerance <= 0)
                return Math.Round(y, 3);

            // Округляем до ближайшего кратного tolerance
            return Math.Round(y / tolerance) * tolerance;
        }

        private XYZ GetElementCenter(Element element)
        {
            if (element == null || !element.IsValidObject)
                return XYZ.Zero;

            try
            {
                BoundingBoxXYZ bb = element.get_BoundingBox(null);
                if (bb != null && bb.Min != null && bb.Max != null)
                {
                    return (bb.Min + bb.Max) * 0.5;
                }

                if (element.Location is LocationPoint locationPoint && locationPoint != null)
                {
                    return locationPoint.Point;
                }

                // Пробуем получить геометрию элемента
                Options options = new Options();
                options.ComputeReferences = true;
                options.DetailLevel = ViewDetailLevel.Fine;

                GeometryElement geomElem = element.get_Geometry(options);
                if (geomElem != null)
                {
                    foreach (GeometryObject geomObj in geomElem)
                    {
                        if (geomObj is Solid solid && solid != null && solid.Volume > 0)
                        {
                            return solid.ComputeCentroid();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Предупреждение",
                    $"Не удалось получить координаты элемента {element.Id}: {ex.Message}");
            }

            return XYZ.Zero;
        }

        private bool SetPositionParameter(Element element, int number, string parameterName)
        {
            if (element == null || !element.IsValidObject)
                return false;

            Parameter param = element.LookupParameter(parameterName);
            if (param == null || param.IsReadOnly)
                return false;

            try
            {
                if (param.StorageType == StorageType.String)
                {
                    param.Set(number.ToString());
                    return true;
                }
                else if (param.StorageType == StorageType.Integer)
                {
                    param.Set(number);
                    return true;
                }
                else if (param.StorageType == StorageType.Double)
                {
                    param.Set((double)number);
                    return true;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка записи параметра",
                    $"Не удалось записать значение в параметр '{parameterName}' элемента {element.Id}: {ex.Message}");
            }

            return false;
        }

        private string GetOrderDescription(GridMarkingSettings settings)
        {
            string xOrder = settings.XDirection == SortDirection.Ascending ? "слева направо" : "справа налево";
            string yOrder = settings.YDirection == SortDirection.Ascending ? "снизу вверх" : "сверху вниз";

            return $"Сначала {yOrder} по строкам, в каждой строке {xOrder}";
        }

        // Вспомогательный класс для хранения элемента с координатами
        private class ElementWithCoords
        {
            public Element Element { get; set; }
            public XYZ Center { get; set; }
        }

        public enum SortDirection { Ascending, Descending }
    }

    /// <summary>
    /// Настройки маркировки по сетке
    /// </summary>
    public class GridMarkingSettings
    {
        public string ParameterName { get; set; } = "ADSK_Позиция";
        public int StartNumber { get; set; } = 1;
        public MarkElementsByGrid.SortDirection XDirection { get; set; } = MarkElementsByGrid.SortDirection.Ascending;
        public MarkElementsByGrid.SortDirection YDirection { get; set; } = MarkElementsByGrid.SortDirection.Ascending;
        public double RowTolerance { get; set; } = 0.1; // метров
    }
}