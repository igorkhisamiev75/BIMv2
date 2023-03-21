using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace DublicateScheetsVPF
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class DublicateScheetsVPF : Window, IExternalCommand
    {
        private Application _app;
        private Document _doc;
        private ViewSet m_allViews = new ViewSet();

        public ICollection<ViewSheet> scheetsInProject = new List<ViewSheet>();

        public ICollection<Element> filtersCollection = new List<Element>();

        public ICollection<Element> filtersCollectionCopy = new List<Element>();

        public ICollection<View> onlyViewPlan3DSection = new List<View>();

        public ICollection<int> filtersInViewsId = new List<int>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            //filtersCollection = GetAllFilters();

            ICollection<ViewSheet> allView = GetAllSheets(_doc);

            foreach (var item in allView)
            {
                if (item!=null)
                {
                    filtersCollectionCopy.Add(item);
                }
            }



            ViewWPF vpViewWpf = new ViewWPF(filtersCollectionCopy);

            vpViewWpf.ShowDialog();

            var checkFilters = vpViewWpf.checkFilters; //выбраны виды

            using (var t = new Transaction(_doc))
            {
                t.Start("Dublicate Sheets 2bim.ru");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                //дописать сколько раз скопировать раз лист
                //добавить возможность копирования видов
                //переключение между видами на WPF SHEETS/VIEW

                CreateCopySheets(_doc, checkFilters);

                TaskDialog.Show("2BIM.RU", $"Sheet with view, scheduls and legend copy done!");

                t.Commit();

            }

            return Result.Succeeded;
        }

        public void CreateCopySheets(Document doc, List<Element> checkFilters)
        {

            foreach (Element el in checkFilters)
            {
                if (el != null)
                {
                    // Check the current active view
                    View selView = doc.ActiveView;
                    ViewSheet vSheet = doc.ActiveView as ViewSheet;

                    //Retrieve titleblock from current sheet and all elements in view
                    FamilyInstance titleblock = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance))
                                    .OfCategory(BuiltInCategory.OST_TitleBlocks).Cast<FamilyInstance>()
                                    .FirstOrDefault(q => q.OwnerViewId == vSheet.Id);


                    //FamilyInstance titleblock = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance))
                    //               .OfCategory(BuiltInCategory.OST_TitleBlocks).Cast<FamilyInstance>()
                    //               .FirstOrDefault();

                    //var titleblock = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance))
                    //               .OfCategory(BuiltInCategory.OST_TitleBlocks).Cast<FamilyInstance>()
                    //               . GetEnumerator(q => q.Name == "нет");

                    var elementsInViewId = new FilteredElementCollector(doc, selView.Id).ToElementIds();

                    // Retrieve viewports in view
                    FilteredElementCollector viewPorts = new FilteredElementCollector(doc, selView.Id)
                        .OfClass(typeof(Viewport));

                    // Retrieve schedules in project
                    FilteredElementCollector schedules = new FilteredElementCollector(doc)
                        .OwnedByView(selView.Id).OfClass(typeof(ScheduleSheetInstance));

                    // Retrieve viewSchedules все спецухи
                    FilteredElementCollector viewSchedules = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSchedule));

                    // Store copied elements and annotation elements
                    var copiedElementIds = new List<ElementId>();
                    var annotationElementsId = new List<ElementId>();

                    using (Transaction t = new Transaction(doc, "Duplicate Sheet"))
                    {
                        // Start transaction to duplicate sheet
                        t.Start();

                        // Duplicate sheet
                        ViewSheet newsheet = ViewSheet.Create(doc, titleblock.GetTypeId());

                        newsheet.SheetNumber = vSheet.SheetNumber + "-COPY2";
                        newsheet.Name = vSheet.Name;

                        // Get origin of the titleblock
                        XYZ originTitle = titleblock.GetTransform().Origin;

                        // Check titleblock position
                        Element copyTitleBlock = new FilteredElementCollector(doc)
                            .OwnedByView(newsheet.Id)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElement();

                        LocationPoint titleLoc = copyTitleBlock.Location as LocationPoint;
                        XYZ titleLocPoint = titleLoc.Point;

                        // Check if title block is in the same position as original
                        if (titleLocPoint.DistanceTo(originTitle) != 0)
                        {
                            // Move it in case it is not
                            titleLoc.Move(originTitle);
                        }

                        // Retrieve all views placed on sheet except schedules
                        foreach (ElementId eId in vSheet.GetAllPlacedViews())
                        {
                            View origView = doc.GetElement(eId) as View;
                            View newView = null;

                            // Legends
                            if (origView.ViewType == ViewType.Legend)
                            {
                                newView = origView;
                            }
                            // Rest of view types
                            else
                            {
                                if (origView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                                {
                                    ElementId newViewId = origView.Duplicate(ViewDuplicateOption.WithDetailing);
                                    newView = doc.GetElement(newViewId) as View;
                                    newView.Name = origView.Name + "-COPY";
                                }
                            }


                            // Loop through viewports
                            foreach (Viewport vp in viewPorts)
                            {
                                if (vp.SheetId == vSheet.Id && vp.ViewId == origView.Id)
                                {
                                    // Retrieve centerpoint of original viewport
                                    XYZ center = vp.GetBoxCenter();
                                    // Create viewport in the original spot
                                    Viewport newVp = Viewport.Create(doc, newsheet.Id, newView.Id, center);
                                }
                                // Add element in copied list
                                copiedElementIds.Add(vp.Id);
                            }
                            // Add element in copied list
                            copiedElementIds.Add(eId);
                        }

                        // Retrieve and copy schedules
                        foreach (ScheduleSheetInstance sch in schedules) //те что на виде
                        {
                            // Check schedule is not a revision inside titleblock
                            if (!sch.IsTitleblockRevisionSchedule)
                            {
                                foreach (ViewSchedule vsc in viewSchedules)
                                {
                                    if (sch.ScheduleId == vsc.Id)
                                    {
                                        // Retrieve center of schedule
                                        XYZ schCenter = sch.Point;

                                        // Create schedule in the same position
                                        ScheduleSheetInstance newSch = ScheduleSheetInstance
                                            .Create(doc, newsheet.Id, vsc.Id, schCenter);

                                        copiedElementIds.Add(sch.Id);
                                    }
                                    //copiedElementIds.Add(vsc.Id);
                                    //doc.Regenerate();
                                }
                            }
                        }


                        // Duplicate annotation elements
                        foreach (ElementId eId in elementsInViewId)
                        {
                            if (!copiedElementIds.Contains(eId))
                            {
                                annotationElementsId.Add(eId);
                            }
                        }

                        // Copy annotation elements
                        ElementTransformUtils.CopyElements(selView, annotationElementsId, newsheet, null, null);


                        // Use ElementClassFilter to find family instances whose name is 60" x 30" Student 
                        ElementClassFilter filter = new ElementClassFilter(typeof(FamilyInstance));


                        var n = newsheet.GetDependentElements(filter);

                        //doc.Regenerate();

                        doc.Delete(n[0]);


                        // Commit transaction
                       // t.Commit();

                    }
                }
            }

         }

   

        //get all sheets
        private ICollection<ViewSheet> GetAllSheets(Document doc)
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
                    if (null == objType /*|| objType.Name.Equals("Schedule")*/
                                        || objType.Name.Equals("Drawing Sheet"))
                    {
                        continue;
                    }
                    else
                    {
                        m_allViews.Insert(view);
                        scheetsInProject.Add(view);
                    }
                }
            }

            return scheetsInProject;
        }
    }
}
