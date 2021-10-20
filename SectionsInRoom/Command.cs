using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace SectionsInRoom
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand

    {
        private const double PRECISION = 0.00000001;
        private Autodesk.Revit.Creation.Application creApp;
        private CurveArray profile;
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
        //get point for room
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            creApp = commandData.Application.Application.Create;

            try
            {
                Selection selection = uidoc.Selection;

                // Pick a light fixture.

                Reference pickedRoomReference = selection.PickObject(
                    ObjectType.Element, new RoomPickFilter(),
                    "Please select room");

                XYZ pickedPoint = pickedRoomReference.GlobalPoint;

                //get Room
                Element selectedRoom = uidoc.Document.GetElement(pickedRoomReference);
                
                Room r1 = (Room)selectedRoom;
                var areaRoom = r1.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                var volumeRoom = r1.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                var heighRoom = volumeRoom / areaRoom;

                //get boundary segment
                CurveArray roomsCurves = new CurveArray();
                SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();

                IList<IList<BoundarySegment>> segments2 = r1.GetBoundarySegments(bo);

                CurveArray curveArray = new CurveArray();

                if (segments2!=null)
                {
                    IList<BoundarySegment> segmentList = segments2[0];

                    foreach (BoundarySegment boundarySegment in segmentList)
                    {
                        Curve c = boundarySegment.GetCurve();
                        curveArray.Append(c);
                    }
                    SortCurves(curveArray);
                }

                var curveArraySize=curveArray.Size;

                var pointList = new List<XYZ>();

                for (int i = 0; i < curveArraySize; i++)
                {
                    int g = 0;
                    var xyzCurve = curveArray.get_Item(i).Tessellate();
                    pointList.Add(xyzCurve[g]);

                }

                //search first point 
                XYZ firstPoint = pointList[0];

                for (int i = 1; i < pointList.Count; i++)
                {
                    double lenght1 = Math.Sqrt(Math.Pow((firstPoint.X - pickedPoint.X), 2)
                                               + Math.Pow((firstPoint.Y - pickedPoint.Y), 2));
                    double lenght2 = Math.Sqrt(Math.Pow((pointList[i].X - pickedPoint.X), 2) 
                                               + Math.Pow((pointList[i].Y - pickedPoint.Y), 2));

                    if (lenght2 < lenght1)
                    {
                        firstPoint = pointList[i];
                    }

                }

                //var l = GetCurveOfPoint(firstPoint, curveArray);

                Curve cvPointCurve = curveArray.get_Item(0);

                //get curve
                for (int i = 1; i < curveArray.Size; i++)
                {
                    XYZ pointCurve = curveArray.get_Item(i).Tessellate()[0];

                    if (firstPoint.X == pointCurve.X && firstPoint.Y == pointCurve.Y)
                    {
                        //TaskDialog.Show("Curve", $"{i}");
                        cvPointCurve = curveArray.get_Item(i);
                    }

                }





                //TaskDialog.Show("Кривая", $"{cvPointCurve.GetEndPoint(0)}");

                //TaskDialog.Show("Первая точка", $"{firstPoint}");
                

                //TaskDialog.Show("Re", $"{pointList}");


                //curveArray.get_Item(0).GetEndPoint(0);

                //BoundingBoxXYZ sectionBox = r1.get_BoundingBox(uidoc.ActiveView);

                //BoundingBoxXYZ sectionBox2 = new BoundingBoxXYZ();
                


                ViewFamilyType vft = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault<ViewFamilyType>(x =>
                        ViewFamily.Section == x.ViewFamily);



                using (Transaction tx = new Transaction(doc))
                {
                    tx.Start("Create Section for Room");


                    //foreach (var array in curveArray)
                    //{
                        //var g=curveArray.get_Item(0).GetEndPoint(0);

                        //TaskDialog.Show("Re", $"{g}");

                       // XYZ p = cvPointCurve.GetEndPoint(0); //first 
                       double lenghtOffset = heighRoom;
                       var zLength = cvPointCurve.Length;
                        //XYZ corbelXYZ = new XYZ(1, 1, 1);
                        //corbelXYZ = firstPoint;

                        Transform rotTransform = Transform.CreateRotationAtPoint(XYZ.BasisY, -Math.PI / 2, firstPoint);
                        
                        BoundingBoxXYZ sectionBox2 = new BoundingBoxXYZ();
                        
                        XYZ xMin = new XYZ(0,0,0 );
                        //xMin = firstPoint;
                        
                        XYZ xMax = new XYZ(zLength, zLength, lenghtOffset);
                    //xMax = cvPointCurve.GetEndPoint(1);

                    sectionBox2.Transform = rotTransform;
                    sectionBox2.Min = xMin;
                        sectionBox2.Max = xMax;
                        //sectionBox2.Transform = t;

                        //crate sections 
                        ViewSection.CreateSection(doc, vft.Id, sectionBox2);
                    //}
                    
                    
                    tx.Commit();

                }



                if (null == pickedRoomReference)
                {
                    return Result.Failed;
                }

                //get point
                //var f=pickedRoomReference.GlobalPoint.ToString();
                

                //get element id
                //var g = pickedRoomReference.ElementId.ToString();

                //TaskDialog.Show("Re", $"{f}");


            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Cancelled;
            }

            return Result.Succeeded;

        }




        
        //create new sheet and add section 

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

        public Curve GetCurveOfPoint(XYZ point, CurveArray curveArray)
        {
            var p = point;
            
            //curveOfPoint = curveArray.get_Item(0);

            for (int i = 0; i < curveArray.Size; i++)
            {
                Curve cv = curveArray.get_Item(0);
                if (p == curveArray.get_Item(i).Tessellate())
                {
                     cv = curveArray.get_Item(i);
                     return cv;
                }
                
            }

            return null;

        }

    }

    public class RoomPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element e)
        {
            return e.Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_Rooms);
        }

        public bool AllowReference(Reference r, XYZ p)
        {
            return false;
        }
    }

    

    
}
