using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2Buttons
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WallFinishing2 : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();
        public List<Ceiling> AllFinishListCeiling = new List<Ceiling>();
        public List<Floor> AllFinishFloor = new List<Floor>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Записываем суммарную площадь отделки");

                WriteToRoomsNull(doc); //ALl finish 

                var allWallElements2 = GetAllWallElements(doc);
                var allCeilingElements2 = GetAllCeilingElements(doc);
                var allFloorElements2 = GetAllWFloorElements(doc);


                #region Walls
                if (allWallElements2.Count > 0)
                {
                    //записываем суммарную площадь в каждую стену
                    WriteToWallSquare(doc);

                    //записываем из типа стены в параметр экземпляра
                    WriteFinishToWall(doc);

                    //объединяем отделку и площадь где это нужно
                    WriteToWallFinishAlliance(doc);

                    //TaskDialog.Show("Сделано", "Стены ОТДЕЛКА посчитаны и площадь записана в параметр MRGT_ВидОтделкиСтеныПлощадь");

                    //записываем информацию в помещения
                    WriteToRooms(doc);

                    TaskDialog.Show("Сделано", "Отделка посчитана для стен");

                }



                #endregion

                #region Ceiling
                if (allCeilingElements2.Count > 0)
                {
                    //записываем суммарную площадь 
                    WriteToCeilingSquare(doc);

                    //записываем из типа стены в параметр экземпляра
                    WriteFinishToCeiling(doc);

                    //объединяем отделку и площадь где это нужно
                    WriteToCeilingFinishAlliance(doc);

                    //записываем информацию в помещения
                    WriteToRoomsCeilingSquare(doc);

                    TaskDialog.Show("Сделано", "Отделка посчитана для потолков");
                }


                #endregion

                #region Floor

                if (allFloorElements2.Count > 0)
                {

                    //записываем суммарную площадь 
                    WriteToFloorsSquare(doc);

                    //записываем из типа в параметр экземпляра
                    WriteFinishToFloors(doc);

                    //объединяем отделку и площадь где это нужно
                    WriteToFloorsFinishAlliance(doc);

                    //записываем информацию в помещения
                    WriteToRoomsFloorsSquare(doc);

                    TaskDialog.Show("Сделано", "Отделка посчитана для полов");

                }
                #endregion

                t.Commit();

            }
            return Result.Succeeded;

        }
        public void WriteToWallSquare(Document doc)
        {
            var allWallElements = GetAllWallElements(doc);
            AllFinishWall.Clear();

            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Wall)element;

                    var descriptionWallType = wall.WallType.LookupParameter("ADSK_Наименование краткое").AsString();

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
            IEnumerable<IGrouping<string, Wall>> groups = AllFinishWall.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var group in groups)
            {
                List<Wall> WallGroup = new List<Wall>();
                WallGroup.Clear();

                foreach (Wall elWall in group)
                {
                    WallGroup.Add(elWall);
                }

                //группировка по типу стены
                IEnumerable<IGrouping<string, Wall>> groupsN = WallGroup.GroupBy(p => p.Name);

                //пробегаемся по сгруппированным значениям

                foreach (IGrouping<string, Element> element in groupsN)
                {

                    IList<string> numroomList = new List<string>();
                    numroomList.Clear();

                    //суммируем площадь

                    double squeWall = 0;

                    foreach (Element t in element)
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

        public void WriteFinishToWall(Document doc)
        {
            var allWallElements = GetAllWallElements(doc);

            //отделка только //мжно в запросе сразу попробовать отфильтровать
            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Wall)element;

                    var descriptionWallType = wall.WallType.LookupParameter("ADSK_Наименование краткое").AsString();

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

                //MRGT_ВидОтделкиСтены
                wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).Set(NameOfBaseFinish);
            }

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

                    var descriptionWallType = wall.WallType.LookupParameter("ADSK_Наименование краткое").AsString();

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
                string pirog = "";
                string square = "";

                //group by type wall
                IEnumerable<IGrouping<string, Wall>> groupByType = grouping.GroupBy(p2 => p2.Name);

                if (groupByType.Count() == 1)
                {
                    square = "";
                    Wall firstWall = groupByType.FirstOrDefault().FirstOrDefault();
                    var square2 = firstWall.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString();

                    // нужно найти сколько слоёв в стене
                    //MRGT_ПирогОбъединенная

                    string LayerInWall = firstWall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                    int countLayerInWall = LayerInWall.Where(x => "\n".IndexOf(x) != -1).Count();//10

                    int countForSquare = countLayerInWall / 2;//5

                    for (int i = 0; i < countForSquare; i++)
                    {
                        square += " \n";
                    }

                    square = square + square2;
                    //TaskDialog.Show("Сделано", "Зашли сюда");
                    foreach (Wall wall in grouping)
                    {
                        pirog = wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();

                        wall.get_Parameter(new Guid("6533b173-94cb-465e-964c-ed1e0dfce339")).Set(pirog);

                        //square in each wall
                        wall.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563")).Set(square);
                    }

                    continue;
                }
                else
                {
                    List<Wall> listWall = new List<Wall>();

                    foreach (Wall wall2 in grouping)
                    {
                        listWall.Add(wall2);
                    }

                    //TaskDialog.Show("Сделано", $"{listWall.Count} стен"); //90

                    //listWall.Distinct();

                    List<Wall> newList = listWall
                        .GroupBy(x => x.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)
                        .AsValueString())
                        .Select(x => x.First())
                        .ToList();

                    List<Wall> newList2 = newList
                        .OrderBy(x => x.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)
                        .AsValueString())
                        .ToList();

                    pirog = "";
                    square = "";

                    foreach (Wall wall in newList2)
                    {
                        //нужно найти сколько слоёв в стене

                        string countProbels = "";

                        //MRGT_ПирогОбъединенная
                        string LayerInWall = wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                        int countLayerInWall = LayerInWall.Where(x => "\n".IndexOf(x) != -1).Count();

                        int countForSquare = countLayerInWall / 2;//5

                        if (countForSquare % 2 == 0)  //четное чмсло
                        {
                            for (int i = 0; i <= countForSquare; i++)
                            {
                                countProbels += " \n";
                            }
                        }

                        if (countForSquare % 2 == 1) //нечетное число
                        {
                            for (int i = 0; i <= countForSquare; i++)
                            {
                                countProbels += " \n";
                            }
                        }



                        pirog += wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString() + "\n\n";


                        if (countLayerInWall == 0)
                        {
                            square += wall.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString() + "\n\n";
                        }

                        if (countLayerInWall > 0)
                        {
                            square += countProbels + wall.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString() + countProbels;
                        }

                    }

                    foreach (var wall in grouping)
                    {
                        wall.get_Parameter(new Guid("6533b173-94cb-465e-964c-ed1e0dfce339")).Set(pirog);

                        wall.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563")).Set(square);
                    }


                }


            }


        }

        private void WriteToRooms(Document doc)
        {
            //all rooms
            var allRooms = GetAllRooms(doc);

            var allWallElements = GetAllWallElements(doc);
            AllFinishWall.Clear();

            foreach (var VARIABLE in allWallElements)
            {
                try
                {
                    var wall = (Wall)VARIABLE;

                    var descriptionWallType = wall.WallType.LookupParameter("ADSK_Наименование краткое").AsString();

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

                    //проходимся по всем стенам и находим совпадения по номеру
                    foreach (var wall in AllFinishWall)
                    {
                        var numberRoomInWall = wall.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();

                        if (numberRoom == numberRoomInWall)
                        {
                            //получаем площадь
                            //MRGT_ПлощадьОбъединенная
                            var squareWall = wall.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563"))
                                .AsString();
                            room.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563")).Set(squareWall);

                            //получаем отделку
                            var finishWall = wall.get_Parameter(new Guid("6533b173-94cb-465e-964c-ed1e0dfce339"))
                                .AsString();
                            room.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).Set(finishWall);


                            continue;
                        }

                    }



                }
            }

        }

        private void WriteToRoomsNull(Document doc)
        {
            //all rooms
            var allRooms = GetAllRooms(doc);

            if (allRooms != null)
            {
                foreach (var room in allRooms)
                {
                    //Walls
                    room.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563")).Set(" ");
                    room.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).Set("Нет отделки");
                    //Floors
                    room.get_Parameter(new Guid("1ac823dd-0dda-4438-acf6-46005617394c")).Set(" ");
                    room.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d")).Set("Нет отделки");
                    //Ceiling
                    room.get_Parameter(new Guid("da79a6c6-7c1d-4c43-87d5-96e8dd83c495")).Set(" ");
                    room.get_Parameter(new Guid("5f9a763c-3a9b-443a-b55b-4d589d533f02")).Set("Нет отделки");

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

        public void WriteToCeilingSquare(Document doc)
        {
            var allCeilingElements = GetAllCeilingElements(doc);
            AllFinishListCeiling.Clear();

            foreach (var element in allCeilingElements)
            {
                try
                {
                    var ceiling = (Ceiling)element;

                    var typeGetCeiling = doc.GetElement(ceiling.GetTypeId());
                    var descriptionWallType = typeGetCeiling.LookupParameter("ADSK_Наименование краткое").AsString();

                    if (descriptionWallType == "Отделка")
                    {
                        AllFinishListCeiling.Add(ceiling);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }


            }

            //group by room
            IEnumerable<IGrouping<string, Ceiling>> groups = AllFinishListCeiling.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var group in groups)
            {
                List<Ceiling> ceilingsGroup = new List<Ceiling>();
                ceilingsGroup.Clear();

                foreach (var elWall in group)
                {
                    ceilingsGroup.Add(elWall);
                }

                //группировка по типу стены
                var groupsN = ceilingsGroup.GroupBy(p => p.Name);

                //пробегаемся по сгруппированным значениям

                foreach (IGrouping<string, Element> element in groupsN)
                {

                    IList<string> numroomList = new List<string>();
                    numroomList.Clear();

                    //суммируем площадь

                    double squeCiling = 0;

                    foreach (var t in element)
                    {
                        var otd = t.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                        squeCiling += otd;

                    }

                    //и записываем это массив в каждый номер этого массива

                    foreach (var t in element)
                    {
                        //MRGT_ВидОтделкиСтенаПлощадь
                        t.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).Set(squeCiling);
                    }

                }


            }


        }

        public IList<Element> GetAllCeilingElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType();

            IList<Element> allCeilingElements = floor_collector.ToElements();
            return allCeilingElements;
        }

        public void WriteFinishToCeiling(Document doc)
        {
            var allCeilingElements = GetAllCeilingElements(doc);

            //отделка только //можно в запросе сразу попробовать отфильтровать
            foreach (var element in allCeilingElements)
            {
                try
                {
                    var ceiling = (Ceiling)element;

                    var typeGetCeiling = doc.GetElement(ceiling.GetTypeId());
                    var s = typeGetCeiling.LookupParameter("ADSK_Наименование краткое").AsString();

                    if (s == "Отделка")
                    {
                        AllFinishListCeiling.Add(ceiling);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }


            }

            foreach (var ceiling in AllFinishListCeiling)
            {
                var typeGetCeiling = doc.GetElement(ceiling.GetTypeId());

                var NameOfBaseFinish = typeGetCeiling.LookupParameter("MRGT_СлоиОтделки").AsString();

                //MRGT_ВидОтделкиСтены
                ceiling.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).Set(NameOfBaseFinish);
            }

        }

        private void WriteToRoomsCeilingSquare(Document doc)
        {
            //all rooms
            var allRooms = GetAllRooms(doc);

            var allWallElements = GetAllCeilingElements(doc);
            AllFinishListCeiling.Clear();

            foreach (var VARIABLE in allWallElements)
            {
                try
                {
                    var wall = (Ceiling)VARIABLE;

                    var typeGetCeiling = doc.GetElement(wall.GetTypeId());
                    var descriptionWallType = typeGetCeiling.LookupParameter("ADSK_Наименование краткое").AsString();

                    if (descriptionWallType == "Отделка")
                    {
                        AllFinishListCeiling.Add(wall);
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
                    foreach (var wall in AllFinishListCeiling)
                    {
                        var numberRoomInWall = wall.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();

                        if (numberRoom == numberRoomInWall)
                        {
                            //получаем площадь
                            var squareWall = wall.get_Parameter(new Guid("da79a6c6-7c1d-4c43-87d5-96e8dd83c495"))
                                .AsString();
                            room.get_Parameter(new Guid("da79a6c6-7c1d-4c43-87d5-96e8dd83c495")).Set(squareWall);

                            //получаем отделку
                            var finishWall = wall.get_Parameter(new Guid("5f9a763c-3a9b-443a-b55b-4d589d533f02"))
                                .AsString();
                            room.get_Parameter(new Guid("5f9a763c-3a9b-443a-b55b-4d589d533f02")).Set(finishWall);

                            continue;
                        }
                    }
                }
            }
        }

        public void WriteToCeilingFinishAlliance(Document doc)
        {
            var allCeilingElements = GetAllCeilingElements(doc);

            //отделка только //мжно в запросе сразу попробовать отфильтровать
            foreach (var element in allCeilingElements)
            {
                try
                {
                    var ceiling = (Ceiling)element;

                    var typeGetCeiling = doc.GetElement(ceiling.GetTypeId());
                    var descriptionWallType = typeGetCeiling.LookupParameter("ADSK_Наименование краткое").AsString();


                    if (descriptionWallType == "Отделка")
                    {
                        AllFinishListCeiling.Add(ceiling);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }

            }


            //group by room
            IEnumerable<IGrouping<string, Ceiling>> groupByRooms = AllFinishListCeiling.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var grouping in groupByRooms)
            {
                string pirog = "";
                string square = "";

                //group by type wall
                IEnumerable<IGrouping<string, Ceiling>> groupByType = grouping.GroupBy(p2 => p2.Name);

                if (groupByType.Count() == 1)
                {
                    foreach (var ceiling in grouping)
                    {
                        //MRGT_ВидОтделкиСтены
                        pirog = ceiling.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                        //MRGT_ВидОтделкиСтенаПлощадь
                        square = ceiling.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString();

                        //MRGT_ВидОтделкиПотолки
                        ceiling.get_Parameter(new Guid("5f9a763c-3a9b-443a-b55b-4d589d533f02")).Set(pirog);
                        //MRGT_ПлощадьОбъединеннаяПотолки
                        ceiling.get_Parameter(new Guid("da79a6c6-7c1d-4c43-87d5-96e8dd83c495")).Set(square);
                    }

                    continue;
                }
                else
                {
                    List<Ceiling> ceilings = new List<Ceiling>();
                    foreach (var wall2 in grouping)
                    {
                        ceilings.Add(wall2);
                    }

                    //удаление дубликатов, групирруем по имени
                    var wallsGroup = ceilings.GroupBy(x => x.Name).Select(x => x.First()).ToList();

                    pirog = "";
                    square = "";

                    foreach (var wall in wallsGroup)
                    {
                        //нужно найти сколько слоёв в стене

                        //MRGT_ВидОтделкиСтены
                        var LayerInWall = wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                        var countLayerInWall = LayerInWall.Where(x => "\n".IndexOf(x) != -1).Count();

                        pirog += wall.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString() + "\n\n";

                        square += wall.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString() + "\n\n";

                        for (int i = 0; i < countLayerInWall; i++)
                        {
                            square += "\n\n";
                        }

                    }

                    foreach (var ceiling in grouping)
                    {
                        //MRGT_ВидОтделкиПотолки
                        ceiling.get_Parameter(new Guid("5f9a763c-3a9b-443a-b55b-4d589d533f02")).Set(pirog);
                        //MRGT_ПлощадьОбъединеннаяПотолки
                        ceiling.get_Parameter(new Guid("da79a6c6-7c1d-4c43-87d5-96e8dd83c495")).Set(square);
                    }


                }

            }


        }

        public void WriteToFloorsSquare(Document doc)
        {
            var elementsFloor = GetAllWFloorElements(doc);
            AllFinishFloor.Clear();

            foreach (var element in elementsFloor)
            {
                try
                {
                    var floor = (Floor)element;

                    var floorType = floor.FloorType.LookupParameter("ADSK_Наименование краткое").AsString();

                    if (floorType == "Отделка")
                    {
                        AllFinishFloor.Add(floor);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }


            }

            //group by room
            IEnumerable<IGrouping<string, Floor>> groups = AllFinishFloor.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var group in groups)
            {
                List<Floor> FloorGroup = new List<Floor>();
                FloorGroup.Clear();

                foreach (var elWall in group)
                {
                    FloorGroup.Add(elWall);
                }

                //группировка по типу 
                var groupsN = FloorGroup.GroupBy(p => p.Name);

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
        public void WriteFinishToFloors(Document doc)
        {
            var allFloorElements = GetAllWFloorElements(doc);

            //отделка только //можно в запросе сразу попробовать отфильтровать
            foreach (var element in allFloorElements)
            {
                try
                {
                    var floor = (Floor)element;

                    var descriptionWallType = floor.FloorType.LookupParameter("ADSK_Наименование краткое").AsString();

                    if (descriptionWallType == "Отделка")
                    {
                        AllFinishFloor.Add(floor);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }


            }

            foreach (var floor in AllFinishFloor)
            {
                var NameOfBaseFinish = floor.FloorType.LookupParameter("MRGT_СлоиОтделки").AsString();

                //MRGT_ВидОтделкиСтены
                floor.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).Set(NameOfBaseFinish);
            }

        }
        public void WriteToFloorsFinishAlliance(Document doc)
        {
            var allFloorElements = GetAllWFloorElements(doc);

            //отделка только //мжно в запросе сразу попробовать отфильтровать
            foreach (var element in allFloorElements)
            {
                try
                {
                    var floor = (Floor)element;

                    var descriptionFloorType = floor.FloorType.LookupParameter("ADSK_Наименование краткое").AsString();

                    if (descriptionFloorType == "Отделка")
                    {
                        AllFinishFloor.Add(floor);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }


            }


            //group by room
            IEnumerable<IGrouping<string, Floor>> groupByRooms = AllFinishFloor.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var grouping in groupByRooms)
            {
                string pirog = "";
                string square = "";

                //group by type wall
                IEnumerable<IGrouping<string, Floor>> groupByType = grouping.GroupBy(p2 => p2.Name);

                if (groupByType.Count() == 1)
                {
                    foreach (var floor in grouping)
                    {
                        //MRGT_ВидОтделкиСтены
                        pirog = floor.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                        //MRGT_ВидОтделкиСтенаПлощадь
                        square = floor.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString();

                        //MRGT_ВидОтделкиПерекрытия
                        floor.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d")).Set(pirog);
                        //MRGT_ПлощадьОбъединенная
                        floor.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563")).Set(square);
                    }

                    continue;
                }
                else
                {
                    List<Floor> listWall = new List<Floor>();
                    foreach (var Floor2 in grouping)
                    {
                        listWall.Add(Floor2);
                    }

                    //удаление дубликатов, групирруем по имени
                    var wallsGroup = listWall.GroupBy(x => x.Name).Select(x => x.First()).ToList();

                    pirog = "";
                    square = "";

                    foreach (var floor in wallsGroup)
                    {
                        //нужно найти сколько слоёв в стене

                        //MRGT_ВидОтделкиСтены
                        var LayerInWall = floor.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString();
                        var countLayerInWall = LayerInWall.Where(x => "\n".IndexOf(x) != -1).Count();

                        pirog += floor.get_Parameter(new Guid("68cc39f3-189c-4992-8c72-d7bfc4bec9ae")).AsString() + "\n\n";

                        square += floor.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).AsValueString();

                        for (int i = 0; i < countLayerInWall; i++)
                        {
                            square += "\n\n";
                        }

                    }

                    foreach (var floor in grouping)
                    {
                        floor.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d")).Set(pirog);

                        floor.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563")).Set(square);
                    }


                }

            }


        }
        private void WriteToRoomsFloorsSquare(Document doc)
        {
            //all rooms
            var allRooms = GetAllRooms(doc);

            var allFloorElements = GetAllWFloorElements(doc);
            AllFinishFloor.Clear();

            foreach (var VARIABLE in allFloorElements)
            {
                try
                {
                    var wall = (Floor)VARIABLE;

                    var descriptionWallType = wall.FloorType.LookupParameter("ADSK_Наименование краткое").AsString();

                    if (descriptionWallType == "Отделка")
                    {
                        AllFinishFloor.Add(wall);
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
                    foreach (var wall in AllFinishFloor)
                    {
                        var numberRoomInWall = wall.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();

                        if (numberRoom == numberRoomInWall)
                        {
                            //получаем площадь
                            //MRGT_ПлощадьОбъединенная
                            var squareWall = wall.get_Parameter(new Guid("1e2bf482-d000-4acd-a903-17caa723e563"))
                                .AsString();
                            //MRGT_ПлощадьОбъединеннаяПерекрытия
                            room.get_Parameter(new Guid("1ac823dd-0dda-4438-acf6-46005617394c")).Set(squareWall);

                            //получаем отделку
                            //MRGT_ВидОтделкиПерекрытия
                            var finishWall = wall.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d"))
                                .AsString();
                            //
                            room.get_Parameter(new Guid("cfa69bc0-57c7-491c-9ba0-e82c50a1e27d")).Set(finishWall);

                            continue;
                        }

                    }



                }
            }

        }

        public IList<Element> GetAllWFloorElements(Document document)
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
