#region Namespace
using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
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
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _doc = uiDoc.Document;

            // Получаем ViewFamilyType для 3D вида
            ViewFamilyType viewFamilyType =
                (from v in new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                 where v.ViewFamily == ViewFamily.ThreeDimensional
                 select v).First();

            // Получаем сплошной паттерн заливки один раз
            ElementId solidFillPatternId = GetSolidFillPatternId();

            using (var t = new Transaction(_doc))
            {
                t.Start("Create view for collisions");

                // Открываем диалог для выбора XML файла
                OpenFileDialog fbd = new OpenFileDialog();
                fbd.Title = "Загрузите XML с коллизиями";
                fbd.Filter = "XML files|*.xml";

                if (fbd.ShowDialog() != DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                string strfilename = fbd.FileName;
                XmlDocument xmldoc = new XmlDocument();
                xmldoc.Load(strfilename);

                // Словарь для хранения информации о конфликтах
                Dictionary<string, List<ElementId>> clashData = new Dictionary<string, List<ElementId>>();

                // Парсим XML
                XmlNodeList clashResults = xmldoc.SelectNodes("//clashresult");
                foreach (XmlNode clashResult in clashResults)
                {
                    string clashName = clashResult.Attributes["name"]?.Value ?? "Unknown";
                    List<ElementId> elementIds = new List<ElementId>();

                    // Получаем ID объектов из конфликта
                    XmlNodeList clashObjects = clashResult.SelectNodes(".//objectattribute/value");
                    foreach (XmlNode clashObject in clashObjects)
                    {
                        if (int.TryParse(clashObject.InnerText, out int elementIdInt))
                        {
                            elementIds.Add(new ElementId(elementIdInt));
                        }
                    }

                    if (elementIds.Count >= 2)
                    {
                        clashData.Add(clashName, elementIds);
                    }
                }

                // Создаем виды для каждого конфликта
                foreach (var clash in clashData)
                {
                    string clashName = clash.Key;
                    List<ElementId> elementIds = clash.Value;

                    // Находим элементы, которые существуют в модели
                    List<Element> validElements = new List<Element>();
                    foreach (ElementId elementId in elementIds)
                    {
                        Element element = _doc.GetElement(elementId);
                        if (element != null && element.Category != null)
                        {
                            validElements.Add(element);
                        }
                    }

                    // Нужно минимум 2 элемента для создания рамки
                    if (validElements.Count < 2) continue;

                    try
                    {
                        // Создаем 3D вид
                        View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                        // Формируем имя вида
                        string idString = string.Join("_", elementIds.Select(id => id.IntegerValue));
                        view.Name = $"{clashName}_IDs_{idString}";

                        // Создаем bounding box, охватывающий все элементы
                        BoundingBoxXYZ combinedBoundingBox = CreateCombinedBoundingBox(validElements);
                        if (combinedBoundingBox != null)
                        {
                            view.SetSectionBox(combinedBoundingBox);
                        }

                        // Переопределяем графику элементов
                        OverrideGraphicSettings(view, elementIds, solidFillPatternId);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Ошибка", $"Не удалось создать вид для конфликта {clashName}: {ex.Message}");
                        continue;
                    }
                }

                t.Commit();
                TaskDialog.Show("2bim.ru", $"Создано {clashData.Count} 3D видов с проблемными местами");
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Создает общий bounding box для нескольких элементов
        /// </summary>
        private BoundingBoxXYZ CreateCombinedBoundingBox(List<Element> elements)
        {
            if (elements == null || elements.Count == 0)
                return null;

            // Инициализируем минимальные и максимальные значения
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double minZ = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            double maxZ = double.MinValue;

            bool hasValidBoundingBox = false;

            foreach (Element element in elements)
            {
                try
                {
                    BoundingBoxXYZ bbox = element.get_BoundingBox(_doc.ActiveView);
                    if (bbox != null)
                    {
                        // Обновляем минимальные значения
                        minX = Math.Min(minX, bbox.Min.X);
                        minY = Math.Min(minY, bbox.Min.Y);
                        minZ = Math.Min(minZ, bbox.Min.Z);

                        // Обновляем максимальные значения
                        maxX = Math.Max(maxX, bbox.Max.X);
                        maxY = Math.Max(maxY, bbox.Max.Y);
                        maxZ = Math.Max(maxZ, bbox.Max.Z);

                        hasValidBoundingBox = true;
                    }
                }
                catch
                {
                    // Пропускаем элементы с ошибками получения bounding box
                    continue;
                }
            }

            if (!hasValidBoundingBox)
                return null;

            // Создаем объединенный bounding box
            BoundingBoxXYZ combinedBox = new BoundingBoxXYZ();

            // Добавляем отступ для лучшего обзора (10% от размера + фиксированный зазор)
            double paddingX = Math.Max((maxX - minX) * 0.1, 2.0);
            double paddingY = Math.Max((maxY - minY) * 0.1, 2.0);
            double paddingZ = Math.Max((maxZ - minZ) * 0.1, 2.0);

            combinedBox.Min = new XYZ(
                minX - paddingX,
                minY - paddingY,
                minZ - paddingZ
            );

            combinedBox.Max = new XYZ(
                maxX + paddingX,
                maxY + paddingY,
                maxZ + paddingZ
            );

            return combinedBox;
        }

        /// <summary>
        /// Переопределяет графику элементов на виде
        /// </summary>
        private void OverrideGraphicSettings(View3D view, List<ElementId> elementIds, ElementId solidFillPatternId)
        {
            int colorIndex = 0;

            foreach (ElementId elementId in elementIds)
            {
                Element element = _doc.GetElement(elementId);
                if (element == null || element.Category == null) continue;

                // Создаем настройки переопределения графики
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();

                // Устанавливаем цвет в зависимости от порядка элемента
                Color color = (colorIndex == 0) ?
                    new Color(255, 0, 0) : // Красный для первого элемента
                    new Color(0, 0, 255);  // Синий для второго элемента

                // Устанавливаем цвет линий
                ogs.SetProjectionLineColor(color);
                ogs.SetCutLineColor(color); // Цвет линий в разрезе

                // Устанавливаем цвет и паттерн заливки для поверхностей
                ogs.SetSurfaceForegroundPatternColor(color);
                if (solidFillPatternId != ElementId.InvalidElementId)
                {
                    ogs.SetSurfaceForegroundPatternId(solidFillPatternId);
                }

                // Устанавливаем цвет и паттерн заливки для разреза
                ogs.SetCutForegroundPatternColor(color);
                if (solidFillPatternId != ElementId.InvalidElementId)
                {
                    ogs.SetCutForegroundPatternId(solidFillPatternId);
                }

                // Применяем переопределение
                view.SetElementOverrides(elementId, ogs);

                colorIndex++;
            }
        }

        /// <summary>
        /// Получает ID сплошного паттерна заливки
        /// </summary>
        private ElementId GetSolidFillPatternId()
        {
            try
            {
                // Ищем сплошной паттерн заливки через фильтр
                FilteredElementCollector collector = new FilteredElementCollector(_doc);
                collector.OfClass(typeof(FillPatternElement));

                foreach (FillPatternElement fillPattern in collector)
                {
                    FillPattern pattern = fillPattern.GetFillPattern();
                    if (pattern.IsSolidFill)
                    {
                        return fillPattern.Id;
                    }
                }

                // Если не нашли, ищем по имени
                return FindSolidPatternByName();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Предупреждение", "Не удалось найти сплошной паттерн заливки: " + ex.Message);
                return ElementId.InvalidElementId;
            }
        }

        /// <summary>
        /// Ищет сплошной паттерн по имени
        /// </summary>
        private ElementId FindSolidPatternByName()
        {
            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(_doc);
                collector.OfClass(typeof(FillPatternElement));

                // Список возможных имен сплошных паттернов
                string[] possibleNames = { "Solid", "Solid fill", "Сплошная", "Сплошная заливка", "Заливка" };

                foreach (FillPatternElement pattern in collector)
                {
                    foreach (string name in possibleNames)
                    {
                        if (pattern.Name.Contains(name))
                        {
                            return pattern.Id;
                        }
                    }
                }

                // Если не нашли по имени, берем первый попавшийся сплошной паттерн
                foreach (FillPatternElement pattern in collector)
                {
                    if (pattern.GetFillPattern().IsSolidFill)
                    {
                        return pattern.Id;
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем InvalidElementId
            }

            return ElementId.InvalidElementId;
        }
    }
}