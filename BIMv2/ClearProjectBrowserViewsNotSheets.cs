using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class ClearProjectBrowserViewsNotSheets : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            //active view
            var notForDeleetedId = _doc.ActiveView.Id;

            ViewSet m_allViews = new ViewSet();

            List<ElementId> viewsForDelWithoutSheet = new List<ElementId>();

            List<ElementId> viewsForDel2 = new List<ElementId>();


            List<ElementId> viewsForDel = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Select(y => y.Id).ToList();

            //get List without sheet
            foreach (var elementId in viewsForDel)
            {
                ElementType objType = _doc.GetElement(elementId) as ElementType;
                if (!objType.Name.Equals("Drawing Sheet"))
                {
                    viewsForDelWithoutSheet.Add(elementId);
                }
            }

            foreach (var elementId in viewsForDelWithoutSheet)
            {
                Element element = _doc.GetElement(elementId) as Element;

                if (!element.GetParameters("Sheet Name").IsReadOnly)
                {
                    viewsForDel2.Add(elementId);
                }

            }



            viewsForDel2.Remove(notForDeleetedId);

           
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



                t.Commit();

            }


            return Result.Succeeded;


        }
    }
}

