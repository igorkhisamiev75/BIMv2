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
    class CreateViewByLinkedFiles : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            ViewSet m_allViews = new ViewSet();

            


            // get a ViewFamilyType for a 3D View
            ViewFamilyType viewFamilyType =
                (from v in new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                 where v.ViewFamily == ViewFamily.ThreeDimensional
                 select v).First();

           

                using (Transaction t = new Transaction(_doc, "Create view "))
            {
                var linkedInstances = FindLinkedInstances();
              

                // loop through all levels
                foreach (var linkFile in linkedInstances)
                {
                    t.Start();

                    try
                    {
                        // Create the 3d view
                        View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                        // Set the name of the view
                        view.Name = "3D View" + linkFile.Name;

                        // Set the name of the transaction
                        // A transaction can be renamed after it has been started
                        t.SetName("Create view " + view.Name);


                    }

                    catch
                    {

                    }
                   

                    

                    t.Commit();


                }

                return Result.Succeeded;

            }
        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }
    }
}

