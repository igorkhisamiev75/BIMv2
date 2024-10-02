using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;

using static Autodesk.Revit.DB.SpecTypeId;

using Floor = Autodesk.Revit.DB.Floor;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GroupTypeFloorForBOP : IExternalCommand
    {
        public List<Floor> AllF = new List<Floor>();
        string ADSK_Примечание = "a85b7661-26b0-412f-979c-66af80b4b2c3";
        string BIM_BOP_ГруппировкаПлощадей = "54b611f4-9020-4616-8023-a13988d3a46e";
        string BIM_BOP_ГруппировкаПомещений = "801dc37f-9ac6-477b-91b4-8459334cb5f5";
        string BIM_BOP_ГруппировкаПомещенийПромежуточная = "9f547840-1277-4e0d-8006-38a8abd24744";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToComment(doc);

                TaskDialog.Show("Сделано", "Полы сгруппированы для ВОР");

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

            //group by level группировка по уровню
            IEnumerable<IGrouping<string, Floor>> groupsByLevel = AllF.GroupBy(p2 =>
                p2.get_Parameter(BuiltInParameter.LEVEL_PARAM).AsValueString());

            foreach (IGrouping<string, Floor> group in groupsByLevel)
            {
                List<Floor> floorGroups = new List<Floor>();
                floorGroups.Clear();

                foreach (Floor el in group)
                {
                  
                  floorGroups.Add(el);
                  
                }

                //группировка по типу 
                IEnumerable<IGrouping<string, Floor>> groupstypeFloor = floorGroups.GroupBy(p => p.Name);

                foreach (IGrouping<string, Floor> group2 in groupstypeFloor)
                {
                    List<string> squeFloorList = new List<string>();
                    List<Floor> floorGroupsByType = new List<Floor>();

                    floorGroupsByType.Clear();
                    //squeFloorList.Clear();
                    foreach (Floor elFloor in group2)
                    {
                        floorGroupsByType.Add(elFloor);
                        //squeFloorList.Add(elFloor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsValueString());
                    }

                    



                    //группировка по имени помещения 
                    //ALL_MODEL_INSTANCE_COMMENTS
                    IEnumerable<IGrouping<string, Floor>> groupsN2ByComment = floorGroupsByType.GroupBy(p =>
                    p.LookupParameter("Комментарии").AsString());

                    //пробегаемся по сгруппированным значениям

                    foreach (IGrouping<string, Floor> element in groupsN2ByComment)
                    {
                        IList<string> numroomList = new List<string>();
                        numroomList.Clear();

                        string nameRoom = "";

                        //создаем массив с номерами помещений
                        foreach (Element t in element)
                        {
                            //ADSK_Примечание
                            string otd = t.get_Parameter(new Guid(ADSK_Примечание)).AsString();
                            if (otd != null)
                            {
                                //string[] subs = otd.Split(' ');
                                numroomList.Add(otd);

                                //nen
                                //squeFloorList.Add(t.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsValueString());


                            }

                            nameRoom = t.LookupParameter("Комментарии").AsString();

                        }

                        //squeFloorList.Reverse();
                        

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
                            //t.get_Parameter(new Guid(BIM_BOP_ГруппировкаПлощадей)).Set(squeFloorString);

                        }
                        //squeFloorList.Clear();
                        doc.Regenerate();

                    }

                    //пройтись и собрать сгруппированые с помещениями
                    IList<string> numroomListPom = new List<string>();

                    foreach (Floor floor in floorGroupsByType) 
                    {
                        string otd = floor.get_Parameter(new Guid(BIM_BOP_ГруппировкаПомещенийПромежуточная)).AsString(); //ADSK_Примечание
                        if (otd != null)
                        {
                            //string[] subs = otd.Split(' ');
                            numroomListPom.Add(otd);
                        }

                    }

                    //clean list
                    List<string> listWithNamberRoom = new List<string>(numroomListPom.Distinct());

                    listWithNamberRoom.Sort();


                    //тут стринг с именами и номерами помещений
                    string numbersAllRoomString2 = string.Join(", ", listWithNamberRoom.ToArray());
                    string[] separators = new string[] { ",", " " };

                    string[] strings = numbersAllRoomString2.Split(separators, StringSplitOptions.RemoveEmptyEntries);


                    //пройдемся по листу и вытащим номера которые подтянут площади

                    foreach (string floor in strings)
                    {
                        //var floorString = floor.Replace(",", "");
                        var floorString = floor.TrimEnd();

                        string sq = GetSquareFloorByNameRoom(floor, floorGroupsByType);
                        if (sq != string.Empty)
                        {
                            squeFloorList.Add(sq);
                        }
                    }

                    string squeFloorString = string.Join(" + ", squeFloorList.ToArray());

                   

                    foreach (Element t in floorGroupsByType)
                    {
                        t.get_Parameter(new Guid(BIM_BOP_ГруппировкаПомещений)).Set(numbersAllRoomString2);
                        t.get_Parameter(new Guid(BIM_BOP_ГруппировкаПлощадей)).Set(squeFloorString);
                    }

                    numroomListPom.Clear();
                    squeFloorList .Clear();
                    //squeFloorString = "";
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


        public string GetSquareFloorByNameRoom(string name, List<Floor> floors)
        {
            string square = "1";

            IEnumerable<Floor> gg = floors.Where(x => x.get_Parameter(new Guid(ADSK_Примечание))
          .AsString() == name);

            //gg.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsValueString();
            if (gg.Count() != 0)
            {
                Floor f = gg.FirstOrDefault();

                return f.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsValueString();
                //return square;
            }
           
            else { return string.Empty; }
           

            
        }

    }
}
