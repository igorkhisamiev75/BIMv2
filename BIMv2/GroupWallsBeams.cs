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
    public class GroupWallsBeams : IExternalCommand
    {
        public List<Wall> AllF = new List<Wall>();
        public List<Wall> AllFinishWall = new List<Wall>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);

                TaskDialog.Show("Сделано", "Стены сгруппированы");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            var allWallElements = GetAllWallElements(doc);

            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Wall)element;

                    var descriptionWallType = wall.LookupParameter("ADSK_Позиция").AsString();
                    var roomName2 = wall.LookupParameter("ADSK_Примечание").AsString();

                    //var descriptionWallType = wall.LookupParameter("ADSK_Позиция").AsString();


                    if (descriptionWallType == "Балка" && roomName2 != null)
                    {
                        AllFinishWall.Add(wall);
                    }


                    //if (descriptionWallType == "Балка")
                    //{
                    //    AllFinishWall.Add(wall);
                    //}

                }
                catch (Exception e)
                {
                    continue;
                }

            }


            //группировка по этапу
            IEnumerable<IGrouping<string, Wall>> groups = AllFinishWall.GroupBy(p2 =>
                p2.LookupParameter("Этап строительства").AsString());

            foreach (var group in groups)
            {
                List<Wall> wallGoupStatus = new List<Wall>();
                wallGoupStatus.Clear();

                foreach (var elWall in group)
                {
                    wallGoupStatus.Add(elWall);
                }

                //группировка по типу 
                var groupsN = wallGoupStatus.GroupBy(p => p.Name);

                //пробегаемся по сгруппированным значениям

                foreach (IGrouping<string, Element> element in groupsN)
                {

                    IList<string> numroomList = new List<string>();
                    numroomList.Clear();

                    //создаем массив с номерами помещений

                    foreach (var t in element)
                    {
                        var otd = t.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();
                        if (otd != null && otd != " ")
                        {
                            string[] subs = otd.Split(' ');
                            numroomList.Add(subs[0]);
                        }

                    }

                    //clean list
                    List<string> newList = new List<string>(numroomList.Distinct());

                    newList.Sort();

                    string numbersAllRoomString = "";

                    if (newList.Count > 1)
                    {
                        numbersAllRoomString = string.Join(", ", newList.ToArray());
                    }
                    else
                    {
                        if (newList.Count == 1)
                        {
                            numbersAllRoomString = newList[0];
                        }

                        else
                        {
                            numbersAllRoomString = "";
                        }

                    }

                    AllFinishWall.Clear();
                    numroomList.Clear();

                    //и записываем это массив в каждый номер этого массива

                    foreach (var t in element)
                    {
                        t.get_Parameter(new Guid("6b89127c-f4ec-4724-b120-8d4b5cdcdcb6")).Set(numbersAllRoomString);
                    }

                }


            }


        }

        public IList<Element> GetAllWallElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

    }
}
