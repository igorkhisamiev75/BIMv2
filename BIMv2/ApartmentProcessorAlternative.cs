using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace ApartmentNumbering
{
    // Этот класс будет запускать обработку файлов по одному
    [Transaction(TransactionMode.Manual)]
    public class BatchApartmentProcessor : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            try
            {
                // Создаем диалог для выбора файлов
                var dialog = new System.Windows.Forms.OpenFileDialog();
                dialog.Filter = "Revit Files (*.rvt)|*.rvt";
                dialog.Multiselect = true;
                dialog.Title = "Выберите файлы для обработки";

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                // Обрабатываем каждый выбранный файл
                var results = new List<string>();
                int totalProcessed = 0;

                foreach (string filePath in dialog.FileNames)
                {
                    try
                    {
                        int processed = ProcessFile(uiApp.Application, filePath);
                        totalProcessed += processed;
                        results.Add($"{Path.GetFileName(filePath)}: {processed} квартир");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"{Path.GetFileName(filePath)}: ОШИБКА - {ex.Message}");
                    }
                }

                // Показываем результаты
                StringBuilder report = new StringBuilder();
                report.AppendLine("РЕЗУЛЬТАТЫ ОБРАБОТКИ:");
                report.AppendLine($"Файлов: {dialog.FileNames.Length}");
                report.AppendLine($"Квартир: {totalProcessed}");
                report.AppendLine();

                foreach (var result in results)
                {
                    report.AppendLine(result);
                }

                TaskDialog.Show("Готово", report.ToString());

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        private int ProcessFile(Application app, string filePath)
        {
            int processed = 0;

            // Открываем файл как основной документ
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

            using (Document doc = app.OpenDocumentFile(modelPath, new OpenOptions()))
            {
                if (doc == null) return 0;

                try
                {
                    // Собираем квартиры
                    var apartments = CollectApartments(doc);

                    if (apartments.Count > 0)
                    {
                        // Вычисляем номера
                        CalculateNumbers(apartments);

                        // Записываем
                        processed = WriteNumbers(doc, apartments);

                        // Сохраняем
                        if (processed > 0)
                        {
                            doc.Save();
                        }
                    }
                }
                finally
                {
                    doc.Close(false);
                }
            }

            return processed;
        }

        private List<Apartment> CollectApartments(Document doc)
        {
            var apartments = new List<Apartment>();

            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.LookupParameter("Назначение")?.AsString() == "Квартиры")
                .ToList();

            foreach (var room in rooms)
            {
                var aptType = room.LookupParameter("ADSK_Тип квартиры");
                var mgn2 = room.LookupParameter("МГН2");
                var area = room.LookupParameter("ADSK_Площадь квартиры");

                if (aptType != null && mgn2 != null && area != null)
                {
                    apartments.Add(new Apartment
                    {
                        Room = room,
                        Type = aptType.AsString(),
                        MGN2 = mgn2.AsString(),
                        Area = area.AsDouble()
                    });
                }
            }

            return apartments;
        }

        private void CalculateNumbers(List<Apartment> apartments)
        {
            var groups = apartments.GroupBy(a => new { a.Type, a.MGN2 });

            foreach (var group in groups)
            {
                var sorted = group.OrderBy(a => a.Area).ToList();
                var areaDict = new Dictionary<double, int>();
                int counter = 1;

                foreach (var apt in sorted)
                {
                    double roundedArea = Math.Round(apt.Area, 2);

                    if (!areaDict.ContainsKey(roundedArea))
                    {
                        areaDict[roundedArea] = counter;
                        counter++;
                    }

                    apt.OrdinalNumber = areaDict[roundedArea];
                }
            }
        }

        private int WriteNumbers(Document doc, List<Apartment> apartments)
        {
            int processed = 0;

            using (Transaction trans = new Transaction(doc, "Нумерация квартир"))
            {
                trans.Start();

                foreach (var apt in apartments)
                {
                    var ordinalParam = apt.Room.LookupParameter("ПорядковыйНомерДляТипа");
                    if (ordinalParam != null && !ordinalParam.IsReadOnly)
                    {
                        if (ordinalParam.StorageType == StorageType.Double)
                            ordinalParam.Set((double)apt.OrdinalNumber);
                        else if (ordinalParam.StorageType == StorageType.Integer)
                            ordinalParam.Set(apt.OrdinalNumber);
                        else if (ordinalParam.StorageType == StorageType.String)
                            ordinalParam.Set(apt.OrdinalNumber.ToString());

                        processed++;
                    }
                }

                trans.Commit();
            }

            return processed;
        }

        private class Apartment
        {
            public Room Room { get; set; }
            public string Type { get; set; }
            public string MGN2 { get; set; }
            public double Area { get; set; }
            public int OrdinalNumber { get; set; }
        }
    }
}