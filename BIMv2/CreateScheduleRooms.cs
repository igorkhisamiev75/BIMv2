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
        BuiltInParameter[] BiParams = new BuiltInParameter[] { BuiltInParameter.ROOM_NAME,
            BuiltInParameter.ROOM_NUMBER, BuiltInParameter.ROOM_AREA, BuiltInParameter.ROOM_DEPARTMENT};

        // built in parameters / fields to add in schedule
        BuiltInParameter[] BiParams2 = new BuiltInParameter[] { BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM,
            BuiltInParameter.WALL_BASE_CONSTRAINT, BuiltInParameter.HOST_AREA_COMPUTED, BuiltInParameter.CURVE_ELEM_LENGTH};

        



        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet)
        {
            // ui document
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            // the current document
            Document doc = uidoc.Document;

            //SharedParameterElement shElem = doc.GetElement(Field.ParameterId) as SharedParameterElement;

           

            // transaction
            Transaction t = new Transaction(doc, "Create Schedule");
            t.Start();

            // get built in room category id
            ElementId roomCategoryId = new ElementId(BuiltInCategory.OST_Rooms);
            ViewSchedule schedule = ViewSchedule.CreateSchedule(doc, roomCategoryId);

            schedule.Name = "Room Schedule Sample";

            var field = schedule.Definition.GetSchedulableFields()
                .FirstOrDefault(x => IsSharedParameterSchedulableField(schedule.Document, x.ParameterId, new Guid("78e3b89c-eb68-4600-84a7-c523de162743")));
                //.FirstOrDefault(x => x.ParameterId == destParameterId);

            if (field != null)
                schedule.Definition.AddField(field);

            ////family and type group sorting
            //ScheduleSortGroupField FamilyTypeSorting = null;

            //// base contraint filter
            //ScheduleFilter BaseConstraintFilter = null;

            //// iterate schedulablefileds
            //foreach (SchedulableField sf in schedule.Definition.GetSchedulableFields())
            //{
            //    // check field if in params
            //    if (CheckField(sf))
            //    {
            //        // add field
            //        ScheduleField scheduleField = schedule.Definition.AddField(sf);

            //        // schedule's filter (to collect base constraint)
            //        if (sf.ParameterId == new ElementId(BuiltInParameter.ROOM_NAME))
            //        {

            //            // create schedulefilter by level
            //            BaseConstraintFilter = new ScheduleFilter(scheduleField.FieldId, ScheduleFilterType.Equal,
            //                GetLevelByName(doc, "Этаж 02").Id);
            //            // add schedule's filter
            //            schedule.Definition.AddFilter(BaseConstraintFilter);
            //        }

            //        // schedule's group sorting (to collect family and type field)
            //        if (sf.ParameterId == new ElementId(BuiltInParameter.ROOM_AREA))
            //        {

            //            // create group sorting (family and type)
            //            FamilyTypeSorting = new ScheduleSortGroupField(scheduleField.FieldId);
            //            // add schedule's group sorting field
            //            schedule.Definition.AddSortGroupField(FamilyTypeSorting);
            //        }

            //    }
            //}
            //// set filter
            //schedule.Definition.SetFilter(0, BaseConstraintFilter);
            //// set sorting/grouping      
            //schedule.Definition.SetSortGroupField(0, FamilyTypeSorting);

            // close transaction
            t.Commit();
            // set active view 
            uidoc.ActiveView = schedule;

            //result
            return Result.Succeeded;
        }

        public bool CheckField(SchedulableField vs)
        {
            foreach (BuiltInParameter bip in BiParams)
            {
                if (new ElementId(bip) == vs.ParameterId)
                    return true;
            }
            return false;
        }

        public Element GetLevelByName(Document doc, string name)
        {
            Level level = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>().Where(x => x.Name == name).FirstOrDefault();

            return level;
        }

        private static bool IsSharedParameterSchedulableField(Document document, ElementId parameterId, Guid sharedParameterId)
        {
            var sharedParameterElement = document.GetElement(parameterId) as SharedParameterElement;

            return sharedParameterElement?.GuidValue == sharedParameterId;
        }
    }
}
