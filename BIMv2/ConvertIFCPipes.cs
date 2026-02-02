using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace IFCtoRevitPipes
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertIFCPipes : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            IList<Reference> picked;
            try
            {
                picked = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    "Выберите IFC трубы");
            }
            catch
            {
                return Result.Cancelled;
            }

            if (picked == null || picked.Count == 0)
                return Result.Cancelled;

            ElementId levelId = GetOrCreateLevel(doc);

            int ok = 0;
            int fail = 0;

            using (Transaction tx = new Transaction(doc, "IFC → Revit Pipes"))
            {
                tx.Start();

                foreach (Reference r in picked)
                {
                    Element ifcElem = doc.GetElement(r);

                    try
                    {
                        // ---------- ДИАМЕТР ----------
                        double diamMM = GetDiameterFromIFCParam(ifcElem);
                        if (diamMM <= 0)
                            throw new Exception("Некорректный диаметр");

                        // ---------- ОСЬ ----------
                        if (!GetAxis(
                            ifcElem,
                            out XYZ p1,
                            out XYZ p2))
                            throw new Exception("Не удалось определить ось");

                        // ---------- ТИП ТРУБЫ ----------
                        PipeType pipeType = FindPipeType(doc);
                        if (pipeType == null)
                            throw new Exception("Нет подходящего типа трубы");

                        // ---------- СОЗДАНИЕ ТРУБЫ БЕЗ СИСТЕМЫ ----------
                        Pipe pipe = Pipe.Create(
                            doc,
                            null, // Нет системы
                            pipeType.Id,
                            levelId,
                            p1,
                            p2);

                        if (pipe == null)
                            throw new Exception("Pipe.Create вернул null");

                        // ---------- УСТАНОВКА ДИАМЕТРА ----------
                        // Конвертируем мм в футы (1 мм = 1/304.8 фута)
                        double diameterFeet = diamMM / 304.8;

                        // Устанавливаем диаметр через параметр
                        Parameter diamParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                        if (diamParam != null && !diamParam.IsReadOnly)
                        {
                            diamParam.Set(diameterFeet);
                        }

                        ok++;
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"Ошибка: {ex.Message}");
                        fail++;
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show(
                "Готово",
                $"Создано труб: {ok}\nОшибок: {fail}\nВсего: {picked.Count}");

            return Result.Succeeded;
        }

        // ======================================================
        // ПОЛУЧЕНИЕ ДИАМЕТРА ИЗ ПАРАМЕТРА MAGiCAD
        // ======================================================

        private double GetDiameterFromIFCParam(Element e)
        {
            double diamMM = 0;

            // Пробуем получить значение из параметра MagiCAD
            Parameter param = e.LookupParameter("ConnectionSize_mm(MagiCAD Pset_Hydronic)");
            if (param != null)
            {
                // Пробуем разные способы получения значения
                if (param.StorageType == StorageType.Double)
                {
                    diamMM = param.AsDouble() * 304.8; // Конвертируем из футов в мм
                }
                else if (param.StorageType == StorageType.String)
                {
                    string valueStr = param.AsString();
                    if (!string.IsNullOrEmpty(valueStr))
                    {
                        // Убираем пробелы и пробуем распарсить
                        valueStr = valueStr.Replace(" ", "").Replace(",", ".");
                        double.TryParse(valueStr, out diamMM);
                    }
                }
                else if (param.StorageType == StorageType.Integer)
                {
                    diamMM = param.AsInteger();
                }
            }

            // Если не нашли в MagiCAD параметре, пробуем другие варианты
            if (diamMM <= 0)
            {
                // Пробуем параметр "Диаметр" через LookupParameter
                Parameter diamParam = e.LookupParameter("Диаметр") ??
                                      e.LookupParameter("Diameter") ??
                                      e.LookupParameter("Размер") ??
                                      e.LookupParameter("Size") ??
                                      e.LookupParameter("DN"); // Дополнительный вариант

                if (diamParam != null && diamParam.HasValue)
                {
                    if (diamParam.StorageType == StorageType.Double)
                    {
                        diamMM = diamParam.AsDouble() * 304.8;
                    }
                    else if (diamParam.StorageType == StorageType.String)
                    {
                        string diamStr = diamParam.AsString();
                        // Пробуем извлечь число из строки
                        Match match = Regex.Match(diamStr, @"\d+(\.\d+)?");
                        if (match.Success)
                        {
                            double.TryParse(match.Value, out diamMM);
                        }
                        else
                        {
                            // Пробуем для формата "DN50", "DN100" и т.д.
                            match = Regex.Match(diamStr, @"DN\s*(\d+)", RegexOptions.IgnoreCase);
                            if (match.Success && match.Groups.Count > 1)
                            {
                                double.TryParse(match.Groups[1].Value, out diamMM);
                            }
                        }
                    }
                }

                if (diamMM <= 0)
                {
                    // Пробуем найти диаметр в геометрии
                    diamMM = GetDiameterFromGeometry(e);
                }
            }

            return diamMM;
        }

        // ======================================================
        // ПОЛУЧЕНИЕ ДИАМЕТРА ИЗ ГЕОМЕТРИИ (упрощенный вариант)
        // ======================================================

        private double GetDiameterFromGeometry(Element e)
        {
            Options opt = new Options
            {
                IncludeNonVisibleObjects = false, // Упрощаем
                DetailLevel = ViewDetailLevel.Fine
            };

            try
            {
                GeometryElement geom = e.get_Geometry(opt);
                if (geom == null)
                    return 0;

                // Простой подход: смотрим на bounding box
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb != null)
                {
                    // Предполагаем, что труба круглая, берем минимальный размер из bounding box
                    double width = (bb.Max.X - bb.Min.X) * 304.8;
                    double height = (bb.Max.Y - bb.Min.Y) * 304.8;
                    double depth = (bb.Max.Z - bb.Min.Z) * 304.8;

                    // Берем минимальный размер (диаметр)
                    double minDim = Math.Min(width, Math.Min(height, depth));
                    return minDim > 0 ? minDim : 0;
                }
            }
            catch
            {
                // Игнорируем ошибки геометрии
            }

            return 0;
        }

        // ======================================================
        // ОПРЕДЕЛЕНИЕ ОСИ ТРУБЫ (упрощенный вариант)
        // ======================================================

        private bool GetAxis(Element e, out XYZ p1, out XYZ p2)
        {
            p1 = p2 = XYZ.Zero;

            // Простой подход через bounding box
            BoundingBoxXYZ bb = e.get_BoundingBox(null);
            if (bb == null)
                return false;

            // Определяем направление по самой длинной стороне bounding box
            double dx = bb.Max.X - bb.Min.X;
            double dy = bb.Max.Y - bb.Min.Y;
            double dz = bb.Max.Z - bb.Min.Z;

            XYZ direction;
            if (dx >= dy && dx >= dz)
                direction = XYZ.BasisX;
            else if (dy >= dx && dy >= dz)
                direction = XYZ.BasisY;
            else
                direction = XYZ.BasisZ;

            // Центр bounding box
            XYZ center = (bb.Min + bb.Max) * 0.5;

            // Длина половины самой длинной стороны
            double halfLength = Math.Max(dx, Math.Max(dy, dz)) / 2.0;

            p1 = center - direction * halfLength;
            p2 = center + direction * halfLength;

            return true;
        }

        // ======================================================
        // ПОИСК ТИПА ТРУБЫ (упрощенный)
        // ======================================================

        private PipeType FindPipeType(Document doc)
        {
            // Ищем первый доступный тип трубы
            return new FilteredElementCollector(doc)
                .OfClass(typeof(PipeType))
                .Cast<PipeType>()
                .FirstOrDefault();
        }

        // ======================================================
        // УРОВЕНЬ
        // ======================================================

        private ElementId GetOrCreateLevel(Document doc)
        {
            Level lvl = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();

            return lvl != null
                ? lvl.Id
                : Level.Create(doc, 0).Id;
        }
    }
}