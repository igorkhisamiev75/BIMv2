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
    public class FloorFinish : IExternalCommand
    {
        public List<Floor> AllFinishWall = new List<Floor>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Записываем суммарную площадь отделки");

                //записываем суммарную площадь 
                WriteToWallSquare(doc);

                //записываем из типа стены в параметр экземпляра
                WriteFinishToWall(doc);

                //объединяем отделку и площадь где это нужно
                WriteToWallFinishAlliance(doc);

                //TaskDialog.Show("Сделано", "Стены ОТДЕЛКА посчитаны и площадь записана в параметр MRGT_ВидОтделкиСтеныПлощадь");

                //записываем информацию в помещения
                WriteToRoomsWallsSquare(doc);

                //TaskDialog.Show("Сделано", "Стены ОТДЕЛКА посчитаны");

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
                    var wall = (Floor)element;

                    var descriptionWallType = wall.FloorType.LookupParameter("Описание").AsString();

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
            IEnumerable<IGrouping<string, Floor>> groupByRooms = AllFinishWall.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var grouping in groupByRooms)
            {
                string pirog = "";
                string square = "";

                //group by type wall
                IEnumerable<IGrouping<string, Floor>> groupByType = grouping.GroupBy(p2 => p2.Name);

                if (groupByType.Count() == 1)
                {
                    foreach (var wall in grouping)
                    {
                        //MRGT_ВидОтделкиСтены
                        pirog = wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                        //MRGT_ВидОтделкиСтенаПлощадь
                        square = wall.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString();

                        //MRGT_ВидОтделкиПерекрытия
                        wall.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d")).Set(pirog);
                        //MRGT_ПлощадьОбъединеннаяПерекрытия
                        wall.get_Parameter(new Guid("1ac823dd-0dda-4438-acf6-46005617394c")).Set(square);
                    }

                    continue;
                }
                else
                {
                    List<Floor> listWall = new List<Floor>();
                    foreach (var wall2 in grouping)
                    {
                        listWall.Add(wall2);
                    }

                    //удаление дубликатов, групирруем по имени
                    var wallsGroup = listWall.GroupBy(x => x.Name).Select(x => x.First()).ToList();

                    pirog = "";
                    square = "";

                    foreach (var wall in wallsGroup)
                    {
                        //нужно найти сколько слоёв в стене

                        //MRGT_ВидОтделкиСтены
                        var LayerInWall = wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                        var countLayerInWall = LayerInWall.Where(x => "\n".IndexOf(x) != -1).Count();

                        pirog += wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString() + "\n\n";

                        square += wall.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString();

                        for (int i = 0; i < countLayerInWall; i++)
                        {
                            square += "\n\n";
                        }

                    }

                    foreach (var wall in grouping)
                    {
                        wall.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d")).Set(pirog);

                        wall.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563")).Set(square);
                    }


                }
                
            }


        }

        public void WriteFinishToWall(Document doc)
        {
            var allWallElements = GetAllWallElements(doc);

            //отделка только //можно в запросе сразу попробовать отфильтровать
            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Floor)element;

                    var descriptionWallType = wall.FloorType.LookupParameter("Описание").AsString();

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
                var NameOfBaseFinish = wall.FloorType.LookupParameter("MRGT_СлоиОтделки").AsString();

                //MRGT_ВидОтделкиСтены
                wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).Set(NameOfBaseFinish);
            }

        }

        private void WriteToRoomsWallsSquare(Document doc)
        {
            //all rooms
            var allRooms = GetAllRooms(doc);

            var allWallElements = GetAllWallElements(doc);
            AllFinishWall.Clear();

            foreach (var VARIABLE in allWallElements)
            {
                try
                {
                    var wall = (Floor)VARIABLE;

                    var descriptionWallType = wall.FloorType.LookupParameter("Описание").AsString();

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

            if (allRooms != null)
            {
                foreach (var room in allRooms)
                {

                    var numberRoom = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                    //проходимся по всем перекрытиям и находим совпадения по номеру
                    foreach (var wall in AllFinishWall)
                    {
                        var numberRoomInWall = wall.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();

                        if (numberRoom == numberRoomInWall)
                        {
                            //получаем площадь
                            var squareWall = wall.get_Parameter(new Guid("1ac823dd-0dda-4438-acf6-46005617394c"))
                                .AsString();
                            room.get_Parameter(new Guid("1ac823dd-0dda-4438-acf6-46005617394c")).Set(squareWall);

                            //получаем отделку
                            var finishWall = wall.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d"))
                                .AsString();
                            room.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d")).Set(finishWall);


                            continue;
                        }

                    }



                }
            }

        }


        public void WriteToWallSquare(Document doc)
        {
            var allWallElements = GetAllWallElements(doc);
            AllFinishWall.Clear();

            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Floor)element;

                    var descriptionWallType = wall.FloorType.LookupParameter("Описание").AsString();

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
            IEnumerable<IGrouping<string, Floor>> groups = AllFinishWall.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var group in groups)
            {
                List<Floor> WallGroup = new List<Floor>();
                WallGroup.Clear();

                foreach (var elWall in group)
                {
                    WallGroup.Add(elWall);
                }

                //группировка по типу стены
                var groupsN = WallGroup.GroupBy(p => p.Name);

                //пробегаемся по сгруппированным значениям

                foreach (IGrouping<string, Element> element in groupsN)
                {

                    IList<string> numroomList = new List<string>();
                    numroomList.Clear();

                    //суммируем площадь

                    double squeWall = 0;

                    foreach (var t in element)
                    {
                        var otd = t.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                        squeWall += otd;

                    }

                    //и записываем это массив в каждый номер этого массива

                    foreach (var t in element)
                    {
                        //MRGT_ВидОтделкиСтенаПлощадь
                        t.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).Set(squeWall);
                    }

                }


            }


        }

        public IList<Element> GetAllWallElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }
        
        public IList<Element> GetAllRooms(Document doc)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

    }
}
