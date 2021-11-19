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
    class CreateViewForLevels : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            // get list of all levels
            IList<Level> levels = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.Elevation).ToList();

            // get a ViewFamilyType for a 3D View
            ViewFamilyType viewFamilyType =
                (from v in new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    where v.ViewFamily == ViewFamily.ThreeDimensional
                    select v).First();

            using (Transaction t = new Transaction(_doc, "Create view"))
            {
                int ctr = 0;
                // loop through all levels
                foreach (Level level in levels)
                {
                    t.Start();

                    // Create the 3d view
                    View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                    // Set the name of the view
                    view.Name = level.Name + " 3D вид";

                    // Set the name of the transaction
                    // A transaction can be renamed after it has been started
                    t.SetName("Create view " + view.Name);

                    // Create a new BoundingBoxXYZ to define a 3D rectangular space
                    BoundingBoxXYZ boundingBoxXYZ = new BoundingBoxXYZ();

                    // Set the lower left bottom corner of the box
                    // Use the Z of the current level.
                    // X & Y values have been hardcoded based on this RVT geometry
                    boundingBoxXYZ.Min = new XYZ(-50, -100, level.Elevation);

                    // Determine the height of the bounding box
                    double zOffset = 0;
                    // If there is another level above this one, use the elevation of that level
                    if (levels.Count > ctr + 1)
                        zOffset = levels.ElementAt(ctr + 1).Elevation;
                    // If this is the top level, use an offset of 10 feet
                    else
                        zOffset = level.Elevation + 10;
                    boundingBoxXYZ.Max = new XYZ(200, 125, zOffset);

                    // Apply this bouding box to the view's section box

                    view.SetSectionBox(boundingBoxXYZ);
                    //view.CropBox = boundingBoxXYZ;

                    t.Commit();

                    // Open the just-created view
                    // There cannot be an open transaction when the active view is set
                    uiDoc.ActiveView = view;

                    ctr++;

                }

                return Result.Succeeded;

            }
        }
    }
}

