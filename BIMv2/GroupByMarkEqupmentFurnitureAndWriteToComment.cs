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
    public class GroupByMarkEqupmentFurnitureAndWriteToComment : IExternalCommand
    {
        public List<Element> AllF = new List<Element>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                

                WriteToComment(doc);

                TaskDialog.Show("Сделано", "Сгруппировано оборудование");

                WriteToComment2(doc);

                TaskDialog.Show("Сделано", "Сгруппирована мебель");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            var allElements = GetAllFloorElements(doc);
          


            //группировка по типу
            var groups = allElements.GroupBy(p => p.Name);

            
                

                foreach (IGrouping<string, Element> element in groups)
                {

                    IList<string> numroomList = new List<string>();
                    numroomList.Clear();

                    //создаем массив с номерами помещений

                    foreach (var t in element)
                    {
                        var otd = t.LookupParameter("Номер помещения (Мебель и оборудование)").AsString();
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

        public void WriteToComment2(Document doc)
        {
            var allElements = GetAllFloorElements2(doc);



            //группировка по типу
            var groups = allElements.GroupBy(p => p.Name);




            foreach (IGrouping<string, Element> element in groups)
            {

                IList<string> numroomList = new List<string>();
                numroomList.Clear();

                //создаем массив с номерами помещений

                foreach (var t in element)
                {
                    var otd = t.LookupParameter("Номер помещения (Мебель и оборудование)").AsString();
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

        public IList<Element> GetAllFloorElements(Document document)
        {
            // List all equpmet
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

        public IList<Element> GetAllFloorElements2(Document document)
        {
            // List all equpmet
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Furniture)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

    }
}
