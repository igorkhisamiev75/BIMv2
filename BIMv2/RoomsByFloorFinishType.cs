using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class RoomsByFloorFinishType : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            IList<string> numFinishList = new List<string>();

            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            //IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            IList<Element> allRooms = room_collector.ToElements();

            var roomsGroups = allRooms.GroupBy(p => p.LookupParameter("Тип отделки пола").AsValueString());


            using (Transaction worksetTransaction = new Transaction(doc, "Set preview view id"))
            {
                worksetTransaction.Start();
                //пробегаемся по сгруппированным значениям

                foreach (IGrouping<string, Element> element in roomsGroups)
                {
                    IList<string> numroomList = new List<string>();

                    //создаем массив с номерами помещений
                    foreach (var t in element)
                    {
                        numroomList.Add(t.LookupParameter("Номер").AsString());
                    }

                    string numbersAllRoom = string.Join(", ", numroomList.ToArray());

                    //TaskDialog.Show("Отделка", numbersAllRoom);

                    //и записываем это массив в каждый номер этого массива

                    foreach (var t in element)
                    {
                        t.LookupParameter("Комментарии").Set(numbersAllRoom);
                    }


                }

                worksetTransaction.Commit();
            }

                

            IEnumerable<IGrouping<string, Element>> roomsGroup=from room in allRooms
                group room by room.LookupParameter("Тип отделки пола").AsValueString();



            foreach (IGrouping<string, Element> element in roomsGroups)
            {
                //TaskDialog.Show("Отделка", element.Key);

                foreach (var t in element)
                {
                    //TaskDialog.Show("Отделка", t.LookupParameter("Номер").AsString());
                }
            }

            foreach (var room in allRooms)
            {
                var g = room.LookupParameter("Тип отделки пола").AsValueString(); //get finishFloor
                numFinishList.Add(g);
                //TaskDialog.Show("Отделка", g);
            }

            numFinishList = numFinishList.GroupBy(x => x.GetHashCode()).Select(group =>
                group.OrderBy(x => x.GetHashCode()).First()).ToList();

            //TaskDialog.Show("Вариантов отделки", numFinishList.Count.ToString());




            return Result.Succeeded;
        }
    }
}
