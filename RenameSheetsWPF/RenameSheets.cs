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
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
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
                if(sheet.LookupParameter("Занесение в список листов").AsValueString() == "Нет"||
                    sheet.Name == "Без имени")
                {
                    continue;
                }
               

                SheetInProject sheetInProject = new SheetInProject();

                sheetInProject.idSheet = sheet.Id;

                sheetInProject.numberSheet = sheet.SheetNumber;
                sheetInProject.nameSheet = sheet.Name;
                sheetInProject.albom = sheet.LookupParameter("MRGT_Альбом/Том").AsString();
                sheetInProject.stadiy = sheet.LookupParameter("MRGT_Стадия").AsString();

                sheetInProject.id = sheet.Id;

                if (sheet.LookupParameter("MRGT_Альбом/Том").HasValue == false)
                {
                    sheetInProject.albom = "???";
                }

                if (sheet.LookupParameter("MRGT_Стадия").HasValue == false)
                {
                    sheetInProject.stadiy = "???";
                }

                //stadiya.Add(sheet.LookupParameter("MRGT_Стадия").AsString());
                //albom.Add(sheet.LookupParameter("MRGT_Альбом/Том").AsString());
                //numberSheets.Add(sheet.SheetNumber);
                //nameSheets.Add(sheet.Name);

                sheetInProjectCollection.Add(sheetInProject);


                //nameN += sheet.LookupParameter("MRGT_Стадия").AsString() + "_" +
                //        sheet.LookupParameter("MRGT_Альбом/Том").AsString() + "_"
                //        + sheet.SheetNumber + "_"
                //        + sheet.Name + "\n";

            }

            BrowserOrganization bOrg = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(_doc);

            IEnumerable<SheetInProject> newList = sheetInProjectCollection
             .OrderBy(x => x.stadiy)
             .ThenBy(x => x.albom)
             .ThenBy(x=>bOrg.GetFolderItems(x.idSheet).First().Name)
             /*.ThenBy(x => x.numberSheet)*/
             .ThenBy(x => x.nameSheet);



            ViewWPF vpViewWpf = new ViewWPF(newList);

            vpViewWpf.ShowDialog();

            //var selShed = vpViewWpf.selectedSheets;

            //TaskDialog.Show("Нажат ли checbox", $"{vpViewWpf.cbStart.IsChecked.Value}");

            var cb = vpViewWpf.cbStart.IsChecked.Value;

            List<SheetInProject> checkFilters = vpViewWpf.checkSheets;
            string tbPrefiks = vpViewWpf.tbName.Text;

            string tbFirstNumber = vpViewWpf.tbStartNumber.Text;

            int firstNumber = Convert.ToInt32(tbFirstNumber);

            //TaskDialog.Show("Nwe", $"{checkFilters.Count}");

            using (var t = new Transaction(_doc))
            {
                t.Start("Переименование листов");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                if (checkFilters.Count != 0 && cb == false)
                {
                    foreach (var sheet in checkFilters)
                    {
                        var el = _doc.GetElement(sheet.idSheet);

                        ViewSheet sheet1 = el as ViewSheet;

                        if (el != null)
                        {

                            string sN = sheet1.SheetNumber;

                            sheet1.SheetNumber = tbPrefiks + sN;
                        }



                    }
                }

                if (checkFilters.Count != 0 && cb == true)
                {
                    foreach (var sheet in checkFilters)
                    {
                        var el = _doc.GetElement(sheet.idSheet);

                        ViewSheet sheet1 = el as ViewSheet;

                        if (el != null)
                        {

                            string sN = sheet1.SheetNumber;

                            sheet1.SheetNumber = firstNumber.ToString();

                            firstNumber++;


                        }



                    }
                    _doc.Regenerate();
                }

                else
                {
                    //System.Diagnostics.Process.Start(
                    //"https://boosty.to/mo3deling");
                }
                

                _doc.Regenerate();
                t.Commit();

            }

            
            return Result.Succeeded;
        }

        

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

       

    }



}

