using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace ApartmentNumbering
{
    [Transaction(TransactionMode.Manual)]
    public class ApartmentNumberingCommand : IExternalCommand
    {
        // GUID параметров (заполните актуальными)
        private readonly Guid paramApartmentTypeGuid = new Guid("78e3b89c-eb68-4600-84a7-c523de162743");
        private readonly Guid paramMGN2Guid = new Guid("cff02c72-adcb-4fe3-9cc2-d2ff7a3deb85");
        private readonly Guid paramAreaGuid = new Guid("d3035d0f-b738-4407-a0e5-30787b92fa49");
        private readonly Guid paramOrdinalGuid = new Guid("813baf4a-e857-4dc5-a033-9c8048d4e2f3");

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Application app = uiApp.Application;
            Document mainDoc = uiApp.ActiveUIDocument.Document;

            try
            {
                // Логирование
                StringBuilder log = new StringBuilder();
                log.AppendLine($"=== НУМЕРАЦИЯ КВАРТИР - {DateTime.Now} ===");

                // 1. ПОЛУЧАЕМ ПУТИ К СВЯЗАННЫМ ФАЙЛАМ
                log.AppendLine("\n1. Поиск связанных файлов...");
                var filePaths = GetLinkedFilePaths(mainDoc);

                if (filePaths.Count == 0)
                {
                    TaskDialog.Show("Info", "Нет связанных моделей в проекте.");
                    return Result.Cancelled;
                }

                log.AppendLine($"Найдено файлов: {filePaths.Count}");

                // 2. ОБРАБАТЫВАЕМ КАЖДЫЙ ФАЙЛ ОТДЕЛЬНО
                int totalProcessed = 0;

                foreach (var fileInfo in filePaths)
                {
                    try
                    {
                        log.AppendLine($"\n--- Обработка файла: {fileInfo.Name} ---");

                        // Обрабатываем файл отдельно
                        int processed = ProcessSingleFile(app, fileInfo.Path, log);
                        totalProcessed += processed;

                        log.AppendLine($"Обработано квартир: {processed}");
                    }
                    catch (Exception ex)
                    {
                        log.AppendLine($"ОШИБКА: {ex.Message}");
                    }
                }

                // 3. РЕЗУЛЬТАТ
                log.AppendLine($"\n=== ИТОГО: {totalProcessed} квартир ===");

                // Сохраняем лог
                SaveLog(log.ToString());

                TaskDialog.Show("Успех",
                    $"Обработка завершена!\n\n" +
                    $"Файлов: {filePaths.Count}\n" +
                    $"Квартир: {totalProcessed}\n\n" +
                    $"Лог сохранен на рабочем столе.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        private List<FileInfo> GetLinkedFilePaths(Document doc)
        {
            var files = new List<FileInfo>();

            var links = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(l => l.GetLinkDocument() != null)
                .ToList();

            foreach (var link in links)
            {
                var linkDoc = link.GetLinkDocument();
                string path = linkDoc.PathName;

                if (File.Exists(path))
                {
                    files.Add(new FileInfo
                    {
                        Path = path,
                        Name = link.Name
                    });
                }
            }

            return files;
        }

        private int ProcessSingleFile(Application app, string filePath, StringBuilder log)
        {
            int processed = 0;

            try
            {
                log.AppendLine($"Открытие файла: {Path.GetFileName(filePath)}");

                // ОТКРЫВАЕМ ФАЙЛ КАК ОСНОВНОЙ ДОКУМЕНТ
                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

                // Проверяем, workshared ли файл
                bool isWorkshared = File.Exists(filePath + ".0000");

                // Опции открытия
                var openOptions = new OpenOptions();

                if (isWorkshared)
                {
                    log.AppendLine("  Файл является Workshared");
                    // Для workshared файлов используем стандартные опции
                }

                // ОТКРЫВАЕМ ФАЙЛ
                using (Document doc = app.OpenDocumentFile(modelPath, openOptions))
                {
                    if (doc == null)
                    {
                        log.AppendLine("  Не удалось открыть файл");
                        return 0;
                    }

                    log.AppendLine($"  Документ открыт: {doc.Title}");

                    // 1. СОБИРАЕМ КВАРТИРЫ
                    var apartments = CollectApartmentsFromDocument(doc, log);

                    if (apartments.Count == 0)
                    {
                        log.AppendLine("  Квартиры не найдены");
                        doc.Close(false);
                        return 0;
                    }

                    log.AppendLine($"  Найдено квартир: {apartments.Count}");

                    // 2. ГРУППИРУЕМ И ВЫЧИСЛЯЕМ НОМЕРА
                    CalculateOrdinalNumbers(apartments);

                    // 3. ЗАПИСЫВАЕМ НОМЕРА
                    processed = WriteOrdinalNumbers(doc, apartments, log);

                    // 4. СОХРАНЯЕМ ИЗМЕНЕНИЯ
                    if (processed > 0)
                    {
                        log.AppendLine("  Сохранение файла...");

                        // Для workshared файлов
                        if (isWorkshared && doc.IsWorkshared)
                        {
                            try
                            {
                                // Синхронизируем с центральным файлом
                                var syncOptions = new SynchronizeWithCentralOptions();
                                var transactOptions = new TransactWithCentralOptions();
                                doc.SynchronizeWithCentral(transactOptions, syncOptions);
                                log.AppendLine("  Синхронизировано с центральным файлом");
                            }
                            catch (Exception syncEx)
                            {
                                log.AppendLine($"  Ошибка синхронизации: {syncEx.Message}");
                                // Сохраняем локально
                                doc.Save();
                            }
                        }
                        else
                        {
                            // Для обычных файлов
                            doc.Save();
                        }
                    }

                    // Закрываем документ
                    doc.Close(false);
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"  Ошибка при обработке файла: {ex.Message}");
                throw;
            }

            return processed;
        }

        private List<Apartment> CollectApartmentsFromDocument(Document doc, StringBuilder log)
        {
            var apartments = new List<Apartment>();

            try
            {
                // Фильтр для помещений
                var collector = new FilteredElementCollector(doc);
                collector.OfCategory(BuiltInCategory.OST_Rooms);
                collector.WhereElementIsNotElementType();

                int roomCount = 0;
                int apartmentCount = 0;

                foreach (Element elem in collector)
                {
                    roomCount++;
                    Room room = elem as Room;
                    if (room == null) continue;

                    // Проверяем, является ли помещение квартирой
                    if (IsApartment(room))
                    {
                        apartmentCount++;

                        // Получаем параметры
                        string aptType = GetParameterValue(room, paramApartmentTypeGuid, "ADSK_Тип квартиры");
                        string mgn2 = GetParameterValue(room, paramMGN2Guid, "МГН2");
                        double area = GetAreaValue(room, paramAreaGuid, "ADSK_Площадь квартиры");

                        if (!string.IsNullOrEmpty(aptType) && !string.IsNullOrEmpty(mgn2) && area > 0)
                        {
                            apartments.Add(new Apartment
                            {
                                Room = room,
                                ApartmentType = aptType,
                                MGN2 = mgn2,
                                Area = area,
                                RoomId = room.Id
                            });
                        }
                    }
                }

                log.AppendLine($"  Всего помещений: {roomCount}, квартир: {apartmentCount}");
            }
            catch (Exception ex)
            {
                log.AppendLine($"  Ошибка при сборе квартир: {ex.Message}");
            }

            return apartments;
        }

        private bool IsApartment(Room room)
        {
            try
            {
                // Пробуем разные варианты названия параметра
                Parameter param = room.LookupParameter("Назначение");
                if (param == null)
                {
                    param = room.LookupParameter("Назначение помещения");
                }

                return param?.AsString() == "Квартиры";
            }
            catch
            {
                return false;
            }
        }

        private string GetParameterValue(Element elem, Guid guid, string paramName)
        {
            try
            {
                // Пробуем по GUID
                Parameter param = null;
                try
                {
                    param = elem.get_Parameter(guid);
                }
                catch { }

                // Пробуем по имени
                if (param == null || !param.HasValue)
                {
                    param = elem.LookupParameter(paramName);
                }

                if (param == null || !param.HasValue) return string.Empty;

                if (param.StorageType == StorageType.String)
                    return param.AsString();
                else if (param.StorageType == StorageType.Integer)
                    return param.AsInteger().ToString();
                else if (param.StorageType == StorageType.Double)
                    return param.AsValueString() ?? param.AsDouble().ToString();

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private double GetAreaValue(Element elem, Guid guid, string paramName)
        {
            try
            {
                // Пробуем по GUID
                Parameter param = null;
                try
                {
                    param = elem.get_Parameter(guid);
                }
                catch { }

                // Пробуем по имени
                if (param == null || !param.HasValue)
                {
                    param = elem.LookupParameter(paramName);
                }

                if (param != null && param.HasValue)
                {
                    return UnitUtils.ConvertFromInternalUnits(param.AsDouble(), UnitTypeId.SquareMeters);
                }

                // Пробуем стандартную площадь помещения
                if (elem is Room room)
                {
                    return UnitUtils.ConvertFromInternalUnits(room.Area, UnitTypeId.SquareMeters);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private void CalculateOrdinalNumbers(List<Apartment> apartments)
        {
            // Группируем по типу и МГН2
            var groups = apartments.GroupBy(a => $"{a.ApartmentType}|{a.MGN2}");

            foreach (var group in groups)
            {
                // Сортируем по площади
                var sorted = group.OrderBy(a => a.Area).ToList();

                // Назначаем порядковые номера с учетом одинаковых площадей
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

        private int WriteOrdinalNumbers(Document doc, List<Apartment> apartments, StringBuilder log)
        {
            int processed = 0;

            using (Transaction trans = new Transaction(doc, "Нумерация квартир"))
            {
                try
                {
                    trans.Start();
                    log.AppendLine("  Начало транзакции...");

                    foreach (var apt in apartments)
                    {
                        try
                        {
                            // Проверяем, что комната все еще существует
                            Room room = doc.GetElement(apt.RoomId) as Room;
                            if (room == null) continue;

                            // Ищем параметр для записи
                            Parameter ordinalParam = null;

                            // 1. По GUID
                            try
                            {
                                ordinalParam = room.get_Parameter(paramOrdinalGuid);
                            }
                            catch { }

                            // 2. По имени
                            if (ordinalParam == null)
                            {
                                ordinalParam = room.LookupParameter("ПорядковыйНомерДляТипа");
                            }

                            if (ordinalParam == null)
                            {
                                ordinalParam = room.LookupParameter("ADSK_Номер квартиры");
                            }

                            if (ordinalParam == null)
                            {
                                ordinalParam = room.LookupParameter("ADSK_Номер квартиры по типу");
                            }

                            if (ordinalParam != null && !ordinalParam.IsReadOnly)
                            {
                                if (ordinalParam.StorageType == StorageType.Double)
                                {
                                    ordinalParam.Set((double)apt.OrdinalNumber);
                                    processed++;
                                }
                                else if (ordinalParam.StorageType == StorageType.Integer)
                                {
                                    ordinalParam.Set(apt.OrdinalNumber);
                                    processed++;
                                }
                                else if (ordinalParam.StorageType == StorageType.String)
                                {
                                    ordinalParam.Set(apt.OrdinalNumber.ToString());
                                    processed++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.AppendLine($"    Ошибка при записи квартиры: {ex.Message}");
                        }
                    }

                    trans.Commit();
                    log.AppendLine($"  Транзакция завершена, обработано: {processed}");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    log.AppendLine($"  Ошибка транзакции: {ex.Message}");
                    throw;
                }
            }

            return processed;
        }

        private void SaveLog(string logContent)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string logPath = Path.Combine(desktop, $"ApartmentLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(logPath, logContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения лога: {ex.Message}");
            }
        }

        private class FileInfo
        {
            public string Path { get; set; }
            public string Name { get; set; }
        }

        private class Apartment
        {
            public Room Room { get; set; }
            public ElementId RoomId { get; set; }
            public string ApartmentType { get; set; }
            public string MGN2 { get; set; }
            public double Area { get; set; }
            public int OrdinalNumber { get; set; }
        }
    }
}