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
    public class WallFinishing : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");

                WriteToWallSquare(doc);

                //TaskDialog.Show("Сделано", "Стены ОТДЕЛКА посчитаны и площадь записана в параметр MRGT_ВидОтделкиСтеныПлощадь");

                WriteToRoomsWallsSquare(doc);

                TaskDialog.Show("Сделано", "Стены ОТДЕЛКА посчитаны");

                t.Commit();

            }
            return Result.Succeeded;

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
                    var wall = (Wall)VARIABLE;

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
                            double squareWall = wall.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c"))
                                .AsDouble();
                            room.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).Set(squareWall);

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

            foreach (var VARIABLE in allWallElements)
            {
                try
                {
                    var wall = (Wall)VARIABLE;

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
            IEnumerable<IGrouping<string, Wall>> groups = AllFinishWall.GroupBy(p2 =>
                p2.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString());

            foreach (var group in groups)
            {
                List<Wall> WallGroup = new List<Wall>();
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
                        //var r = sqFootToSquareM(squeWall);
                        t.get_Parameter(new Guid("2c097f7a-e32f-4455-9e8a-ac72e131b24c")).Set(squeWall);
                    }

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


        /// <summary>
        /// Convert square feet to square meters 
        /// with two decimal places precision.
        /// </summary>
        double sqFootToSquareM(double sqFoot)
        {
            return Math.Round(sqFoot * 0.092903, 2);
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
