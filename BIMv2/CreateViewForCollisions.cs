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
using System.IO;
using System.Text;
#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateViewForCollisions : IExternalCommand
    {
        private Document _doc;
        private UIDocument _uiDoc;
        private List<View3D> _createdViews = new List<View3D>();
        private List<ElementId> _allElementsToColor = new List<ElementId>();

        private Dictionary<int, int> _genericModelSymbolMap = new Dictionary<int, int>();
        private Dictionary<int, List<FamilyInstance>> _symbolToInstancesMap = new Dictionary<int, List<FamilyInstance>>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;
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

                progressForm = new ProgressForm2();
                progressForm.Show();
                progressForm.ProgressBar.Maximum = clashResults.Count + 1;
                progressForm.StatusLabel.Text = "Построение базы элементов...";
                Application.DoEvents();

                BuildNewElementDatabase(progressForm);
                progressForm.ProgressBar.Value = 1;

                int viewCount = 0;
                int processed = 0;

                using (Transaction t = new Transaction(_doc, "Создание видов коллизий"))
                {
                    t.Start();

                    foreach (XmlNode clash in clashResults)
                    {
                        processed++;
                        progressForm.StatusLabel.Text = $"Обработка коллизий {processed} из {clashResults.Count}";
                        progressForm.ProgressBar.Value = processed + 1;
                        Application.DoEvents();

                        if (progressForm.CancelRequested)
                            break;

                        string name = clash.Attributes["name"]?.Value ?? $"Коллизия_{processed}";

                        List<int> allIds = GetAllIdsFromClashNode(clash);
                        if (allIds.Count == 0)
                            continue;

                        _allElementsToColor.Clear();
                        List<Element> foundElements = new List<Element>();

                        foreach (int id in allIds)
                        {
                            bool elementFound = false;

                            Element directElement = _doc.GetElement(new ElementId(id));
                            if (directElement != null)
                            {
                                ProcessDirectElement(directElement, foundElements, id);
                                elementFound = true;
                            }

                            if (elementFound) continue;

                            if (_genericModelSymbolMap.ContainsValue(id))
                            {
                                var matchingElements = _genericModelSymbolMap.Where(x => x.Value == id).ToList();
                                foreach (var match in matchingElements)
                                {
                                    Element element = _doc.GetElement(new ElementId(match.Key));
                                    if (element != null && !foundElements.Any(e => e.Id == element.Id))
                                    {
                                        foundElements.Add(element);
                                        _allElementsToColor.Add(element.Id);
                                    }
                                }
                                elementFound = true;
                            }

                            if (elementFound) continue;

                            if (_symbolToInstancesMap.ContainsKey(id))
                            {
                                var instances = _symbolToInstancesMap[id];
                                foreach (var instance in instances)
                                {
                                    if (!foundElements.Any(e => e.Id == instance.Id))
                                        foundElements.Add(instance);
                                    if (!_allElementsToColor.Contains(instance.Id))
                                        _allElementsToColor.Add(instance.Id);
                                }
                                elementFound = true;
                            }
                        }

                        if (foundElements.Count > 0)
                        {
                            if (CreateCollisionView(name, foundElements, viewFamilyType, solidFillPatternId))
                            {
                                viewCount++;
                            }
                        }
                    }

                    t.Commit();
                }

                timer.Stop();
                progressForm?.Close();

                TaskDialog.Show("Результат",
                    $"Создано {viewCount} 3D видов.\nВремя: {timer.Elapsed:mm\\:ss}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                progressForm?.Close();
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        private void BuildNewElementDatabase(ProgressForm2 progressForm)
        {
            _genericModelSymbolMap.Clear();
            _symbolToInstancesMap.Clear();

            var genericModels = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType()
                .ToList();

            int processed = 0;

            foreach (Element element in genericModels)
            {
                processed++;
                if (processed % 50 == 0)
                {
                    progressForm.StatusLabel.Text = $"Анализ обобщенных моделей: {processed} из {genericModels.Count}";
                    Application.DoEvents();
                }

                try
                {
                    int? symbolId = GetCorrectSymbolIdFromGeometry(element);

                    if (symbolId.HasValue)
                    {
                        _genericModelSymbolMap[element.Id.IntegerValue] = symbolId.Value;

                        if (element is FamilyInstance instance)
                        {
                            AddToSymbolMap(symbolId.Value, instance);
                        }
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        private int? GetCorrectSymbolIdFromGeometry(Element element)
        {
            try
            {
                Options options = new Options
                {
                    ComputeReferences = true,
                    DetailLevel = ViewDetailLevel.Medium,
                    IncludeNonVisibleObjects = true
                };

                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null)
                    return null;

                var geometryInstances = GetAllGeometryInstances(geometry);

                foreach (var geometryInstance in geometryInstances)
                {
                    try
                    {
                        GeometryElement symbolGeometry = geometryInstance.GetSymbolGeometry();
                        if (symbolGeometry == null)
                            continue;

                        FamilySymbol matchingSymbol = FindExactMatchingFamilySymbol(symbolGeometry);
                        if (matchingSymbol != null)
                        {
                            return matchingSymbol.Id.IntegerValue;
                        }
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private List<GeometryInstance> GetAllGeometryInstances(GeometryElement geometry)
        {
            var geometryInstances = new List<GeometryInstance>();

            if (geometry == null) return geometryInstances;

            foreach (GeometryObject geomObj in geometry)
            {
                if (geomObj is GeometryInstance geometryInstance)
                {
                    geometryInstances.Add(geometryInstance);

                    try
                    {
                        GeometryElement instanceGeometry = geometryInstance.GetInstanceGeometry();
                        if (instanceGeometry != null)
                        {
                            geometryInstances.AddRange(GetAllGeometryInstances(instanceGeometry));
                        }
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
            }

            return geometryInstances;
        }

        private FamilySymbol FindExactMatchingFamilySymbol(GeometryElement targetGeometry)
        {
            try
            {
                var allSymbols = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .ToList();

                BoundingBoxXYZ targetBBox = targetGeometry.GetBoundingBox();
                if (targetBBox == null)
                    return null;

                double targetVolume = GetBoundingBoxVolume(targetBBox);

                foreach (var symbol in allSymbols)
                {
                    try
                    {
                        Options options = new Options
                        {
                            ComputeReferences = true,
                            DetailLevel = ViewDetailLevel.Medium,
                            IncludeNonVisibleObjects = true
                        };

                        GeometryElement symbolGeometry = symbol.get_Geometry(options);
                        if (symbolGeometry == null) continue;

                        BoundingBoxXYZ symbolBBox = symbolGeometry.GetBoundingBox();
                        if (symbolBBox == null) continue;

                        double symbolVolume = GetBoundingBoxVolume(symbolBBox);

                        if (AreGeometryElementsIdentical(targetGeometry, symbolGeometry))
                        {
                            return symbol;
                        }

                        double volumeDifference = Math.Abs(targetVolume - symbolVolume) / Math.Max(targetVolume, symbolVolume);
                        if (volumeDifference < 0.1)
                        {
                            return symbol;
                        }
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool AreGeometryElementsIdentical(GeometryElement geom1, GeometryElement geom2)
        {
            try
            {
                BoundingBoxXYZ bbox1 = geom1.GetBoundingBox();
                BoundingBoxXYZ bbox2 = geom2.GetBoundingBox();

                if (bbox1 == null || bbox2 == null) return false;

                double tolerance = 0.001;
                if (Math.Abs(bbox1.Min.X - bbox2.Min.X) > tolerance) return false;
                if (Math.Abs(bbox1.Min.Y - bbox2.Min.Y) > tolerance) return false;
                if (Math.Abs(bbox1.Min.Z - bbox2.Min.Z) > tolerance) return false;
                if (Math.Abs(bbox1.Max.X - bbox2.Max.X) > tolerance) return false;
                if (Math.Abs(bbox1.Max.Y - bbox2.Max.Y) > tolerance) return false;
                if (Math.Abs(bbox1.Max.Z - bbox2.Max.Z) > tolerance) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private double GetBoundingBoxVolume(BoundingBoxXYZ bbox)
        {
            return (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y) * (bbox.Max.Z - bbox.Min.Z);
        }

        private void AddToSymbolMap(int symbolId, FamilyInstance instance)
        {
            if (!_symbolToInstancesMap.ContainsKey(symbolId))
                _symbolToInstancesMap[symbolId] = new List<FamilyInstance>();

            if (!_symbolToInstancesMap[symbolId].Any(inst => inst.Id == instance.Id))
                _symbolToInstancesMap[symbolId].Add(instance);
        }

        private void ProcessDirectElement(Element directElement, List<Element> foundElements, int id)
        {
            if (directElement is FamilySymbol foundSymbol)
            {
                var instances = GetInstancesOfFamilySymbol(foundSymbol);
                if (instances.Count > 0)
                {
                    foreach (var inst in instances)
                    {
                        if (!foundElements.Any(e => e.Id == inst.Id))
                            foundElements.Add(inst);
                        if (!_allElementsToColor.Contains(inst.Id))
                            _allElementsToColor.Add(inst.Id);
                    }
                }
            }
            else
            {
                if (!foundElements.Any(e => e.Id == directElement.Id))
                    foundElements.Add(directElement);
                if (!_allElementsToColor.Contains(directElement.Id))
                    _allElementsToColor.Add(directElement.Id);
            }
        }

        private List<Element> GetInstancesOfFamilySymbol(FamilySymbol familySymbol)
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.Id == familySymbol.Id)
                    .Cast<Element>()
                    .ToList();
            }
            catch
            {
                return new List<Element>();
            }
        }

        private List<int> GetAllIdsFromClashNode(XmlNode clashNode)
        {
            List<int> ids = new List<int>();
            var valueNodes = clashNode.SelectNodes(".//value");
            if (valueNodes != null)
            {
                foreach (XmlNode valueNode in valueNodes)
                {
                    string valueText = valueNode.InnerText.Trim();
                    if (int.TryParse(valueText, out int id) && id > 0)
                        ids.Add(id);
                }
            }
            return ids;
        }

        private bool CreateCollisionView(string clashName, List<Element> elements,
            ViewFamilyType viewFamilyType, ElementId solidFillPatternId)
        {
            try
            {
                View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                string categories = string.Join("_", elements
                    .Where(e => e.Category != null)
                    .Select(e => e.Category.Name)
                    .Distinct()
                    .Take(2));

                string ids = string.Join("_", elements.Select(e => e.Id.IntegerValue).Take(3));
                string viewName = $"{clashName.Substring(0, Math.Min(50, clashName.Length))}_{categories}_{ids}";
                if (viewName.Length > 200)
                    viewName = viewName.Substring(0, 200);

                view.Name = viewName;
                view.DisplayStyle = DisplayStyle.Shading;
                view.DetailLevel = ViewDetailLevel.Fine;
                view.Discipline = ViewDiscipline.Coordination;

                BoundingBoxXYZ sectionBox = CreateSectionBoxFromElements(elements);
                if (sectionBox != null)
                    view.SetSectionBox(sectionBox);

                ApplyColorOverrides(view, _allElementsToColor, solidFillPatternId);

                _createdViews.Add(view);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private BoundingBoxXYZ CreateSectionBoxFromElements(List<Element> elements)
        {
            List<BoundingBoxXYZ> boxes = new List<BoundingBoxXYZ>();
            foreach (var el in elements)
            {
                var bb = el.get_BoundingBox(null);
                if (bb != null)
                    boxes.Add(bb);
            }
            if (boxes.Count == 0) return null;

            double minX = boxes.Min(b => b.Min.X);
            double minY = boxes.Min(b => b.Min.Y);
            double minZ = boxes.Min(b => b.Min.Z);
            double maxX = boxes.Max(b => b.Max.X);
            double maxY = boxes.Max(b => b.Max.Y);
            double maxZ = boxes.Max(b => b.Max.Z);

            double offset = Math.Max((maxX - minX) * 0.2, 1.0);
            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX - offset, minY - offset, minZ - offset),
                Max = new XYZ(maxX + offset, maxY + offset, maxZ + offset)
            };
        }

        private void ApplyColorOverrides(View3D view, List<ElementId> elementIds, ElementId solidFillPatternId)
        {
            Color[] colors = {
                new Color(255, 0, 0),
                new Color(0, 255, 0),
                new Color(0, 0, 255),
                new Color(255, 165, 0)
            };
            int index = 0;

            foreach (var id in elementIds)
            {
                Color c = colors[index % colors.Length];
                OverrideGraphicSettings ogs = new OverrideGraphicSettings()
                    .SetProjectionLineColor(c)
                    .SetProjectionLineWeight(5)
                    .SetSurfaceForegroundPatternId(solidFillPatternId)
                    .SetSurfaceForegroundPatternColor(c)
                    .SetSurfaceTransparency(0);

                try
                {
                    view.SetElementOverrides(id, ogs);
                }
                catch
                {
                    // Ignore errors
                }
                index++;
            }
        }

        private ElementId GetSolidFillPatternId()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill)?.Id
                ?? ElementId.InvalidElementId;
        }
    }
}