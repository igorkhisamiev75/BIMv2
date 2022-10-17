using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;


namespace CreateScheduleWPF
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreateScheduleWPF : Window, IExternalCommand
    {
        private Application _app;
        private Document _doc;
        private ViewSet m_allViews = new ViewSet();

        public ICollection<View> viewsInProject = new List<View>();

        public ICollection<Element> filtersCollection = new List<Element>();

        public ICollection<Element> filtersCollectionCopy = new List<Element>();

        public ICollection<Element> unusedFiltersCollection = new List<Element>();

        public ICollection<View> onlyViewPlan3DSection = new List<View>();

        public ICollection<int> filtersInViewsId = new List<int>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            filtersCollection = GetAllFilters();

            ICollection<View> allView = GetAllViews(_doc);

            foreach (var item in allView)
            {
                if (item.ViewType == ViewType.FloorPlan
                    || item.ViewType == ViewType.ThreeD
                    || item.ViewType == ViewType.Elevation
                    || item.ViewType == ViewType.Section)
                {
                    onlyViewPlan3DSection.Add(item);
                }
            }

            //get filters ID
            foreach (var view in onlyViewPlan3DSection)
            {
                var f = view.GetOrderedFilters();
                if (f != null)
                {
                    foreach (var item in f)
                    {
                        filtersInViewsId.Add(item.GetHashCode());
                    }
                }

            }

            filtersInViewsId = filtersInViewsId.GroupBy(x => x.GetHashCode()).Select(group =>
                group.OrderBy(x => x.GetHashCode()).First()).ToList();

            //clean filters list

            foreach (var VARIABLE in filtersCollection)
            {
                filtersCollectionCopy.Add(VARIABLE);
            }

            foreach (var filterAllfilters in filtersCollection)
            {

                var i = filterAllfilters.Id.GetHashCode();

                foreach (var element in filtersInViewsId)
                {
                    if (i.ToString() == element.ToString())
                    {
                        filtersCollectionCopy.Remove(filterAllfilters);
                    }

                }

            }


            ViewWPF vpViewWpf = new ViewWPF(filtersCollectionCopy);

            vpViewWpf.ShowDialog();

            var checkFilters = vpViewWpf.checkFilters;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                //del filters checked
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

        //get all filters
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
        private ICollection<View> GetAllViews(Document doc)
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
                        viewsInProject.Add(view);
                    }
                }
            }

            return viewsInProject;
        }

    }

}

