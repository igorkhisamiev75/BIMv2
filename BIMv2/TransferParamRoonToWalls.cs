#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
#endregion
namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class TransferParamRoonToWalls : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        public List<Wall> AllFinishWall = new List<Wall>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            using (var t = new Transaction(_doc))
            {
                t.Start("Find name room for finish walls");

                WriteNameRoomWall(_doc);
               

                TaskDialog.Show("У тебя получилось", "Complite!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }
        public void WriteNameRoomWall(Document _doc)
        {
            var allWallElements = GetAllWallElements(_doc);
                       
            var allRooms = GetAllRooms(_doc);

            
            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Wall)element;

                    var descriptionWallType = wall.LookupParameter("ADSK_Позиция").AsString();
                    var roomName2 = wall.LookupParameter("ADSK_Примечание").AsString();

                    //var descriptionWallType = wall.LookupParameter("ADSK_Позиция").AsString();


                    if (descriptionWallType == "Балка" && roomName2 != null)
                    {
                        AllFinishWall.Add(wall);
                    }


                    //if (descriptionWallType == "Балка")
                    //{
                    //    AllFinishWall.Add(wall);
                    //}

                }
                catch (Exception e)
                {
                    continue;
                }

            }


            foreach (var wall in AllFinishWall)
            {
                var roomNameWall = wall.LookupParameter("ADSK_Примечание").AsString();

                foreach (var element in allRooms)
                {
                    var descriptionWallType2 = element.LookupParameter("Номер").AsString() + " " +
                       element.LookupParameter("Имя").AsString();

                    if(roomNameWall== descriptionWallType2)
                    {
                        var n1 = element.LookupParameter("CPI_Потолок_Тип").AsString();
                        var n2 = element.LookupParameter("CPI_Потолок_Описание").AsString();
                        var n3 = element.LookupParameter("CPI_Потолок_Краткое описание").AsString();

                        wall.LookupParameter("CPI_Потолок_Тип")
                        .Set(n1);
                        wall.LookupParameter("CPI_Потолок_Описание")
                        .Set(n2);
                        wall.LookupParameter("CPI_Потолок_Краткое описание")
                        .Set(n3);

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