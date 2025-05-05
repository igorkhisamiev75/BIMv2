using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;
using System.Diagnostics;
using System.Windows.Controls;

namespace LintelsOnTheFloor2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LintelsOnTheFloor2 : Window, IExternalCommand
    {
        private Application _app;
        private Document _doc;

        //Levels
        public List<Level> levelCollection = new List<Level>();
        public ICollection<Element> lintelsCollection = new List<Element>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            //уровни
            levelCollection = GetAllLevels();
            //обобщенные модели
            lintelsCollection = GetGenericModel();


            MainWindow vpViewWpf = new MainWindow(levelCollection, lintelsCollection);

            vpViewWpf.ShowDialog();

            //var checkFilters = vpViewWpf.checkFilters;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                //del filters checked
                //ViewFiltersDelete(_doc, checkFilters);

                TaskDialog.Show("here", "here");
                t.Commit();

            }

            return Result.Succeeded;
        }

        //get all levels
        public List<Level> GetAllLevels()
        {
            // Search all level
            FilteredElementCollector filteredLevelCollector = new FilteredElementCollector(_doc);
            filteredLevelCollector.OfClass(typeof(Level));
            List<Level> levelCollection = filteredLevelCollector.Cast<Level>().ToList<Level>();

            return levelCollection;
        }

        public ICollection<Element> GetGenericModel()
        {
            // Создаем фильтр для элементов категории "Обобщенные модели"
            BuiltInCategory genericModelCategory = BuiltInCategory.OST_GenericModel;
            ElementCategoryFilter categoryFilter = new ElementCategoryFilter(genericModelCategory);

            // Используем фильтр для получения всех обобщенных моделей в проекте
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            IList<Element> genericModels = collector.WherePasses(categoryFilter).WhereElementIsNotElementType().ToElements();

            return genericModels;
        }

    }
}
