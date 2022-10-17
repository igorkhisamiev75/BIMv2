using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateScheduleRooms : IExternalCommand
    {
        // built in parameters / fields to add in schedule
        BuiltInParameter[] BiParams = new BuiltInParameter[] { BuiltInParameter.ROOM_NAME, BuiltInParameter.ROOM_NUMBER };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet)
        {
            // ui document
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            // the current document
            Document doc = uidoc.Document;

            //SharedParameterElement shElem = doc.GetElement(Field.ParameterId) as SharedParameterElement;

            string adskNumberSection = "b59a3474-a5f4-430a-b087-a20f1a4eb57e";
            string adskTypeRoom = "78e3b89c-eb68-4600-84a7-c523de162743";
            string adskSqueRoomLiving = "178e222b-903b-48f5-8bfc-b624cd67d13c";
            string adskSqueRoom = "d3035d0f-b738-4407-a0e5-30787b92fa49";
            string adskSqueRoomOur = "af973552-3d15-48e3-aad8-121fe0dda34e";
            string lsrSque = "467593ec-2ec6-497a-8711-95622d6da67f";
            string adskNumberRoom = "10fb72de-237e-4b9c-915b-8849b8907695";



            // transaction
            Transaction t = new Transaction(doc, "Create Schedule");
            t.Start();

            // get built in room category id
            ElementId roomCategoryId = new ElementId(BuiltInCategory.OST_Rooms);
            ViewSchedule schedule = ViewSchedule.CreateSchedule(doc, roomCategoryId);

            schedule.Name = "TEST_Room Schedule Sample11";

            SchedulableField field = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid(adskNumberSection)));
            //.FirstOrDefault(x => x.ParameterId == destParameterId);
            SchedulableField field2 = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid(adskTypeRoom)));
            SchedulableField field3 = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid(adskSqueRoomLiving)));
            SchedulableField field4 = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid(adskSqueRoom)));
            SchedulableField field5 = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid(adskSqueRoomOur)));
            SchedulableField field6 = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid(lsrSque)));

            //get level
            SchedulableField field7 = schedule.Definition.GetSchedulableFields()
            .Where(x => x.FieldType == ScheduleFieldType.Instance || x.FieldType == ScheduleFieldType.ElementType)
            .First(x => x.ParameterId == new ElementId(BuiltInParameter.ROOM_LEVEL_ID));

            //get name
            SchedulableField field8 = schedule.Definition.GetSchedulableFields()
            .Where(x => x.FieldType == ScheduleFieldType.Instance || x.FieldType == ScheduleFieldType.ElementType)
            .First(x => x.ParameterId == new ElementId(BuiltInParameter.ROOM_NAME));

            SchedulableField field9 = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid(adskNumberRoom)));



            //if (field != null)
            //    schedule.Definition.GetSchedulableFields();
            schedule.Definition.AddField(field);
            schedule.Definition.AddField(field2);
            schedule.Definition.AddField(field3);
            schedule.Definition.AddField(field4);
            schedule.Definition.AddField(field5);
            schedule.Definition.AddField(field6);
            schedule.Definition.AddField(field7);
            schedule.Definition.AddField(field8);
            schedule.Definition.AddField(field9);

            schedule.Definition.GetField(8).IsHidden = true;

            

            //add count room
            schedule.Definition.AddField(ScheduleFieldType.Count);

            schedule.Definition.GetFieldCount();

            //размеры столбиков
            //фильтровать
            //собирать все уровни и блоки

            //add filter
            //schedule.Definition.AddFilter(new ScheduleFilter(schedule.Id, ScheduleFilterType.Equal, "your value"));

            

            // base contraint filter
            ScheduleFilter filter1 = null;

            ScheduleFieldId scheduleField = schedule.Definition.GetFieldId(0);

            //create schedulefilter 
            filter1 = new ScheduleFilter(scheduleField, ScheduleFilterType.Equal, "1");

            schedule.Definition.GetField(0).IsHidden = true;


            // add schedule's filter
            schedule.Definition.AddFilter(filter1);

            // base contraint filter
            ScheduleFilter filter2 = null;

            ScheduleFieldId scheduleField2 = schedule.Definition.GetFieldId(7);
            schedule.Definition.GetField(7).IsHidden = true;

            


            //create schedulefilter 
            filter2 = new ScheduleFilter(scheduleField2, ScheduleFilterType.Equal, "Прихожая");

            // add schedule's filter
            schedule.Definition.AddFilter(filter2);

            // filter имя уровня
            ScheduleFilter filter3 = null;

            ElementId levelId = GetLevelIdByName(doc, "Этаж 02");

            ScheduleFieldId scheduleField3 = schedule.Definition.GetFieldId(6);
            schedule.Definition.GetField(6).IsHidden = true;
            //create schedulefilter 
            filter3 = new ScheduleFilter(scheduleField3, ScheduleFilterType.Equal, levelId);

            // add schedule's filter
            schedule.Definition.AddFilter(filter3);

            //group sorting
            ScheduleSortGroupField scheduleSortGroupField = null;

            //create group sorting(family and type)
            scheduleSortGroupField = new ScheduleSortGroupField(schedule.Definition.GetFieldId(1));
            // add schedule's group sorting field
            schedule.Definition.AddSortGroupField(scheduleSortGroupField);

            //family and type group sorting
            ScheduleSortGroupField scheduleSortGroupField2 = null;
            //create group sorting(family and type)
            scheduleSortGroupField2 = new ScheduleSortGroupField(schedule.Definition.GetFieldId(8));
            // add schedule's group sorting field
            schedule.Definition.AddSortGroupField(scheduleSortGroupField2);

            schedule.Definition.GetField(1).ColumnHeading = "Тип квартиры";
            schedule.Definition.GetField(2).ColumnHeading = "Жилая площадь квартиры";
            schedule.Definition.GetField(3).ColumnHeading = "Площадь квартиры";
            schedule.Definition.GetField(4).ColumnHeading = "Общая площадь квартиры(с балконами с коэф.)";
            schedule.Definition.GetField(5).ColumnHeading = "Общая площадь квартиры(с балконами без коэф.)";
            schedule.Definition.GetField(9).ColumnHeading = "Кол-во";




            // close transaction
            t.Commit();
            // set active view 
            uidoc.ActiveView = schedule;

            //result
            return Result.Succeeded;
        }

        public ElementId GetLevelIdByName(Document doc, string name)
        {
            Level level = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>().Where(x => x.Name == name).FirstOrDefault();

            ElementId levelLevelId = level.Id;

            return levelLevelId;
        }

        //public bool CheckField(SchedulableField vs)
        //{
        //    foreach (BuiltInParameter bip in BiParams)
        //    {
        //        if (new ElementId(bip) == vs.ParameterId)
        //            return true;
        //    }
        //    return false;
        //}

        //public Element GetLevelByName(Document doc, string name)
        //{
        //    Level level = new FilteredElementCollector(doc).OfClass(typeof(Level))
        //        .Cast<Level>().Where(x => x.Name == name).FirstOrDefault();

        //    return level;
        //}

        private static bool IsSharedParameterSchedulableField(Document document, ElementId parameterId, Guid sharedParameterId)
        {
            var sharedParameterElement = document.GetElement(parameterId) as SharedParameterElement;

            return sharedParameterElement?.GuidValue == sharedParameterId;
        }

        public static ScheduleField FindField(ViewSchedule schedule, BuiltInParameter paramEnum)
        {
            ScheduleDefinition definition = schedule.Definition;
            ScheduleField foundField = null;
            ElementId paramId = new ElementId(paramEnum);

            foreach (ScheduleFieldId fieldId in definition.GetFieldOrder())
            {
                foundField = definition.GetField(fieldId);
                if (foundField.ParameterId == paramId)
                {
                    return foundField;
                }
            }

            return null;
        }
    }
}
