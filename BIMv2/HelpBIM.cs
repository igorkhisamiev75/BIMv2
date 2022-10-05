#region Namespace

using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HelpBIM : IExternalCommand

    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            Phase newConstructionPhase = doc.Phases.get_Item(1);

            using (var t = new Transaction(doc))
            {
                t.Start("Create Rooms");
               

                try
                {
                   
                    // create room using Phase
                    Room newScheduleRoom = doc.Create.NewRoom(newConstructionPhase);
                    // set the Room Number and Name
                    string newRoomNumber = "101";
                    string newRoomName = "Class Room 1";
                    newScheduleRoom.Name = newRoomName;
                    newScheduleRoom.Number = newRoomNumber;


                    TaskDialog.Show("Revit", "Id");
                    t.Commit();
                    //doc.Regenerate();
                    return Result.Succeeded;
                    
                }
                
            

                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", ex.Message);
                    return Result.Failed;
                }

            }
            
        }

       
    }
}