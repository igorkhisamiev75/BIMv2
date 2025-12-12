using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace IFCtoRevitDucts
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertIFCDucts : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication ui = commandData.Application;
            UIDocument uidoc = ui.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                var picked = uidoc.Selection.PickObjects(ObjectType.Element, "Выберите IFC воздуховоды");
                if (picked == null || picked.Count == 0)
                    return Result.Cancelled;

                ElementId levelId = GetOrCreateLevel(doc);
                ElementId systemTypeId = GetOrCreateAirSystemType(doc);

                if (levelId == null || systemTypeId == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось подготовить базовые данные (уровень или системный тип).");
                    return Result.Failed;
                }

                int ok = 0, fail = 0;

                using (Transaction tx = new Transaction(doc, "Преобразование IFC воздуховодов"))
                {
                    tx.Start();

                    foreach (var r in picked)
                    {
                        Element ifcElem = doc.GetElement(r);

                        try
                        {
                            // 1 — осевая линия
                            if (!GetIFCAxis(ifcElem, out XYZ p1, out XYZ p2))
                                throw new Exception("Не удалось определить осевую линию");

                            // 2 — чтение MagiCAD параметров IFC
                            string sizeStr = GetIFCPropertyString(ifcElem, "ConnectionSize_mm");
                            string partType = GetIFCPropertyString(ifcElem, "PartType");

                            // 3 — парсинг размеров
                            if (!TryParseMagICADSize(sizeStr, partType,
                                out double widthMM, out double heightMM, out double diamMM))
                            {
                                // fallback — определение из bounding box
                                GetIFCSize(ifcElem, out widthMM, out heightMM, out diamMM);
                            }

                            // 4 — выбор типа воздуховода (Revit FamilySymbol)
                            var ductSym = FindOrCreateDuctType(doc, partType);
                            if (ductSym == null)
                                throw new Exception("Нет подходящего типа воздуховода Revit");

                            if (!ductSym.IsActive)
                                ductSym.Activate();

                            // 5 — создание воздуховода
                            if (!CreateDuct(doc, levelId, systemTypeId, ductSym.Id, p1, p2, widthMM, heightMM, diamMM))
                                throw new Exception("Ошибка создания воздуховода");

                            ok++;
                        }
                        catch (Exception ex)
                        {
                            Debug.Print($"IFC error: {ex.Message}");
                            fail++;
                        }
                    }

                    tx.Commit();
                }

                TaskDialog.Show("Готово", $"Создано: {ok}\nОшибок: {fail}\nВсего: {picked.Count}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        // -------------------------------------------------------------
        //  IFC PROPERTY READER
        // -------------------------------------------------------------
        private string GetIFCPropertyString(Element e, string propName)
        {
            if (e == null)
                return "";

            // Прямой поиск MagiCAD параметров
            Parameter p = e.LookupParameter(propName);
            if (p != null)
            {
                if (p.StorageType == StorageType.String)
                    return p.AsString();

                return p.AsValueString();
            }

            // Дополнительный перебор параметров (на случай неточного имени)
            foreach (Parameter par in e.Parameters)
            {
                if (par.Definition?.Name == propName)
                {
                    if (par.StorageType == StorageType.String)
                        return par.AsString();
                    return par.AsValueString();
                }
            }

            return "";
        }


        // -------------------------------------------------------------
        //  PARSE MAGICAD SIZE
        // -------------------------------------------------------------
        private bool TryParseMagICADSize(string s, string partType,
            out double width, out double height, out double diam)
        {
            width = height = diam = 0;

            if (string.IsNullOrWhiteSpace(s))
                return false;

            s = s.Trim().ToLower();

            // ROUND: "125"
            if (Regex.IsMatch(s, @"^\d+$"))
            {
                diam = double.Parse(s);
                return true;
            }

            // RECT: "800x500"
            if (Regex.IsMatch(s, @"^\d+\s*[xх]\s*\d+$"))
            {
                var parts = Regex.Split(s, @"[xх]");
                width = double.Parse(parts[0]);
                height = double.Parse(parts[1]);
                return true;
            }

            // TRANSITIONS "100/125" — берём первый размер
            if (s.Contains("/"))
            {
                var parts = s.Split('/');
                if (double.TryParse(parts[0], out double d))
                {
                    // круглый переход
                    diam = d;
                    return true;
                }
            }

            // fallback via PartType
            if (partType?.ToLower() == "rect")
            {
                if (TryExtractTwoNumbers(s, out width, out height))
                    return true;
            }

            if (partType?.ToLower() == "round")
            {
                if (TryExtractOneNumber(s, out diam))
                    return true;
            }

            return false;
        }

        private bool TryExtractOneNumber(string s, out double n)
        {
            n = 0;
            var m = Regex.Match(s, @"(\d+)");
            if (m.Success)
            {
                n = double.Parse(m.Groups[1].Value);
                return true;
            }
            return false;
        }

        private bool TryExtractTwoNumbers(string s, out double a, out double b)
        {
            a = b = 0;
            var m = Regex.Matches(s, @"(\d+)");
            if (m.Count >= 2)
            {
                a = double.Parse(m[0].Value);
                b = double.Parse(m[1].Value);
                return true;
            }
            return false;
        }

        // -------------------------------------------------------------
        //  GEOMETRY — axis line
        // -------------------------------------------------------------
        private bool GetIFCAxis(Element e, out XYZ p1, out XYZ p2)
        {
            p1 = p2 = XYZ.Zero;

            var opts = new Options { ComputeReferences = true, IncludeNonVisibleObjects = true };
            GeometryElement geom = e.get_Geometry(opts);
            if (geom == null) return false;

            Solid solid = FindSolid(geom);
            if (solid == null) return false;

            // берем крайние точки солида
            var pts = new List<XYZ>();
            foreach (Edge ed in solid.Edges)
            {
                var c = ed.AsCurve();
                pts.Add(c.GetEndPoint(0));
                pts.Add(c.GetEndPoint(1));
            }

            if (pts.Count < 2) return false;

            double maxDist = 0;
            XYZ A = XYZ.Zero, B = XYZ.Zero;

            for (int i = 0; i < pts.Count; i++)
                for (int j = i + 1; j < pts.Count; j++)
                {
                    double d = pts[i].DistanceTo(pts[j]);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        A = pts[i];
                        B = pts[j];
                    }
                }

            if (maxDist < 0.01) return false;
            p1 = A; p2 = B;
            return true;
        }

        private Solid FindSolid(GeometryElement geom)
        {
            foreach (var g in geom)
            {
                if (g is Solid s && s.Volume > 0)
                    return s;

                if (g is GeometryInstance gi)
                {
                    foreach (var ig in gi.GetInstanceGeometry())
                    {
                        if (ig is Solid ss && ss.Volume > 0)
                            return ss;
                    }
                }
            }
            return null;
        }

        // -------------------------------------------------------------
        //  GEOMETRY — fallback size detection
        // -------------------------------------------------------------
        private bool GetIFCSize(Element e, out double w, out double h, out double d)
        {
            w = h = d = 0;

            var bb = e.get_BoundingBox(null);
            if (bb == null) return false;

            double dx = (bb.Max.X - bb.Min.X) * 304.8;
            double dy = (bb.Max.Y - bb.Min.Y) * 304.8;
            double dz = (bb.Max.Z - bb.Min.Z) * 304.8;

            var arr = new[] { dx, dy, dz };
            Array.Sort(arr);

            // круглый?
            if (Math.Abs(arr[0] - arr[1]) < 3)
            {
                d = (arr[0] + arr[1]) / 2.0;
                return true;
            }

            w = arr[1];
            h = arr[0];
            return true;
        }

        // -------------------------------------------------------------
        //  LEVEL
        // -------------------------------------------------------------
        private ElementId GetOrCreateLevel(Document doc)
        {
            Level lvl = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();

            if (lvl != null) return lvl.Id;

            using (var t = new Transaction(doc, "Add Level"))
            {
                t.Start();
                lvl = Level.Create(doc, 0);
                lvl.Name = "IFC Level";
                t.Commit();
            }
            return lvl.Id;
        }

        // -------------------------------------------------------------
        //  SYSTEM TYPE
        // -------------------------------------------------------------
        private ElementId GetOrCreateAirSystemType(Document doc)
        {
            var st = new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystemType))
                .Cast<MechanicalSystemType>()
                .FirstOrDefault();

            if (st != null) return st.Id;

            using (var t = new Transaction(doc, "Create System Type"))
            {
                t.Start();
                st = MechanicalSystemType.Create(doc, MEPSystemClassification.SupplyAir, "IFC Air System");
                t.Commit();
            }
            return st.Id;
        }

        // -------------------------------------------------------------
        //  FAMILY SYMBOL DETECTION
        // -------------------------------------------------------------
        private FamilySymbol FindOrCreateDuctType(Document doc, string partType)
        {
            var symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_DuctCurves)
                .Cast<FamilySymbol>()
                .ToList();

            if (symbols.Count == 0)
                return null;

            string key = partType?.ToLower();

            if (key == "round")
                return symbols.FirstOrDefault(s => s.FamilyName.ToLower().Contains("круг") ||
                                                   s.Name.ToLower().Contains("round"))
                       ?? symbols.First();

            if (key == "rect")
                return symbols.FirstOrDefault(s => s.FamilyName.ToLower().Contains("прям") ||
                                                   s.Name.ToLower().Contains("rect"))
                       ?? symbols.First();

            return symbols.First();
        }

        // -------------------------------------------------------------
        //  CREATE REVIT DUCT
        // -------------------------------------------------------------
        private bool CreateDuct(
            Document doc,
            ElementId level,
            ElementId systemType,
            ElementId ductType,
            XYZ p1,
            XYZ p2,
            double widthMM,
            double heightMM,
            double diamMM)
        {
            try
            {
                var duct = Duct.Create(doc, systemType, ductType, level, p1, p2);
                if (duct == null)
                    return false;

                if (diamMM > 1)
                {
                    double ft = diamMM / 304.8;
                    SetParam(duct, "Диаметр", ft);
                    SetParam(duct, "Diameter", ft);
                }
                else
                {
                    double wf = widthMM / 304.8;
                    double hf = heightMM / 304.8;
                    SetParam(duct, "Ширина", wf);
                    SetParam(duct, "Высота", hf);
                    SetParam(duct, "Width", wf);
                    SetParam(duct, "Height", hf);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"Duct error: {ex.Message}");
                return false;
            }
        }

        private void SetParam(Element e, string name, double v)
        {
            Parameter p = e.LookupParameter(name);
            if (p != null && p.StorageType == StorageType.Double)
            {
                try { p.Set(v); } catch { }
            }
        }
    }
}
