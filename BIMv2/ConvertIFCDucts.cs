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
            ElementId systemTypeId = GetOrCreateAirSystemType(doc);

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
                        // ---------- РАЗМЕРЫ И ФОРМА ----------
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
                            isRound,
                            widthMM,
                            heightMM,
                            out XYZ p1,
                            out XYZ p2))
                            throw new Exception("Не удалось определить ось");

                        // ---------- ТИП ----------
                        DuctType ductType = FindDuctType(doc, isRound);
                        if (ductType == null)
                            throw new Exception("Нет подходящего типа воздуховода");

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

        // =========================================================
        // ОСЬ ВОЗДУХОВОДА (ФИНАЛЬНАЯ ВЕРСИЯ)
        // =========================================================

        private bool GetAxis(
    Element e,
    bool isRound,
    double widthMM,
    double heightMM,
    out XYZ p1,
    out XYZ p2)
        {
            p1 = p2 = XYZ.Zero;

            Curve axis = GetLongestCurve(e);
            if (axis == null)
                return false;

            XYZ a = axis.GetEndPoint(0);
            XYZ b = axis.GetEndPoint(1);
            XYZ dir = (b - a).Normalize();

            // ======================================================
            // КРУГЛЫЕ
            // ======================================================
            if (isRound)
            {
                Solid solid = GetSolid(e);
                if (solid == null || solid.Volume < 1e-6)
                {
                    p1 = a;
                    p2 = b;
                    return true;
                }

                XYZ centroid = solid.ComputeCentroid();
                XYZ proj = a + dir * ((centroid - a).DotProduct(dir));
                XYZ offset = centroid - proj;

                p1 = a + offset;
                p2 = b + offset;
                return true;
            }

            // ======================================================
            // ПРЯМОУГОЛЬНЫЕ — ЕДИНСТВЕННО ВЕРНЫЙ СПОСОБ
            // ======================================================
            Solid rectSolid = GetSolid(e);
            if (rectSolid == null || rectSolid.Volume < 1e-6)
            {
                // fallback — без смещения
                p1 = a;
                p2 = b;
                return true;
            }

            XYZ centroidR = rectSolid.ComputeCentroid();
            XYZ projR = a + dir * ((centroidR - a).DotProduct(dir));
            XYZ realOffset = centroidR - projR;

            // ⚠️ НИКАКОЙ МАТЕМАТИКИ С WIDTH / HEIGHT
            // используем реальный вектор смещения профиля

            p1 = a + realOffset;
            p2 = b + realOffset;

            return true;
        }





        // =========================================================
        // НАПРАВЛЕНИЕ = САМАЯ ДЛИННАЯ CURVE
        // =========================================================

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

        // =========================================================
        // SOLID (ТОЛЬКО ДЛЯ КРУГЛЫХ)
        // =========================================================

        private Solid GetSolid(Element e)
        {
            Options opt = new Options
            {
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = e.get_Geometry(opt);
            if (geom == null)
                return null;

            foreach (GeometryObject go in geom)
            {
                if (go is Solid s && s.Volume > 0)
                    return s;

                if (go is GeometryInstance gi)
                {
                    foreach (GeometryObject igo in gi.GetInstanceGeometry())
                    {
                        if (igo is Solid ss && ss.Volume > 0)
                            return ss;
                    }
                }
            }

            return null;
        }

        // =========================================================
        // РАЗМЕРЫ MAGiCAD
        // =========================================================

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

        // =========================================================
        // ТИП ВОЗДУХОВОДА
        // =========================================================

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

        // =========================================================
        // SERVICE
        // =========================================================

        private ElementId GetOrCreateLevel(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault()?.Id
                ?? Level.Create(doc, 0).Id;
        }

        private ElementId GetOrCreateAirSystemType(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystemType))
                .Cast<MechanicalSystemType>()
                .FirstOrDefault(t =>
                    t.SystemClassification == MEPSystemClassification.SupplyAir)
                ?.Id
                ?? MechanicalSystemType.Create(
                    doc,
                    MEPSystemClassification.SupplyAir,
                    "IFC Air System").Id;
        }
    }
}
