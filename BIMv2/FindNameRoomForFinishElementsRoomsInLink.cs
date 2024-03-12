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

                //WriteNameRoomWall(uiApp);

                // WriteNameRoomFloors(uiApp);


                //потолки

                WriteNameRoomCeillings(uiApp);
                //светильники
                //мебель?
                //комплекты мебели,
                //окна
                //сантех.прибор
                //ограждение?
                //перекрытие

                TaskDialog.Show("У тебя получилось", "Complite03-24!!!☺☺☺");
                t.Commit();
               
            }
            return Result.Succeeded;
        }
        public void WriteNameRoomFloors (UIApplication uiApp)
        {
            var allWallElements = GetAllFloorsElements(_doc);

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


            foreach (var element in allWallElements)
            {
                Room myRoom;
                string nameRoom = "";

                XYZ gXyz = new XYZ(0, 0, 0);
                XYZ zOfset = new XYZ(0, 0, 2);

                XYZ pointLine = new XYZ(0, 0, 0);

                Floor floor = (Floor)element;

                ElementId eid = floor.Id;

                Options opts = new Options();

                List<List<XYZ>> polygons = new List<List<XYZ>>();
                List<XYZ> findList = new List<XYZ>();

                GeometryElement geo = floor.get_Geometry(opts);

                foreach (GeometryObject obj in geo) // 2013
                {
                    Solid solid = obj as Solid;
                    if (solid != null)
                    {
                        PlanarFace lowest = null;
                        FaceArray faces = solid.Faces; //faces

                        EdgeArray edgeArray = solid.Edges; //edges

                        foreach (Edge elEdgeArray in edgeArray)
                        {
                            pointLine = elEdgeArray.Tessellate()[0];

                            findList.Add(pointLine);

                            //if (g != null)
                            //{
                            //    pointLine = g.Tessellate()[0];
                            //    break;
                            //}



                        }

                        //foreach (Face f in faces)
                        //{

                        //    PlanarFace pf = f as PlanarFace;

                        //    RuledFace rf=f as RuledFace;

                        //    CylindricalFace cf=f as CylindricalFace;

                        //    if (pf != null)
                        //    {

                        //        gXyz = pf.Origin;

                        //        if (pf.FaceNormal.X == 0 && pf.FaceNormal.Y == 0 && pf.FaceNormal.Z > 0)
                        //        {

                        //            break;
                        //        }
                        //    }

                        //    if (rf != null)
                        //    {
                        //        XYZ rfXYZ=rf.get_Point(0);

                        //        gXyz = rfXYZ;
                        //        break;

                        //    }

                        //}

                    }
                }

                //List<GeometryElement> geometryElements = new List<GeometryElement>(); 

                //geometryElements.Add(geo);

                //CurveArray profile = new CurveArray();

                //foreach (var geometryle in geometryElements)
                //{
                //    foreach (var obj in geometryle)
                //    {

                //            profile.Append(obj as Curve);

                //    }
                //}

                foreach (XYZ pointForFind in findList) //пройтись по всем точкам
                {
                    XYZ newSearchPoint = pointForFind + zOfset;

                    myRoom = FindRoom(_doc, newSearchPoint);

                    if (myRoom != null)
                    {
                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                   myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                        element.LookupParameter("ADSK_Примечание").Set(nameRoom);
                        break;

                    }

                    else
                    {
                       
                       nameRoom = "неНашел";
                       element.LookupParameter("ADSK_Примечание").Set(nameRoom);
                       
                    }

                }


            }
        }

        public void WriteNameRoomCeillings(UIApplication uiApp)
        {
            var allWallElements = GetAllCeillingElements(_doc);

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


            foreach (var element in allWallElements)
            {
                Room myRoom;
                string nameRoom = "";

                XYZ gXyz = new XYZ(0, 0, 0);
                XYZ zOfset = new XYZ(0, 0, -4);

                XYZ pointLine = new XYZ(0, 0, 0);

                Ceiling floor = (Ceiling)element;

                ElementId eid = floor.Id;

                Options opts = new Options();

                List<List<XYZ>> polygons = new List<List<XYZ>>();
                List<XYZ> findList = new List<XYZ>();

                GeometryElement geo = floor.get_Geometry(opts);

                foreach (GeometryObject obj in geo) // 2013
                {
                    Solid solid = obj as Solid;
                    if (solid != null)
                    {
                        PlanarFace lowest = null;
                        FaceArray faces = solid.Faces; //faces

                        EdgeArray edgeArray = solid.Edges; //edges

                        foreach (Edge elEdgeArray in edgeArray)
                        {
                            pointLine = elEdgeArray.Tessellate()[0];

                            findList.Add(pointLine);

                            //if (g != null)
                            //{
                            //    pointLine = g.Tessellate()[0];
                            //    break;
                            //}



                        }

                        //foreach (Face f in faces)
                        //{

                        //    PlanarFace pf = f as PlanarFace;

                        //    RuledFace rf=f as RuledFace;

                        //    CylindricalFace cf=f as CylindricalFace;

                        //    if (pf != null)
                        //    {

                        //        gXyz = pf.Origin;

                        //        if (pf.FaceNormal.X == 0 && pf.FaceNormal.Y == 0 && pf.FaceNormal.Z > 0)
                        //        {

                        //            break;
                        //        }
                        //    }

                        //    if (rf != null)
                        //    {
                        //        XYZ rfXYZ=rf.get_Point(0);

                        //        gXyz = rfXYZ;
                        //        break;

                        //    }

                        //}

                    }
                }

                //List<GeometryElement> geometryElements = new List<GeometryElement>(); 

                //geometryElements.Add(geo);

                //CurveArray profile = new CurveArray();

                //foreach (var geometryle in geometryElements)
                //{
                //    foreach (var obj in geometryle)
                //    {

                //            profile.Append(obj as Curve);

                //    }
                //}

                foreach (XYZ pointForFind in findList) //пройтись по всем точкам
                {
                    XYZ newSearchPoint = pointForFind + zOfset;

                    myRoom = FindRoom(_doc, newSearchPoint);

                    if (myRoom != null)
                    {
                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();/*+ " " +
                                   myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();*/

                        element.LookupParameter("ADSK_Примечание").Set(nameRoom);
                        break;

                    }

                    else
                    {

                        nameRoom = "неНашел";
                        element.LookupParameter("ADSK_Примечание").Set(nameRoom);

                    }

                }


            }
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

            
            foreach (var wall in allWallElements)
            {

                LocationCurve wallLine = wall.Location as LocationCurve;

                IList<XYZ> wallPoints = wallLine.Curve.Tessellate();

                //Location loc = e.Location;

                //var locPoint = (LocationPoint)loc;

                var pointPF1 = wallPoints[0];
                var pointPF2 = wallPoints[1];


                var pointPF = (pointPF1 + pointPF2) / 2;


                var myRoom = FindRoom (_doc, pointPF);
                var nameRoom = "";

                if (myRoom != null)
                {
                    nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                   myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                    wall.get_Parameter(new Guid("e0c30580-14c7-4cb0-8be2-444179eb94c9"))
                        .Set(nameRoom);

                }
                else
                {
                    var linkedInstances = FindLinkedInstances();
                    foreach (RevitLinkInstance linkInstance in linkedInstances)
                    {
                        var getLinkDoc=linkInstance.GetLinkDocument();

                        if (getLinkDoc != null) { 
                        var transform = linkInstance.GetTotalTransform().Inverse;
                        var targetPoint = transform.OfPoint(pointPF);
                        var linkDocument = linkInstance.GetLinkDocument();

                        var room = FindRoom(linkDocument, targetPoint);
                        if (room != null)
                        {
                            //string[] subs = room.Name.Split(' ');
                            var stringLevel = room.get_Parameter(new Guid("1a6e5bca-2b14-4f06-b2bf-efce95bd92b9"))
                                    .AsDouble(); //yровень
                            var poGroupName = room.get_Parameter(new Guid("a1731d92-0650-40ed-8b23-0cb7e355dd59"))
                                    .AsString();//ПО_ргуппа
                            var numberName = room.get_Parameter(new Guid("e0c30580-14c7-4cb0-8be2-444179eb94c9"))
                                    .AsString();//ПО_номер

                                wall.get_Parameter(new Guid("e0c30580-14c7-4cb0-8be2-444179eb94c9"))
                                .Set(stringLevel + "." + poGroupName+ "."+ numberName);
                        }
                        else
                        {
                            nameRoom = "It's not in Room";
                            wall.get_Parameter(new Guid("e0c30580-14c7-4cb0-8be2-444179eb94c9"))
                                .Set(nameRoom);
                        }
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

        public IList<Element> GetAllFloorsElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

        public IList<Element> GetAllCeillingElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Ceilings)
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