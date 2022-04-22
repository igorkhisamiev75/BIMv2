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
    public class WallFinishAlliance : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToWallFinishAlliance(doc);
                
                TaskDialog.Show("Сделано", "Пироги стен сгруппированы");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void WriteToWallFinishAlliance(Document doc)
        {
            var allWallElements = GetAllWallElements(doc);

            //отделка только //мжно в запросе сразу попробовать отфильтровать
            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Wall)element;

                    var descriptionWallType = wall.WallType.LookupParameter("Описание").AsString();

                    if (descriptionWallType == "Отделка")
                    {
                        AllFinishWall.Add(wall);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }


            }

           
            //group by room
            IEnumerable<IGrouping<string, Wall>> groupByRooms = AllFinishWall.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());
            
            foreach (var grouping in groupByRooms)
            {
                string pirog="WALL";
                //group by type wall
                IEnumerable<IGrouping<string, Wall>> groupByType = grouping.GroupBy(p2 => p2.Name);

                //TaskDialog.Show("1", groupByRooms2.Count().ToString());
                

                List<Wall> wallGoupStatus = new List<Wall>();

                if (groupByType.Count() == 1)
                {
                    foreach (var wall in grouping)
                    {
                        pirog = wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();

                        wall.get_Parameter(new Guid("6533b173-94cb-465e-964c-ed1e0dfce339")).Set(pirog);
                    }

                    continue;
                }
                else
                {
                    foreach (var wall in groupByType)
                    {
                        pirog = "WALL2";

                        foreach (var wall2 in wall)
                        {
                           
                            wall2.get_Parameter(new Guid("6533b173-94cb-465e-964c-ed1e0dfce339")).Set(pirog);
                        }
                       

                    }
                }

               



            }


        }
        /// <summary>
        /// Get all elements
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
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
