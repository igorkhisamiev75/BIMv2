using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;


namespace RenameSheetsWPF
{
    [Transaction(TransactionMode.Manual)]
    //[Regeneration(RegenerationOption.Manual)]
    //[Journaling(JournalingMode.NoCommandData)]
    public class RenameSheets : Window, IExternalCommand
    {
        public Application _app;
        public Document _doc;
        //private ViewSet m_allViews = new ViewSet();

        //public ICollection<View> viewsInProject = new List<View>();

        public ICollection<string> stadiya = new List<string>();
        public ICollection<string> albom = new List<string>();
        public ICollection<string> numberSheets = new List<string>();
        public ICollection<string> nameSheets = new List<string>();

        //public ICollection<Element> filtersCollectionCopy = new List<Element>();

        //public ICollection<Element> unusedFiltersCollection = new List<Element>();

        public ICollection<SheetInProject> sheetInProjectCollection = new List<SheetInProject>();

        //public ICollection<int> filtersInViewsId = new List<int>();

        public ICollection<ViewSheet> viewSheets = new List<ViewSheet>();

        

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            var allViewSheets = GetAllSheets(_doc);

            //string nameN = "";

            

            foreach (var sheet in allViewSheets)
            {
                SheetInProject sheetInProject = new SheetInProject();

                sheetInProject.idSheet = sheet.Id;
                sheetInProject.numberSheet = sheet.SheetNumber;
                sheetInProject.nameSheet = sheet.Name;
                sheetInProject.albom = sheet.LookupParameter("MRGT_Альбом/Том").AsString();
                sheetInProject.stadiy = sheet.LookupParameter("MRGT_Стадия").AsString();

                stadiya.Add(sheet.LookupParameter("MRGT_Стадия").AsString());
                albom.Add(sheet.LookupParameter("MRGT_Альбом/Том").AsString());
                numberSheets.Add(sheet.SheetNumber);
                nameSheets.Add(sheet.Name);

                sheetInProjectCollection.Add(sheetInProject);


                //nameN += sheet.LookupParameter("MRGT_Стадия").AsString() + "_" +
                //        sheet.LookupParameter("MRGT_Альбом/Том").AsString() + "_"
                //        + sheet.SheetNumber + "_"
                //        + sheet.Name + "\n";

            }

            


            ViewWPF vpViewWpf = new ViewWPF(stadiya, albom, numberSheets, nameSheets, sheetInProjectCollection);

            vpViewWpf.ShowDialog();

            //var checkFilters = vpViewWpf.checkFilters;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                var allViewSheets2 = GetAllSheets(_doc);

                //foreach (var sheet in allViewSheets2)
                //{
                //    string sN = sheet.SheetNumber;

                //    sheet.SheetNumber = "-"+sN;
                //}

                //del filters checked
                //ViewFiltersDelete(_doc, checkFilters);
                t.Commit();

            }

            return Result.Succeeded;
        }

        //public void ViewFiltersDelete(Document doc, List<Element> checkFilters)
        //{
        //    ArrayList oViewFiltersToDelete = new ArrayList();

        //    FilteredElementCollector collector = new FilteredElementCollector(doc);
        //    FilteredElementIterator itor = collector.OfClass(typeof(FilterElement)).GetElementIterator();
        //    itor.Reset();

        //    // Iterate through each object found
        //    while (itor.MoveNext())
        //    {
        //        // Get the object
        //        FilterElement imp = itor.Current as FilterElement;

        //        foreach (var VARIABLE in checkFilters)
        //        {
        //            if (imp.Name == VARIABLE.Name)
        //            {
        //                oViewFiltersToDelete.Add(imp.Id);
        //            }

        //        }

        //    }

        //    foreach (ElementId oElemID in oViewFiltersToDelete)
        //    {
        //        try
        //        {
        //            doc.Delete(oElemID);
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show(ex.Message);
        //        }
        //    }
        //}

        //get all filters
        //public ICollection<Element> GetAllFilters()
        //{
        //    FilteredElementCollector collector = new FilteredElementCollector(_doc);
        //    FilteredElementIterator itor = collector.OfClass(typeof(FilterElement)).GetElementIterator();
        //    itor.Reset();

        //    while (itor.MoveNext())
        //    {
        //        // Get the object
        //        FilterElement imp = itor.Current as FilterElement;

        //        filtersCollection.Add(imp);
        //    }

        //    return filtersCollection;
        //}

        //get all view
        //private ICollection<View> GetAllViews(Document doc)
        //{
        //    FilteredElementCollector collector = new FilteredElementCollector(doc);
        //    FilteredElementIterator itor = collector.OfClass(typeof(View)).GetElementIterator();
        //    itor.Reset();
        //    while (itor.MoveNext())
        //    {
        //        View view = itor.Current as View;
        //        // skip view templates because they're invisible in project browser
        //        if (null == view || view.IsTemplate)
        //        {
        //            continue;
        //        }
        //        else
        //        {
        //            ElementType objType = doc.GetElement(view.GetTypeId()) as ElementType;
        //            if (null == objType || objType.Name.Equals("Schedule")
        //                                || objType.Name.Equals("Drawing Sheet"))
        //            {
        //                continue;
        //            }
        //            else
        //            {
        //                m_allViews.Insert(view);
        //                viewsInProject.Add(view);
        //            }
        //        }
        //    }

        //    return viewsInProject;
        //}

        public ICollection<ViewSheet> GetAllSheets(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            FilteredElementIterator itor = collector.OfClass(typeof(ViewSheet)).GetElementIterator();
            itor.Reset();
            while (itor.MoveNext())
            {
                ViewSheet view = itor.Current as ViewSheet;
                // skip view templates because they're invisible in project browser
                if (null == view || view.IsTemplate)
                {
                    continue;
                }
                else
                {
                    ElementType objType = doc.GetElement(view.GetTypeId()) as ElementType;
                    if (null != objType || objType.Name.Equals("Drawing Sheet"))
                    {
                        viewSheets.Add(view);
                        continue;
                    }
                    else
                    {
                        //m_allViews.Insert(view);
                        //viewsInProject.Add(view);
                    }
                }
            }

            return viewSheets;
        }

        public IEnumerable<ViewSheet> GetAllSheets2(Document doc)
        {

            IEnumerable<ViewSheet> sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .OfCategory(BuiltInCategory.OST_Sheets)
                .Cast<ViewSheet>()
                ;

            return sheets;
        }

        public class SheetInProject
        {
            public string stadiy { get; set; }
            public string albom { get; set; }
            public string numberSheet { get; set; }
            public string nameSheet { get; set; }
            public ElementId idSheet { get; set; }



        }

    }



}

