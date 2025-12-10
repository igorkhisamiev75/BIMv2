using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
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
                // Выбор элементов
                IList<Reference> selectedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    "Выберите IFC воздуховоды для преобразования");

                // Получаем системный тип по умолчанию
                MechanicalSystemType defaultSystemType = GetDefaultMechanicalSystemType(doc);

                if (defaultSystemType == null)
                {
                    TaskDialog.Show("Ошибка", "Не найден подходящий системный тип для воздуховодов");
                    return Result.Cancelled;
                }

                // Фильтруем только элементы воздуховодов
                List<Element> ifcDucts = new List<Element>();
                foreach (Reference reference in selectedRefs)
                {
                    Element elem = doc.GetElement(reference);
                    if (elem != null && elem.Category?.Id?.IntegerValue == (int)BuiltInCategory.OST_DuctCurves)
                    {
                        ifcDucts.Add(elem);
                    }
                }

                if (ifcDucts.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "Не выбраны элементы воздуховодов");
                    return Result.Cancelled;
                }

                using (Transaction trans = new Transaction(doc, "Преобразование IFC воздуховодов"))
                {
                    trans.Start();

                    int convertedCount = 0;

                    foreach (Element ifcDuct in ifcDucts)
                    {
                        try
                        {
                            // Определяем тип воздуховода по имени семейства или типоразмеру
                            string ductType = GetDuctTypeString(ifcDuct);

                            // Получаем параметр ConnectionSize_mm из MagiCAD
                            string sizeValue = GetSizeParameterValue(ifcDuct);

                            if (string.IsNullOrEmpty(sizeValue))
                            {
                                TaskDialog.Show("Предупреждение",
                                    $"Элемент {ifcDuct.Id} не имеет параметра ConnectionSize_mm. Пропущен.");
                                continue;
                            }

                            if (ductType.Contains("Круглые") || ductType.Contains("Round") ||
                                !sizeValue.Contains("x")) // Если нет 'x', предполагаем круглый
                            {
                                // Круглый воздуховод
                                FamilySymbol roundFamilySymbol = GetFamilySymbol(
                                    doc,
                                    "Воздуховод круглого сечения",
                                    "ADSK_Оцинковка_Врезки_ГОСТ 14918-2020");

                                if (roundFamilySymbol != null)
                                {
                                    // Извлекаем диаметр (убираем "мм" если есть)
                                    string diameterStr = sizeValue.Replace("мм", "").Trim();
                                    if (double.TryParse(diameterStr, out double diameterMM))
                                    {
                                        if (CreateRoundDuct(doc, ifcDuct, roundFamilySymbol,
                                            defaultSystemType.Id, diameterMM / 304.8))
                                        {
                                            convertedCount++;
                                        }
                                    }
                                    else
                                    {
                                        TaskDialog.Show("Ошибка",
                                            $"Неверный формат диаметра: {sizeValue} для элемента {ifcDuct.Id}");
                                    }
                                }
                            }
                            else if (ductType.Contains("Прямоугольные") || ductType.Contains("Rectangular") ||
                                     sizeValue.Contains("x"))
                            {
                                // Прямоугольный воздуховод
                                FamilySymbol rectangularFamilySymbol = GetFamilySymbol(
                                    doc,
                                    "Воздуховод прямоугольного сечения",
                                    "ADSK_Оцинковка_Врезки_ГОСТ 14918-2020");

                                if (rectangularFamilySymbol != null)
                                {
                                    // Парсим размеры формата "400x250"
                                    string[] dimensions = sizeValue.Split('x');
                                    if (dimensions.Length >= 2)
                                    {
                                        // Убираем "мм" и лишние символы
                                        string widthStr = dimensions[0].Replace("мм", "").Trim();
                                        string heightStr = dimensions[1].Replace("мм", "").Trim();

                                        if (double.TryParse(widthStr, out double widthMM) &&
                                            double.TryParse(heightStr, out double heightMM))
                                        {
                                            if (CreateRectangularDuct(doc, ifcDuct, rectangularFamilySymbol,
                                                defaultSystemType.Id, widthMM / 304.8, heightMM / 304.8))
                                            {
                                                convertedCount++;
                                            }
                                        }
                                        else
                                        {
                                            TaskDialog.Show("Ошибка",
                                                $"Неверный формат размеров: {sizeValue} для элемента {ifcDuct.Id}");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                TaskDialog.Show("Предупреждение",
                                    $"Не удалось определить тип воздуховода для элемента {ifcDuct.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Ошибка обработки",
                                $"Ошибка при обработке элемента {ifcDuct.Id}: {ex.Message}");
                        }
                    }

                    trans.Commit();

                    TaskDialog.Show("Успех", $"Преобразовано {convertedCount} из {ifcDucts.Count} воздуховодов");
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
                TaskDialog.Show("Критическая ошибка", ex.Message);
                return Result.Failed;
            }
        }

        private MechanicalSystemType GetDefaultMechanicalSystemType(Document doc)
        {
            // Получаем первый доступный системный тип для воздуховодов
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystemType));

            // Предпочтительные типы систем (в порядке приоритета)
            string[] preferredTypes = { "Приточный воздух", "Вытяжной воздух", "Воздуховоды", "Supply Air", "Exhaust Air", "Other" };

            foreach (string typeName in preferredTypes)
            {
                foreach (MechanicalSystemType systemType in collector)
                {
                    if (systemType.Name.Contains(typeName))
                        return systemType;
                }
            }

            // Если не нашли по имени, возвращаем первый доступный
            return collector.FirstOrDefault() as MechanicalSystemType;
        }

        private string GetDuctTypeString(Element duct)
        {
            // Пытаемся определить тип разными способами
            string typeName = "";

            // 1. Из параметра семейства
            Parameter familyParam = duct.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM);
            if (familyParam != null)
            {
                typeName = familyParam.AsValueString();
            }

            // 2. Из имени типа
            if (string.IsNullOrEmpty(typeName))
            {
                ElementId typeId = duct.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    Element ductType = duct.Document.GetElement(typeId);
                    if (ductType != null)
                    {
                        typeName = ductType.Name;
                    }
                }
            }

            // 3. Из имени семейства
            if (string.IsNullOrEmpty(typeName))
            {
                FamilyInstance fi = duct as FamilyInstance;
                if (fi != null && fi.Symbol != null && fi.Symbol.Family != null)
                {
                    typeName = fi.Symbol.Family.Name;
                }
            }

            return typeName ?? "";
        }

        private string GetSizeParameterValue(Element element)
        {
            // Пытаемся получить значение параметра разными способами
            string sizeValue = "";

            // 1. Ищем по имени "ConnectionSize_mm"
            Parameter param = element.LookupParameter("ConnectionSize_mm");
            if (param != null && param.HasValue)
            {
                sizeValue = param.AsString();
                if (string.IsNullOrEmpty(sizeValue))
                {
                    sizeValue = param.AsValueString();
                }
            }

            // 2. Ищем через все параметры (иногда параметры имеют разные имена)
            if (string.IsNullOrEmpty(sizeValue))
            {
                foreach (Parameter p in element.Parameters)
                {
                    if (p.Definition != null &&
                        (p.Definition.Name.Contains("ConnectionSize") ||
                         p.Definition.Name.Contains("Размер соединения")))
                    {
                        if (p.HasValue)
                        {
                            sizeValue = p.AsString();
                            if (string.IsNullOrEmpty(sizeValue))
                            {
                                sizeValue = p.AsValueString();
                            }
                            break;
                        }
                    }
                }
            }

            return sizeValue ?? "";
        }

        private FamilySymbol GetFamilySymbol(Document doc, string familyName, string typeName)
        {
            try
            {
                // Ищем семейство и тип
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .WhereElementIsElementType();

                foreach (FamilySymbol fs in collector)
                {
                    if (fs.Family != null &&
                        fs.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                        fs.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!fs.IsActive)
                            fs.Activate();

                        return fs;
                    }
                }

                // Если не нашли точное совпадение, ищем частичное
                foreach (FamilySymbol fs in collector)
                {
                    if (fs.Family != null &&
                        fs.Family.Name.IndexOf(familyName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        fs.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!fs.IsActive)
                            fs.Activate();

                        return fs;
                    }
                }

                TaskDialog.Show("Ошибка", $"Не найден тип: '{typeName}' в семействе: '{familyName}'");
                return null;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка поиска семейства", ex.Message);
                return null;
            }
        }

        private bool CreateRoundDuct(Document doc, Element ifcDuct, FamilySymbol familySymbol,
            ElementId systemTypeId, double diameter)
        {
            try
            {
                if (ifcDuct.Location is LocationCurve locationCurve)
                {
                    Curve curve = locationCurve.Curve;

                    // Создаем воздуховод Revit
                    Duct newDuct = Duct.Create(doc,
                        systemTypeId,
                        familySymbol.Id,
                        levelId: ifcDuct.LevelId,
                        startPoint: curve.GetEndPoint(0),
                        endPoint: curve.GetEndPoint(1));

                    if (newDuct != null)
                    {
                        // Устанавливаем диаметр
                        SetDuctSize(newDuct, diameter, 0, "Диаметр");

                        // Копируем другие параметры
                        CopyParameters(ifcDuct, newDuct);

                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка создания круглого воздуховода", ex.Message);
                return false;
            }
        }

        private bool CreateRectangularDuct(Document doc, Element ifcDuct, FamilySymbol familySymbol,
            ElementId systemTypeId, double width, double height)
        {
            try
            {
                if (ifcDuct.Location is LocationCurve locationCurve)
                {
                    Curve curve = locationCurve.Curve;

                    // Создаем воздуховод Revit
                    Duct newDuct = Duct.Create(doc,
                        systemTypeId,
                        familySymbol.Id,
                        levelId: ifcDuct.LevelId,
                        startPoint: curve.GetEndPoint(0),
                        endPoint: curve.GetEndPoint(1));

                    if (newDuct != null)
                    {
                        // Устанавливаем ширину и высоту
                        SetDuctSize(newDuct, width, height, "Ширина", "Высота");

                        // Копируем другие параметры
                        CopyParameters(ifcDuct, newDuct);

                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка создания прямоугольного воздуховода", ex.Message);
                return false;
            }
        }

        private void SetDuctSize(Duct duct, double primarySize, double secondarySize,
            string primaryParamName = "Диаметр", string secondaryParamName = "")
        {
            // Устанавливаем основной размер (диаметр или ширину)
            Parameter primaryParam = duct.LookupParameter(primaryParamName);
            if (primaryParam != null)
            {
                if (primaryParam.StorageType == StorageType.Double)
                {
                    primaryParam.Set(primarySize);
                }
                else if (primaryParam.StorageType == StorageType.String)
                {
                    // Если параметр строковый, конвертируем в мм
                    primaryParam.Set(Math.Round(primarySize * 304.8).ToString());
                }
            }

            // Устанавливаем второй размер (высоту), если нужно
            if (!string.IsNullOrEmpty(secondaryParamName) && secondarySize > 0)
            {
                Parameter secondaryParam = duct.LookupParameter(secondaryParamName);
                if (secondaryParam != null)
                {
                    if (secondaryParam.StorageType == StorageType.Double)
                    {
                        secondaryParam.Set(secondarySize);
                    }
                    else if (secondaryParam.StorageType == StorageType.String)
                    {
                        // Если параметр строковый, конвертируем в мм
                        secondaryParam.Set(Math.Round(secondarySize * 304.8).ToString());
                    }
                }
            }
        }

        private void CopyParameters(Element source, Element target)
        {
            try
            {
                // Копирование общих параметров
                string[] parametersToCopy = { "Комментарии", "Имя", "Марка", "Примечания", "Description", "Comments" };

                foreach (string paramName in parametersToCopy)
                {
                    Parameter sourceParam = source.LookupParameter(paramName);
                    Parameter targetParam = target.LookupParameter(paramName);

                    if (sourceParam != null && targetParam != null && sourceParam.HasValue &&
                        sourceParam.StorageType == targetParam.StorageType)
                    {
                        switch (sourceParam.StorageType)
                        {
                            case StorageType.String:
                                targetParam.Set(sourceParam.AsString());
                                break;
                            case StorageType.Double:
                                targetParam.Set(sourceParam.AsDouble());
                                break;
                            case StorageType.Integer:
                                targetParam.Set(sourceParam.AsInteger());
                                break;
                            case StorageType.ElementId:
                                targetParam.Set(sourceParam.AsElementId());
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки копирования параметров
                System.Diagnostics.Debug.WriteLine($"Ошибка копирования параметров: {ex.Message}");
            }
        }
    }
}