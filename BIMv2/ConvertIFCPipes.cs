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
        // Константа для перевода миллиметров в футы (1 фут = 304.8 мм)
        private const double MM_TO_FEET = 1.0 / 304.8;
        // Минимальная длина MEP кривой в футах (1/10 дюйма ≈ 2.54 мм)
        private const double MIN_CURVE_LENGTH = 0.00833333;

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
                    "Выберите IFC трубы/фитинги");
            }
            catch
            {
                return Result.Cancelled;
            }

            if (picked == null || picked.Count == 0)
                return Result.Cancelled;

            ElementId levelId = GetOrCreateLevel(doc);

            PipeType pipeType = GetOrCreatePipeType(doc);
            if (pipeType == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось создать/найти тип трубы");
                return Result.Failed;
            }

            ElementId pipingSystemTypeId = GetDefaultPipingSystemTypeId(doc);

            int ok = 0;
            int fail = 0;
            List<string> errors = new List<string>();

            using (Transaction tx = new Transaction(doc, "IFC → Revit Pipes"))
            {
                tx.Start();

                foreach (Reference r in picked)
                {
                    Element ifcElem = doc.GetElement(r);

                    try
                    {
                        Debug.WriteLine($"\n--- Обработка элемента {ifcElem.Id} ---");

                        // Выводим все параметры элемента для диагностики (упрощенно)
                        DumpAllParameters(ifcElem);

                        // ---------- ДИАМЕТР ----------
                        double diameterFeet = GetDiameterFromIFCElement(ifcElem);
                        if (diameterFeet <= 0.0001)
                        {
                            errors.Add($"Элемент {ifcElem.Id}: не удалось определить диаметр");
                            fail++;
                            continue;
                        }

                        // ---------- ДЛИНА ИЗ IFC ----------
                        double lengthMM = GetLengthFromIFC(ifcElem);
                        if (lengthMM <= 1)
                        {
                            // Если не нашли длину, пробуем определить по bounding box как запасной вариант
                            lengthMM = GetLengthFromBoundingBox(ifcElem);
                            if (lengthMM <= 1)
                            {
                                errors.Add($"Элемент {ifcElem.Id}: не удалось определить длину");
                                fail++;
                                continue;
                            }
                            else
                            {
                                Debug.WriteLine($"Длина определена по BoundingBox: {lengthMM} мм");
                            }
                        }

                        // ---------- ОСЬ ----------
                        if (!GetAxis(ifcElem, lengthMM, out XYZ p1, out XYZ p2))
                        {
                            errors.Add($"Элемент {ifcElem.Id}: не удалось определить ось");
                            fail++;
                            continue;
                        }

                        // Проверяем минимальную длину
                        double length = p1.DistanceTo(p2);
                        if (length < MIN_CURVE_LENGTH)
                        {
                            XYZ direction = (p2 - p1).Normalize();
                            if (direction.IsZeroLength())
                                direction = XYZ.BasisX;
                            p2 = p1 + direction * MIN_CURVE_LENGTH;
                        }

                        // ---------- СОЗДАНИЕ ТРУБЫ ----------
                        Pipe pipe = Pipe.Create(
                            doc,
                            pipingSystemTypeId,
                            pipeType.Id,
                            levelId,
                            p1,
                            p2);

                        if (pipe == null)
                        {
                            errors.Add($"Элемент {ifcElem.Id}: Pipe.Create вернул null");
                            fail++;
                            continue;
                        }

                        // ---------- УСТАНОВКА ДИАМЕТРА ----------
                        Parameter diamParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                        if (diamParam != null && !diamParam.IsReadOnly)
                        {
                            diamParam.Set(diameterFeet);
                        }

                        // ---------- КОПИРУЕМ ДОПОЛНИТЕЛЬНЫЕ ПАРАМЕТРЫ ----------
                        CopyParameters(ifcElem, pipe);

                        ok++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Элемент {ifcElem.Id}: {ex.Message}");
                        fail++;
                    }
                }

                tx.Commit();
            }

            string resultMsg = $"Создано труб: {ok}\nОшибок: {fail}\nВсего: {picked.Count}";
            if (errors.Count > 0)
            {
                resultMsg += "\n\nОшибки:\n" + string.Join("\n", errors.Take(10));
                if (errors.Count > 10)
                    resultMsg += $"\n... и еще {errors.Count - 10} ошибок";
            }

            TaskDialog.Show("Готово", resultMsg);

            return Result.Succeeded;
        }

        // ======================================================
        // ДИАГНОСТИКА - ВЫВОД ВСЕХ ПАРАМЕТРОВ (УПРОЩЕННО)
        // ======================================================
        private void DumpAllParameters(Element e)
        {
            Debug.WriteLine($"Параметры элемента {e.Id}:");
            foreach (Parameter p in e.Parameters)
            {
                string name = p.Definition?.Name ?? "Unknown";
                string val = "null";

                if (p.HasValue)
                {
                    if (p.StorageType == StorageType.String)
                        val = $"\"{p.AsString()}\"";
                    else if (p.StorageType == StorageType.Double)
                        val = $"{p.AsDouble():F3}";
                    else if (p.StorageType == StorageType.Integer)
                        val = $"{p.AsInteger()}";
                }

                Debug.WriteLine($"  {name}: {val} ({p.StorageType})");
            }
        }

        // ======================================================
        // ПОЛУЧЕНИЕ ДЛИНЫ ИЗ IFC (УПРОЩЕННО)
        // ======================================================
        private double GetLengthFromIFC(Element e)
        {
            // Простой поиск по известным именам параметров
            string[] lengthParams = {
                "Length_mm",
                "Length_mm(MagiCAD Pset_Hydronic)",
                "Length",
                "Длина"
            };

            foreach (string paramName in lengthParams)
            {
                Parameter lp = e.LookupParameter(paramName);
                if (lp != null && lp.HasValue)
                {
                    double val = GetDoubleValue(lp);
                    if (val > 0)
                    {
                        Debug.WriteLine($"Найдена длина в параметре '{paramName}': {val} мм");
                        return val;
                    }
                }
            }

            return 0;
        }

        // ======================================================
        // ПОЛУЧЕНИЕ ЧИСЛОВОГО ЗНАЧЕНИЯ ИЗ ПАРАМЕТРА
        // ======================================================
        private double GetDoubleValue(Parameter p)
        {
            if (p == null || !p.HasValue)
                return 0;

            try
            {
                if (p.StorageType == StorageType.Double)
                {
                    double val = p.AsDouble();
                    // Проверяем, не в футах ли значение
                    if (val < 1000 && val > 0.001)
                    {
                        // Маленькое число - вероятно футы
                        return val * 304.8;
                    }
                    else
                    {
                        return val;
                    }
                }

                if (p.StorageType == StorageType.Integer)
                {
                    return p.AsInteger();
                }

                if (p.StorageType == StorageType.String)
                {
                    string valStr = p.AsString()?.Trim();
                    if (!string.IsNullOrEmpty(valStr))
                    {
                        valStr = valStr.Replace(" ", "").Replace(",", ".");
                        var match = Regex.Match(valStr, @"(\d+(?:\.\d+)?)");
                        if (match.Success && double.TryParse(match.Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double val))
                        {
                            return val;
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки
            }

            return 0;
        }

        // ======================================================
        // ПОЛУЧЕНИЕ ДЛИНЫ ИЗ BOUNDING BOX (ЗАПАСНОЙ ВАРИАНТ)
        // ======================================================
        private double GetLengthFromBoundingBox(Element e)
        {
            BoundingBoxXYZ bb = e.get_BoundingBox(null);
            if (bb == null)
                return 0;

            double dx = Math.Abs(bb.Max.X - bb.Min.X) * 304.8;
            double dy = Math.Abs(bb.Max.Y - bb.Min.Y) * 304.8;
            double dz = Math.Abs(bb.Max.Z - bb.Min.Z) * 304.8;

            // Берем максимальный размер как длину
            double length = Math.Max(dx, Math.Max(dy, dz));

            Debug.WriteLine($"BoundingBox: dx={dx:F1}, dy={dy:F1}, dz={dz:F1}, длина={length:F1} мм");

            return length;
        }

        // ======================================================
        // ОСЬ (центр + направление из геометрии, длина из IFC)
        // ======================================================
        private bool GetAxis(Element e, double lengthMM, out XYZ p1, out XYZ p2)
        {
            p1 = p2 = XYZ.Zero;

            if (lengthMM <= 1)
                return false;

            double halfLen = (lengthMM * MM_TO_FEET) / 2.0;

            // ---------- НАПРАВЛЕНИЕ ИЗ ГЕОМЕТРИИ ----------
            XYZ dir = GetAxisDirection(e);
            if (dir == null || dir.IsZeroLength())
            {
                dir = XYZ.BasisX;
            }

            // ---------- ЦЕНТР ИЗ BOUNDING BOX ----------
            BoundingBoxXYZ bb = e.get_BoundingBox(null);
            if (bb == null)
                return false;

            XYZ center = (bb.Min + bb.Max) * 0.5;

            p1 = center - dir * halfLen;
            p2 = center + dir * halfLen;

            Debug.WriteLine($"Труба: длина={lengthMM} мм, " +
                          $"центр=({center.X * 304.8:F1}, {center.Y * 304.8:F1}, {center.Z * 304.8:F1}), " +
                          $"напр=({dir.X:F3}, {dir.Y:F3}, {dir.Z:F3})");

            return true;
        }

        // ======================================================
        // ПОЛУЧЕНИЕ НАПРАВЛЕНИЯ ИЗ ГЕОМЕТРИИ
        // ======================================================
        private XYZ GetAxisDirection(Element e)
        {
            Options opt = new Options
            {
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = e.get_Geometry(opt);
            if (geom == null)
                return null;

            XYZ best = null;
            double bestLen = 0;

            void Collect(GeometryObject go)
            {
                if (go is Solid s)
                {
                    foreach (Edge ed in s.Edges)
                    {
                        IList<XYZ> pts = ed.Tessellate();
                        if (pts.Count < 2) continue;

                        XYZ v = pts[1] - pts[0];
                        double len = v.GetLength();
                        if (len > bestLen)
                        {
                            bestLen = len;
                            best = v.Normalize();
                        }
                    }
                }
                else if (go is GeometryInstance gi)
                {
                    foreach (GeometryObject igo in gi.GetInstanceGeometry())
                        Collect(igo);
                }
            }

            foreach (GeometryObject go in geom)
                Collect(go);

            return best;
        }

        // ======================================================
        // ПОЛУЧЕНИЕ ДИАМЕТРА ИЗ IFC
        // ======================================================
        private double GetDiameterFromIFCElement(Element ifcElem)
        {
            // 1. ConnectionSize_mm (MagiCAD)
            double diamMM = GetDoubleValue(ifcElem.LookupParameter("ConnectionSize_mm"));
            if (diamMM > 0)
            {
                Debug.WriteLine($"Диаметр из ConnectionSize_mm: {diamMM} мм");
                return diamMM * MM_TO_FEET;
            }

            // 2. Другие параметры диаметра
            string[] sizeParams = {
                "Диаметр", "Diameter", "Размер", "Size", "DN", "ConnectionSize",
                "NominalDiameter"
            };

            foreach (string paramName in sizeParams)
            {
                diamMM = GetDoubleValue(ifcElem.LookupParameter(paramName));
                if (diamMM > 0)
                {
                    Debug.WriteLine($"Диаметр из параметра '{paramName}': {diamMM} мм");
                    return diamMM * MM_TO_FEET;
                }

                // Пробуем из строки
                string sizeStr = GetStringParameter(ifcElem, paramName);
                if (!string.IsNullOrEmpty(sizeStr))
                {
                    diamMM = ExtractDiameterFromString(sizeStr);
                    if (diamMM > 0)
                    {
                        Debug.WriteLine($"Диаметр из строки '{sizeStr}': {diamMM} мм");
                        return diamMM * MM_TO_FEET;
                    }
                }
            }

            // 3. Из геометрии
            diamMM = GetDiameterFromGeometry(ifcElem);
            if (diamMM > 0)
            {
                Debug.WriteLine($"Диаметр из геометрии: {diamMM} мм");
                return diamMM * MM_TO_FEET;
            }

            return 0;
        }

        // ======================================================
        // ИЗВЛЕЧЕНИЕ ДИАМЕТРА ИЗ СТРОКИ
        // ======================================================
        private double ExtractDiameterFromString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return 0;

            // DN50, DN 100
            var dnMatch = Regex.Match(input, @"DN\s*(\d+)", RegexOptions.IgnoreCase);
            if (dnMatch.Success && dnMatch.Groups.Count > 1)
            {
                if (double.TryParse(dnMatch.Groups[1].Value, out double dn))
                    return dn;
            }

            // 25mm, 32 мм
            var mmMatch = Regex.Match(input, @"(\d+)\s*мм", RegexOptions.IgnoreCase);
            if (mmMatch.Success && mmMatch.Groups.Count > 1)
            {
                if (double.TryParse(mmMatch.Groups[1].Value, out double mm))
                    return mm;
            }

            // Просто число
            var numMatch = Regex.Match(input, @"\b(\d+)\b");
            if (numMatch.Success)
            {
                if (double.TryParse(numMatch.Value, out double num))
                {
                    if (num < 100)
                        return num;
                }
            }

            return 0;
        }

        // ======================================================
        // ПОЛУЧЕНИЕ ДИАМЕТРА ИЗ ГЕОМЕТРИИ
        // ======================================================
        private double GetDiameterFromGeometry(Element e)
        {
            try
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb != null)
                {
                    double dx = Math.Abs(bb.Max.X - bb.Min.X) * 304.8;
                    double dy = Math.Abs(bb.Max.Y - bb.Min.Y) * 304.8;
                    double dz = Math.Abs(bb.Max.Z - bb.Min.Z) * 304.8;

                    double diam = Math.Min(Math.Min(dx, dy), dz);

                    if (diam > 5 && diam < 500)
                        return diam;
                }
            }
            catch { }
            return 0;
        }

        private string GetStringParameter(Element e, string paramName)
        {
            Parameter param = e.LookupParameter(paramName);
            if (param != null && param.StorageType == StorageType.String)
                return param.AsString() ?? "";
            return "";
        }

        // ======================================================
        // СИСТЕМНЫЙ ТИП
        // ======================================================
        private ElementId GetDefaultPipingSystemTypeId(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> systemTypes = collector
                .OfClass(typeof(PipingSystemType))
                .ToElements();

            foreach (Element elem in systemTypes)
            {
                PipingSystemType systemType = elem as PipingSystemType;
                if (systemType != null)
                {
                    string name = systemType.Name;
                    if (name.Contains("ГВС") || name.Contains("ХВС") ||
                        name.Contains("Hot") || name.Contains("Cold") ||
                        name.Contains("Water") || name.Contains("Водоснабжение"))
                    {
                        return systemType.Id;
                    }
                }
            }

            if (systemTypes.Count > 0)
                return systemTypes.First().Id;

            return ElementId.InvalidElementId;
        }

        // ======================================================
        // ТИП ТРУБЫ
        // ======================================================
        private PipeType GetOrCreatePipeType(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            PipeType pipeType = collector
                .OfClass(typeof(PipeType))
                .Cast<PipeType>()
                .FirstOrDefault();

            return pipeType;
        }

        // ======================================================
        // КОПИРОВАНИЕ ПАРАМЕТРОВ
        // ======================================================
        private void CopyParameters(Element source, Element target)
        {
            string[] paramsToCopy = {
                "Комментарии", "Comments", "Описание", "Description",
                "Марка", "Mark"
            };

            foreach (string paramName in paramsToCopy)
            {
                Parameter srcParam = source.LookupParameter(paramName);
                Parameter tgtParam = target.LookupParameter(paramName);

                if (srcParam != null && tgtParam != null && !tgtParam.IsReadOnly)
                {
                    try
                    {
                        if (srcParam.StorageType == StorageType.String)
                            tgtParam.Set(srcParam.AsString() ?? "");
                        else if (srcParam.StorageType == StorageType.Double)
                            tgtParam.Set(srcParam.AsDouble());
                        else if (srcParam.StorageType == StorageType.Integer)
                            tgtParam.Set(srcParam.AsInteger());
                    }
                    catch { }
                }
            }
        }

        // ======================================================
        // УРОВЕНЬ
        // ======================================================
        private ElementId GetOrCreateLevel(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            Level lvl = collector
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();

            return lvl?.Id ?? Level.Create(doc, 0).Id;
        }
    }
}