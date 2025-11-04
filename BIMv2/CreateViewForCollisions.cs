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
#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateViewForCollisions : IExternalCommand
    {
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _doc = commandData.Application.ActiveUIDocument.Document;
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                // Получаем тип для 3D-вида
                ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                ElementId solidFillPatternId = GetSolidFillPatternId();

                using (Transaction t = new Transaction(_doc, "Создание видов коллизий"))
                {
                    t.Start();

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
                    int viewCount = 0;

                    // Загружаем все элементы один раз
                    var allElements = new FilteredElementCollector(_doc)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .ToList();

                    foreach (XmlNode clash in clashResults)
                    {
                        string name = clash.Attributes["name"]?.Value ?? "Без_имени";

                        // Читаем ID из отчета
                        List<int> geomIds = clash.SelectNodes(".//objectattribute[name='ID объекта']/value")
                            .Cast<XmlNode>()
                            .Select(x => int.TryParse(x.InnerText.Trim(), out int id) ? id : -1)
                            .Where(id => id > 0)
                            .Distinct()
                            .ToList();

                        if (geomIds.Count == 0)
                            continue;

                        // Находим реальные элементы Revit по этим ID
                        List<Element> realElements = FindElementsByGeometryIds(geomIds, allElements);

                        if (realElements.Count == 0)
                        {
                            Debug.WriteLine($"⚠️ {name}: не найдено элементов для ID [{string.Join(",", geomIds)}]");
                            continue;
                        }

                        // Создаем 3D вид
                        View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);
                        string idsString = string.Join("_", geomIds);
                        view.Name = $"{name}_IDs_{idsString}";

                        // Создаем секцию вокруг найденных элементов
                        BoundingBoxXYZ box = CreateBoundingBox(realElements);
                        if (box != null)
                            view.SetSectionBox(box);

                        // Подсвечиваем коллизионные элементы
                        ApplyOverrides(view, realElements.Select(e => e.Id).ToList(), solidFillPatternId);

                        viewCount++;
                    }

                    t.Commit();
                    timer.Stop();
                    TaskDialog.Show("Результат", $"Создано {viewCount} 3D видов\nВремя: {timer.Elapsed:mm\\:ss}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        // 🔍 Поиск реальных элементов по GeometryInstance.Symbol.Id
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
                        if (geomObj is GeometryInstance gi)
                        {
                            if (gi.Symbol != null)
                            {
                                int symId = gi.Symbol.Id.IntegerValue;
                                if (geomIds.Contains(symId))
                                {
                                    found.Add(el);
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return found.Distinct(new ElementIdComparer()).ToList();
        }

        // 🧱 BoundingBox всех элементов
        private BoundingBoxXYZ CreateBoundingBox(List<Element> elements)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (var e in elements)
            {
                try
                {
                    BoundingBoxXYZ bb = e.get_BoundingBox(null);
                    if (bb == null) continue;

                    minX = Math.Min(minX, bb.Min.X);
                    minY = Math.Min(minY, bb.Min.Y);
                    minZ = Math.Min(minZ, bb.Min.Z);
                    maxX = Math.Max(maxX, bb.Max.X);
                    maxY = Math.Max(maxY, bb.Max.Y);
                    maxZ = Math.Max(maxZ, bb.Max.Z);
                }
                catch { }
            }

            if (minX == double.MaxValue)
                return null;

            double pad = 2.0;
            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX - pad, minY - pad, minZ - pad),
                Max = new XYZ(maxX + pad, maxY + pad, maxZ + pad)
            };
        }

        // 🎨 Подсветка
        private void ApplyOverrides(View3D view, List<ElementId> elementIds, ElementId solidFillPatternId)
        {
            Color[] colors = { new Color(255, 0, 0), new Color(0, 0, 255) };

            // Серая прозрачность по всей модели
            OverrideGraphicSettings gray = new OverrideGraphicSettings();
            gray.SetProjectionLineColor(new Color(180, 180, 180));
            gray.SetSurfaceForegroundPatternColor(new Color(180, 180, 180));
            gray.SetSurfaceTransparency(80);
            if (solidFillPatternId != ElementId.InvalidElementId)
                gray.SetSurfaceForegroundPatternId(solidFillPatternId);

            foreach (var id in new FilteredElementCollector(_doc).WhereElementIsNotElementType().ToElementIds())
            {
                try { view.SetElementOverrides(id, gray); } catch { }
            }

            // Подсветка коллизионных
            for (int i = 0; i < elementIds.Count; i++)
            {
                ElementId id = elementIds[i];
                Color color = colors[i % colors.Length];

                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(color);
                ogs.SetSurfaceForegroundPatternColor(color);
                ogs.SetSurfaceTransparency(0);
                if (solidFillPatternId != ElementId.InvalidElementId)
                    ogs.SetSurfaceForegroundPatternId(solidFillPatternId);

                try { view.SetElementOverrides(id, ogs); } catch { }
            }
        }

        // 🔳 Поиск сплошной заливки
        private ElementId GetSolidFillPatternId()
        {
            foreach (FillPatternElement fpe in new FilteredElementCollector(_doc).OfClass(typeof(FillPatternElement)))
                if (fpe.GetFillPattern().IsSolidFill)
                    return fpe.Id;
            return ElementId.InvalidElementId;
        }

        private class ElementIdComparer : IEqualityComparer<Element>
        {
            public bool Equals(Element x, Element y) => x.Id.IntegerValue == y.Id.IntegerValue;
            public int GetHashCode(Element obj) => obj.Id.IntegerValue.GetHashCode();
        }
    }
}
