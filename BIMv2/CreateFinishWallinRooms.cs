#region Namespaces
using System;
using System.Collections.Generic;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]

    public class CreateFinishWallinRooms : IExternalCommand
    {
        Application _app;
        Document _doc;
        private const double PRECISION = 0.00000001;
        private CurveArray profile;
        //private Hashtable floorTypes;
        private List<string> floorTypesName;
        private FloorType floorType;
        private Level level;
        private bool structural;
        private Autodesk.Revit.Creation.Application creApp;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            creApp = commandData.Application.Application.Create;


            using (Transaction t = new Transaction(_doc))
            {
                t.Start("Create finish walls");

                Element elem = SelectedElement(_doc); //select room

                CreateWallInRooms(elem);

                TaskDialog.Show("У тебя получилось", "Стены созданы ☺☺☺");
                t.Commit();

                //t.Start();
                //CreateFloorOpenings(elem);

                //var res = t.Commit();

            }

            return Result.Succeeded;

        }


        // a floor type to be used by the new floor instead of the default type
        public FloorType FloorType
        {
            get
            {
                return floorType;
            }
            set
            {
                floorType = value;
            }
        }

        // the Level on which the floors are to be placed
        public Level Level
        {
            get
            {
                return level;
            }
            set
            {
                level = value;
            }
        }

        // an array of planar lines and arcs that represent the horizontal profile of the floor
        public CurveArray Profile
        {
            get
            {
                return profile;
            }
            set
            {
                profile = value;
            }
        }

        // determine wether the floor is structural
        public bool Structural
        {
            get
            {
                return structural;
            }
            set
            {
                structural = value;
            }
        }

        // a list of all Floor Types Name that could be used by the new Floor
        public List<string> FloorTypesName
        {
            get
            {
                return floorTypesName;
            }
            set
            {
                floorTypesName = value;
            }
        }



        public void CreateFloorOpenings(Element elem)
        {

            WallType floorType;
            IList<Element> rooms;
            GetRoomInProject(out rooms);

            // List all the floors instances 
            var wallCollector = new FilteredElementCollector(_doc).OfClass(typeof(Wall));
            IList<Element> wallList = wallCollector.ToElements();


            CurveArray roomsCurves = new CurveArray();
            Room r1 = (Room)elem;
            Wall f1 = (Wall)wallList[0];

            SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> segments2 = r1.GetBoundarySegments(bo);

            CurveArray temp = new CurveArray();

            if (null != segments2)
            {
                for (int j = 1; j < segments2.Count; j++)
                {
                    IList<BoundarySegment> segmentList = segments2[j];

                    foreach (BoundarySegment boundarySegment in segmentList)
                    {
                        Curve c = boundarySegment.GetCurve();
                        temp.Append(c);
                    }
                    SortCurves(temp);
                    var opening = _doc.Create.NewOpening(f1, temp, true); //create opening
                }

            }



        }

        public void CreateWallInRooms(Element elem)
        {

            WallType wallType;
            IList<Element> rooms;
            wallType = GetWallTypeInProject();
            Floor sourr;
            Floor sourr2;

            //GetRoomInProject(out rooms);


            CurveArray roomsCurves = new CurveArray();
            Room room = (Room)elem;

            ElementId levelId = room.LevelId;
            double heightRoom = room.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET).AsDouble();
            string nameRoom = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();


            SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> segments2 = room.GetBoundarySegments(bo);


            CurveArray temp = new CurveArray();

            if (null != segments2)
            {

                foreach (IList<BoundarySegment> segment in segments2)
                {
                    IList<BoundarySegment> segmentList = segment;


                    foreach (BoundarySegment boundarySegment in segmentList)
                    {
                        Curve c = boundarySegment.GetCurve();
                        temp.Append(c);
                    }
                }
                
                

                //SortCurves(temp);

                //sourr = _doc.Create.NewFloor(temp, floorType, r1.Level, false);
            }

            foreach(Curve g in temp)
            {
                if (g == null) continue;
                if (g.IsCyclic )
                {
                    var wall=Wall.Create(_doc, g, levelId, false);
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).AsInteger();

                    //set bounding
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);

                    //set room name

                    wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);

                    //wall.LookupParameter("Room Bounding").Set(0);

                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(heightRoom);
                }

                if (!g.IsCyclic)
                {
                    var wall = Wall.Create(_doc, g, levelId, false);
                    //wall.LookupParameter("Room Bounding").Set(0);
                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);
                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(heightRoom);
                    wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(nameRoom);
                }
            }


        }

        public void GetRoomInProject(out IList<Element> rooms)
        {


            // The normal vector (0,0,1) that must be perpendicular to the profile.
            //XYZ normal = XYZ.BasisZ;

            //Get room's 
            RoomFilter filter = new RoomFilter();
            // Apply the filter to the elements in the active document
            FilteredElementCollector collector3 = new FilteredElementCollector(_doc);
            rooms = collector3.WherePasses(filter).ToElements();
        }

        public WallType GetWallTypeInProject()
        {
            WallType wallType;
            //// Get a floor type for floor creation
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(WallType));
            wallType = collector.FirstElement() as WallType;
            return wallType;
        }

        public void SortCurves(CurveArray lines)
        {
            XYZ pointCurves = lines.get_Item(0).GetEndPoint(1);
            Curve temCurve = lines.get_Item(0);


            Profile = creApp.NewCurveArray();

            Profile.Append(temCurve);

            while (Profile.Size != lines.Size)
            {

                temCurve = GetNext(lines, pointCurves, temCurve);

                if (Math.Abs(pointCurves.X - temCurve.GetEndPoint(0).X) < PRECISION
                 && Math.Abs(pointCurves.Y - temCurve.GetEndPoint(0).Y) < PRECISION)
                {
                    pointCurves = temCurve.GetEndPoint(1);
                }
                else
                {
                    pointCurves = temCurve.GetEndPoint(0);
                }

                Profile.Append(temCurve);
            }

        }
        public Curve GetNext(CurveArray profile, XYZ connected, Curve line)
        {
            foreach (Curve c in profile)
            {
                if (c.Equals(line))
                {
                    continue;
                }
                if ((Math.Abs(c.GetEndPoint(0).X - line.GetEndPoint(1).X) < PRECISION &&
                    Math.Abs(c.GetEndPoint(0).Y - line.GetEndPoint(1).Y) < PRECISION &&
                    Math.Abs(c.GetEndPoint(0).Z - line.GetEndPoint(1).Z) < PRECISION) &&
                    (Math.Abs(c.GetEndPoint(1).X - line.GetEndPoint(0).X) < PRECISION &&
                    Math.Abs(c.GetEndPoint(1).Y - line.GetEndPoint(0).Y) < PRECISION &&
                    Math.Abs(c.GetEndPoint(1).Z - line.GetEndPoint(0).Z) < PRECISION) &&
                    2 != profile.Size)
                {
                    continue;
                }

                if (Math.Abs(c.GetEndPoint(0).X - connected.X) < PRECISION &&
                    Math.Abs(c.GetEndPoint(0).Y - connected.Y) < PRECISION &&
                    Math.Abs(c.GetEndPoint(0).Z - connected.Z) < PRECISION)
                {
                    return c;
                }
                else if (Math.Abs(c.GetEndPoint(1).X - connected.X) < PRECISION &&
                         Math.Abs(c.GetEndPoint(1).Y - connected.Y) < PRECISION &&
                         Math.Abs(c.GetEndPoint(1).Z - connected.Z) < PRECISION)
                {
                    if (c.GetType().Name.Equals("Line"))
                    {
                        XYZ start = c.GetEndPoint(1);
                        XYZ end = c.GetEndPoint(0);
                        return Line.CreateBound(start, end);
                    }
                    else if (c.GetType().Name.Equals("Arc"))
                    {
                        int size = c.Tessellate().Count;
                        XYZ start = c.Tessellate()[0];
                        XYZ middle = c.Tessellate()[size / 2];
                        XYZ end = c.Tessellate()[size];

                        return Arc.Create(start, end, middle);
                    }
                }
            }
            throw new InvalidOperationException("The Room Boundary should be closed.");
        }

        public Element SelectedElement(Document sDocument)
        {
            
            try
            {
                

                var uidoc = new UIDocument(_doc);

                //sDocument.Regenerate();

                var r = uidoc.Selection.PickObject(ObjectType.Element,
                    "Выбрать элемент для размещения");
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
    }

}

