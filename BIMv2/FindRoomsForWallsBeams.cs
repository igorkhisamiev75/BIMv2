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
    public class FindRoomsForWallsBeams : IExternalCommand
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

                WriteNameRoomWall(uiApp);
                //потолки
                //светильники
                //мебель?
                //комплекты мебели,
                //окна
                //сантех.прибор
                //ограждение?
                //перекрытие

                TaskDialog.Show("У тебя получилось", "Complite!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }
        public void WriteNameRoomWall(UIApplication uiApp)
        {
            var allWallElements = GetAllWallElements(_doc);

            //отделка только //мжно в запросе сразу попробовать отфильтровать
            foreach (var element in allWallElements)
            {
                try
                {
                    var wall = (Wall)element;

                    //var descriptionWallType = wall.LookupParameter("ADSK_Позиция").AsString();
                    //var roomName2 = wall.LookupParameter("ADSK_Примечание").AsString();

                    var descriptionWallType = wall.LookupParameter("ADSK_Позиция").AsString();
                    

                    //if (descriptionWallType == "Балка" && roomName2==null)
                    //{
                    //    AllFinishWall.Add(wall);
                    //}


                    if (descriptionWallType == "Балка")
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

                LocationCurve wallLine = wall.Location as LocationCurve;

                IList<XYZ> wallPoints = wallLine.Curve.Tessellate();

                //Location loc = e.Location;

                //var locPoint = (LocationPoint)loc;

                var pointPF1 = wallPoints[0];
                var pointPF2 = wallPoints[1];

                XYZ pointPF3 = new XYZ(0,0,5);


                var pointPF = (pointPF1 + pointPF2) / 2 - pointPF3;


                var myRoom = FindRoom(_doc, pointPF);
                var nameRoom = "";

                if (myRoom != null)
                {
                    nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                   myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                    wall.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3"))
                        .Set(nameRoom);

                }
                else
                {
                    nameRoom = "It's not in Room";
                    wall.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3"))
                        .Set(nameRoom);
                   
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

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }
        private static Room FindRoom(Document document, XYZ point)
        {
            PhaseArray nn = document.Phases;

            Phase g = nn.get_Item(1);

            return document
                .Phases
                .Cast<Phase>()
                .Select(x => document.GetRoomAtPoint(point, g))
                .FirstOrDefault(x => x != null);
        }


    }
}