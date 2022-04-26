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
    public class WallFinishComposition : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteFinishToWall(doc);
                
                TaskDialog.Show("Сделано", "Пироги стен скопированы");

                t.Commit();

            }
            return Result.Succeeded;

        }
        
        public void WriteFinishToWall(Document doc)
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

            foreach (var wall in AllFinishWall)
            {
                var NameOfBaseFinish = wall.WallType.LookupParameter("MRGT_СлоиОтделки").AsString();

                wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).Set(NameOfBaseFinish);
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
