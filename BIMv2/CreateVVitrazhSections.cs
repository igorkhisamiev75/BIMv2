#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

#endregion

namespace RevitAddIn2BIMRU.Commands.Views
{
    [Transaction(TransactionMode.Manual)]
    public class CreateVVitrazhSections : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. Получаем все витражные стены (марка начинается с "ВВ-")
                List<Wall> vitrazhWalls = GetVitrazhWalls(doc);

                if (vitrazhWalls.Count == 0)
                {
                    TaskDialog.Show("Информация", "В проекте не найдены витражные стены (марка начинается с 'ВВ-').");
                    return Result.Cancelled;
                }

                TaskDialog.Show("Найдены стены",
                    $"Найдено витражных стен: {vitrazhWalls.Count}\n" +
                    $"Создание разрезов и размещение на листе...");

                // 2. Создаем разрезы для каждой стены
                List<ViewSection> createdSections = new List<ViewSection>();

                using (Transaction trans = new Transaction(doc, "Создание разрезов витражных стен"))
                {
                    trans.Start();

                    foreach (Wall wall in vitrazhWalls)
                    {
                        ViewSection section = CreateSectionForWall(doc, wall);
                        if (section != null)
                        {
                            createdSections.Add(section);
                        }
                    }

                    trans.Commit();
                }

                if (createdSections.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "Не удалось создать разрезы.");
                    return Result.Failed;
                }

                // 3. Создаем лист и размещаем разрезы
                using (Transaction trans = new Transaction(doc, "Создание листа с разрезами"))
                {
                    trans.Start();

                    // Получаем любой доступный штамп
                    ViewSheet sheet = CreateSheetWithSections(doc, createdSections);

                    if (sheet == null)
                    {
                        TaskDialog.Show("Ошибка", "Не удалось создать лист.");
                        trans.RollBack();
                        return Result.Failed;
                    }

                    trans.Commit();

                    TaskDialog.Show("Успех",
                        $"Создано:\n" +
                        $"- Разрезов: {createdSections.Count}\n" +
                        $"- Лист: {sheet.SheetNumber} - {sheet.Name}\n" +
                        $"Все разрезы размещены на листе.");

                    // Активируем созданный лист
                    uiDoc.ActiveView = sheet;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Не удалось создать разрезы: {ex.Message}\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Получает все витражные стены (марка начинается с "ВВ-")
        /// </summary>
        private List<Wall> GetVitrazhWalls(Document doc)
        {
            List<Wall> vitrazhWalls = new List<Wall>();

            // Получаем все стены в проекте
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> walls = collector
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element element in walls)
            {
                Wall wall = element as Wall;
                if (wall != null)
                {
                    // Получаем параметр "Марка"
                    Parameter markParam = wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (markParam != null && markParam.HasValue)
                    {
                        string mark = markParam.AsString();
                        if (!string.IsNullOrEmpty(mark) && mark.StartsWith("ВВ-", StringComparison.OrdinalIgnoreCase))
                        {
                            vitrazhWalls.Add(wall);
                        }
                    }
                }
            }

            return vitrazhWalls;
        }

        /// <summary>
        /// Создает разрез для стены
        /// </summary>
        private ViewSection CreateSectionForWall(Document doc, Wall wall)
        {
            try
            {
                // Получаем тип вида "Разрез"
                ViewFamilyType sectionType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);

                if (sectionType == null)
                {
                    TaskDialog.Show("Ошибка", "Не найден тип вида 'Разрез'.");
                    return null;
                }

                // Получаем марку стены для имени разреза
                string wallMark = "Разрез";
                Parameter markParam = wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                if (markParam != null && markParam.HasValue)
                {
                    string mark = markParam.AsString();
                    if (!string.IsNullOrEmpty(mark))
                    {
                        wallMark = $"Разрез_{mark}";
                    }
                    else
                    {
                        wallMark = $"Разрез_Стена_{wall.Id}";
                    }
                }
                else
                {
                    wallMark = $"Разрез_Стена_{wall.Id}";
                }

                // Создаем BoundingBox для разреза (параллельно стене)
                BoundingBoxXYZ sectionBox = GetSectionViewParallelToWall(wall);

                // Создаем разрез
                ViewSection section = ViewSection.CreateSection(doc, sectionType.Id, sectionBox);

                if (section == null)
                {
                    TaskDialog.Show("Ошибка", $"Не удалось создать разрез для стены {wall.Id}");
                    return null;
                }

                // Задаем имя разреза
                try
                {
                    // Проверяем уникальность имени
                    string baseName = wallMark;
                    string finalName = baseName;
                    int counter = 1;

                    while (ViewNameExists(doc, finalName))
                    {
                        finalName = $"{baseName}_{counter}";
                        counter++;
                    }

                    section.Name = finalName;
                }
                catch
                {
                    // Если не удалось задать имя, оставляем системное
                }

                // Настраиваем параметры вида
                try
                {
                    // Отключаем отображение эталонных осей
                    section.AreImportCategoriesHidden = true;

                    // Настраиваем масштаб (например, 1:50)
                    Parameter scaleParam = section.get_Parameter(BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC);
                    if (scaleParam != null)
                    {
                        // 1:50 = 50
                        scaleParam.Set(50);
                    }
                }
                catch
                {
                    // Игнорируем ошибки настройки
                }

                return section;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось создать разрез для стены {wall.Id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Проверяет существование вида с таким именем
        /// </summary>
        private bool ViewNameExists(Document doc, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Создает BoundingBox для разреза параллельно стене
        /// </summary>
        private BoundingBoxXYZ GetSectionViewParallelToWall(Wall wall)
        {
            LocationCurve lc = wall.Location as LocationCurve;
            if (lc == null) return null;

            Curve curve = lc.Curve;

            XYZ p = curve.GetEndPoint(0);
            XYZ q = curve.GetEndPoint(1);
            XYZ v = q - p;

            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            double minZ = bb.Min.Z;
            double maxZ = bb.Max.Z;

            double w = v.GetLength(); // длина стены
            double h = maxZ - minZ; // высота стены
            double d = wall.WallType.Width; // толщина стены

            double offset = d * 2; // смещение разреза (2 толщины стены)

            // Размеры бокса для разреза
            XYZ min = new XYZ(-w * 0.5 - 1.0, -1.0, -offset);
            XYZ max = new XYZ(w * 0.5 + 1.0, h + 1.0, offset);

            XYZ midpoint = p + 0.5 * v;
            XYZ walldir = v.Normalize();
            XYZ up = XYZ.BasisZ;
            XYZ viewdir = walldir.CrossProduct(up);

            Transform t = Transform.Identity;
            t.Origin = midpoint;
            t.BasisX = walldir;
            t.BasisY = up;
            t.BasisZ = viewdir;

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
            sectionBox.Transform = t;
            sectionBox.Min = min;
            sectionBox.Max = max;

            return sectionBox;
        }

        /// <summary>
        /// Создает лист и размещает на нем разрезы
        /// </summary>
        private ViewSheet CreateSheetWithSections(Document doc, List<ViewSection> sections)
        {
            try
            {
                // Получаем любой доступный штамп (семейство Title Block)
                FamilySymbol titleBlock = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();

                if (titleBlock == null)
                {
                    TaskDialog.Show("Ошибка", "Не найден штамп (Title Block) в проекте.");
                    return null;
                }

                // Создаем новый лист
                ViewSheet sheet = ViewSheet.Create(doc, titleBlock.Id);

                if (sheet == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось создать лист.");
                    return null;
                }

                // Задаем имя и номер листа
                string sheetNumber = GenerateSheetNumber(doc);
                string sheetName = $"Разрезы витражных стен ({DateTime.Now:dd.MM.yyyy})";

                try
                {
                    sheet.SheetNumber = sheetNumber;
                    sheet.Name = sheetName;
                }
                catch
                {
                    // Если не удалось задать имя, оставляем системное
                }

                // Размещаем разрезы на листе
                ArrangeViewsOnSheet(doc, sheet, sections);

                return sheet;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось создать лист: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Генерирует номер для листа
        /// </summary>
        private string GenerateSheetNumber(Document doc)
        {
            // Получаем все существующие листы
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            var existingSheets = collector
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Select(s => s.SheetNumber)
                .ToList();

            // Ищем номер, начиная с ЛВВ-01
            int counter = 1;
            string proposedNumber;

            do
            {
                proposedNumber = $"ЛВВ-{counter.ToString("D2")}";
                counter++;
            }
            while (existingSheets.Contains(proposedNumber) && counter < 100);

            return proposedNumber;
        }

        /// <summary>
        /// Размещает виды на листе в сетке
        /// </summary>
        private void ArrangeViewsOnSheet(Document doc, ViewSheet sheet, List<ViewSection> views)
        {
            if (views.Count == 0) return;

            // Параметры размещения
            double margin = 20.0 / 304.8; // 20 мм в футах (1 фут = 304.8 мм)
            double spacing = 10.0 / 304.8; // 10 мм между видами

            // Получаем размеры листа из штампа
            double sheetWidth = 840.0 / 304.8; // A1 в мм, переводим в футы (ширина)
            double sheetHeight = 594.0 / 304.8; // A1 в мм (высота)

            // Размеры для каждого вида (примерно A4)
            double viewWidth = 297.0 / 304.8; // A4 ширина
            double viewHeight = 210.0 / 304.8; // A4 высота

            // Рассчитываем количество колонок в сетке
            int columns = Math.Min(3, Math.Max(1, (int)Math.Floor((sheetWidth - 2 * margin) / (viewWidth + spacing))));
            int rows = (int)Math.Ceiling((double)views.Count / columns);

            // Центрируем сетку на листе
            double totalGridWidth = columns * viewWidth + (columns - 1) * spacing;
            double totalGridHeight = rows * viewHeight + (rows - 1) * spacing;

            double startX = (sheetWidth - totalGridWidth) / 2;
            double startY = sheetHeight - margin - viewHeight; // Начинаем сверху

            // Размещаем каждый вид
            for (int i = 0; i < views.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;

                double x = startX + col * (viewWidth + spacing);
                double y = startY - row * (viewHeight + spacing);

                // Создаем Viewport
                Viewport viewport = Viewport.Create(doc, sheet.Id, views[i].Id, new XYZ(x, y, 0));

                if (viewport != null)
                {
                    // Настраиваем масштаб видового экрана
                    try
                    {
                        Parameter scaleParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_SCALE);
                        if (scaleParam != null)
                        {
                            scaleParam.Set(50); // Масштаб 1:50
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки настройки масштаба
                    }
                }
            }
        }
    }
}