#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CopyRoomsFromLink : IExternalCommand

    {
        private Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            Document doc = commandData.Application.ActiveUIDocument.Document;
           
            using (var t = new Transaction(doc))
            {
                t.Start("Проверка помещений");

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();


                IList<Room> roomsLinkedProject = new List<Room>();

                //помещения в модели
                IList<Room> roomsCurrentProject = (IList<Room>)GetAllRooms(doc);

                int i = 0;
                int j = 0;
               

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {
                    try
                    {
                        if (linkedInstances != null && linkedInstance.Name.Contains("AR"))
                        {
                            foreach (var linkInstance in linkedInstances)
                            {
                                Document linkDocument = linkInstance.GetLinkDocument();

                                if (linkDocument.IsLinked == true)
                                {
                                    //собрать помещения из связанной АР

                                    roomsLinkedProject = (IList<Room>)GetAllRooms(linkDocument);
                                    
                                    foreach (Room room in roomsLinkedProject)
                                    {
                                        string nameRoomLink=room.Name;
                                        string namberRoomLink = room.Number;

                                        foreach (Room roomCurent in roomsCurrentProject)
                                        {
                                            string nameRoomCurent = room.Name;
                                            string namberRoomRoourent = room.Number;

                                            if (namberRoomLink == namberRoomRoourent && nameRoomLink== nameRoomCurent)
                                            {
                                                i++;
                                            }

                                            else
                                            {
                                                j++;
                                            }

                                        }

                                    }


                                }

                                else { break; }




                            }

                        }
                    }
                    catch
                    {

                    }
                }

                TaskDialog.Show("Revit", $"Колличество помещений в текущей {roomsCurrentProject.Count}");
                TaskDialog.Show("Revit", $"Колличество помещений в текущей {roomsLinkedProject.Count}");

                TaskDialog.Show("Revit", $"Совпадающие {i} имена и номера");
                TaskDialog.Show("Revit", $"Не совпадающие {j} имена и номера");

                try
                {
                   

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

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
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