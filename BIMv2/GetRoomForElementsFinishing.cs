#region Namespace
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class GetRoomForElementsFinishing : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        public List<Room> projectRooms = new List<Room>();
        public List<Room> allRooms = new List<Room>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            using (var t = new Transaction(_doc))
            {
                t.Start("Стены");
                WriteNameRoomInElementMethod(uiApp);
                TaskDialog.Show("У тебя получилось", "Complite!!!☺☺☺");
                t.Commit();
                //t.Start();
                //TransactionStatus res = t.Commit();
            }
            return Result.Succeeded;
        }
        public void WriteNameRoomInElementMethod(UIApplication uiApp)
        {
            //get all Walls
            var allElements = GetAllElements(_doc);

            //TaskDialog.Show("2bim.ru", $"{allElements.Count} Стен");


            //get all linked rooms
            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

            if (linkedInstances != null)
            {
                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                    //Transform transform = linkInstance.GetTotalTransform();

                    List<Room> roomsLink = GetAllRoomsLinked(linkDocument);

                    allRooms.AddRange(roomsLink);
                }
                
                
            }
            
           //find collisions

            foreach (Element e in allElements)
            {
                BoundingBoxXYZ boundingBoxXYZ = e.get_BoundingBox(null);

                foreach (Room item in allRooms)
                {
                    if (item != null)
                    {

                    BoundingBoxXYZ boundingBoxXYZ2 = item.get_BoundingBox(null);
                    var nameRoom = item.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                item.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                        if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ2))
                    {
                        try
                        {
                               
                            //ПО_№_Помещения
                            e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(nameRoom);
                              //_doc.Regenerate();
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException a)
                        {

                            //Debug.Print("Ошибка" + a.ToString());
                        }


                    }
                }

                }
                
            }
        }

        public bool bbIntersect(BoundingBoxXYZ BoxXYZ, BoundingBoxXYZ BoxXYZ2)
        {
            if (BoxXYZ != null && BoxXYZ2 != null)
            {
                Outline outline1 = new Outline(BoxXYZ.Min, BoxXYZ.Max);
                Outline outline2 = new Outline(BoxXYZ2.Min, BoxXYZ2.Max);

                if (outline1.Intersects(outline2, 0.0001) || outline2.Intersects(outline1, 0.0001))
                {
                    return true;
                }
            }

            return true;

        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        public List<Room> GetAllRoomsLinked(Document document)
        {

            FilteredElementCollector roomCollect = new FilteredElementCollector(document);
            roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
            Room room = null;
            projectRooms = new List<Room>();

            foreach (Element elem in roomCollect)
            {
                room = elem as Room;
                if (room != null)
                {
                    projectRooms.Add(room);
                }
            }
            return projectRooms;
        }
        public IList<Element> GetAllElements(Document _doc)
        {
            // List all 
            var floor_collector = new FilteredElementCollector(_doc)
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