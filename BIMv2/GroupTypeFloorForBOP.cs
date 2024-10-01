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
    public class GroupTypeFloorForBOP : IExternalCommand
    {
        public List<Floor> AllF = new List<Floor>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);

                TaskDialog.Show("Сделано", "Полы сгруппированы для Ведомостей Объемов Работ");

                t.Commit();

            }

            return Result.Succeeded;

        }

        public void WriteToComment(Document doc)
        {
            string BIM_BOP_ГруппировкаПлощадей = "54b611f4-9020-4616-8023-a13988d3a46e";
            string BIM_BOP_ГруппировкаПомещений = "801dc37f-9ac6-477b-91b4-8459334cb5f5";
            string BIM_BOP_ГруппировкаПомещенийПромежуточная = "9f547840-1277-4e0d-8006-38a8abd24744";

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

            //group by level группировка по уровню
            IEnumerable<IGrouping<string, Floor>> groups = AllF.GroupBy(p2 =>
                p2.get_Parameter(BuiltInParameter.LEVEL_PARAM).AsValueString());

            foreach (IGrouping<string, Floor> group in groups)
            {
                List<Floor> floorGroups = new List<Floor>();
                floorGroups.Clear();

                foreach (Floor el in group)
                {
                  
                  floorGroups.Add(el);
                  
                }

                //группировка по типу 
                IEnumerable<IGrouping<string, Floor>> groupsN = floorGroups.GroupBy(p => p.Name);

                string squeFloor = "";

                foreach (IGrouping<string, Floor> group2 in groupsN)
                {
                    List<Floor> floorGroups2 = new List<Floor>();
                    floorGroups2.Clear();

                    foreach (Floor elWall in group2)
                    {

                        floorGroups2.Add(elWall);

                    }

                    //группировка по имени помещения 
                    //ALL_MODEL_INSTANCE_COMMENTS
                    IEnumerable<IGrouping<string, Floor>> groupsN2 = floorGroups2.GroupBy(p =>
                    p.LookupParameter("Комментарии").AsString());

                    //пробегаемся по сгруппированным значениям

                    foreach (IGrouping<string, Floor> element in groupsN2)
                    {
                        IList<string> numroomList = new List<string>();
                        numroomList.Clear();

                        string nameRoom = "";

                        //создаем массив с номерами помещений
                        foreach (Element t in element)
                        {
                            //ADSK_Примечание
                            string otd = t.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();
                            if (otd != null)
                            {
                                //string[] subs = otd.Split(' ');
                                numroomList.Add(otd);
                            }

                            nameRoom = t.LookupParameter("Комментарии").AsString();

                        }

                        //clean list
                        List<string> newList = new List<string>(numroomList.Distinct());

                        newList.Sort();

                        string numbersAllRoomString = string.Join(", ", newList.ToArray());

                        numroomList.Clear();

                        //и записываем это массив в каждый номер этого массива

                        foreach (Element t in element)
                        {
                            //ADSK_Отделка_Группировка
                            //t.get_Parameter(new Guid("6b89127c-f4ec-4724-b120-8d4b5cdcdcb6")).Set(numbersAllRoomString);
                            //BIM_BOP_ГруппировкаПомещенийПромежуточная
                            t.get_Parameter(new Guid(BIM_BOP_ГруппировкаПомещенийПромежуточная)).Set(nameRoom + " пом.: " + numbersAllRoomString);

                        }
                        doc.Regenerate();

                    }

                    //пройтись и собрать сгруппированые с помещениями
                    IList<string> numroomListPom = new List<string>();

                    foreach (Floor floor in floorGroups2) 
                    {
                        string otd = floor.get_Parameter(new Guid(BIM_BOP_ГруппировкаПомещенийПромежуточная)).AsString(); //ADSK_Примечание
                        if (otd != null)
                        {
                            //string[] subs = otd.Split(' ');
                            numroomListPom.Add(otd);
                        }

                    }

                    //clean list
                    List<string> newList3 = new List<string>(numroomListPom.Distinct());

                    newList3.Sort();

                    string numbersAllRoomString2 = string.Join(", ", newList3.ToArray());

                    numroomListPom.Clear();

                    foreach (Element t in floorGroups2)
                    {
                        t.get_Parameter(new Guid(BIM_BOP_ГруппировкаПомещений)).Set(numbersAllRoomString2); 
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
