#region Namespace

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateFromDWG : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        public List<Room> projectRooms = new List<Room>();
       

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;


            using (var t = new Transaction(_doc))
            {
                t.Start("Create");
                
                IList<Reference> R2 = uiDoc.Selection.PickObjects(ObjectType.Element,  "SELECT ELEMENTS");

                //

                List<Element> elements1 = new List<Element>();

                if (R2 != null)
                {

                    foreach (var e in R2)
                    {
                        Element elem = uiDoc.Document.GetElement(e);
                        elements1.Add(elem);
                    }


                }

                Reference r = uiDoc.Selection.PickObject(ObjectType.Element, "Выбрать сплинклер");
                Element elem3 = uiDoc.Document.GetElement(r);


                CreateElement(elements1, elem3);
                //CreateAnyElementInLinkedFiles(elem);

                TaskDialog.Show("Complite!", $"Размещено {elements1.Count} элементов");
                t.Commit();

            }

            return Result.Succeeded;
        }

        private void CreateElement(List<Element> elemList, Element elementSplinkler)
        {


            FamilyInstance familyInstance = elementSplinkler as FamilyInstance;
            FamilySymbol familySymbol = familyInstance.Symbol;

            var level2 = familyInstance.LevelId;

            Level level1 = GetLevelFromRoom(level2);

            foreach (var element in elemList) { 
            
                ImportInstance importInstance = element as ImportInstance;

                if (importInstance != null) {

                    //var transf = importInstance.GetTransform().Origin;
                    XYZ transf = (element.get_BoundingBox( _doc.ActiveView).Min+ element.get_BoundingBox(_doc.ActiveView).Max)/2;



                    FamilyInstance instLight = _doc.Create.NewFamilyInstance(
                              transf, familySymbol, level1, StructuralType.NonStructural);

                }

            }

            //var Rooms = GetAllRooms();

            //if (Rooms != null)
            //{
            //    int i = 0;
            //    foreach (var room in Rooms)
            //    {
            //        var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
            //        var volumeRoom = room.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
            //        var heighRoom = volumeRoom / areaRoom;

            //        if (areaRoom != 0)
            //        {
            //            var level = room.LevelId;

            //            Level level1 = GetLevelFromRoom(level);

            //            LocationPoint lpPoint = (LocationPoint)room.Location;

            //            XYZ pointFoLigh = lpPoint.Point;

            //            XYZ placeXyzPoint = new XYZ(pointFoLigh.X, pointFoLigh.Y, heighRoom);

            //            FamilyInstance instLight = _doc.Create.NewFamilyInstance(
            //                    placeXyzPoint, familySymbol, level1, StructuralType.NonStructural);
            //            i++;
            //        }

            //    }


            //}

        }

        private void CreateAnyElementInLinkedFiles(Element elem)
        {
            FamilyInstance familyInstance = elem as FamilyInstance;
            FamilySymbol familySymbol = familyInstance.Symbol;

            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

            if (linkedInstances != null)
            {
                int j = 0;
                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                    Transform transform = linkInstance.GetTotalTransform();

                    List<Room> roomsLink = GetAllRoomsLinked(linkDocument);

                    if (roomsLink != null)
                    {
                        foreach (var room in roomsLink)
                        {

                            var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                            var volumeRoom = room.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                            var heightRoom = volumeRoom / areaRoom;

                            if (areaRoom != 0)
                            {
                                var level = room.LevelId;

                                Level level1 = GetLevelFromRoom(level);

                                if (level1 == null)
                                {
                                    level1 = GetLevelFromRoomByName(room);
                                }

                                LocationPoint lpPoint = (LocationPoint)room.Location;

                                XYZ pointFoLigh = lpPoint.Point;

                                var targetPoint = transform.OfPoint(pointFoLigh);

                                XYZ placeXyzPoint = new XYZ(targetPoint.X, targetPoint.Y, heightRoom);

                                FamilyInstance instLight = _doc.Create.NewFamilyInstance(
                                        placeXyzPoint, familySymbol, level1, StructuralType.NonStructural);
                                j++;
                            }


                        }
                    }


                }
              
            }

        }

        private Level GetLevelFromRoom(ElementId elementId)
        {
            FilteredElementCollector lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (Element l in lvlCollection)
            {
                if (l.Id == elementId)
                {
                    return (Level)l;
                }

            }

            return null;

        }

        private Level GetLevelFromRoomByName(Room room)
        {
            FilteredElementCollector lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (Element l in lvlCollection)
            {
                if (l.Name == room.Level.Name)
                {
                    return (Level)l;
                }

            }
            return null;

        }



        public IList<Element> GetAllRooms()
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
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

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }


    }
}