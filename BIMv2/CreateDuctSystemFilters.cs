using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DuctSystemFilters
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateDuctSystemFilters : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

            try
            {
                // Собираем все уникальные имена систем воздуховодов
                HashSet<string> systemNames = GetUniqueDuctSystemNames(doc);

                if (systemNames.Count == 0)
                {
                    TaskDialog.Show("Информация", "Не найдено ни одной системы воздуховодов.");
                    return Result.Cancelled;
                }

                // Список категорий для фильтра
                List<BuiltInCategory> categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_DuctCurves,           // Воздуховоды
                    BuiltInCategory.OST_DuctFitting,          // Соединительные детали
                    BuiltInCategory.OST_DuctTerminal,         // Воздухораспределители
                    BuiltInCategory.OST_DuctAccessory         // Арматура воздуховодов
                };

                using (Transaction trans = new Transaction(doc, "Создание фильтров систем воздуховодов"))
                {
                    trans.Start();

                    foreach (string systemName in systemNames)
                    {
                        // Генерируем случайный цвет
                        Color randomColor = GenerateRandomColor();

                        // Создаем один фильтр для всех категорий
                        CreateFilterForCategories(
                            doc,
                            activeView,
                            systemName,
                            categories,
                            randomColor);
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Успех",
                    $"Создано фильтров: {systemNames.Count}\n" +
                    $"Найдено систем: {systemNames.Count}\n\n" +
                    $"Категории в фильтрах:\n" +
                    $"- Воздуховоды\n" +
                    $"- Соединительные детали\n" +
                    $"- Воздухораспределители\n" +
                    $"- Арматура воздуховодов\n\n" +
                    $"Каждый фильтр применяется ко всем категориям одновременно");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private HashSet<string> GetUniqueDuctSystemNames(Document doc)
        {
            HashSet<string> systemNames = new HashSet<string>();

            // Собираем воздуховоды
            FilteredElementCollector ductCollector = new FilteredElementCollector(doc);
            ductCollector.OfClass(typeof(Duct));

            foreach (Duct duct in ductCollector)
            {
                string systemName = duct.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM).AsString();
                if (!string.IsNullOrEmpty(systemName))
                {
                    systemNames.Add(systemName);
                }
            }

            // Собираем соединительные детали (фитинги)
            FilteredElementCollector fittingCollector = new FilteredElementCollector(doc);
            fittingCollector.OfClass(typeof(FamilyInstance));
            fittingCollector.WhereElementIsNotElementType();

            foreach (FamilyInstance fitting in fittingCollector)
            {
                if (fitting.Category != null &&
                    fitting.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting)
                {
                    string systemName = fitting.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM).AsString();
                    if (!string.IsNullOrEmpty(systemName))
                    {
                        systemNames.Add(systemName);
                    }
                }
            }

            // Собираем воздухораспределители
            FilteredElementCollector terminalCollector = new FilteredElementCollector(doc);
            terminalCollector.OfClass(typeof(FamilyInstance));
            terminalCollector.WhereElementIsNotElementType();

            foreach (FamilyInstance terminal in terminalCollector)
            {
                if (terminal.Category != null &&
                    terminal.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctTerminal)
                {
                    string systemName = terminal.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM).AsString();
                    if (!string.IsNullOrEmpty(systemName))
                    {
                        systemNames.Add(systemName);
                    }
                }
            }

            // Собираем арматуру воздуховодов
            FilteredElementCollector accessoryCollector = new FilteredElementCollector(doc);
            accessoryCollector.OfClass(typeof(FamilyInstance));
            accessoryCollector.WhereElementIsNotElementType();

            foreach (FamilyInstance accessory in accessoryCollector)
            {
                if (accessory.Category != null &&
                    accessory.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctAccessory)
                {
                    string systemName = accessory.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM).AsString();
                    if (!string.IsNullOrEmpty(systemName))
                    {
                        systemNames.Add(systemName);
                    }
                }
            }

            return systemNames;
        }

        private void CreateFilterForCategories(
            Document doc,
            View view,
            string systemName,
            List<BuiltInCategory> categories,
            Color color)
        {
            // Создаем имя фильтра
            string filterName = $"Система воздуховодов - {systemName}";

            // Проверяем, существует ли уже фильтр с таким именем
            ParameterFilterElement existingFilter = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => f.Name == filterName);

            ParameterFilterElement filter = null;

            if (existingFilter == null)
            {
                // Создаем правило для фильтрации по имени системы
                ParameterValueProvider provider = new ParameterValueProvider(
                    new ElementId(BuiltInParameter.RBS_SYSTEM_NAME_PARAM));

                FilterStringRuleEvaluator evaluator = new FilterStringEquals();
                FilterRule rule = new FilterStringRule(provider, evaluator, systemName);
                ElementParameterFilter paramFilter = new ElementParameterFilter(rule);

                // Создаем набор категорий (все категории в одном фильтре)
                List<ElementId> categoryIds = new List<ElementId>();
                foreach (BuiltInCategory category in categories)
                {
                    categoryIds.Add(new ElementId(category));
                }

                // Создаем фильтр для всех категорий одновременно
                filter = ParameterFilterElement.Create(doc, filterName, categoryIds);
                filter.SetElementFilter(paramFilter);
            }
            else
            {
                filter = existingFilter;
            }

            // Применяем фильтр к виду
            bool filterExists = view.GetFilters().Contains(filter.Id);
            if (!filterExists)
            {
                view.AddFilter(filter.Id);
            }

            // Настраиваем графику фильтра
            OverrideGraphicSettings graphicSettings = new OverrideGraphicSettings();

            // Устанавливаем сплошную заливку (solid fill)
            int solidFillId = GetSolidFillPatternId(doc);
            if (solidFillId != -1)
            {
                graphicSettings.SetSurfaceForegroundPatternColor(color);
                graphicSettings.SetSurfaceForegroundPatternId(new ElementId(solidFillId));
                graphicSettings.SetSurfaceForegroundPatternVisible(true);

                // Также устанавливаем фоновый паттерн для совместимости
                graphicSettings.SetSurfaceBackgroundPatternId(new ElementId(solidFillId));
                graphicSettings.SetSurfaceBackgroundPatternVisible(true);
            }

            // Устанавливаем цвет линий для контура
            graphicSettings.SetProjectionLineColor(color);

            // Применяем настройки к фильтру
            view.SetFilterOverrides(filter.Id, graphicSettings);

            // Включаем видимость фильтра
            view.SetFilterVisibility(filter.Id, true);
        }

        private int GetSolidFillPatternId(Document doc)
        {
            // Получаем ID паттерна сплошной заливки (Solid Fill)
            FillPatternElement solidFillPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fpe => fpe.GetFillPattern().IsSolidFill);

            if (solidFillPattern != null)
            {
                return solidFillPattern.Id.IntegerValue;
            }

            return -1;
        }

        private Color GenerateRandomColor()
        {
            Random random = new Random(Guid.NewGuid().GetHashCode());

            // Генерируем яркие цвета (избегаем слишком темных)
            byte red = (byte)random.Next(100, 255);
            byte green = (byte)random.Next(100, 255);
            byte blue = (byte)random.Next(100, 255);

            return new Color(red, green, blue);
        }
    }
}