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

                        double lengthMM = GetIfcLengthMM(ifcElem);
                        if (lengthMM <= 1)
                            throw new Exception("Некорректная длина IFC");

                        if (!GetAxisFromEdge(
                            ifcElem,
                            lengthMM,
                            out XYZ p1,
                            out XYZ p2))
                            throw new Exception("Не удалось определить ось");

                        DuctType ductType = FindDuctType(doc, isRound);
                        if (ductType == null)
                            throw new Exception("Нет типа воздуховода");

                        ElementId systemTypeId =
                            GetSystemTypeByIfcSystem(doc, GetIfcSystem(ifcElem));

                        Duct duct = Duct.Create(
                            doc,
                            systemTypeId,
                            ductType.Id,
                            levelId,
                            p1,
                            p2);

                        if (duct == null)
                            throw new Exception("Duct.Create == null");

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
                $"Создан1о: {ok}\nОшибок: {fail}\nВсего: {picked.Count}");

            return Result.Succeeded;
        }

        // ======================================================
        // ОСЬ ИЗ EDGE ПО IFC LENGTH
        // ======================================================

        private bool GetAxisFromEdge(
    Element e,
    double lengthMM,
    out XYZ p1,
    out XYZ p2)
        {
            p1 = p2 = XYZ.Zero;

            double targetLen = lengthMM / 304.8;

            BoundingBoxXYZ bb = e.get_BoundingBox(null);
            if (bb == null)
                return false;

            XYZ center = (bb.Min + bb.Max) * 0.5;

            Solid solid = GetSolid(e);

            // -------------------------------
            // 1. ПЫТАЕМСЯ ЧЕРЕЗ EDGE
            // -------------------------------
            if (solid != null)
            {
                double tol = targetLen * 0.2;
                Edge bestEdge = null;
                double bestDelta = double.MaxValue;

                foreach (Edge edge in solid.Edges)
                {
                    double len = edge.ApproximateLength;
                    double delta = Math.Abs(len - targetLen);

                    if (delta < tol && delta < bestDelta)
                    {
                        bestDelta = delta;
                        bestEdge = edge;
                    }
                }

                if (bestEdge != null)
                {
                    IList<XYZ> pts = bestEdge.Tessellate();
                    if (pts.Count >= 2)
                    {
                        XYZ dir = (pts.Last() - pts.First()).Normalize();
                        p1 = center - dir * targetLen / 2;
                        p2 = center + dir * targetLen / 2;
                        return true;
                    }
                }
            }

            // -------------------------------
            // 2. FALLBACK: ПО FACE NORMAL
            // -------------------------------
            if (solid != null)
            {
                Face bestFace = null;
                double maxArea = 0;

                foreach (Face f in solid.Faces)
                {
                    if (f.Area > maxArea)
                    {
                        maxArea = f.Area;
                        bestFace = f;
                    }
                }

                if (bestFace != null)
                {
                    XYZ normal = bestFace.ComputeNormal(new UV(0.5, 0.5));
                    XYZ dir = normal.CrossProduct(XYZ.BasisZ);

                    if (dir.GetLength() < 1e-6)
                        dir = normal.CrossProduct(XYZ.BasisX);

                    dir = dir.Normalize();

                    p1 = center - dir * targetLen / 2;
                    p2 = center + dir * targetLen / 2;
                    return true;
                }
            }

            // -------------------------------
            // 3. LAST FALLBACK: BOUNDING BOX
            // -------------------------------
            XYZ size = bb.Max - bb.Min;

            XYZ fallbackDir =
                size.X >= size.Y && size.X >= size.Z ? XYZ.BasisX :
                size.Y >= size.X && size.Y >= size.Z ? XYZ.BasisY :
                XYZ.BasisZ;

            p1 = center - fallbackDir * targetLen / 2;
            p2 = center + fallbackDir * targetLen / 2;

            return true;
        }



        // ======================================================
        // SOLID
        // ======================================================

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

        // ======================================================
        // IFC LENGTH
        // ======================================================

        private double GetIfcLengthMM(Element e)
        {
            Parameter p = e.LookupParameter("Length(MagiCAD Pset_Duct)");
            if (p == null) return 0;

            double.TryParse(
                p.AsValueString()?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v);

            return v;
        }

        // ======================================================
        // SIZE
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
        // SYSTEM
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
            string target =
                s.Contains("приток") ? "ADSK_М_Приток" :
                s.Contains("вытяж") ? "ADSK_М_Вытяжка" :
                null;

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
        // DUCT TYPE
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
