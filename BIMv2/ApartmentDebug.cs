using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace ApartmentNumbering
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ApartmentDebug : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document mainDoc = uiDoc.Document;

            StringBuilder report = new StringBuilder();

            try
            {
                // 1. Проверяем связанные модели
                var links = new FilteredElementCollector(mainDoc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(l => l.GetLinkDocument() != null)
                    .ToList();

                report.AppendLine($"Найдено связанных моделей: {links.Count}");

                foreach (var link in links)
                {
                    report.AppendLine($"  - {link.Name}");
                }

                // 2. Собираем информацию о параметрах
                if (links.Count > 0)
                {
                    var firstLink = links[0];
                    var linkDoc = firstLink.GetLinkDocument();

                    // Находим первое помещение
                    var room = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .FirstElement() as Room;

                    if (room != null)
                    {
                        report.AppendLine("\nПараметры помещения в связанной модели:");

                        // Проверяем конкретные параметры
                        string[] paramNames = {
                            
                            "ADSK_Тип квартиры",
                            "МГН2",
                            "ADSK_Площадь квартиры",
                            "ПорядковыйНомерДляТипа"
                        };

                        foreach (var paramName in paramNames)
                        {
                            var param = room.LookupParameter(paramName);
                            if (param != null)
                            {
                                report.AppendLine($"  {paramName}: найден, GUID={param.GUID}");
                            }
                            else
                            {
                                report.AppendLine($"  {paramName}: НЕ НАЙДЕН");
                            }
                        }
                    }
                }

                // Показываем отчет
                TaskDialog.Show("Отладка", report.ToString());

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                report.AppendLine($"\nОшибка: {ex.Message}");
                TaskDialog.Show("Отладка", report.ToString());
                return Result.Failed;
            }
        }
    }
}