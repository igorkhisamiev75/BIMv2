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
    //[RegenerationAttribute(RegenerationOption.Manual)]
    public class CreateFloorInRoom : IExternalCommand
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

                Element elem = SelectedElement(_doc); //select room

                CreateFloorInRooms(elem);

                TaskDialog.Show("У тебя получилось", "Ты красавчик!☺☺☺");
                t.Commit();

                t.Start();
                CreateFloorOpenings();

                var res = t.Commit();

            }

            return Result.Succeeded;

        }

        private void CreateFloorOpenings()
        {

            FloorType floorType;
            IList<Element> rooms;
            GetRoomInProject(out rooms);

            // List all the floors instances 
            var floorCollector = new FilteredElementCollector(_doc).OfClass(typeof(Floor));
            IList<Element> floorList = floorCollector.ToElements();

            for (int i = 0; i < rooms.Count; i++)
            {
                CurveArray roomsCurves = new CurveArray();
                var r1 = (Room)rooms[i];
                var f1 = (Floor)floorList[i];

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

        }

        private void CreateFloorInRooms(Element elem)
        {

            FloorType floorType;
            IList<Element> rooms;
            floorType = GetFloorTypeInProject();
            Floor sourr;
            Floor sourr2;

            GetRoomInProject(out rooms);

            
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
                    sourr = _doc.Create.NewFloor(temp, floorType, r1.Level, false);

                }


        }

        private void GetRoomInProject(out IList<Element> rooms)
        {


            // The normal vector (0,0,1) that must be perpendicular to the profile.
            //XYZ normal = XYZ.BasisZ;

            //Get room's 
            RoomFilter filter = new RoomFilter();
            // Apply the filter to the elements in the active document
            FilteredElementCollector collector3 = new FilteredElementCollector(_doc);
            rooms = collector3.WherePasses(filter).ToElements();
        }

        private FloorType GetFloorTypeInProject()
        {
            FloorType floorType;
            //// Get a floor type for floor creation
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(FloorType));
            floorType = collector.FirstElement() as FloorType;
            return floorType;
        }

        private void SortCurves(CurveArray lines)
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
        private Curve GetNext(CurveArray profile, XYZ connected, Curve line)
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
            //var document = m_revit.ActiveUIDocument.Document;
            //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
            //var elem = document.GetElement(earthId);
            //uiDoc = m_revit.ActiveUIDocument;
            //using (Transaction subTransaction = new Transaction(sDocument, Guid.NewGuid().GetHashCode().ToString()))
            //using (SubTransaction subTransaction = new SubTransaction(sDocument))
            //{
            //subTransaction.Start();
            try
            {
                /*if (*/
                //subTransaction.Start("sdf");/* == TransactionStatus.Started)*/
                //if (subTransaction.Start() == TransactionStatus.Started)
                //if (subTransaction.Start() == TransactionStatus.Started)
                //{

                //var uiDoc = m_revit.ActiveUIDocument;
                //uiDoc.ActiveView;

                var uidoc = new UIDocument(_doc);

                //sDocument.Regenerate();

                var r = uidoc.Selection.PickObject(ObjectType.Element,
                    "Выбрать элемент для размещения");
                var elem = uidoc.Document.GetElement(r);
                return elem;
                //TaskDialog.Show("Revit", "Exception" + elem);

                //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
                //var elem = sDocument.GetElement(earthId);


                //if (subTransaction.Commit() == TransactionStatus.Committed)
                //{
                //    return elem;
                //}
                //}
            }

            catch (Exception ex)
            {
                TaskDialog.Show("Revit", "Exception" + ex.Message);
                //subTransaction.RollBack();
                return null;
            }


            //}
        }
    }

}

