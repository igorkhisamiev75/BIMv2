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
    public class LG_RoomsInformation : IExternalCommand

    {
       
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            

            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Проверка помещений");


                //помещения в модели
                IList<Element> roomsCurrentProject = GetAllRooms(doc);

                foreach (var room in roomsCurrentProject) {

                    //LG_room	
                    room.LookupParameter("LG_room").Set("LG_room-продумать скрипт");

                    //LG_Имя помещения
                    room.LookupParameter("LG_Имя помещения").Set(room.LookupParameter("Имя").AsString());

                    //LG_Площадь помещения
                    room.LookupParameter("LG_Площадь помещения").Set(room.LookupParameter("Площадь").AsDouble());

                    //LG_Жилая площадь
                    room.LookupParameter("LG_Жилая площадь").Set("LG_Жилая площадь-продумать скрипт");

                    //LG_Тип квартиры
                    room.LookupParameter("LG_Тип квартиры").Set("LG_Тип квартиры-продумать скрипт");

                    //LG_Количество жилых комнат
                    room.LookupParameter("LG_Количество жилых комнат").Set("LG_Количество жилых комнат-продумать скрипт");

                    //LG_Количество спален
                    room.LookupParameter("LG_Количество спален").Set("LG_Количество спален-продумать скрипт");

                    //LG_Высота потолка
                    room.LookupParameter("LG_Высота потолка").Set(room.LookupParameter("Полная высота").AsValueString());

                    //LG_Общая площадь квартиры
                    room.LookupParameter("LG_Общая площадь квартиры").Set("LG_Общая площадь квартиры-продумать скрипт");

                    //LG_Общая приведенная площадь квартиры
                    room.LookupParameter("LG_Общая приведенная площадь квартиры").Set("LG_Общая приведенная площадь квартиры-продумать скрипт");



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