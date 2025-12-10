using System;
using System.Collections.Generic;
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
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Выбор IFC элементов
                IList<Reference> selectedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new IFCSelectionFilter(),
                    "Выберите IFC воздуховоды для преобразования");

                if (selectedRefs.Count == 0)
                {
                    TaskDialog.Show("Информация", "Не выбрано ни одного элемента");
                    return Result.Cancelled;
                }

                // 2. Подготовка: находим необходимые семейства и системный тип
                FamilySymbol roundDuctSymbol = FindDuctFamilySymbol(doc, "круглый");
                FamilySymbol rectangularDuctSymbol = FindDuctFamilySymbol(doc, "прямоугольный");
                ElementId systemTypeId = GetSystemTypeId(doc);

                if (systemTypeId == ElementId.InvalidElementId)
                {
                    TaskDialog.Show("Ошибка", "В проекте нет системных типов для воздуховодов. Создайте хотя бы один через интерфейс Revit.");
                    return Result.Cancelled;
                }

                // 3. Обработка элементов
                using (Transaction trans = new Transaction(doc, "Преобразование IFC в воздуховоды"))
                {
                    trans.Start();

                    int successCount = 0;
                    int failedCount = 0;
                    List<ElementId> processedElements = new List<ElementId>();

                    foreach (Reference reference in selectedRefs)
                    {
                        Element ifcElement = doc.GetElement(reference);
                        if (ifcElement == null) continue;

                        try
                        {
                            bool created = ProcessIFCElement(doc, ifcElement,
                                roundDuctSymbol, rectangularDuctSymbol, systemTypeId);

                            if (created)
                            {
                                successCount++;
                                processedElements.Add(ifcElement.Id);
                            }
                            else
                            {
                                failedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Ошибка элемента",
                                $"Ошибка при обработке элемента {ifcElement.Id}: {ex.Message}");
                            failedCount++;
                        }
                    }

                    // 4. Опционально: скрываем оригинальные IFC элементы
                    //if (successCount > 0 && TaskDialog.Show("Скрыть оригиналы?",
                    //    "Скрыть оригинальные IFC элементы?", TaskDialogCommonButtons.YesNo) == TaskDialogResult.Yes)
                    //{
                    //    HideElements(doc, processedElements);
                    //}

                    trans.Commit();

                    // 5. Результат
                    TaskDialog.Show("Результат",
                        $"Успешно преобразовано: {successCount}\n" +
                        $"Не удалось преобразовать: {failedCount}\n" +
                        $"Всего обработано: {selectedRefs.Count}");
                }

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

        private bool ProcessIFCElement(Document doc, Element ifcElement,
            FamilySymbol roundSymbol, FamilySymbol rectangularSymbol, ElementId systemTypeId)
        {
            // 1. Получаем геометрию IFC элемента
            GeometryElement geometry = ifcElement.get_Geometry(new Options());
            if (geometry == null) return false;

            // 2. Ищем кривую (линию) воздуховода
            Curve ductCurve = ExtractCurveFromGeometry(geometry);
            if (ductCurve == null) return false;

            // 3. Определяем тип сечения (круглое/прямоугольное)
            DuctSectionType sectionType = DetermineSectionType(ifcElement, geometry);

            // 4. Получаем размеры
            double width = 0, height = 0, diameter = 0;
            GetDuctDimensions(ifcElement, sectionType, out width, out height, out diameter);

            // 5. Создаем воздуховод Revit
            Duct newDuct = null;
            if (sectionType == DuctSectionType.Round && roundSymbol != null)
            {
                newDuct = Duct.Create(doc, systemTypeId, roundSymbol.Id,
                    ifcElement.LevelId, ductCurve.GetEndPoint(0), ductCurve.GetEndPoint(1));

                if (newDuct != null && diameter > 0)
                {
                    SetDuctDiameter(newDuct, diameter);
                }
            }
            else if (sectionType == DuctSectionType.Rectangular && rectangularSymbol != null)
            {
                newDuct = Duct.Create(doc, systemTypeId, rectangularSymbol.Id,
                    ifcElement.LevelId, ductCurve.GetEndPoint(0), ductCurve.GetEndPoint(1));

                if (newDuct != null && width > 0 && height > 0)
                {
                    SetRectangularDuctSize(newDuct, width, height);
                }
            }

            if (newDuct == null) return false;

            // 6. Копируем общие свойства
            CopyElementProperties(ifcElement, newDuct);

            return true;
        }

        private Curve ExtractCurveFromGeometry(GeometryElement geometry)
        {
            foreach (GeometryObject geomObj in geometry)
            {
                // Ищем кривые в геометрии
                if (geomObj is Curve curve)
                {
                    return curve;
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    // Ищем кривые в экземплярах геометрии
                    GeometryElement instGeometry = geomInst.GetSymbolGeometry();
                    if (instGeometry != null)
                    {
                        foreach (GeometryObject instGeomObj in instGeometry)
                        {
                            if (instGeomObj is Curve instCurve)
                            {
                                return instCurve;
                            }
                        }
                    }
                }
                else if (geomObj is Solid solid)
                {
                    // Ищем края у твердого тела
                    foreach (Edge edge in solid.Edges)
                    {
                        Curve edgeCurve = edge.AsCurve();
                        if (edgeCurve != null && edgeCurve.Length > 0.1) // Игнорируем очень короткие кривые
                        {
                            return edgeCurve;
                        }
                    }
                }
            }
            return null;
        }

        private DuctSectionType DetermineSectionType(Element element, GeometryElement geometry)
        {
            // 1. Пробуем определить по параметрам IFC
            Parameter param = element.LookupParameter("ConnectionSize_mm");
            if (param != null && param.HasValue)
            {
                string sizeValue = param.AsString() ?? param.AsValueString();
                if (!string.IsNullOrEmpty(sizeValue))
                {
                    if (sizeValue.Contains("x")) return DuctSectionType.Rectangular;
                    else return DuctSectionType.Round;
                }
            }

            // 2. Пробуем определить по имени типа/семейства
            string elementInfo = element.Name ?? "";
            if (elementInfo.Contains("Круглый") || elementInfo.Contains("Round") ||
                elementInfo.Contains("Круглое") || elementInfo.Contains("Circular"))
                return DuctSectionType.Round;
            if (elementInfo.Contains("Прямоугольный") || elementInfo.Contains("Rectangular") ||
                elementInfo.Contains("Прямоугольное") || elementInfo.Contains("Rect"))
                return DuctSectionType.Rectangular;

            // 3. Пробуем определить по геометрии
            foreach (GeometryObject geomObj in geometry)
            {
                if (geomObj is Solid solid)
                {
                    // Анализируем профиль твердого тела
                    BoundingBoxXYZ bbox = solid.GetBoundingBox();
                    if (bbox != null)
                    {
                        double width = bbox.Max.X - bbox.Min.X;
                        double height = bbox.Max.Y - bbox.Min.Y;
                        double depth = bbox.Max.Z - bbox.Min.Z;

                        // Если сечение примерно круглое (ширина ≈ высоте)
                        if (Math.Abs(width - height) / Math.Max(width, height) < 0.2)
                            return DuctSectionType.Round;
                        else
                            return DuctSectionType.Rectangular;
                    }
                }
            }

            // По умолчанию считаем круглым
            return DuctSectionType.Round;
        }

        private void GetDuctDimensions(Element element, DuctSectionType sectionType,
            out double width, out double height, out double diameter)
        {
            width = height = diameter = 0;

            // Ищем параметры с размерами
            string sizeValue = "";
            Parameter sizeParam = element.LookupParameter("ConnectionSize_mm");
            if (sizeParam != null && sizeParam.HasValue)
            {
                sizeValue = sizeParam.AsString() ?? sizeParam.AsValueString() ?? "";
            }

            if (string.IsNullOrEmpty(sizeValue))
            {
                // Ищем другие возможные параметры
                foreach (Parameter param in element.Parameters)
                {
                    if (param.Definition.Name.Contains("Size") ||
                        param.Definition.Name.Contains("Размер") ||
                        param.Definition.Name.Contains("Diameter") ||
                        param.Definition.Name.Contains("Диаметр"))
                    {
                        if (param.HasValue)
                        {
                            sizeValue = param.AsString() ?? param.AsValueString() ?? "";
                            if (!string.IsNullOrEmpty(sizeValue)) break;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(sizeValue))
            {
                sizeValue = sizeValue.Replace("мм", "").Replace("mm", "").Trim();

                if (sectionType == DuctSectionType.Round)
                {
                    // Парсим диаметр
                    Match match = Regex.Match(sizeValue, @"\d+(\.\d+)?");
                    if (match.Success && double.TryParse(match.Value, out double dim))
                    {
                        diameter = dim / 304.8; // мм в футы
                    }
                }
                else if (sectionType == DuctSectionType.Rectangular)
                {
                    // Парсим ширину x высоту
                    if (sizeValue.Contains("x"))
                    {
                        string[] parts = sizeValue.Split('x');
                        if (parts.Length >= 2)
                        {
                            Match widthMatch = Regex.Match(parts[0], @"\d+(\.\d+)?");
                            Match heightMatch = Regex.Match(parts[1], @"\d+(\.\d+)?");

                            if (widthMatch.Success && double.TryParse(widthMatch.Value, out double w))
                                width = w / 304.8;
                            if (heightMatch.Success && double.TryParse(heightMatch.Value, out double h))
                                height = h / 304.8;
                        }
                    }
                }
            }

            // Если не нашли в параметрах, пробуем из геометрии
            if ((sectionType == DuctSectionType.Round && diameter <= 0) ||
                (sectionType == DuctSectionType.Rectangular && (width <= 0 || height <= 0)))
            {
                EstimateDimensionsFromGeometry(element, ref width, ref height, ref diameter);
            }
        }

        private void EstimateDimensionsFromGeometry(Element element, ref double width, ref double height, ref double diameter)
        {
            Options options = new Options();
            GeometryElement geometry = element.get_Geometry(options);

            foreach (GeometryObject geomObj in geometry)
            {
                if (geomObj is Solid solid)
                {
                    BoundingBoxXYZ bbox = solid.GetBoundingBox();
                    if (bbox != null)
                    {
                        width = (bbox.Max.X - bbox.Min.X);
                        height = (bbox.Max.Y - bbox.Min.Y);

                        // Если сечение круглое, диаметр ≈ средней размер
                        if (Math.Abs(width - height) < 0.1)
                        {
                            diameter = (width + height) / 2;
                        }
                        break;
                    }
                }
            }
        }

        private FamilySymbol FindDuctFamilySymbol(Document doc, string familyType)
        {
            try
            {
                // Ищем семейства воздуховодов
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .WhereElementIsElementType()
                    .OfCategory(BuiltInCategory.OST_DuctCurves);

                List<FamilySymbol> candidates = new List<FamilySymbol>();

                foreach (FamilySymbol symbol in collector)
                {
                    if (symbol == null || symbol.Family == null) continue;

                    string name = symbol.Name.ToLower() + " " + symbol.Family.Name.ToLower();

                    if (familyType == "круглый")
                    {
                        if (name.Contains("круг") || name.Contains("round") ||
                            name.Contains("circular") || name.Contains("цилиндр"))
                        {
                            candidates.Add(symbol);
                        }
                    }
                    else if (familyType == "прямоугольный")
                    {
                        if (name.Contains("прямо") || name.Contains("rectangular") ||
                            name.Contains("rect") || name.Contains("квадрат"))
                        {
                            candidates.Add(symbol);
                        }
                    }
                }

                // Возвращаем первый подходящий или первый вообще
                if (candidates.Count > 0)
                {
                    FamilySymbol selected = candidates.First();
                    if (!selected.IsActive) selected.Activate();
                    return selected;
                }
                else if (collector.Any())
                {
                    FamilySymbol first = collector.First() as FamilySymbol;
                    if (first != null && !first.IsActive) first.Activate();
                    return first;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private ElementId GetSystemTypeId(Document doc)
        {
            // Ищем любой системный тип для воздуховодов
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystemType));

            MechanicalSystemType systemType = collector.FirstOrDefault() as MechanicalSystemType;
            return systemType?.Id ?? ElementId.InvalidElementId;
        }

        private void SetDuctDiameter(Duct duct, double diameter)
        {
            if (diameter <= 0) return;

            string[] paramNames = { "Диаметр", "Diameter", "d", "Ø", "Size", "Размер" };
            foreach (string name in paramNames)
            {
                Parameter param = duct.LookupParameter(name);
                if (param != null)
                {
                    try
                    {
                        if (param.StorageType == StorageType.Double)
                            param.Set(diameter);
                        else if (param.StorageType == StorageType.String)
                            param.Set(Math.Round(diameter * 304.8).ToString());
                        break;
                    }
                    catch { }
                }
            }
        }

        private void SetRectangularDuctSize(Duct duct, double width, double height)
        {
            if (width <= 0 || height <= 0) return;

            // Ширина
            string[] widthNames = { "Ширина", "Width", "b", "A" };
            foreach (string name in widthNames)
            {
                Parameter param = duct.LookupParameter(name);
                if (param != null)
                {
                    try
                    {
                        if (param.StorageType == StorageType.Double)
                            param.Set(width);
                        else if (param.StorageType == StorageType.String)
                            param.Set(Math.Round(width * 304.8).ToString());
                        break;
                    }
                    catch { }
                }
            }

            // Высота
            string[] heightNames = { "Высота", "Height", "h", "B" };
            foreach (string name in heightNames)
            {
                Parameter param = duct.LookupParameter(name);
                if (param != null)
                {
                    try
                    {
                        if (param.StorageType == StorageType.Double)
                            param.Set(height);
                        else if (param.StorageType == StorageType.String)
                            param.Set(Math.Round(height * 304.8).ToString());
                        break;
                    }
                    catch { }
                }
            }
        }

        private void CopyElementProperties(Element source, Element target)
        {
            try
            {
                //// Копируем уровень
                //if (source.LevelId != null && source.LevelId != ElementId.InvalidElementId)
                //{
                //    target.LevelId = source.LevelId;
                //}

                // Копируем комментарии
                Parameter sourceComment = source.LookupParameter("Комментарии");
                Parameter targetComment = target.LookupParameter("Комментарии");
                if (sourceComment != null && targetComment != null && sourceComment.HasValue)
                {
                    targetComment.Set(sourceComment.AsString());
                }

                // Копируем марку
                Parameter sourceMark = source.LookupParameter("Марка");
                Parameter targetMark = target.LookupParameter("Марка");
                if (sourceMark != null && targetMark != null && sourceMark.HasValue)
                {
                    targetMark.Set(sourceMark.AsString());
                }
            }
            catch { }
        }

        private void HideElements(Document doc, List<ElementId> elementIds)
        {
            try
            {
                foreach (ElementId id in elementIds)
                {
                    Element element = doc.GetElement(id);
                    if (element != null)
                    {
                        // Прячем элемент
                        //element.get_Parameter(BuiltInParameter.ELEM_CATEGORY_HIDDEN)?.Set(1);
                    }
                }
            }
            catch { }
        }

        private enum DuctSectionType
        {
            Round,
            Rectangular
        }
    }

    // Фильтр для выбора только IFC элементов
    public class IFCSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem == null) return false;

            // Разрешаем элементы, которые могут быть IFC импортом
            // IFC элементы обычно имеют специфические категории или являются семействами
            if (elem.Category != null)
            {
                // Воздуховоды или трубы
                if (elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctCurves ||
                    elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeCurves ||
                    elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericModel)
                {
                    // Проверяем, не является ли это уже нативным элементом Revit
                    if (elem is FamilyInstance || elem is MEPCurve)
                    {
                        // Это уже Revit элемент, пропускаем
                        return false;
                    }
                    return true;
                }
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}