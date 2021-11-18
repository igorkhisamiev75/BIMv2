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
        /// <summary>
        /// The angle in the XY plane (azimuth),
        /// typically 0 to 360.
        /// </summary>
        double angleHorizD;

        /// <summary>
        /// The vertical tilt (altitude),
        /// typically -90 to 90.
        /// </summary>
        double angleVertD;

        public XYZ VectorFromHorizVertAngles(double angleHorizD, double angleVertD)
        {
            // Convert degreess to radians.

            double degToRadian = Math.PI * 2 / 360;
            double angleHorizR = angleHorizD * degToRadian;
            double angleVertR = angleVertD * degToRadian;

            // Return unit vector in 3D

            double a = Math.Cos(angleVertR);
            double b = Math.Cos(angleHorizR);
            double c = Math.Sin(angleHorizR);
            double d = Math.Sin(angleVertR);

            return new XYZ(a * b, a * c, d);
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            XYZ eye = XYZ.Zero;

            XYZ forward = VectorFromHorizVertAngles(angleHorizD, angleVertD);

            XYZ up = VectorFromHorizVertAngles(angleHorizD, angleVertD + 90);

            ViewOrientation3D viewOrientation3D = new ViewOrientation3D(eye, up, forward);

            FilteredElementCollector collector = new FilteredElementCollector(_doc);

            collector.OfClass(typeof(ViewPlan));

            List<ViewPlan> ViewPlans = collector.Cast<ViewPlan>().ToList();

            using (var t = new Transaction(_doc))
            {
                t.Start("Create");
                ViewFamilyType viewFamilyType3D = new FilteredElementCollector(_doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault<ViewFamilyType>(
                            x => ViewFamily.ThreeDimensional
                                 == x.ViewFamily);

                View3D view3d = View3D.CreateIsometric(_doc, viewFamilyType3D.Id);

                //view3d.SetOrientation(viewOrientation3D);

                BoundingBoxXYZ b = ViewPlans[0].get_BoundingBox(null);

                ViewPlans[0].CropBox

                view3d.SetSectionBox(b);

                TaskDialog.Show("Complite!", "Созданы 3D виды");
                t.Commit();

            }

            return Result.Succeeded;
        }
    }
}
