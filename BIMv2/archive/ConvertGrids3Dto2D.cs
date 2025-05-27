using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Application = Autodesk.Revit.ApplicationServices.Application;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class ConvertGrids3Dto2D : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;
                try
                {
                    ConvertTo2D(_doc, uiDoc);
                    TaskDialog.Show("Готово", "Оси переведены в 2D/3D");
                }
                catch
                {
                    TaskDialog.Show("Не сработало", "Вообще программирование это как в лесу на Аляске искать дорогу домой и спрашивать дорогу у глухонемого алеута");
                }
                
                t.Commit();

            }


            return Result.Succeeded;
        }


        public void ConvertTo2D(Document doc, UIDocument uiDoc)
        {
            IList<Element> grids = GetAllGridsElements(doc);

            if (grids.Count != 0)
            {
                Grid grid = (Grid)grids[0];

                if ((grid.GetDatumExtentTypeInView(DatumEnds.End0, doc.ActiveView) == DatumExtentType.Model) ||
                    (grid.GetDatumExtentTypeInView(DatumEnds.End1, doc.ActiveView) == DatumExtentType.Model))
                {
                    foreach (Element VARIABLE in grids)
                    {
                        Grid grid2 = VARIABLE as Grid;

                        grid2.SetDatumExtentType(DatumEnds.End0, doc.ActiveView, DatumExtentType.ViewSpecific);
                        grid2.SetDatumExtentType(DatumEnds.End1, doc.ActiveView, DatumExtentType.ViewSpecific);

                    }
                }

                else
                {
                    foreach (var VARIABLE in grids)
                    {
                        Grid grid2 = VARIABLE as Grid;

                        grid2.SetDatumExtentType(DatumEnds.End0, doc.ActiveView, DatumExtentType.Model);
                        grid2.SetDatumExtentType(DatumEnds.End1, doc.ActiveView, DatumExtentType.Model);
                    }
                }
            }
            


         }

        

        public IList<Element> GetAllGridsElements(Document document)
        {
            // List all Grids's
            var grid_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Grids)
                .WhereElementIsNotElementType();

            IList<Element> allGrid = grid_collector.ToElements();
            return allGrid;
        }
    }

}
