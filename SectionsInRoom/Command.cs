using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace SectionsInRoom
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand

    {
        //get point for room
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Selection selection = uidoc.Selection;

                // Pick a light fixture.

                Reference pickedRoomReference = selection.PickObject(
                    ObjectType.Element, new RoomPickFilter(),
                    "Please select room");

                if (null == pickedRoomReference)
                {
                    return Result.Failed;
                }

                var g = pickedRoomReference.ElementId.ToString();

                TaskDialog.Show("Re", $"{g}");


            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Cancelled;
            }

            return Result.Succeeded;

        }


        //get room
        //get boundary segment
        //crate sections 
        //create new sheet and add section 
    }

    public class RoomPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element e)
        {
            return e.Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_Rooms);
        }

        public bool AllowReference(Reference r, XYZ p)
        {
            return false;
        }
    }
}
