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
        private const string POSITION_PARAMETER_NAME = "ADSK_Позиция";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

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
                    Element element = _doc.GetElement(reference);
                    if (element != null && HasPositionParameter(element))
                    {
                        selectedElements.Add(element);
                    }
                }

                if (selectedElements.Count == 0)
                {
                    TaskDialog.Show("Ошибка",
                        "Среди выбранных элементов нет ни одного с параметром ADSK_Позиция");
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

                    TaskDialog.Show("Готово",
                        $"Успешно промаркировано {markedCount} элементов\n" +
                        $"Порядок: {GetOrderDescription(dialog.Settings)}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }
        }

        private bool HasPositionParameter(Element element)
        {
            Parameter param = element.LookupParameter(POSITION_PARAMETER_NAME);
            return param != null && !param.IsReadOnly;
        }

        /// <summary>
        /// Маркировка элементов по сетке X-Y
        /// </summary>
        private int MarkByGrid(List<Element> elements, GridMarkingSettings settings)
        {
            int markedCount = 0;

            // Группируем элементы по строкам (Y-координата)
            var groupedByRow = elements
                .Select(e => new
                {
                    Element = e,
                    Center = GetElementCenter(e)
                })
                .GroupBy(item => GetRowKey(item.Center.Y, settings.RowTolerance))
                .OrderBy(group => settings.YDirection == SortDirection.Ascending
                    ? group.Average(x => x.Center.Y)
                    : -group.Average(x => x.Center.Y))
                .ToList();

            // Нумеруем строки
            int currentNumber = settings.StartNumber;

            foreach (var rowGroup in groupedByRow)
            {
                // Сортируем элементы в строке по X
                var sortedInRow = settings.XDirection == SortDirection.Ascending
                    ? rowGroup.OrderBy(x => x.Center.X).ToList()
                    : rowGroup.OrderByDescending(x => x.Center.X).ToList();

                // Маркируем элементы в строке
                foreach (var item in sortedInRow)
                {
                    if (SetPositionParameter(item.Element, currentNumber))
                    {
                        markedCount++;
                        currentNumber++;
                    }
                }

                // Если включен режим "только по X", продолжаем нумерацию
                if (!settings.MarkByRows)
                {
                    continue;
                }

                // Если нужно сбрасывать нумерацию для каждой строки
                if (settings.ResetNumberingPerRow)
                {
                    currentNumber = settings.StartNumber;
                }
            }

            return markedCount;
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
            BoundingBoxXYZ bb = element.get_BoundingBox(null);
            if (bb != null)
            {
                return (bb.Min + bb.Max) * 0.5;
            }

            if (element.Location is LocationPoint locationPoint)
            {
                return locationPoint.Point;
            }

            return XYZ.Zero;
        }

        private bool SetPositionParameter(Element element, int number)
        {
            Parameter param = element.LookupParameter(POSITION_PARAMETER_NAME);
            if (param == null || param.IsReadOnly)
                return false;

            try
            {
                string value = number.ToString();

                if (param.StorageType == StorageType.String)
                {
                    param.Set(value);
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
            catch
            {
                // Игнорируем ошибки
            }

            return false;
        }

        private string GetOrderDescription(GridMarkingSettings settings)
        {
            string xOrder = settings.XDirection == SortDirection.Ascending ? "слева направо" : "справа налево";
            string yOrder = settings.YDirection == SortDirection.Ascending ? "снизу вверх" : "сверху вниз";

            if (settings.MarkByRows)
            {
                return $"По строкам ({yOrder}), в строке {xOrder}";
            }
            else
            {
                return $"Непрерывно {xOrder} и {yOrder}";
            }
        }

        public enum SortDirection { Ascending, Descending }
    }

    /// <summary>
    /// Настройки маркировки по сетке
    /// </summary>
    public class GridMarkingSettings
    {
        public int StartNumber { get; set; } = 1;
        public SortDirection XDirection { get; set; } = SortDirection.Ascending;
        public SortDirection YDirection { get; set; } = SortDirection.Ascending;
        public bool MarkByRows { get; set; } = true;
        public bool ResetNumberingPerRow { get; set; } = false;
        public double RowTolerance { get; set; } = 0.1; // метров
    }
}