#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;


#endregion

namespace BIMv2.CheckRooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CheckRooms2 : Window, IExternalCommand

    {
        private Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            Document doc = commandData.Application.ActiveUIDocument.Document;

            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

            //create form 
            CheckRoomsSelectLink vpViewWpf = new CheckRoomsSelectLink(linkedInstances);

            //show form
            vpViewWpf.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            vpViewWpf.ShowDialog();

            RevitLinkInstance checkLink = vpViewWpf.LinkInstance;


            using (var t = new Transaction(doc))
            {
                t.Start("Проверка помещений");

                //помещения в модели
                IList<Element> roomsCurrentProject = GetAllRooms(doc);
                TaskDialog.Show("Revit", $"Колличество помещений в текущей {roomsCurrentProject.Count}");
                int i = 0;
                int j = 0;


                try
                {
                    if (checkLink != null)
                    {

                        Document linkDocument = checkLink.GetLinkDocument();

                        //собрать помещения из связанной АР

                        IList<Element> roomsLinkedProject = GetAllRooms(linkDocument);


                        TaskDialog.Show("Revit", $"Колличество помещений в связанной {roomsLinkedProject.Count}");

                        int raz = roomsCurrentProject.Count() - roomsLinkedProject.Count();

                        TaskDialog.Show("Revit", $"Расхождения по помещениям =  {raz} проверьте параметр BIM_Error в помещении должно быть написано Верно");

                        //foreach (Room room in roomsLinkedProject)
                        //{
                        //    string nameRoomLink = room.Name;
                        //    string namberRoomLink = room.Number;
                        //    string squereRoomLink = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsValueString();

                        //    string nameRoomCurent = "";
                        //    string namberRoomCurrent = "";
                        //    string squereRoomCurrent = "";


                        //    foreach (Room roomCurent in roomsCurrentProject)
                        //    {
                        //        nameRoomCurent = roomCurent.Name;
                        //        namberRoomCurrent = roomCurent.Number;
                        //        squereRoomCurrent = roomCurent.get_Parameter(BuiltInParameter.ROOM_AREA).AsValueString();

                        //        if (namberRoomLink == namberRoomCurrent && nameRoomLink == nameRoomCurent && squereRoomLink == squereRoomCurrent)
                        //        {
                        //            i++;
                        //            roomCurent.get_Parameter(new Guid("6feb836e-de15-4412-817e-f147e44fe0ea")).Set("Верно");

                        //            //nameRoomLink = "";
                        //            //namberRoomLink = "";
                        //            //nameRoomCurent = "";
                        //            //namberRoomCurrent = "";
                        //            continue;
                        //        }
                        //        else
                        //        {
                        //            j++;


                        //        }

                        //    }



                        //}





                    }
                }

                catch
                {

                }
                
                //TaskDialog.Show("Revit", $"Совпадающие {i} имена и номера");
                //TaskDialog.Show("Revit", $"Не совпадающие {j} имена и номера");


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