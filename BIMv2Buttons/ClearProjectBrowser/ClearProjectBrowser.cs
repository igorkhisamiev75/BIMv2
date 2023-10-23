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
    class ClearProjectBrowser : IExternalCommand
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

            //IEnumerable<View> views = new FilteredElementCollector(_doc).
            //    OfClass(typeof(View))
            //    .WhereElementIsNotElementType()
            //    .Cast<View>();

            List<ElementId> viewsForDel = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Select(y => y.Id).ToList();

            viewsForDel.Remove(notForDeleetedId);

            #region OldCode

            //list draftingView
            List<ElementId> L = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Where(x => x.ViewType == ViewType.DraftingView).
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L2 = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Where(x => x.ViewType == ViewType.Schedule).
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L3 = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Where(x => x.ViewType == ViewType.FloorPlan).
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L4 = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Where(x => x.ViewType == ViewType.CeilingPlan).
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L5 = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Where(x => x.ViewType == ViewType.Elevation).
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L6 = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Where(x => x.ViewType == ViewType.Legend).
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L7 = new FilteredElementCollector(_doc).
                OfClass(typeof(ViewSheet)).ToElements().Cast<ViewSheet>().
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L8 = new FilteredElementCollector(_doc).
                OfClass(typeof(View)).ToElements().Cast<View>().
                Where(x => x.ViewType == ViewType.Section).
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L9 = new FilteredElementCollector(_doc).
                OfClass(typeof(ImageView)).ToElements().Cast<ImageView>().
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L10 = new FilteredElementCollector(_doc).
                OfClass(typeof(ViewSection)).ToElements().Cast<ViewSection>().
                Select(y => y.Id).ToList();

            //list View
            List<ElementId> L11 = new FilteredElementCollector(_doc).
                OfClass(typeof(ViewPlan)).ToElements().Cast<ViewPlan>().
                Select(y => y.Id).ToList();

            // get list of all ViewPlan
            //FilteredElementCollector collector = new FilteredElementCollector(_doc);
            //FilteredElementIterator itor = collector.OfClass(typeof(View)).GetElementIterator();
            #endregion
            using (Transaction t = new Transaction(_doc))
            {
                t.Start("Deleting Views");

                foreach(var n in viewsForDel)
                {
                    try
                    {
                        //_doc.Delete(n);
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
    }
}

