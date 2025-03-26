using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class ClearDependencies : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            //получить все светильники

            var allElements = GetAllLightElements(_doc);
            //найти у них зависимости

            foreach (var element in allElements) { 
            
            }


            //пройтись по ним и удалить


            //active view
            var notForDeleetedId = _doc.ActiveView.Id;

            ViewSet m_allViews = new ViewSet();

            //IEnumerable<View> views = new FilteredElementCollector(_doc).
            //    OfClass(typeof(View))
            //    .WhereElementIsNotElementType()
            //    .Cast<View>();

            List<ElementId> viewsForDel = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Select(y => y.Id).ToList();

            viewsForDel.Remove(notForDeleetedId);

          
            using (Transaction t = new Transaction(_doc))
            {
                t.Start("Deleting Views");

                foreach (var n in viewsForDel)
                {
                    try
                    {
                        _doc.Delete(n);
                    }

                    catch
                    {
                        //TaskDialog.Show("Error", $"Тут трабл с{n}");
                    }
                }


                //_doc.Delete(L);
                //_doc.Delete(L2);
                //_doc.Delete(L3);
                //_doc.Delete(L4);
                //_doc.Delete(L5);
                //_doc.Delete(L6);
                //_doc.Delete(L7);
                //_doc.Delete(L8);
                //_doc.Delete(L9);
                //_doc.Delete(L10);
                //_doc.Delete(L11);


                t.Commit();

            }


            return Result.Succeeded;




        }

        public IList<Element> GetAllLightElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }
    }
}

