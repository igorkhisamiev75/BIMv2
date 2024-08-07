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
    public class CountFinishCeiling : IExternalCommand
    {
        public List<Ceiling> AllFinishListCeiling = new List<Ceiling>();
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Записываем суммарную площадь отделки");

                WriteToRoomsNull(doc); //ALl finish 

                var allCeilingElements2 = GetAllCeilingElements(doc);
               
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

                t.Commit();

            }
            return Result.Succeeded;

        }
     
        private void WriteToRoomsNull(Document doc)
        {
            //all rooms
            var allRooms = GetAllRooms(doc);

            if (allRooms != null)
            {
                foreach (var room in allRooms)
                {
                    
                    //Ceiling
                    room.get_Parameter(new Guid("da79a6c6-7c1d-4c43-87d5-96e8dd83c495")).Set(" ");
                    room.get_Parameter(new Guid("5f9a763c-3a9b-443a-b55b-4d589d533f02")).Set("Нет отделки");

                }
            }

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


    }
}
