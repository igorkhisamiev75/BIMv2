using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;


namespace CreateFinishWallinRooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreteFinishWalls : Window, IExternalCommand
    {
        // Private Members
        IList<WallType> m_wallTypeCollection;         // Store all the wall types in current document

        private Application _app;
        private Document _doc;
        //private ViewSet m_allViews = new ViewSet();

        public ICollection<View> viewsInProject = new List<View>();

        public ICollection<Element> roomsCollection = new List<Element>();

        public List<Level> levelCollection2 = new List<Level>();

        public List<Room> roomsCollection2 = new List<Room>();

        public List<String> roomsNameInProject = new List<String>();

        public ICollection<Element> filtersCollectionCopy = new List<Element>();

        public ICollection<Element> unusedFiltersCollection = new List<Element>();

        public IList<WallType> typeCollectionWall = new List<WallType>();

        public ICollection<int> filtersInViewsId = new List<int>();

        public int roomBoardingWall; //для границы помещений
        Level selectedLevelInForms; //выбор уровня
        double wallHeight=0;

        // Properties
        /// <summary>
        /// Inform all the wall types can be created in current document
        /// </summary>
        public IList<WallType> WallTypes
        {
            get
            {
                return m_wallTypeCollection;
            }
        }

        [Obsolete]
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            //m_wallTypeCollection = new IList<WallType>();

            // Search all level
            FilteredElementCollector filteredLevelCollector = new FilteredElementCollector(_doc);
            filteredLevelCollector.OfClass(typeof(Level));
            List<Level> levelCollection = filteredLevelCollector.Cast<Level>().ToList<Level>();

            levelCollection2 = levelCollection;

            //TaskDialog.Show("2bim.ru", $"{levelCollection.Count()}");

            // Search all the wall types in the Revit
            FilteredElementCollector filteredElementCollector = new FilteredElementCollector(_doc);
            filteredElementCollector.OfClass(typeof(WallType));
            m_wallTypeCollection = filteredElementCollector.Cast<WallType>().ToList<WallType>();


            roomsCollection = GetAllRooms(_doc);
           
            foreach (var rooms in roomsCollection)
            {
                roomsCollection2.Add((Room)rooms);
            }

            

            //удаление дубликатов, групирруем по имени помещения
            List<Room> roomGroup = roomsCollection2.GroupBy(x => 
            x.get_Parameter(BuiltInParameter.ROOM_NAME).AsString()).Select(x => x.First()).ToList();

            foreach(Room room in roomGroup)
            {
                roomsNameInProject.Add(room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString());
            }

            //create form 
            ViewWPF vpViewWpf = new ViewWPF(roomsNameInProject, WallTypes, levelCollection2);

            //show form
            vpViewWpf.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            vpViewWpf.ShowDialog();

            if (vpViewWpf.heighWallUser != "")
            {
                wallHeight = Convert.ToDouble(vpViewWpf.heighWallUser);
                wallHeight = UnitUtils.Convert(wallHeight,
                    DisplayUnitType.DUT_MILLIMETERS,
                    DisplayUnitType.DUT_DECIMAL_FEET);
                //TaskDialog.Show("2bim.ru", $"Высота стен {wallHeight.ToString()}");
            }
            

            //room boarding cheack box

            if (vpViewWpf.roomBoording == true)
            { roomBoardingWall = 1; }

            else { roomBoardingWall = 0; }

            //get selected walltype
            WallType selectedWallTpe = vpViewWpf.typeWallCollectionSelected;

            //get rooms checked
            List<String> checkRoomsString = vpViewWpf.checkRooms;

            //get selected level

            selectedLevelInForms = vpViewWpf.selectedLevel;
            //if(selectedLevelInForms != null)
            //{
            //    TaskDialog.Show("2bim.ru", $"Level name selected {selectedLevelInForms.Name}");
            //}
            


            List<Room> checkRooms=new List<Room>();

            foreach (Room room in roomsCollection2)
            {
                string nameRoome = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                var levelRoom = room.get_Parameter(BuiltInParameter.ROOM_LEVEL_ID).AsValueString();
                var levelSelect = selectedLevelInForms.Name;

                if (checkRoomsString.Contains(nameRoome) && levelSelect == levelRoom)
                {
                    checkRooms.Add(room);
                }
            }

            //whats button
            string whatsButton = vpViewWpf.whatsButton;

            bool rb = vpViewWpf.roomBoording;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                if (whatsButton == "отделкаВыборомПомещения")
                {

                    //TaskDialog.Show("2bim.ru", $"{rb.ToString()}");
                    Element elem = SelectedElement(_doc); //select room
                    try
                    {
                        CreateWallInRoom(elem, uiDoc, roomBoardingWall, selectedWallTpe);
                        TaskDialog.Show("2bim.ru", "Отделка стен создана ☺☺☺");
                    }

                    catch
                    {
                        //TaskDialog.Show("2bim.ru", "Отделка стен создана ☺☺☺");
                        //
                    }
                }

                if (whatsButton == "отделкаПоСпеке")
                {
                    foreach (Room room in checkRooms)
                    {
                        
                        try
                        {
                            Element element = (Element)room;
                            CreateWallInRoom(element, uiDoc, roomBoardingWall, selectedWallTpe);
                            _doc.Regenerate();

                            TaskDialog.Show("2bim.ru", "Отделка стен создана ☺☺☺");
                        }

                        catch
                        {
                            //TaskDialog.Show("2bim.ru", "Ошибка");
                            //TaskDialog.Show("2bim.ru", "Не сработало");
                        }
                    }
                }

                if (whatsButton == "удалитьОтделку")
                {
                    //TaskDialog.Show("2bim.ru", "3");
                    //Element elem = SelectedElement(_doc); //select room
                    try
                    {
                        //удалить отделку
                    }

                    catch
                    {
                        //TaskDialog.Show("2bim.ru", "Отделка стен создана ☺☺☺");
                        //TaskDialog.Show("2bim.ru", "Не сработало");
                    }
                }


                t.Commit();

            }

            return Result.Succeeded;
        }

        public void CreateWallInRoom(Element elem, UIDocument uiDoc, int _roomBoardingWall, WallType wallType)
        {
            string ADSK_Primechanie = "a85b7661-26b0-412f-979c-66af80b4b2c3";

            IList<Wall> walls = new List<Wall>();

            ElementId wallTypeId = wallType.GetTypeId();

            var elementIdWall = wallType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).Element.Id;

            CurveArray roomsCurves = new CurveArray();
                       
            Room room = (Room)elem;

            ElementId levelId = room.LevelId;
            double heightRoom= room.get_Parameter(BuiltInParameter.ROOM_HEIGHT).AsDouble(); 

            if (wallHeight != 0)
            {
                heightRoom = wallHeight;
            }
                     
            if (heightRoom < 0)
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
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(_roomBoardingWall);

                    //set room name and number
                    wall.get_Parameter(new Guid(ADSK_Primechanie)).Set(numberRoom);
                    wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                    //set height
                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(heightRoom);

                    //set type 
                    wall.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).Set(elementIdWall);


                    _doc.Regenerate();

                    //IList<Reference> sideFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);

                    //// MOVE WALL
                    //Face face = uiDoc.Document.GetElement(sideFaces[0])
                    //    .GetGeometryObjectFromReference(sideFaces[0]) as Face;
                    //PlanarFace pf = face as PlanarFace;
                    //XYZ normal_reverted = pf.FaceNormal;
                    //wall.Location.Move(-normal_reverted * (wall.WallType.Width / 2.0));

                    walls.Add(wall);
                    continue;
                }
                if (!g.IsCyclic)
                {

                    var wall = Wall.Create(_doc, g, levelId, false);
                    //Wall wall = Wall.Create(_doc, curves, wallTypeId, levelId, false);
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(_roomBoardingWall);
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
            //TaskDialog.Show("2bim.ru", "Все стены соеденены");
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
                            //TaskDialog.Show("2bim.ru", "разъеденены");

                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }


        }

        public Element SelectedElement(Document sDocument)
        {

            try
            {
                var uidoc = new UIDocument(_doc);

                //sDocument.Regenerate();

                var r = uidoc.Selection.PickObject(ObjectType.Element,
                    "Выбрать помещение");
                var elem = uidoc.Document.GetElement(r);
                return elem;

            }

            catch (Exception ex)
            {
                TaskDialog.Show("Revit", "Exception" + ex.Message);
                //subTransaction.RollBack();
                return null;
            }

        }

        //public WallType GetWallTypeInProject()
        //{
        //    WallType wallType;
        //    //// Get a floor type for floor creation
        //    FilteredElementCollector collector = new FilteredElementCollector(_doc);
        //    collector.OfClass(typeof(WallType));
        //    //wallType = collector.Where(x => x.Name == nameTypeWall) as WallType;
        //    wallType = collector.FirstOrDefault()  as WallType;
            

        //    return wallType;
        //}

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

        
        //public WallType GetWallTypeInProject(string nameTypeWall)
        //{
        //    WallType wallType;
        //    //// Get a floor type for floor creation
        //    FilteredElementCollector collector = new FilteredElementCollector(_doc);
        //    collector.OfClass(typeof(WallType));
        //    wallType = collector.Where(x => x.Name == nameTypeWall) as WallType;
        //    //as WallType;

        //    FilteredElementCollector a = new FilteredElementCollector(_doc).OfClass(typeof(WallType));
        //    foreach (WallType wallType2 in a)
        //    {
        //        if (wallType2.Name == nameTypeWall)
        //        {
        //            return wallType2;
        //        }
        //    }

        //    return wallType;
        //}
    }
}
