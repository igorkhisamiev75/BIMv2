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
    public class VarificationWalls : IExternalCommand
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
            string bimError = "6feb836e-de15-4412-817e-f147e44fe0ea";

            foreach (var wall in allWallElements)
            {
                string currentNamber = wall.get_Parameter(new Guid(adskPrim)).AsValueString();
                string currentName =  wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsValueString();

                try
                {
                    LocationCurve wallLine = wall.Location as LocationCurve;

                    IList<XYZ> wallPoints = wallLine.Curve.Tessellate();

                    var pointPF1 = wallPoints[0];
                    XYZ pointPF2 = wallPoints[1];

                    var pointPF = (pointPF1 + pointPF2) / 2;

                    var myRoom = FindRoom(_doc, pointPF);
                    string nameRoom = "";
                    string numRoom = "";


                    if (myRoom != null)
                    {
                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                        numRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                        if (currentNamber == numRoom && currentName == nameRoom) 
                        {
                            wall.get_Parameter(new Guid(bimError)).Set("Верно");

                        }

                        if (currentNamber == numRoom && currentName != nameRoom)
                        {
                            wall.get_Parameter(new Guid(bimError)).Set("ПроверитьИменаПереписал");
                            wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                        }


                        if (currentNamber != numRoom && currentName == nameRoom)
                        {
                            wall.get_Parameter(new Guid(bimError)).Set("ПроверитьНомера");

                        }

                        if (currentNamber != numRoom && currentName != nameRoom)
                        {
                            wall.get_Parameter(new Guid(bimError)).Set("ПроверитьНомераИмена");

                        }


                    }
                    else
                    {
                        wall.get_Parameter(new Guid(bimError)).Set("ПроверитьРасположение");
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