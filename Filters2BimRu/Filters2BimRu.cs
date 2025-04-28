using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace Filters2BimRu
{
    [Transaction(TransactionMode.Manual)]
    //[Regeneration(RegenerationOption.Manual)]
    //[Journaling(JournalingMode.NoCommandData)]
    public class Filters2BimRu : Window, IExternalCommand

    {
        //private Application _app;
        private Document _doc;
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            //_app = uiApp.Application;
            _doc = uiDoc.Document;


            //ViewWPF vpViewWpf = new ViewWPF();

            //vpViewWpf.ShowDialog();

            

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;


                //get all elements on ActiveView
                var col = new FilteredElementCollector(_doc, _doc.ActiveView.Id).Cast<Element>().ToList();

                var filtersInViewsId2 = col.GroupBy(x => x.GetHashCode()).Select(group =>
                   group.OrderBy(x => x.GetHashCode()).First()).ToList();


                TaskDialog.Show("111", filtersInViewsId2.Count().ToString());

                //TaskDialog.Show("123", "123");

                //del filters checked
                //ViewFiltersDelete(_doc, (List<Element>)checkFilters);
                t.Commit();

            }

            return Result.Succeeded;
        }

        

       

    }


}

