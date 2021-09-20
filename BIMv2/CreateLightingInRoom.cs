#region Namespace

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateLightingInRoom : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        //private List<Room> projectRooms;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            

            using (var t = new Transaction(_doc))
            { 
                t.Start("Create");

                CreateLightingFixtreInRooms();

                TaskDialog.Show("Complite!", "Complite!  ☺☺☺");
                t.Commit();
               
            }

            return Result.Succeeded;
        }

        private void CreateLightingFixtreInRooms()
        {
            FilteredElementCollector collector= new FilteredElementCollector(_doc);
            List<ElementId> _added_element_ids= new List<ElementId>();
            collector.OfCategory(BuiltInCategory.OST_LightingFixtures);
            collector.OfClass(typeof(FamilySymbol));

            FamilySymbol symbol = collector.FirstElement() as FamilySymbol;
            
            var _rooms=GetAllRooms();
            if (_rooms != null)
            {
                foreach (var elementRoom in _rooms)
                {
                    var a = elementRoom.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                    var b = elementRoom.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                    var h = b / a;

                    var level = elementRoom.LevelId;

                    Level level1 = GetLevelFromRoom(level);

                    LocationPoint lpPoint = (LocationPoint)elementRoom.Location;

                    XYZ pointFoLigh = lpPoint.Point;

                    XYZ placeXyzPoint = new XYZ(pointFoLigh.X, pointFoLigh.Y, h);

                    FamilyInstance instLight = _doc.Create.NewFamilyInstance(
                            placeXyzPoint, symbol, level1, StructuralType.NonStructural);

                }
            }
            

            var linkedInstances = FindLinkedInstances();

            foreach (var linkInstance in linkedInstances)
            {
                var linkDocument = linkInstance.GetLinkDocument();

                var roomLink=GetAllRoomsLinked(linkDocument);

                if (roomLink != null)
                {
                    foreach (var elementRoom in roomLink)
                    {
                        var a = elementRoom.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                        var b = elementRoom.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                        var h = b / a;

                        var level = elementRoom.LevelId;

                        Level level1 = GetLevelFromRoom(level);

                        LocationPoint lpPoint = (LocationPoint)elementRoom.Location;

                        XYZ pointFoLigh = lpPoint.Point;

                        XYZ placeXyzPoint = new XYZ(pointFoLigh.X, pointFoLigh.Y, h);

                        FamilyInstance instLight = _doc.Create.NewFamilyInstance(
                                placeXyzPoint, symbol, level1, StructuralType.NonStructural);

                    }
                }

            }

        }


        private Level GetLevelFromRoom(ElementId elementId)
        {
            FilteredElementCollector lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach(Element l in lvlCollection)
            {
                if (l.Id == elementId)
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
            List<Room> projectRooms=null;
            FilteredElementCollector roomCollect = new FilteredElementCollector(document);
            roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
            Room room = null;

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