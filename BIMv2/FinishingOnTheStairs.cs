#region Namespaces
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using Floor = Autodesk.Revit.DB.Floor;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]

    public class FinishingOnTheStairs : IExternalCommand
    {
        Application _app;
        Document _doc;
        private const double PRECISION = 0.00000001;
        private CurveArray profile;
        //private Hashtable floorTypes;
        private List<string> floorTypesName;
        public FloorType floorType;
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
                t.Start("Create floor");

                //создаем перекрытие на марше
                

                FloorType floorType = GetFloorTypeInProject();

                Stairs str =GetStairInfo(_doc);

                ElementId lv = str.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsElementId();

                CurveArray temp = new CurveArray();

                Level level = (Level)_doc.GetElement(lv);

                ICollection<ElementId> landings = str.GetStairsLandings();
                ICollection<ElementId> runs = str.GetStairsRuns();

                double elevation=0;

                foreach (ElementId landing in landings)
                {
                    StairsLanding stairsL = _doc.GetElement(landing) as StairsLanding;
                    var cl=stairsL.GetFootprintBoundary();

                    elevation = stairsL.BaseElevation;

                    foreach (var item in cl)
                    {
                        temp.Append(item);
                    }

                }

                var nf = _doc.Create.NewFloor(temp, floorType, level, false);
                nf.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(elevation);


                //создаем стены

                WallType wallType;
                IList<Element> rooms;
                wallType = GetWallTypeInProject("Отделка");

                foreach (var staitRun in runs)
                {
                    StairsRun stairsRun = _doc.GetElement(staitRun) as StairsRun;

                    var hightStar = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RISER_HEIGHT).AsDouble();

                    Options options = new Options();

                    GeometryElement geom = stairsRun.get_Geometry(options);

                    //рамка марша
                    CurveLoop cl=stairsRun.GetFootprintBoundary();

                    //IList<Curve> curves = new List<Curve>();

                    foreach (var item in cl)
                    {
                        //curves.Add(item);

                        var wall = Wall.Create(_doc, item, lv, false);
                        wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(hightStar);
                        //Line line = item as Line;

                        //var tre = line.Tessellate();

                    }

                    

                }


                TaskDialog.Show("Лестница", "Лестница ☺☺☺");
                t.Commit();


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

        

        
       
        public void CreateFloorInRooms(Element elem)
        {

            FloorType floorType = GetFloorTypeInProject();

            CurveArray roomsCurves = new CurveArray();
            Room r1 = (Room)elem;

            SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> segments2 = r1.GetBoundarySegments(bo);

            CurveArray temp = new CurveArray();

            if (null != segments2)
            {
                IList<BoundarySegment> segmentList = segments2[0];

                foreach (BoundarySegment boundarySegment in segmentList)
                {
                    Curve c = boundarySegment.GetCurve();
                    temp.Append(c);
                }
                SortCurves(temp);
                _ = _doc.Create.NewFloor(temp, floorType, r1.Level, false);


            }


        }

       

        public FloorType GetFloorTypeInProject()
        {
            // Search all the floor types in the Revit
            FilteredElementCollector filteredElementCollectorFloor = new FilteredElementCollector(_doc);
            filteredElementCollectorFloor.OfClass(typeof(FloorType));
            var m_FloorTypeCollection = filteredElementCollectorFloor.Cast<FloorType>().ToList<FloorType>();
            FloorType g = m_FloorTypeCollection[3];
            return g;

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

        public Stairs SelectedElement(Document sDocument)
        {
           
            try
            {
                
                var uidoc = new UIDocument(_doc);

               
                var r = uidoc.Selection.PickObject(ObjectType.Element,
                    "Выбрать лестницу");
                var elem = uidoc.Document.GetElement(r);


                return (Stairs)elem;
                
            }

            catch (Exception ex)
            {
                TaskDialog.Show("Revit", "Exception" + ex.Message);
                
                return null;
            }


        }

        private Stairs GetStairInfo(Document document)
        {
            Stairs stairs = null;

            FilteredElementCollector collector = new FilteredElementCollector(document);
            ICollection<ElementId> stairsIds = collector.WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_Stairs).ToElementIds();

            

            foreach (ElementId stairId in stairsIds)
            {
                if (Stairs.IsByComponent(document, stairId) == true)
                {
                    stairs = document.GetElement(stairId) as Stairs;

                    // Format the information
                    String info = "\nNumber of stories:  " + stairs.NumberOfStories;
                    info += "\nHeight of stairs:  " + stairs.Height;
                    info += "\nNumber of treads:  " + stairs.ActualTreadsNumber;
                    info += "\nTread depth:  " + stairs.ActualTreadDepth;

                    // Show the information to the user.
                    //TaskDialog.Show("Revit", info);
                }
            }

            return stairs;
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


    }

}

