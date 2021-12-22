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
    [Regeneration(RegenerationOption.Manual)]
    public class GroupTypeCeilingAndWriteToComment : IExternalCommand
    {
        public List<Element> projectRooms = new List<Element>();
        public List<Element> AllRooms = new List<Element>();
        public List<Ceiling> AllF = new List<Ceiling>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);
                TaskDialog.Show("Сделано", "Сгруппированы перекрытия");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            var allFloors = GetAllFloorElements(doc);

            foreach (var VARIABLE in allFloors)
            {
                AllF.Add((Ceiling)VARIABLE);

            }

            //группировка по типу пола
            var groups = AllF.GroupBy(p => p.Name);

            //пробегаемся по сгруппированным значениям

            foreach (IGrouping<string, Element> element in groups)
            {
                IList<string> numroomList = new List<string>();
                numroomList.Clear();

                //создаем массив с номерами помещений
                foreach (var t in element)
                {
                    var otd = t.LookupParameter("Номер помещения (отделка)").AsString();
                    string[] subs = otd.Split(' ');
                    numroomList.Add(subs[0]);
                }

                //clean list
                List<string> newList = new List<string>(numroomList.Distinct());

                newList.Sort();

                string numbersAllRoomString = string.Join(", ", newList.ToArray());

                numroomList.Clear();

                //и записываем это массив в каждый номер этого массива

                foreach (var t in element)
                {

                    t.LookupParameter("Комментарии").Set(numbersAllRoomString);
                }

            }

        }

        public IList<Element> GetAllFloorElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType();
            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

    }
}
