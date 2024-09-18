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
    public class FindNameRoomForFloor : IExternalCommand
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

                ////найти для перекрытий помещения
                WriteNameRoomFloors(uiApp);

                TaskDialog.Show("У тебя получилось", "☺☺☺");

                t.Commit();

            }
            return Result.Succeeded;
        }
                
        public void WriteNameRoomFloors(UIApplication uiApp)
        {
            var allWallElements = GetAllFloorsElements(_doc);

            //отделка только //мжно в запросе сразу попробовать отфильтровать
            foreach (var element in allWallElements)
            {
                try
                {
                    Wall wall = (Wall)element;

                    string descriptionWallType = wall.WallType.LookupParameter("Описание").AsString();

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


            foreach (var element in allWallElements)
            {
                var nameWall = element.LookupParameter("ADSK_Примечание").AsString();

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


                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                        element.LookupParameter("Комментарии").Set(nameRoom);

                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                        element.LookupParameter("ADSK_Примечание").Set(nameRoom);
                        break;

                    }

                    else
                    {

                        //nameRoom = "неНашел";


                        //nameRoom = "неНашел";
                        //element.LookupParameter("Комментарии").Set(nameRoom);
                        //element.LookupParameter("ADSK_Примечание").Set(nameRoom);

                    }

                }


            }
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