using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateElementsInRooms
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Journaling(Autodesk.Revit.Attributes.JournalingMode.NoCommandData)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {

                Transaction tran = new Transaction(commandData.Application.ActiveUIDocument.Document, "Rooms");
                tran.Start();
                // create a new instance of class data
                RoomsData data = new RoomsData(commandData);
                                
                // create a form to display the information of rooms
                using (roomsInformationForm infoForm = new roomsInformationForm(data, commandData))
                {
                    infoForm.ShowDialog();
                }

                tran.Commit();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // If there are something wrong, give error information and return failed
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
