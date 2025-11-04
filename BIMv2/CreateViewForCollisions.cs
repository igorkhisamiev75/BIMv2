#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateViewForCollisions : IExternalCommand
    {
        private Document _doc;
        private readonly Dictionary<int, List<Solid>> elementSolidsCache = new Dictionary<int, List<Solid>>();
        private readonly Dictionary<int, BoundingBoxXYZ> elementBboxCache = new Dictionary<int, BoundingBoxXYZ>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _doc = commandData.Application.ActiveUIDocument.Document;
            Stopwatch timer = Stopwatch.StartNew();

            ProgressForm2 progressForm = null;

            try
            {
                ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                ElementId solidFillPatternId = GetSolidFillPatternId();

                OpenFileDialog ofd = new OpenFileDialog
                {
                    Title = "Выберите XML отчет Navisworks",
                    Filter = "XML files|*.xml"
                };
                if (ofd.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                XmlDocument xml = new XmlDocument();
                xml.Load(ofd.FileName);
                var clashResults = xml.SelectNodes("//clashresult");

                if (clashResults == null || clashResults.Count == 0)
                {
                    TaskDialog.Show("Информация", "В отчёте не найдено коллизий.");
                    return Result.Cancelled;
                }

                var allElements = new FilteredElementCollector(_doc)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .ToList();

                progressForm = new ProgressForm2();
                progressForm.Show();
                progressForm.ProgressBar.Maximum = clashResults.Count;
                progressForm.StatusLabel.Text = "Начало обработки...";
                Application.DoEvents();

                int viewCount = 0;
                int processed = 0;
                StringBuilder log = new StringBuilder();

                using (Transaction t = new Transaction(_doc, "Создание видов коллизий"))
                {
                    t.Start();

                    foreach (XmlNode clash in clashResults)
                    {
                        processed++;

                        // Проверка отмены
                        if (progressForm.CancelRequested)
                        {
                            progressForm.StatusLabel.Text = "Отмена пользователем...";
                            Application.DoEvents();
                            break;
                        }

                        progressForm.StatusLabel.Text = $"Обработка {processed} из {clashResults.Count}";
                        progressForm.ProgressBar.Value = processed;
                        Application.DoEvents();

                        string name = clash.Attributes["name"]?.Value ?? "Без_имени";

                        List<int> geomIds = clash.SelectNodes(".//objectattribute[name='ID объекта']/value")
                            .Cast<XmlNode>()
                            .Select(x => int.TryParse(x.InnerText.Trim(), out int id) ? id : -1)
                            .Where(id => id > 0)
                            .Distinct()
                            .ToList();

                        if (geomIds.Count == 0)
                            continue;

                        List<Element> realElements = FindElementsByGeometryIds(geomIds, allElements);
                        if (realElements.Count == 0)
                        {
                            log.AppendLine($"⚠️ {name}: не найдено элементов для ID [{string.Join(",", geomIds)}]");
                            continue;
                        }

                        List<Element> collidingElements = VerifyActualCollision(realElements, name);
                        if (collidingElements.Count < 2)
                        {
                            log.AppendLine($"⚠️ {name}: элементы не сталкиваются");
                            continue;
                        }

                        string categoriesInfo = GetCategoriesInfo(collidingElements);

                        View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);
                        string idsString = string.Join("_", collidingElements.Select(e => e.Id.IntegerValue));
                        view.Name = $"{name}_{categoriesInfo}_IDs_{idsString}";
                        ConfigureView(view);

                        BoundingBoxXYZ sectionBox = CreatePreciseSectionBox(collidingElements);
                        if (sectionBox != null)
                            view.SetSectionBox(sectionBox);

                        ApplyOverridesOnlyColliding(view, collidingElements.Select(e => e.Id).ToList(), solidFillPatternId);
                        viewCount++;
                    }

                    t.Commit();
                }

                timer.Stop();
                progressForm.Close();

                TaskDialog.Show("Результат", $"Создано {viewCount} 3D видов\nВремя: {timer.Elapsed:mm\\:ss}");
                Debug.WriteLine(log.ToString());
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                if (progressForm != null)
                    progressForm.Close();

                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        #region ⚡ Проверка коллизий

        private List<Element> VerifyActualCollision(List<Element> elements, string clashName)
        {
            if (elements.Count < 2)
                return elements;

            List<Element> trulyColliding = new List<Element>();
            Options geomOptions = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Coarse,
                IncludeNonVisibleObjects = false
            };

            try
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    Element elem1 = elements[i];
                    List<Solid> solids1 = GetCachedSolids(elem1, geomOptions);
                    BoundingBoxXYZ bbox1 = GetCachedBoundingBox(elem1);

                    if (solids1.Count == 0 || bbox1 == null)
                        continue;

                    for (int j = i + 1; j < elements.Count; j++)
                    {
                        Element elem2 = elements[j];
                        BoundingBoxXYZ bbox2 = GetCachedBoundingBox(elem2);

                        if (bbox2 == null || !DoBoundingBoxesIntersect(bbox1, bbox2))
                            continue;

                        List<Solid> solids2 = GetCachedSolids(elem2, geomOptions);
                        if (solids2.Count == 0)
                            continue;

                        if (CheckSolidsIntersectionFast(solids1, solids2))
                        {
                            if (!trulyColliding.Contains(elem1))
                                trulyColliding.Add(elem1);
                            if (!trulyColliding.Contains(elem2))
                                trulyColliding.Add(elem2);
                        }
                    }
                }

                Debug.WriteLine($"🔍 {clashName}: из {elements.Count} элементов действительно сталкиваются {trulyColliding.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка проверки коллизии {clashName}: {ex.Message}");
                return elements;
            }

            return trulyColliding;
        }

        private bool CheckSolidsIntersectionFast(List<Solid> solids1, List<Solid> solids2)
        {
            foreach (Solid s1 in solids1)
            {
                if (s1 == null || s1.Volume < 0.001)
                    continue;

                BoundingBoxXYZ bb1 = s1.GetBoundingBox();
                foreach (Solid s2 in solids2)
                {
                    if (s2 == null || s2.Volume < 0.001)
                        continue;

                    BoundingBoxXYZ bb2 = s2.GetBoundingBox();

                    if (!DoBoundingBoxesIntersect(bb1, bb2))
                        continue;

                    try
                    {
                        Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                            s1, s2, BooleanOperationsType.Intersect);
                        if (intersection != null && intersection.Volume > 0.0001)
                            return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        private List<Solid> GetCachedSolids(Element element, Options options)
        {
            int id = element.Id.IntegerValue;
            if (elementSolidsCache.TryGetValue(id, out List<Solid> cached))
                return cached;

            List<Solid> solids = new List<Solid>();
            try
            {
                GeometryElement geom = element.get_Geometry(options);
                if (geom != null)
                    solids.AddRange(GetSolidsFromGeometry(geom));
            }
            catch { }

            elementSolidsCache[id] = solids;
            return solids;
        }

        private BoundingBoxXYZ GetCachedBoundingBox(Element element)
        {
            int id = element.Id.IntegerValue;
            if (elementBboxCache.TryGetValue(id, out BoundingBoxXYZ cached))
                return cached;

            BoundingBoxXYZ bbox = null;
            try
            {
                bbox = element.get_BoundingBox(null);
                if (bbox == null)
                    bbox = element.get_BoundingBox(_doc.ActiveView);
            }
            catch { }

            elementBboxCache[id] = bbox;
            return bbox;
        }

        private bool DoBoundingBoxesIntersect(BoundingBoxXYZ bb1, BoundingBoxXYZ bb2)
        {
            return bb1.Min.X <= bb2.Max.X && bb1.Max.X >= bb2.Min.X &&
                   bb1.Min.Y <= bb2.Max.Y && bb1.Max.Y >= bb2.Min.Y &&
                   bb1.Min.Z <= bb2.Max.Z && bb1.Max.Z >= bb2.Min.Z;
        }

        #endregion

        #region 🧱 Geometry

        private List<Solid> GetSolidsFromGeometry(GeometryElement geometry)
        {
            List<Solid> solids = new List<Solid>();
            foreach (GeometryObject geomObj in geometry)
            {
                if (geomObj is Solid solid && solid.Volume > 0.001)
                    solids.Add(solid);
                else if (geomObj is GeometryInstance gi)
                {
                    GeometryElement instGeom = gi.GetInstanceGeometry();
                    if (instGeom != null)
                        solids.AddRange(GetSolidsFromGeometry(instGeom));
                }
            }
            return solids;
        }

        private BoundingBoxXYZ CreatePreciseSectionBox(List<Element> elements)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (Element element in elements)
            {
                BoundingBoxXYZ bbox = GetCachedBoundingBox(element);
                if (bbox == null) continue;

                minX = Math.Min(minX, bbox.Min.X);
                minY = Math.Min(minY, bbox.Min.Y);
                minZ = Math.Min(minZ, bbox.Min.Z);
                maxX = Math.Max(maxX, bbox.Max.X);
                maxY = Math.Max(maxY, bbox.Max.Y);
                maxZ = Math.Max(maxZ, bbox.Max.Z);
            }

            double padding = Math.Max(Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)) * 0.2, 1.0);
            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX - padding, minY - padding, minZ - padding),
                Max = new XYZ(maxX + padding, maxY + padding, maxZ + padding)
            };
        }

        #endregion

        #region 🎨 View настройки

        private void ConfigureView(View3D view)
        {
            view.Discipline = ViewDiscipline.Coordination;
            view.DetailLevel = ViewDetailLevel.Fine;
        }

        private void ApplyOverridesOnlyColliding(View3D view, List<ElementId> collidingIds, ElementId solidFillPatternId)
        {
            if (collidingIds.Count == 0) return;

            Color[] colors = {
                new Color(255,165,0),
                new Color(0,100,255),
                new Color(50,205,50),
                new Color(255,255,0),
                new Color(255,0,255),
                new Color(0,255,255)
            };

            for (int i = 0; i < collidingIds.Count; i++)
            {
                ElementId id = collidingIds[i];
                Element el = _doc.GetElement(id);
                if (el == null) continue;

                Color color = colors[i % colors.Length];
                OverrideGraphicSettings ogs = new OverrideGraphicSettings()
                    .SetProjectionLineColor(color)
                    .SetSurfaceForegroundPatternColor(color)
                    .SetSurfaceTransparency(0);

                if (solidFillPatternId != ElementId.InvalidElementId)
                    ogs.SetSurfaceForegroundPatternId(solidFillPatternId);

                view.SetElementOverrides(id, ogs);
            }
        }

        #endregion

        #region 🔎 Вспомогательные функции

        private List<Element> FindElementsByGeometryIds(List<int> geomIds, List<Element> allElements)
        {
            List<Element> found = new List<Element>();

            foreach (Element el in allElements)
            {
                try
                {
                    Options opt = new Options { IncludeNonVisibleObjects = true, ComputeReferences = false };
                    GeometryElement geomElem = el.get_Geometry(opt);
                    if (geomElem == null) continue;

                    foreach (GeometryObject geomObj in geomElem)
                    {
                        if (geomObj is GeometryInstance gi && gi.Symbol != null)
                        {
                            if (geomIds.Contains(gi.Symbol.Id.IntegerValue))
                            {
                                found.Add(el);
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            return found.Distinct(new ElementIdComparer()).ToList();
        }

        private ElementId GetSolidFillPatternId()
        {
            foreach (FillPatternElement fpe in new FilteredElementCollector(_doc).OfClass(typeof(FillPatternElement)))
                if (fpe.GetFillPattern().IsSolidFill)
                    return fpe.Id;
            return ElementId.InvalidElementId;
        }

        private string GetCategoriesInfo(List<Element> elements)
        {
            var categories = elements.Where(e => e?.Category != null)
                .Select(e => e.Category.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (categories.Count == 0) return "Unknown";
            if (categories.Count == 1) return categories[0];
            if (categories.Count == 2) return $"{categories[0]}_vs_{categories[1]}";
            return string.Join("_", categories.Take(3)) + "_etc";
        }

        private class ElementIdComparer : IEqualityComparer<Element>
        {
            public bool Equals(Element x, Element y) => x.Id.IntegerValue == y.Id.IntegerValue;
            public int GetHashCode(Element obj) => obj.Id.IntegerValue.GetHashCode();
        }

        #endregion
    }
}
