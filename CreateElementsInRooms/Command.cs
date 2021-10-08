using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;



namespace CreateElementsInRooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class Command : IExternalCommand
    {
        public virtual Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                //TransactionGroup transactionGroup = new TransactionGroup(commandData.Application.ActiveUIDocument.Document);
                //Create a transaction
                //transactionGroup.Start("Create elements");

                // create a new instance of class RoomsData
                RoomsData data = new RoomsData(commandData);

                // create a form to display the information of rooms
                roomsInformationForm infoForm = new roomsInformationForm(data, commandData);
                infoForm.ShowDialog();

                var s = infoForm.checListRoom;

                //TaskDialog.Show("выбраны", $"{s}");

                //transactionGroup.Commit();
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
