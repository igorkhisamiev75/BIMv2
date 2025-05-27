#region Namespace

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateOpeningsInWallForStructuralFraming : IExternalCommand
    {
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;


            using (var t = new Transaction(_doc))
            {
                t.Start("Создание проемов в стене для балок");

                //все стены                   
                IList<Element> allWalls = GetAllWall(_doc);

                int k = 0; 

                //получить связанную модель 

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {
                    if (linkedInstances != null)
                    {
                        foreach (var linkInstance in linkedInstances)
                        {

                            Document linkDocument = linkedInstance.GetLinkDocument();


                            //собрать балки
                            IList<Element> listStrFramings = GetAllStFraming(linkDocument);
                            //IList<Element> listStrFramings = GetAllWall(linkDocument);


                            TaskDialog.Show("Complite!", $"Всего балок {listStrFramings.Count} и {allWalls.Count} стен");


                            //пройитись по каждой стене  

                            foreach (Element wall in allWalls)
                            {
                                
                                Wall wall1 = (Wall)wall;

                                //по каждой балке
                                foreach (var balka in listStrFramings)

                                {
                                    try
                                    {
                                        if (balka != null )
                                        {
                                            
                                            //bondingbox wall
                                            BoundingBoxXYZ boundingBoxXYZ = wall.get_BoundingBox(_doc.ActiveView);

                                            //bounding Box window
                                            BoundingBoxXYZ boundingBoxXYZ3 = balka.get_BoundingBox(_doc.ActiveView);

                                            var fi = balka as FamilyInstance;

                                            LocationCurve locationCurve = fi.Location as LocationCurve;

                                            var line = locationCurve.Curve as Line;

                                            if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ3))
                                            {
                                                try
                                                {
                                                    //точка пересеяни 2 элементов

                                                    LocationCurve locationCurve2 = wall.Location as LocationCurve;

                                                    var line2 = locationCurve2.Curve as Line;

                                                    //XYZ pointWin = GetIntersection(line, line2);

                                                    //Point A = new Point(4, -1, 2);

                                                    //балка
                                                    XYZ A2 = line.Tessellate()[0];
                                                    XYZ A3 = line.Tessellate()[1];

                                                    Point A = new Point(A2.X, A2.Y, A2.Z);
                                                    Point B = new Point(A3.X, A3.Y, A3.Z);

                                                    //стена
                                                    XYZ A4 = line2.Tessellate()[0];
                                                    XYZ A5 = line2.Tessellate()[1];

                                                    Point C = new Point(A4.X, A4.Y, A4.Z);
                                                    Point D = new Point(A5.X, A5.Y, A5.Z);

                                                    Point IX = Intersection(A, B, C, D);


                                                    FamilyInstance familyInstance = balka as FamilyInstance;

                                                    GeometryElement geometryElement = wall.get_Geometry(new Options());
                                                    GeometryElement geometryElement2 = balka.get_Geometry(new Options());

                                                    GeometryElement geoElement = balka.get_Geometry(new Options());

                                                    var gS = GetSolids(balka);

                                                    Solid solid2 = geometryElement2.Last() as Solid;

                                                    foreach (GeometryObject geoObject in geoElement)
                                                    {
                                                        Solid instance = geoObject as Solid;

                                                        if (null != instance)
                                                        {
                                                            
                                                                //var solid = instObj as Solid;

                                                                if (null != instance && instance.Faces.Size != 0)
                                                                {
                                                                    solid2= instance;
                                                                    break;
                                                                }

                                                                //Transform instTransform = instance.Transform;
                                                                //// Get the faces and edges from solid, and transform the formed points
                                                                //foreach (Face face in solid.Faces)
                                                                //{
                                                                //    Mesh mesh = face.Triangulate();
                                                                //    foreach (XYZ ii in mesh.Vertices)
                                                                //    {
                                                                //        XYZ point = ii;
                                                                //        XYZ transformedPoint = instTransform.OfPoint(point);
                                                                //    }
                                                                //}
                                                                //foreach (Edge edge in solid.Edges)
                                                                //{
                                                                //    foreach (XYZ ii in edge.Tessellate())
                                                                //    {
                                                                //        XYZ point = ii;
                                                                //        XYZ transformedPoint = instTransform.OfPoint(point);
                                                                //    }
                                                                //}
                                                            
                                                        }
                                                    }


                                                        //geometryElement2.GetInstance 

                                                        Solid solidA = geometryElement.First() as Solid;

                                                    Solid solidB = geometryElement2.First() as Solid;

                                                    //получаем солид пересечения
                                                    Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(solidA, gS[0], BooleanOperationsType.Intersect);
                                                    
                                                    //var intersection = BooleanOperationsType.Intersect()
                                                    //double volumeOfIntersection = intersection.Volume;

                                                    XYZ xyz11 = intersection.GetBoundingBox().Max;
                                                    XYZ yyz22 = intersection.GetBoundingBox().Min;

                                                    XYZ pointMidlMaxZ=new XYZ(xyz11.X, xyz11.Y, xyz11.Z); //максимальная
                                                    XYZ pointMidlZMin2=new XYZ(yyz22.X, yyz22.Y, yyz22.Z);//минимальная

                                                    IX.Z = IX.Z - ((pointMidlMaxZ.Z - pointMidlZMin2.Z) / 2);

                                                    Opening opening =_doc.Create.NewOpening(wall1, yyz22, xyz11);

                                                    XYZ taXYZ=new XYZ(IX.X, IX.Y, IX.Z);

                                                    ElementTransformUtils.MoveElement(_doc, opening.Id, taXYZ);

                                                    k++;
                                                    //_doc.Regenerate();
                                                    continue;
                                                }

                                                catch
                                                {
                                                    //TaskDialog.Show("N", "отверстие не сделано сделано");
                                                    continue;
                                                }

                                            }
                                            else
                                            {
                                                continue;
                                            }
                                        }

                                    }


                                    catch
                                    {
                                        continue;
                                    }

                                }

                            }


                        }


                        TaskDialog.Show("Complite!", $"{k} ПРОЕМОВ");

                        _doc.Regenerate();
                        t.Commit();
                    }
                }

                return Result.Succeeded;
            }
        }

        public IList<Element> GetAllStFraming(Document document)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

        public IList<Element> GetAllWall(Document document)
        {
            var room_collector = new FilteredElementCollector(document)
                                 .OfCategory(BuiltInCategory.OST_Walls)
                                 .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            IList<Element> allWalls = room_collector.ToElements();

            return allWalls;

        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        public bool bbIntersect(BoundingBoxXYZ BoxXYZ, BoundingBoxXYZ BoxXYZ2)
        {
            if (BoxXYZ != null && BoxXYZ2 != null)
            {
                Outline outline1 = new Outline(BoxXYZ.Min, BoxXYZ.Max);

                Outline outline2 = new Outline(BoxXYZ2.Min, BoxXYZ2.Max);

                if (outline1.Intersects(outline2, 0))
                {
                    //TaskDialog.Show("N", $"пересекаются {BoxXYZ.ToString()} и {BoxXYZ2.ToString()}");
                    return true;
                }
            }

            return false;

        }

        private List<Solid> GetSolids(Element element)
        {
            var solids = new List<Solid>();

            if (element.get_Geometry(new Options { IncludeNonVisibleObjects = true }) is GeometryElement geometryElement)
            {
                foreach (var geometryObject in geometryElement)
                {
                    // Get solid
                    if (geometryObject is Solid geometrySolid &&
                        geometrySolid.Faces.Size > 0 && geometrySolid.Edges.Size > 0 && geometrySolid.Volume > 0)
                    {
                        solids.Add(geometrySolid);
                    }

                    // Get geometryInstance solid
                    else if (geometryObject is GeometryInstance geometryInstance)
                    {
                        if (geometryInstance.GetInstanceGeometry() is GeometryElement instanceGeometryElement &&
                        geometryInstance.IsElementGeometry && geometryInstance.Transform.IsConformal)
                        {
                            foreach (var instanceGeometryObject in instanceGeometryElement)
                            {
                                if (instanceGeometryObject is Solid instanceGeometrySolid &&
                                    instanceGeometrySolid.Faces.Size > 0 && instanceGeometrySolid.Edges.Size > 0)
                                {
                                    solids.Add(instanceGeometrySolid);
                                }
                            }
                        }
                    }
                }
            }
            return solids;
        }

        private XYZ GetIntersection(Line line1,Line line2)
        {
            IntersectionResultArray results;

            SetComparisonResult result
              = line1.Intersect(line2, out results);

            if (result != SetComparisonResult.Overlap)
                throw new InvalidOperationException(
                  "Input lines did not intersect.");

            if (results == null || results.Size != 1)
                throw new InvalidOperationException(
                  "Could not extract line intersection point.");

            IntersectionResult iResult
              = results.get_Item(0);

            return iResult.XYZPoint;
        }

        static public Point Intersection(Point A, Point B, Point C, Point D)
        {
            double xo = A.X, yo = A.Y, zo = A.Z;
            double p = B.X - A.X, q = B.Y - A.Y, r = B.Z - A.Z;

            double x1 = C.X, y1 = C.Y, z1 = C.Z;
            double p1 = D.X - C.X, q1 = D.Y - C.Y, r1 = D.Z - C.Z;

            double x = (xo * q * p1 - x1 * q1 * p - yo * p * p1 + y1 * p * p1) /
                (q * p1 - q1 * p);
            double y = (yo * p * q1 - y1 * p1 * q - xo * q * q1 + x1 * q * q1) /
                (p * q1 - p1 * q);
            double z = (zo * q * r1 - z1 * q1 * r - yo * r * r1 + y1 * r * r1) /
                (q * r1 - q1 * r);

            if (z >= 0 || z < 0)
            {
                z = zo;
            }
            else
            {
                z = zo;

            }

            return new Point(x, y, z);
        }

    }

    public class Point
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public Point() { }
        public Point(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}



