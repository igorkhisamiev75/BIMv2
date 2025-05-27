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

                //TaskDialog.Show("Revit", $"Колличество связей  {linkedInstances.Count()}");


                //помещения в модели
                IList<Element> roomsCurrentProject = GetAllRooms(doc);

                int i = 0;
                int j = 0;


                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {
                    try
                    {
                        if (linkedInstance != null && linkedInstance.Name == "27_PD_AR_ALL_MRGT_R21_SHD.rvt : 12 : позиция <Не общедоступное>")
                        {

                            Document linkDocument = linkedInstance.GetLinkDocument();

                            //собрать помещения из связанной АР

                            IList<Element> roomsLinkedProject = GetAllRooms(linkDocument);

                            TaskDialog.Show("Revit", $"Колличество помещений в текущей {roomsCurrentProject.Count}");
                            TaskDialog.Show("Revit", $"Колличество помещений в текущей {roomsLinkedProject.Count}");

                            foreach (Room room in roomsLinkedProject)
                            {
                                string nameRoomLink = room.Name;
                                string namberRoomLink = room.Number;

                                string nameRoomCurent = "";
                                string namberRoomCurrent = "";


                                foreach (Room roomCurent in roomsCurrentProject)
                                {
                                    nameRoomCurent = room.Name;
                                    namberRoomCurrent = room.Number;

                                    if (namberRoomLink == namberRoomCurrent && nameRoomLink == nameRoomCurent)
                                    {
                                        i++;
                                        nameRoomLink = "";
                                        namberRoomLink = "";
                                        nameRoomCurent = "";
                                        namberRoomCurrent = "";
                                        continue;
                                    }
                                    else
                                    {
                                        j++;


                                    }

                                }



                            }

                            TaskDialog.Show("Revit", $"Совпадающие {i} имена и номера");
                            TaskDialog.Show("Revit", $"Не совпадающие {j} имена и номера");


                        }
                    }
                    catch
                    {

                    }
                }



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