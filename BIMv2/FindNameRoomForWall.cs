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
    public class FindNameRoomForWall : IExternalCommand
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

                //найти для стен помещения
                WriteNameRoomWall(uiApp);


                TaskDialog.Show("У тебя получилось", "☺☺☺");

                t.Commit();

            }
            return Result.Succeeded;
        }

        public void WriteNameRoomWall(UIApplication uiApp)
        {
            IList<Element> allWallElements = GetAllWallElements(_doc);
            string adskPrim = "a85b7661-26b0-412f-979c-66af80b4b2c3";

            //отделка только //мжно в запросе сразу попробовать отфильтровать
            //foreach (var element in allWallElements)
            //{
            //    try
            //    {
            //        var wall = (Wall)element;

            //        var descriptionWallType = wall.WallType.LookupParameter("Описание").AsString();


            //        if (descriptionWallType == "Отделка")
            //        {
            //            AllFinishWall.Add(wall);
            //        }

            //    }
            //    catch (Exception e)
            //    {
            //        continue;
            //    }

            //}


            foreach (var wall in allWallElements)
            {
                try
                {
                    //var nameWall = wall.LookupParameter("ADSK_Примечание").AsString();

                    //if (nameWall != "")
                    //{
                    //    continue;
                    //}



                    LocationCurve wallLine = wall.Location as LocationCurve;



                    IList<XYZ> wallPoints = wallLine.Curve.Tessellate();


                    //Location loc = e.Location;

                    //var locPoint = (LocationPoint)loc;

                    var pointPF1 = wallPoints[0];
                    XYZ pointPF2 = wallPoints[1];

                    //XYZ pointPF3 = new XYZ(0, 0, 2);

                    var pointPF = (pointPF1 + pointPF2) / 2;


                    var myRoom = FindRoom(_doc, pointPF);
                    string nameRoom = "";
                    string numRoom = "";


                    if (myRoom != null)
                    {
                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                        numRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();


                        wall.get_Parameter(new Guid(adskPrim)).Set(numRoom);
                        wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                    }
                    else
                    {
                        nameRoom = "неНайдено";
                        numRoom = "неНайдено";


                        wall.get_Parameter(new Guid(adskPrim)).Set(numRoom);
                        wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                        //    var linkedInstances = FindLinkedInstances();
                        //    foreach (RevitLinkInstance linkInstance in linkedInstances)
                        //    {
                        //        var getLinkDoc = linkInstance.GetLinkDocument();

                        //        if (getLinkDoc != null)
                        //        {
                        //            var transform = linkInstance.GetTotalTransform().Inverse;
                        //            var targetPoint = transform.OfPoint(pointPF);
                        //            var linkDocument = linkInstance.GetLinkDocument();

                        //            var room = FindRoom(linkDocument, targetPoint);
                        //            if (room != null)
                        //            {
                        //                //string[] subs = room.Name.Split(' ');
                        //                //var stringLevel = room.get_Parameter(new Guid("1a6e5bca-2b14-4f06-b2bf-efce95bd92b9"))
                        //                //        .AsDouble(); //yровень
                        //                //var poGroupName = room.get_Parameter(new Guid("a1731d92-0650-40ed-8b23-0cb7e355dd59"))
                        //                //        .AsString();//ПО_ргуппа
                        //                //var numberName = room.get_Parameter(new Guid("e0c30580-14c7-4cb0-8be2-444179eb94c9"))
                        //                //        .AsString();//ПО_номер

                        //                nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                        //                numRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();


                        //                wall.get_Parameter(new Guid(adskPrim)).Set(numRoom);
                        //                wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                        //            }
                        //            else
                        //            {
                        //                nameRoom = "It's not in Room";
                        //                numRoom= "It's not in Room";

                        //                wall.get_Parameter(new Guid(adskPrim)).Set(numRoom);
                        //                wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                        //            }
                        //        }
                        //  }
                    }



                }


                catch { }
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