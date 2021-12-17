using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class CreateNumberRoomForFloors : IExternalCommand
    {
        public List<Room> projectRooms = new List<Room>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            
            // List all Floor's
            var floor_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();
           
            IList<Element> allFloors = floor_collector.ToElements();

            //список линкованных файлов
            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances(doc); 
           
            if (linkedInstances != null)
            {
               
                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                    var transform = linkInstance.GetTotalTransform();
                    
                    var roomsLink = GetAllRoomsLinked(linkDocument);

                    if (roomsLink != null)
                    {
                        TaskDialog.Show("Rooms", roomsLink.Count.ToString());

                    }
                        

                    //if (roomsLink != null)
                    //{
                    //    foreach (var room in roomsLink)
                    //    {
                    //    }
                    //}
                }
            }

            //IList<string> numFinishList = new List<string>();

                            //группировка по отделке
                            //var roomsGroups = allFloors.GroupBy(p => p.LookupParameter("Тип отделки пола").AsValueString());

                            //using (Transaction worksetTransaction = new Transaction(doc, "Set preview view id"))
                            //{
                            //    worksetTransaction.Start();
                            //    //пробегаемся по сгруппированным значениям

                            //    foreach (IGrouping<string, Element> element in roomsGroups)
                            //    {
                            //        IList<string> numroomList = new List<string>();
                            //        numroomList.Clear();

                            //        //создаем массив с номерами помещений
                            //        foreach (var t in element)
                            //        {
                            //            numroomList.Add(t.LookupParameter("Номер").AsString());
                            //        }

                            //        string numbersAllRoomString = string.Join(", ", numroomList.ToArray());

                            //        numroomList.Clear();


                            //        foreach (var t in element)
                            //        {

                            //            t.LookupParameter("ADSK_Номера помещений для пола").Set(numbersAllRoomString);
                            //        }

                            //    }

                            //    worksetTransaction.Commit();
                            //}


            return Result.Succeeded;


        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances(Document doc)
        {
            var collector = new FilteredElementCollector(doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        public List<Room> GetAllRoomsLinked(Document document)
        {
            FilteredElementCollector roomCollect = new FilteredElementCollector(document);
            roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
            Room room = null;
            projectRooms = new List<Room>();

            foreach (Element elem in roomCollect)
            {
                room = elem as Room;
                if (room != null)
                {
                    projectRooms.Add(room);
                }
            }

            return projectRooms;
        }

    }
}
