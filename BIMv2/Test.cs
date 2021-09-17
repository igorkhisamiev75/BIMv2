#region Namespaces
using System;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using OperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;
#endregion

namespace BIMv2
{
    #region LightPickFilter
    public class LightPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element e)
        {
            return e.Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_LightingFixtures);
        }

        public bool AllowReference(Reference r, XYZ p)
        {
            return false;
        }
    }
    #endregion // LightPickFilter

    [Transaction(TransactionMode.Manual)]
    public class Test : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message,ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Selection selection = uidoc.Selection;

                // Pick a light fixture.

                var pickedLightReference = selection.PickObject(
                  ObjectType.Element, new LightPickFilter(),
                  "Please select lighting fixture to place");

                if (null == pickedLightReference)
                {
                    return Result.Failed;
                }

                // Get Family Instance of the selected light reference.

                FamilyInstance lightFamilyInstance= doc.GetElement(pickedLightReference) as FamilyInstance;

                // Get FamilySymbol of the family instance.

                if (lightFamilyInstance == null)
                {
                    return Result.Failed;
                }

                FamilySymbol lightFamilySymbol= lightFamilyInstance.Symbol;

                // Determine this family's placement type.
                // This is an important step towards determining
                // which NewFamilyInstance overload to use to
                // place new instances of it.

                FamilyPlacementType placementType  = lightFamilySymbol.Family.FamilyPlacementType;

                // Placement type is WorkPlaneBased, so determine
                // the host face that defines the work plane.

                Reference hostFace = lightFamilyInstance.HostFace;

                // Prompt for placement point of copy.

                XYZ placeXyzPoint = selection.PickPoint(
                  "Select point to place new light:");

                // The location point gives Z elevation value.

                LocationPoint lp = lightFamilyInstance.Location as LocationPoint;

                // Assuming the ceiling is horizontal, set
                // the location point Z value for the copy
                // equal to the original.

                placeXyzPoint = new XYZ(placeXyzPoint.X, placeXyzPoint.Y, lp.Point.Z);

                using (var trans = new Transaction(doc))
                {
                    trans.Start("LightArray");

                    FamilyInstance lightFamilyInstance2
                      = doc.Create.NewFamilyInstance(hostFace, placeXyzPoint, XYZ.BasisX,
                        lightFamilySymbol);

                    trans.Commit();
                }
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
            return Result.Succeeded;
        }
    }
}