#region Namespace
using System;
using System.Collections.Generic;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateRoomNumber : IExternalCommand

    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            IList<Element> rooms = GetAllRooms(doc);
            IList<Element> rooms2=new List<Element>();

            foreach (var f in rooms)
            {
                if (f.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT).AsString() == "Квартиры")
                {
                    rooms2.Add(f);
                }
            }

            using (var t = new Transaction(doc))
            {
                t.Start("CreateRoomNumber");


                try
                {
                  
                    foreach (var room in rooms2)
                    {
                        var str1=room.LookupParameter("ADSK_Номер квартиры").AsString();
                        var str2 = room.LookupParameter("ADSK_Номер секции").AsString();
                        var str3 = room.LookupParameter("Уровень").AsValueString();
                        string[] words = str3.Split(' ');
                        var str4 = words[1];

                        room.LookupParameter("ADSK_Индекс квартиры").Set(str1+"-"+ str2+ "-" + str4);


                    }

                    t.Commit();
                    return Result.Succeeded;

                }



                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", ex.Message);
                    return Result.Failed;
                }

            }

        }

        public IList<Element> GetAllRooms(Document doc)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

    }
}