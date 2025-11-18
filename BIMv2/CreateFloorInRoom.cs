//#region Namespaces
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Architecture;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;
//using Floor = Autodesk.Revit.DB.Floor;

//#endregion

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    [Journaling(JournalingMode.NoCommandData)]

//    public class CreateFloorInRoom : IExternalCommand
//    {
//        Application _app;
//        Document _doc;
//        private const double PRECISION = 0.00000001;
//        private CurveArray profile;
//        //private Hashtable floorTypes;
//        private List<string> floorTypesName;
//        public FloorType floorType;
//        private Level level;
//        private bool structural;
//        private Autodesk.Revit.Creation.Application creApp;

//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {

//            UIApplication uiApp = commandData.Application;
//            UIDocument uiDoc = uiApp.ActiveUIDocument;
//            _app = uiApp.Application;
//            _doc = uiDoc.Document;
//            creApp = commandData.Application.Application.Create;


//            using (Transaction t = new Transaction(_doc))
//            {
//                t.Start("Create floor");

//                Element elem = SelectedElement(_doc); //select room

//                CreateFloorInRooms(elem);

//                //CreateCeiling(elem);

//                TaskDialog.Show("У тебя получилось", "Пол создан ☺☺☺");
//                t.Commit();

//                t.Start();
//                CreateFloorOpenings(elem);

//                var res = t.Commit();

//            }

//            return Result.Succeeded;

//        }


//        // a floor type to be used by the new floor instead of the default type
//        public FloorType FloorType
//        {
//            get
//            {
//                return floorType;
//            }
//            set
//            {
//                floorType = value;
//            }
//        }

//        // the Level on which the floors are to be placed
//        public Level Level
//        {
//            get
//            {
//                return level;
//            }
//            set
//            {
//                level = value;
//            }
//        }

//        // an array of planar lines and arcs that represent the horizontal profile of the floor
//        public CurveArray Profile
//        {
//            get
//            {
//                return profile;
//            }
//            set
//            {
//                profile = value;
//            }
//        }

//        // determine wether the floor is structural
//        public bool Structural
//        {
//            get
//            {
//                return structural;
//            }
//            set
//            {
//                structural = value;
//            }
//        }

//        // a list of all Floor Types Name that could be used by the new Floor
//        public List<string> FloorTypesName
//        {
//            get
//            {
//                return floorTypesName;
//            }
//            set
//            {
//                floorTypesName = value;
//            }
//        }

//        //Ceiling CreateCeilingAtElevation(Document document, Level level, double elevation)
//        //{
//        //    XYZ first = new XYZ(0, 0, 0);
//        //    XYZ second = new XYZ(20, 0, 0);
//        //    XYZ third = new XYZ(20, 15, 0);
//        //    XYZ fourth = new XYZ(0, 15, 0);
//        //    CurveLoop profile = new CurveLoop();
//        //    profile.Append(Line.CreateBound(first, second));
//        //    profile.Append(Line.CreateBound(second, third));
//        //    profile.Append(Line.CreateBound(third, fourth));
//        //    profile.Append(Line.CreateBound(fourth, first));

//        //    var ceiling = Ceiling.Create(document, new List<CurveLoop> { profile }, ElementId.InvalidElementId, level.Id);
//        //    Parameter param = ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
//        //    param.Set(elevation);

//        //    return ceiling;
//        //}

//        public void CreateCeiling(Element elem)
//        {
//            CurveArray roomsCurves = new CurveArray();
//            Room r1 = (Room)elem;

//            SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();
//            IList<IList<BoundarySegment>> segments2 = r1.GetBoundarySegments(bo);


//            CurveArray temp = new CurveArray();

//            if (null != segments2)
//            {
//                IList<BoundarySegment> segmentList = segments2[0];


//                foreach (BoundarySegment boundarySegment in segmentList)
//                {
//                    Curve c = boundarySegment.GetCurve();
//                    temp.Append(c);
//                }
//                SortCurves(temp);
//                //sourr = _doc.Create.NewFloor(temp, floorType, r1.Level, false);

//            }

//            ElementId ceilingId = new ElementId(282885);

//            Element ceiling = _doc.GetElement(ceilingId);

//            //get ceiling sketch
//            ElementClassFilter filter =
//                new ElementClassFilter(typeof(Sketch));

//            ElementId sketchId =
//                ceiling.GetDependentElements(filter).First();

//            Sketch ceilingSketch =
//                _doc.GetElement(sketchId) as Sketch;

//            CurveArrArray ceilingProfile = ceilingSketch.Profile;

//            filter = new ElementClassFilter(typeof(CurveElement));

//            IEnumerable curves = ceiling.GetDependentElements(filter)
//                .Select(id => _doc.GetElement(id));

//            //IEnumerable modelLines = curves.Where(e => e is ModelLine).Cast();//target

//            //if (curves.Count() != modelLines.Count())
//            //    throw new Exception("The ceiling contains" +
//            //                        " non straight lines");

//            IList<IList<ModelLine>> editableSketch = new List<IList<ModelLine>>();

//            //foreach (CurveArray loop in ceilingProfile)
//            //{
//            //    List<ModelLine> newLoop = new List<ModelLine>();

//            //    foreach (Curve edge in loop)
//            //    {

//            //        foreach (ModelLine modelLine in modelLines)
//            //        {

//            //            Curve currentLine = ((modelLine as ModelLine)
//            //              .Location as LocationCurve).Curve;

//            //            if (currentLine.Intersect(edge)
//            //                 == SetComparisonResult.Equal)
//            //            {

//            //                newLoop.Add(modelLine);
//            //                break;
//            //            }

//            //            editableSketch.Add(newLoop);
//            //        }
//            //    }
//            //}

//        }

//        public void CreateFloorOpenings(Element elem)
//        {

//            FloorType floorType;
//            IList<Element> rooms;
//            GetRoomInProject(out rooms);

//            // List all the floors instances 
//            var floorCollector = new FilteredElementCollector(_doc).OfClass(typeof(Floor));
//            IList<Element> floorList = floorCollector.ToElements();

            
//                CurveArray roomsCurves = new CurveArray();
//                var r1 = (Room)elem;
//                var f1 = (Floor)floorList[0];

//                SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();
//                IList<IList<BoundarySegment>> segments2 = r1.GetBoundarySegments(bo);

//                CurveArray temp = new CurveArray();

//                if (null != segments2)
//                {
//                    for (int j = 1; j < segments2.Count; j++)
//                    {
//                        IList<BoundarySegment> segmentList = segments2[j];

//                        foreach (BoundarySegment boundarySegment in segmentList)
//                        {
//                            Curve c = boundarySegment.GetCurve();
//                            temp.Append(c);
//                        }
//                        SortCurves(temp);
//                        var opening = _doc.Create.NewOpening(f1, temp, true); //create opening
//                    }

//                }

            

//        }

//        public void CreateFloorInRooms(Element elem)
//        {

//            FloorType floorType = GetFloorTypeInProject();

//            //TaskDialog.Show("У тебя получилось", $"{floorType.GetType()} Пол создан ☺☺☺");


//            //IList<Element> rooms;


//            //FloorType= GetFloorTypeInProject();

//            //GetRoomInProject(out rooms);


//            CurveArray roomsCurves = new CurveArray();
//            Room r1 = (Room)elem;

//            level = r1.Level;

//            SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();
//            IList<IList<BoundarySegment>> segments2 = r1.GetBoundarySegments(bo);
            
//            CurveArray temp = new CurveArray();

//                if (null != segments2)
//                {
//                    IList<BoundarySegment> segmentList = segments2[0];

//                    foreach (BoundarySegment boundarySegment in segmentList)
//                    {
//                        Curve c = boundarySegment.GetCurve();
//                        temp.Append(c);
//                    }

//                SortCurves(temp);

//                //TaskDialog.Show("У тебя получилось", $"{temp.Size} Пол создан ☺☺☺");

//                CurveLoop profile = new CurveLoop();

//                foreach (Curve g in temp)
//                {
//                    //CurveLoop curves = 
//                    profile.Append((Line)g);
//                }





//#if R2019 || R2020 || R2021 || R2022

//                _ = _doc.Create.NewFloor(temp, floorType, r1.Level, false);
//#else
//                //TaskDialog.Show("У тебя получилось", $"{profile.Count()} Пол создан ☺☺☺");
//                Floor nf = Floor.Create(_doc, new List<CurveLoop> { profile }, floorType.Id, level.Id);
//#endif


//            }


//        }

//        public void GetRoomInProject(out IList<Element> rooms)
//        {


//            // The normal vector (0,0,1) that must be perpendicular to the profile.
//            //XYZ normal = XYZ.BasisZ;

//            //Get room's 
//            RoomFilter filter = new RoomFilter();
//            // Apply the filter to the elements in the active document
//            FilteredElementCollector collector3 = new FilteredElementCollector(_doc);
//            rooms = collector3.WherePasses(filter).ToElements();
//        }

//        public FloorType GetFloorTypeInProject()
//        {
//            // Search all the floor types in the Revit
//            FilteredElementCollector filteredElementCollectorFloor = new FilteredElementCollector(_doc);
//            filteredElementCollectorFloor.OfClass(typeof(FloorType));
//            var m_FloorTypeCollection = filteredElementCollectorFloor.Cast<FloorType>().ToList<FloorType>();
//            FloorType g = m_FloorTypeCollection[3];
//            return g;
            
//        }

//        public void SortCurves(CurveArray lines)
//        {
//            XYZ pointCurves = lines.get_Item(0).GetEndPoint(1);
//            Curve temCurve = lines.get_Item(0);


//            Profile = creApp.NewCurveArray();

//            Profile.Append(temCurve);

//            while (Profile.Size != lines.Size)
//            {

//                temCurve = GetNext(lines, pointCurves, temCurve);

//                if (Math.Abs(pointCurves.X - temCurve.GetEndPoint(0).X) < PRECISION
//                 && Math.Abs(pointCurves.Y - temCurve.GetEndPoint(0).Y) < PRECISION)
//                {
//                    pointCurves = temCurve.GetEndPoint(1);
//                }
//                else
//                {
//                    pointCurves = temCurve.GetEndPoint(0);
//                }

//                Profile.Append(temCurve);
//            }

//        }
//        public Curve GetNext(CurveArray profile, XYZ connected, Curve line)
//        {
//            foreach (Curve c in profile)
//            {
//                if (c.Equals(line))
//                {
//                    continue;
//                }
//                if ((Math.Abs(c.GetEndPoint(0).X - line.GetEndPoint(1).X) < PRECISION &&
//                    Math.Abs(c.GetEndPoint(0).Y - line.GetEndPoint(1).Y) < PRECISION &&
//                    Math.Abs(c.GetEndPoint(0).Z - line.GetEndPoint(1).Z) < PRECISION) &&
//                    (Math.Abs(c.GetEndPoint(1).X - line.GetEndPoint(0).X) < PRECISION &&
//                    Math.Abs(c.GetEndPoint(1).Y - line.GetEndPoint(0).Y) < PRECISION &&
//                    Math.Abs(c.GetEndPoint(1).Z - line.GetEndPoint(0).Z) < PRECISION) &&
//                    2 != profile.Size)
//                {
//                    continue;
//                }

//                if (Math.Abs(c.GetEndPoint(0).X - connected.X) < PRECISION &&
//                    Math.Abs(c.GetEndPoint(0).Y - connected.Y) < PRECISION &&
//                    Math.Abs(c.GetEndPoint(0).Z - connected.Z) < PRECISION)
//                {
//                    return c;
//                }
//                else if (Math.Abs(c.GetEndPoint(1).X - connected.X) < PRECISION &&
//                         Math.Abs(c.GetEndPoint(1).Y - connected.Y) < PRECISION &&
//                         Math.Abs(c.GetEndPoint(1).Z - connected.Z) < PRECISION)
//                {
//                    if (c.GetType().Name.Equals("Line"))
//                    {
//                        XYZ start = c.GetEndPoint(1);
//                        XYZ end = c.GetEndPoint(0);
//                        return Line.CreateBound(start, end);
//                    }
//                    else if (c.GetType().Name.Equals("Arc"))
//                    {
//                        int size = c.Tessellate().Count;
//                        XYZ start = c.Tessellate()[0];
//                        XYZ middle = c.Tessellate()[size / 2];
//                        XYZ end = c.Tessellate()[size];

//                        return Arc.Create(start, end, middle);
//                    }
//                }
//            }
//            throw new InvalidOperationException("The Room Boundary should be closed.");
//        }

//        public Element SelectedElement(Document sDocument)
//        {
//            //var document = m_revit.ActiveUIDocument.Document;
//            //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
//            //var elem = document.GetElement(earthId);
//            //uiDoc = m_revit.ActiveUIDocument;
//            //using (Transaction subTransaction = new Transaction(sDocument, Guid.NewGuid().GetHashCode().ToString()))
//            //using (SubTransaction subTransaction = new SubTransaction(sDocument))
//            //{
//            //subTransaction.Start();
//            try
//            {
//                /*if (*/
//                //subTransaction.Start("sdf");/* == TransactionStatus.Started)*/
//                //if (subTransaction.Start() == TransactionStatus.Started)
//                //if (subTransaction.Start() == TransactionStatus.Started)
//                //{

//                //var uiDoc = m_revit.ActiveUIDocument;
//                //uiDoc.ActiveView;

//                var uidoc = new UIDocument(_doc);

//                //sDocument.Regenerate();

//                var r = uidoc.Selection.PickObject(ObjectType.Element,
//                    "Выбрать элемент для размещения");
//                var elem = uidoc.Document.GetElement(r);
//                return elem;
//                //TaskDialog.Show("Revit", "Exception" + elem);

//                //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
//                //var elem = sDocument.GetElement(earthId);


//                //if (subTransaction.Commit() == TransactionStatus.Committed)
//                //{
//                //    return elem;
//                //}
//                //}
//            }

//            catch (Exception ex)
//            {
//                TaskDialog.Show("Revit", "Exception" + ex.Message);
//                //subTransaction.RollBack();
//                return null;
//            }


//            //}
//        }
//    }

//}

