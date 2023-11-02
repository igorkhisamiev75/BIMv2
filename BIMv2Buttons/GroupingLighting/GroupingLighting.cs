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
    public class GroupingLighting : IExternalCommand
    {
        public List<Element> AllF = new List<Element>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);

                TaskDialog.Show("Сделано", "Сгруппированы светильники по типу");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            var allElements = GetAllElements(doc);

            foreach (var element in allElements)
            {
                ElementType type = doc.GetElement(element.GetTypeId()) as ElementType;
                var adskOboznach = type.get_Parameter(new Guid("9c98831b-9450-412d-b072-7d69b39f4029")).AsString();

                element.LookupParameter("Марка").Set(adskOboznach);
                doc.Regenerate();
            }


            //группировка по марке
            var groups = allElements.GroupBy(p => p.LookupParameter("Марка").AsString());

            //TaskDialog.Show("123", $"{groups.Count()}");

            foreach (var group in groups)
            {
                List<Element> GroupByType = new List<Element>();

                GroupByType.Clear();

                foreach (var element2 in group)
                {
                    GroupByType.Add(element2);
                }

                //группировка по позиции

                //var groups2 = GroupByType.GroupBy(p => p.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038")).AsString());
                string numbersAllRoomString = "";
                IList<int> numroomList = new List<int>();

                foreach (var element in GroupByType)
                {
                    //создаем массив с номерами помещений

                    string otd = element.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038")).AsString();

                    numroomList.Add(int.Parse(otd));

                }

                
                //numroomList.Distinct();

                //TaskDialog.Show("123", $"{numroomList.Count()}");

                //clean list
                List<int> newList = new List<int>(numroomList.Distinct());
                //TaskDialog.Show("123", $"{newList.Count()}");
                //newList.

                newList.Sort();

                numbersAllRoomString = string.Join(", ", newList.ToArray());

                //numroomList.Clear();



                foreach (var element in GroupByType)
                {
                    //и записываем это массив в каждый номер этого массива

                    element.get_Parameter(new Guid("72c95d18-43b8-45be-ad84-c0a08631ace9")).Set(numbersAllRoomString);

                    numroomList.Clear();

                }






            }

        }

       

        public IList<Element> GetAllElements(Document document)
        {
            // List all equpmet
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

       

    }
}
