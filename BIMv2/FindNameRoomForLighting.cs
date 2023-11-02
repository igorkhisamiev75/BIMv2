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
    public class FindNameRoomForLighting : IExternalCommand
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
                t.Start("Find name room for lighting");

                //WriteNameRoomWall(uiApp);

                WriteNameRoom(uiApp);
                //потолки
                //светильники
                //мебель?
                //комплекты мебели,
                //окна
                //сантех.прибор
                //ограждение?
                //перекрытие

                TaskDialog.Show("У тебя получилось", "Complite2!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }
        public void WriteNameRoom(UIApplication uiApp)
        {
            var allElements = GetAllLightElements(_doc);

            foreach (var e in allElements)
            {
                var loc = e.Location;
                var locPoint = (LocationPoint)loc;
                var pointPF = locPoint.Point;
                var myRoom = FindRoom(_doc, pointPF);
                var nameRoom = "";

                if (myRoom != null)
                {
                    nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                    e.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038"))
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

                        if(room != null)
                        {
                            if (room != null)
                            {
                                string[] subs = room.Name.Split(' ');

                                e.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038"))
                                    .Set(room.Number);
                            }
                            else
                            {
                                nameRoom = "It's not in Room";
                                e.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038"))
                                    .Set(nameRoom);
                            }
                        }

                        
                       

                    }
                }
            }
        }
        

        public IList<Element> GetAllLightElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
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