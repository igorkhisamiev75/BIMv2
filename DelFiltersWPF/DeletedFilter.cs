using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;


namespace DelFiltersWPF
{
    [Transaction(TransactionMode.Manual)]
    public class DeletedFilter : Window, IExternalCommand
    {
        private Application _app;
        private Document _doc;
        private ViewSet m_allViews = new ViewSet();

        public ICollection<Element> filtersCollection = new List<Element>();

        //public DeletedFilter(ExternalCommandData commandData)
        //{
        //    GetAllFilters();
        //}

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            filtersCollection=GetAllFilters();

            //FilteredElementCollector collector = new FilteredElementCollector(_doc);
            //FilteredElementIterator itor = collector.OfClass(typeof(FilterElement)).GetElementIterator();
            //itor.Reset();

            //while (itor.MoveNext())
            //{
            //    Get the object
            //   FilterElement imp = itor.Current as FilterElement;

            //    filtersCollection.Add(imp);
            //}

            var allView = GetAllViews(_doc);

            ViewWPF vpViewWpf = new ViewWPF(filtersCollection);

            vpViewWpf.ShowDialog();

            var checkFilters=vpViewWpf.checkFilters;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;
                
                ViewFiltersDelete(_doc, checkFilters);
                t.Commit();

            }


            return Result.Succeeded;
        }


        public void ViewFiltersDelete(Document doc, List<Element> checkFilters)
        {
            ArrayList oViewFiltersToDelete = new ArrayList();

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            FilteredElementIterator itor = collector.OfClass(typeof(FilterElement)).GetElementIterator();
            itor.Reset();

            // Iterate through each object found
            while (itor.MoveNext())
            {
                // Get the object
                FilterElement imp = itor.Current as FilterElement;

                foreach (var VARIABLE in checkFilters)
                {
                    if (imp.Name == VARIABLE.Name)
                    {
                        oViewFiltersToDelete.Add(imp.Id);
                    }
                    
                }
                
            }

            foreach (ElementId oElemID in oViewFiltersToDelete)
            {
                try
                {
                    doc.Delete(oElemID);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        public ICollection<Element> GetAllFilters()
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            FilteredElementIterator itor = collector.OfClass(typeof(FilterElement)).GetElementIterator();
            itor.Reset();

            while (itor.MoveNext())
            {
                // Get the object
                FilterElement imp = itor.Current as FilterElement;

                filtersCollection.Add(imp);
            }

            return filtersCollection;
        }

        //get all view
        private ViewSet GetAllViews(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            FilteredElementIterator itor = collector.OfClass(typeof(View)).GetElementIterator();
            itor.Reset();
            while (itor.MoveNext())
            {
                View view = itor.Current as View;
                // skip view templates because they're invisible in project browser
                if (null == view || view.IsTemplate)
                {
                    continue;
                }
                else
                {
                    ElementType objType = doc.GetElement(view.GetTypeId()) as ElementType;
                    if (null == objType || objType.Name.Equals("Schedule")
                                        || objType.Name.Equals("Drawing Sheet"))
                    {
                        continue;
                    }
                    else
                    {
                        m_allViews.Insert(view);
                        //AssortViews(view.Name, objType.Name);
                    }
                }
            }

            return m_allViews;
        }

    }

}

