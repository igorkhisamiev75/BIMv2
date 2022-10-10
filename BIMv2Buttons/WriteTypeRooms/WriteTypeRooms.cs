#region Namespace

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using ExcelDataReader;



#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WriteTypeRooms : IExternalCommand

    {
        public List<string> RoomNameList = new List<string>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            //adsk_номер квартиры 10fb72de-237e-4b9c-915b-8849b8907695
            //adsk_тип квартиры 78e3b89c-eb68-4600-84a7-c523de162743

            using (var t = new Transaction(doc))
            {
                t.Start("Write Type Rooms");

                //get rooms
                var Rooms = GetAllRooms(doc);

                List<Room> rooms = new List<Room>();

                foreach (Element element in Rooms)
                {
                    try
                    {
                        var ggg = (Room)element;
                        //get square 
                        if ((ggg.get_Parameter(BuiltInParameter.ROOM_VOLUME)).AsDouble() > 0)
                        {
                            rooms.Add(ggg);
                        }
                        
                    }
                    catch (Exception e)
                    {
                        continue;
                    }

                }

                //group by number apartment
                IEnumerable<IGrouping<string, Room>> groupsRoom = rooms.GroupBy(p2 =>
                    p2.get_Parameter(new Guid("10fb72de-237e-4b9c-915b-8849b8907695")).AsString());
                
                //del not placedrooms
                
                try
                {
                    foreach (var variable in groupsRoom)
                    {

                        //посчитаем коэфициент квартир

                        double koefRoom = 0;

                        foreach (var room in variable)
                        {
                            if (room.get_Parameter((BuiltInParameter.ROOM_NAME)).AsString() == "Жилая комната")
                            {
                                koefRoom = koefRoom + 1;
                            }

                            if(room.get_Parameter((BuiltInParameter.ROOM_NAME)).AsString() == "Кухня-гостиная")
                            {
                                koefRoom = koefRoom + 0.5;
                            }
                            
                        }


                        //int i = variable.Count();

                        if (koefRoom == 1)
                        {
                            foreach (var room in variable)
                            {
                                room.get_Parameter(new Guid("78e3b89c-eb68-4600-84a7-c523de162743")).Set("СТ");
                            }
                            continue;

                        }

                        if (koefRoom == 1.5)
                        {
                            foreach (var room in variable)
                            {
                                room.get_Parameter(new Guid("78e3b89c-eb68-4600-84a7-c523de162743")).Set("1(1С)");
                            }
                            continue;

                        }

                        if (koefRoom == 2.5)
                        {
                            foreach (var room in variable)
                            {
                                room.get_Parameter(new Guid("78e3b89c-eb68-4600-84a7-c523de162743")).Set("2(2С)");
                            }
                            continue;

                        }

                        if (koefRoom == 3.5)
                        {
                            foreach (var room in variable)
                            {
                                room.get_Parameter(new Guid("78e3b89c-eb68-4600-84a7-c523de162743")).Set("3(3С)");
                            }
                            continue;

                        }

                        if (koefRoom == 4.5)
                        {
                            foreach (var room in variable)
                            {
                                room.get_Parameter(new Guid("78e3b89c-eb68-4600-84a7-c523de162743")).Set("4(4С)");
                            }
                        }

                        else
                        {
                            foreach (var room in variable)
                            {
                                if ((room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)).AsString() == "Квартиры")
                                {
                                    room.get_Parameter(new Guid("78e3b89c-eb68-4600-84a7-c523de162743")).Set("работай");
                                    continue;
                                }
                                continue;
                            }
                            
                        }
                        
                    }

                    TaskDialog.Show("Revit", $"Тип квартир записан для {groupsRoom.Count()} квартир");
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