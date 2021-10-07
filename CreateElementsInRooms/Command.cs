using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using Autodesk.Revit.Attributes;



namespace CreateElementsInRooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TransactionGroup transactionGroup = new TransactionGroup(commandData.Application.ActiveUIDocument.Document);
            try
            {
                //Create a transaction
                transactionGroup.Start("Create elements");

                // create a new instance of class RoomsData
                RoomsData data = new RoomsData(commandData);

                // create a form to display the information of rooms
                roomsInformationForm infoForm = new roomsInformationForm(data, commandData);
                infoForm.ShowDialog();
                transactionGroup.Commit();
                return Result.Succeeded;
            }

            catch (Exception ex)
            {
                // If there are something wrong, give error information and return failed
                transactionGroup.RollBack();
                message = ex.Message;
                return Result.Failed;
            }

            

        }
    }
    
}
