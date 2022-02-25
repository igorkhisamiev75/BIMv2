using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GroupTypeFloorByNote : IExternalCommand
    {
        public List<Floor> AllF = new List<Floor>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);

                TaskDialog.Show("Сделано", "Полы сгруппированы");

                t.Commit();

            }

            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            IList<Element> allFloors = GetAllFloorElements(doc);
            AllF.Clear();

            foreach (Element VARIABLE in allFloors)
            {
                try
                {
                    var ggg = (Floor)VARIABLE;
                    AllF.Add(ggg);
                }
                catch (Exception e)
                {
                    continue;
                }
                
            }

            //group by level
            IEnumerable<IGrouping<string, Floor>> groups = AllF.GroupBy(p2 =>
                p2.LookupParameter("Level").AsValueString());

            foreach (IGrouping<string, Floor> group in groups)
            {
                List<Floor> floorGroups = new List<Floor>();
                floorGroups.Clear();

                foreach (Floor elWall in group)
                {
                    floorGroups.Add(elWall);
                }

                //группировка по типу 
                IEnumerable<IGrouping<string, Floor>> groupsN = floorGroups.GroupBy(p => p.Name);

                //пробегаемся по сгруппированным значениям

                foreach (IGrouping<string, Element> element in groupsN)
                {
                    IList<string> numroomList = new List<string>();
                    numroomList.Clear();

                    //создаем массив с номерами помещений
                    foreach (Element t in element)
                    {
                        string otd = t.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();
                        if (otd != null)
                        {
                            //string[] subs = otd.Split(' ');
                            numroomList.Add(otd);
                        }

                    }

                    //clean list
                    List<string> newList = new List<string>(numroomList.Distinct());

                    newList.Sort();

                    string numbersAllRoomString = string.Join(", ", newList.ToArray());

                    numroomList.Clear();

                    //и записываем это массив в каждый номер этого массива

                    foreach (Element t in element)
                    {
                        t.get_Parameter(new Guid("6b89127c-f4ec-4724-b120-8d4b5cdcdcb6")).Set(numbersAllRoomString);
                    }

                }

            }
        }

        public IList<Element> GetAllFloorElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();
            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

    }
}
