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
    public class GroupTypeWallsAndWriteToComment : IExternalCommand
    {
       public List<Wall> AllF = new List<Wall>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);

                TaskDialog.Show("Сделано", "Сгруппированы стены");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            var allFloors = GetAllFloorElements(doc);

            foreach (var VARIABLE in allFloors)
            {
                
                AllF.Add((Wall)VARIABLE);

            }
            

                //группировка по отделке
            var groups = AllF.GroupBy(p2 => p2.get_Parameter(new Guid("037b6cb5-a3fe-473e-9994-293f5b0265b6")).AsString());

            foreach (var group in groups)
            {
                List<Wall> wallGoupStatus = new List<Wall>();
                wallGoupStatus.Clear();

                foreach (var elWall in group)
                {
                    if (elWall.GetType().ToString() == "Wall")
                    {
                        wallGoupStatus.Add(elWall);
                    }

                    //группировка по типу пола
                    var groupsN = wallGoupStatus.GroupBy(p => p.Name);

                    //пробегаемся по сгруппированным значениям

                    foreach (IGrouping<string, Element> element in groups)
                    {
                        IList<string> numroomList = new List<string>();
                        numroomList.Clear();

                        //создаем массив с номерами помещений
                        foreach (var t in element)
                        {
                            var otd = t.LookupParameter("Номер помещения (отделка)").AsString();
                            if (otd != null)
                            {
                                string[] subs = otd.Split(' ');
                                numroomList.Add(subs[0]);
                            }

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
            }
            
           
        }

        public IList<Element> GetAllFloorElements(Document document)
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
