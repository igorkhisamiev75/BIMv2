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
    public class FindNameRoomForFinishElementsRoomsInLink : IExternalCommand
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

            
            foreach (var wall in AllFinishWall)
            {

                LocationCurve wallLine = wall.Location as LocationCurve;

                IList<XYZ> wallPoints = wallLine.Curve.Tessellate();

                //Location loc = e.Location;

                //var locPoint = (LocationPoint)loc;
                var pointPF = wallPoints[0];


                var myRoom = FindRoom(_doc, pointPF);
                var nameRoom = "";

                if (myRoom != null)
                {
                    nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                   myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                    wall.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                        .Set(nameRoom);

                }
                else
                {
                    var linkedInstances = FindLinkedInstances();
                    foreach (var linkInstance in linkedInstances)
                    {
                        var transform = linkInstance.GetTotalTransform().Inverse;
                        var targetPoint = transform.OfPoint(pointPF);
                        var linkDocument = linkInstance.GetLinkDocument();

                        var room = FindRoom(linkDocument, targetPoint);
                        if (room != null)
                        {
                            string[] subs = room.Name.Split(' ');

                            wall.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                .Set(room.Number + " " + subs[0]);
                        }
                        else
                        {
                            nameRoom = "It's not in Room";
                            wall.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                .Set(nameRoom);
                        }

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
            return document
                .Phases
                .Cast<Phase>()
                .Select(x => document.GetRoomAtPoint(point, x))
                .FirstOrDefault(x => x != null);
        }


    }
}