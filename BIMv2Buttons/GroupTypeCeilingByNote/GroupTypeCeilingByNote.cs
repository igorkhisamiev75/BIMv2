using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2Buttons
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GroupTypeCeilingByNote : IExternalCommand
    {
        public List<Element> AllF = new List<Element>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);
                TaskDialog.Show("Сделано", "Потолки сгруппированы");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            var allFloors = GetAllFloorElements(doc);
            AllF.Clear();

            foreach (var VARIABLE in allFloors)
            {

                AllF.Add((Element)VARIABLE);

            }


            //группировка по уровню
            var groups = AllF.GroupBy(p2 =>
                p2.get_Parameter(BuiltInParameter.LEVEL_PARAM).AsValueString());

            foreach (var group in groups)
            {
                List<Element> ceillingGoupBylevel = new List<Element>();
                ceillingGoupBylevel.Clear();

                foreach (var elWall in group)
                {
                    ceillingGoupBylevel.Add(elWall);
                }

                //группировка по типу 
                var groupsN = ceillingGoupBylevel.GroupBy(p => p.Name);

                //пробегаемся по сгруппированным значениям

                foreach (IGrouping<string, Element> element in groupsN)
                {
                    IList<string> numroomList = new List<string>();
                    numroomList.Clear();

                    //создаем массив с номерами помещений
                    foreach (var t in element)
                    {
                        var otd = t.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();
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

                    foreach (var t in element)
                    {
                        //ADSK_Отделка_Группировка
                        t.get_Parameter(new Guid("6b89127c-f4ec-4724-b120-8d4b5cdcdcb6")).Set(numbersAllRoomString);
                    }

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
