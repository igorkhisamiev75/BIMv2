using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Autodesk.Revit.DB.Architecture;

namespace CreateFinishWallinRooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreteFinishWalls : Window, IExternalCommand
    {
        private Application _app;
        private Document _doc;
        private ViewSet m_allViews = new ViewSet();

        public ICollection<View> viewsInProject = new List<View>();

        public ICollection<Element> roomsCollection = new List<Element>();

        public List<Room> roomsCollection2 = new List<Room>();

        public ICollection<Element> filtersCollectionCopy = new List<Element>();

        public ICollection<Element> unusedFiltersCollection = new List<Element>();

        public ICollection<View> onlyViewPlan3DSection = new List<View>();

        public ICollection<int> filtersInViewsId = new List<int>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            roomsCollection = GetAllRooms(_doc);

            foreach(var rooms in roomsCollection)
            {
                roomsCollection2.Add((Room)rooms);
            }

            ViewWPF vpViewWpf = new ViewWPF(roomsCollection2);

            vpViewWpf.ShowDialog();

            var checkRooms = vpViewWpf.checkRooms;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                //create finish
                CreateWallInRooms(checkRooms, uiDoc);
                t.Commit();

            }

            return Result.Succeeded;
        }

        public void CreateWallInRooms(List<Room> roomsCollection2, UIDocument uiDoc)
        {
            string ADSK_Primechanie = "a85b7661-26b0-412f-979c-66af80b4b2c3";

            IList<Wall> walls = new List<Wall>();

            WallType wallType;
            IList<Element> rooms;
            wallType = GetWallTypeInProject("Отделка");

            ElementId wallTypeId = wallType.GetTypeId();

            var elementIdWall = wallType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).Element.Id;

            CurveArray roomsCurves = new CurveArray();
            Room room = roomsCollection2[0];

            ElementId levelId = room.LevelId;
            double heightRoom = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT).AsDouble();

            if (heightRoom <= 0)
            {
                heightRoom = 1;
            }
            string numberRoom = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();
            string nameRoom = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

            IList<Curve> curves = new List<Curve>();

            SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> segments2 = room.GetBoundarySegments(bo);


            //CurveArray temp = new CurveArray();

            if (null != segments2)
            {

                foreach (IList<BoundarySegment> segment in segments2)
                {
                    IList<BoundarySegment> segmentList = segment;


                    foreach (BoundarySegment boundarySegment in segmentList)
                    {
                        Curve c = boundarySegment.GetCurve();
                        roomsCurves.Append(c);
                    }
                }


            }

            foreach (Curve g in roomsCurves)
            {
                if (g == null) continue;
                if (g.IsCyclic)
                {
                    var wall = Wall.Create(_doc, g, levelId, false);

                    //set bounding граница помещения
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(1);

                    //set room name and number
                    wall.get_Parameter(new Guid(ADSK_Primechanie)).Set(numberRoom);
                    wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                    //set height
                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(heightRoom);

                    //set type 
                    wall.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).Set(elementIdWall);


                    _doc.Regenerate();


                    walls.Add(wall);
                    continue;
                }
                if (!g.IsCyclic)
                {

                    var wall = Wall.Create(_doc, g, levelId, false);
                    //Wall wall = Wall.Create(_doc, curves, wallTypeId, levelId, false);
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(1);
                    //wall.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).Set(wallTypeId);
                    wall.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).Set(elementIdWall);
                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(heightRoom);
                    wall.get_Parameter(new Guid(ADSK_Primechanie)).Set(numberRoom);
                    wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                    _doc.Regenerate();

                    IList<Reference> sideFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);
                    // MOVE WALL
                    Face face = uiDoc.Document.GetElement(sideFaces[0])
                        .GetGeometryObjectFromReference(sideFaces[0]) as Face;
                    PlanarFace pf = face as PlanarFace;
                    XYZ normal_reverted = pf.FaceNormal;
                    wall.Location.Move(-normal_reverted * (wall.WallType.Width / 2.0));

                    walls.Add(wall);

                }
            }

            //join walls
            foreach (var wall1 in walls)
            {
                foreach (var wall2 in walls)
                {
                    if (wall1 != wall2)
                    {
                        try
                        {
                            if (!JoinGeometryUtils.AreElementsJoined(_doc, wall1, wall2))
                            {
                                JoinGeometryUtils.JoinGeometry(_doc, wall1, wall2);
                                //_doc.Regenerate();
                                //JoinGeometryUtils.IsCuttingElementInJoin(_doc, wall1, wall2)||
                            }
                            else
                            {
                                continue;

                            }
                        }
                        catch { continue; }

                    }

                    else { continue; }

                }

                //TaskDialog.Show("2bim.ru", "Все стены соеденены");

            }
            TaskDialog.Show("2bim.ru", "Все стены соеденены");
            _doc.Regenerate();
            //unjoin walls
            foreach (var wall1 in walls)
            {
                foreach (var wall2 in walls)
                {
                    if (wall1 != wall2 && JoinGeometryUtils.IsCuttingElementInJoin(_doc, wall1, wall2))
                    {
                        if (JoinGeometryUtils.AreElementsJoined(_doc, wall1, wall2))
                        {
                            JoinGeometryUtils.UnjoinGeometry(_doc, wall1, wall2);
                            TaskDialog.Show("2bim.ru", "разъеденены");

                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }


        }

        public WallType GetWallTypeInProject(string nameTypeWall)
        {
            WallType wallType;
            //// Get a floor type for floor creation
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(WallType));
            wallType = collector.Where(x => x.Name == nameTypeWall) as WallType;
            //as WallType;

            FilteredElementCollector a = new FilteredElementCollector(_doc).OfClass(typeof(WallType));
            foreach (WallType wallType2 in a)
            {
                if (wallType2.Name == nameTypeWall)
                {
                    return wallType2;
                }
            }

            return wallType;
        }

        //get all rooms
        public IList<Element> GetAllRooms(Document doc)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

       
    }
}
