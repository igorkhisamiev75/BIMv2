#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;

using Application = Autodesk.Revit.ApplicationServices.Application;
using Floor = Autodesk.Revit.DB.Floor;
#endregion
namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class VarificationFloors : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        public List<Autodesk.Revit.DB.Floor> AllFinishWall = new List<Autodesk.Revit.DB.Floor>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("BIM_Error for floors");

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

            string adskPrim = "a85b7661-26b0-412f-979c-66af80b4b2c3";
            string bimError = "6feb836e-de15-4412-817e-f147e44fe0ea";

            foreach (var element in allWallElements)
            {
                string currentNamber = element.get_Parameter(new Guid(adskPrim)).AsValueString();
                string currentName = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsValueString();

                Room myRoom;
                string nameRoom = "";
                string numRoom = "";

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

                        }

                
                    }
                }


                foreach (XYZ pointForFind in findList) //пройтись по всем точкам
                {
                    XYZ newSearchPoint = pointForFind + zOfset;

                    myRoom = FindRoom(_doc, newSearchPoint);

                    if (myRoom != null)
                    {
                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                        numRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                        if (currentNamber == numRoom && currentName == nameRoom)
                        {
                            element.get_Parameter(new Guid(bimError)).Set("Верно");

                        }

                        if (currentNamber == numRoom && currentName != nameRoom)
                        {
                            element.get_Parameter(new Guid(bimError)).Set("ПроверитьИменаПереписал");
                            element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                        }


                        if (currentNamber != numRoom && currentName == nameRoom)
                        {
                            element.get_Parameter(new Guid(bimError)).Set("ПроверитьНомера");

                        }

                        if (currentNamber != numRoom && currentName != nameRoom)
                        {
                            element.get_Parameter(new Guid(bimError)).Set("ПроверитьНомераИмена");

                        }


                        break;

                    }

                    else
                    {
                        element.get_Parameter(new Guid(bimError)).Set("ПроверитьРасположение");

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