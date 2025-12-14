using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

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
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            IList<Reference> picked;
            try
            {
                picked = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    "Выберите IFC воздуховоды");
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

            using (Transaction tx = new Transaction(doc, "IFC → Revit Ducts"))
            {
                tx.Start();

                foreach (Reference r in picked)
                {
                    Element ifcElem = doc.GetElement(r);

                    try
                    {
                        // ---------- РАЗМЕРЫ ----------
                        GetSizeFromIFCParam(
                            ifcElem,
                            out bool isRound,
                            out double widthMM,
                            out double heightMM,
                            out double diamMM);

                        if (isRound && diamMM <= 0)
                            throw new Exception("Некорректный диаметр");

                        if (!isRound && (widthMM <= 0 || heightMM <= 0))
                            throw new Exception("Некорректные размеры");

                        // ---------- ОСЬ ----------
                        if (!GetAxis(
                            ifcElem,
                            out XYZ p1,
                            out XYZ p2))
                            throw new Exception("Не удалось определить ось");

                        // ---------- ТИП ВОЗДУХОВОДА ----------
                        DuctType ductType = FindDuctType(doc, isRound);
                        if (ductType == null)
                            throw new Exception("Нет подходящего типа воздуховода");

                        // ---------- ТИП СИСТЕМЫ ----------
                        string ifcSystem = GetIfcSystem(ifcElem);
                        ElementId systemTypeId =
                            GetSystemTypeByIfcSystem(doc, ifcSystem);

                        // ---------- СОЗДАНИЕ ----------
                        Duct duct = Duct.Create(
                            doc,
                            systemTypeId,
                            ductType.Id,
                            levelId,
                            p1,
                            p2);

                        if (duct == null)
                            throw new Exception("Duct.Create вернул null");

                        // ---------- РАЗМЕРЫ ----------
                        if (isRound)
                        {
                            duct.get_Parameter(
                                BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)
                                ?.Set(diamMM / 304.8);
                        }
                        else
                        {
                            duct.get_Parameter(
                                BuiltInParameter.RBS_CURVE_WIDTH_PARAM)
                                ?.Set(widthMM / 304.8);

                            duct.get_Parameter(
                                BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)
                                ?.Set(heightMM / 304.8);

                            // 🔑 ФИКС ПОВОРОТА ПРОФИЛЯ
                            FixRectangularProfileOrientation(
    duct,
    widthMM,
    heightMM,
    Math.Abs((p2 - p1).GetLength()) * 304.8); // длина в мм

                        }

                        ok++;
                    }
                    catch (Exception ex)
                    {
                        Debug.Print(ex.Message);
                        fail++;
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show(
                "Готово",
                $"Создано: {ok}\nОшибок: {fail}\nВсего: {picked.Count}");

            return Result.Succeeded;
        }

        // ======================================================
        // ОСЬ (через самую длинную геом. линию + центр BB)
        // ======================================================

        private bool GetAxis(
     Element e,
     out XYZ p1,
     out XYZ p2)
        {
            p1 = p2 = XYZ.Zero;

            // ---------- ДЛИНА ИЗ IFC ----------
            double lengthMM = 0;
            Parameter lp = e.LookupParameter("Length(MagiCAD Pset_Duct)");
            if (lp != null)
                double.TryParse(
                    lp.AsValueString()?.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out lengthMM);

            if (lengthMM <= 1)
                return false;

            double halfLen = (lengthMM / 304.8) / 2.0;

            // ---------- НАПРАВЛЕНИЕ ----------
            XYZ dir = GetAxisDirection(e);
            if (dir == null)
                return false;

            // ---------- ЦЕНТР ----------
            BoundingBoxXYZ bb = e.get_BoundingBox(null);
            if (bb == null)
                return false;

            XYZ center = (bb.Min + bb.Max) * 0.5;

            p1 = center - dir * halfLen;
            p2 = center + dir * halfLen;
            return true;
        }

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
        // САМАЯ ДЛИННАЯ CURVE
        // ======================================================

        private Curve GetLongestCurve(Element e)
        {
            Options opt = new Options
            {
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = e.get_Geometry(opt);
            if (geom == null)
                return null;

            Curve longest = null;
            double maxLen = 0;

            void Collect(GeometryObject go)
            {
                if (go is Curve c)
                {
                    if (c.Length > maxLen)
                    {
                        maxLen = c.Length;
                        longest = c;
                    }
                }
                else if (go is Solid s)
                {
                    foreach (Edge ed in s.Edges)
                        Collect(ed.AsCurve());
                }
                else if (go is GeometryInstance gi)
                {
                    foreach (GeometryObject igo in gi.GetInstanceGeometry())
                        Collect(igo);
                }
            }

            foreach (GeometryObject go in geom)
                Collect(go);

            return longest;
        }

        // ======================================================
        // ФИКС ПОВОРОТА ПРЯМОУГОЛЬНОГО ПРОФИЛЯ
        // ======================================================

        // ======================================================
        // ФИКС ПОВОРОТА ПРЯМОУГОЛЬНОГО ПРОФИЛЯ (ГОРИЗОНТАЛЬНЫЕ)
        // ======================================================

        // ======================================================
        // ФИКС ПОВОРОТА ПРЯМОУГОЛЬНОГО ПРОФИЛЯ
        // ПОВОРОТ В ПЛОСКОСТИ XY (ВОКРУГ ОСИ Z)
        // ======================================================

        private void FixRectangularProfileOrientation(
            Duct duct,
            double widthMM,
            double heightMM,
            double lengthMM)
        {
            LocationCurve lc = duct.Location as LocationCurve;
            if (lc == null)
                return;

            Line ductLine = lc.Curve as Line;
            if (ductLine == null)
                return;

            XYZ dir = ductLine.Direction.Normalize();

            // ❌ вертикальные НЕ ТРОГАЕМ
            if (Math.Abs(dir.DotProduct(XYZ.BasisZ)) > 0.9)
                return;

            // ✅ только если длина меньше ширины или высоты
            if (lengthMM >= widthMM && lengthMM >= heightMM)
                return;

            // ---------- центр воздуховода ----------
            XYZ center = (ductLine.GetEndPoint(0) + ductLine.GetEndPoint(1)) * 0.5;

            // ---------- ОСЬ ВРАЩЕНИЯ: Z ----------
            Line rotationAxis = Line.CreateUnbound(center, XYZ.BasisZ);

            try
            {
                duct.Location.Rotate(rotationAxis, Math.PI / 2);
            }
            catch
            {
                // Revit иногда отказывается — игнорируем
            }
        }



        // ======================================================
        // РАЗМЕРЫ ИЗ MAGiCAD
        // ======================================================

        private void GetSizeFromIFCParam(
            Element e,
            out bool isRound,
            out double widthMM,
            out double heightMM,
            out double diamMM)
        {
            isRound = false;
            widthMM = heightMM = diamMM = 0;

            string s = e.LookupParameter(
                "ConnectionSize_mm(MagiCAD Pset_Duct)")
                ?.AsString()
                ?.ToLower()
                ?.Replace(" ", "");

            if (string.IsNullOrEmpty(s))
                return;

            if (Regex.IsMatch(s, @"^\d+$"))
            {
                isRound = true;
                diamMM = double.Parse(s);
                return;
            }

            if (Regex.IsMatch(s, @"^\d+[xх]\d+$"))
            {
                var p = Regex.Split(s, @"[xх]");
                widthMM = double.Parse(p[0]);
                heightMM = double.Parse(p[1]);
            }
        }

        // ======================================================
        // IFC SYSTEM → MECHANICAL SYSTEM TYPE
        // ======================================================

        private string GetIfcSystem(Element e)
        {
            Parameter p = e.LookupParameter("IfcSystem");
            return p?.AsString() ?? p?.AsValueString() ?? "";
        }

        private ElementId GetSystemTypeByIfcSystem(
            Document doc,
            string ifcSystem)
        {
            string s = (ifcSystem ?? "").ToLower();
            string target = null;

            if (s.Contains("приток")) target = "ADSK_М_Приток";
            else if (s.Contains("вытяж")) target = "ADSK_М_Вытяжка";

            if (target != null)
            {
                var mst = new FilteredElementCollector(doc)
                    .OfClass(typeof(MechanicalSystemType))
                    .Cast<MechanicalSystemType>()
                    .FirstOrDefault(t =>
                        t.Name.Equals(target,
                        StringComparison.OrdinalIgnoreCase));

                if (mst != null)
                    return mst.Id;
            }

            return new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystemType))
                .Cast<MechanicalSystemType>()
                .FirstOrDefault()
                ?.Id;
        }

        // ======================================================
        // ТИП ВОЗДУХОВОДА
        // ======================================================

        private DuctType FindDuctType(Document doc, bool isRound)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DuctType))
                .Cast<DuctType>()
                .FirstOrDefault(t =>
                    isRound
                        ? t.Shape == ConnectorProfileType.Round
                        : t.Shape == ConnectorProfileType.Rectangular);
        }

        // ======================================================
        // LEVEL
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



