using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
                //Create a transaction
                Transaction tran = new Transaction(commandData.Application.ActiveUIDocument.Document, "Rooms");
                tran.Start();
                // create a new instance of class RoomsData
                RoomsData data = new RoomsData(commandData);

               //DialogResult result;

                // create a form to display the information of rooms
                roomsInformationForm infoForm = new roomsInformationForm(data, commandData);
                
                    //result=
                    infoForm.ShowDialog();
                    tran.Commit();
                    return Result.Succeeded;

                //if (result == DialogResult.OK)
                //{
                //    tran.Commit();
                //    return Result.Succeeded;
                //}
                //else
                //{
                //    tran.RollBack();
                //    return Result.Cancelled;
                //}

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
