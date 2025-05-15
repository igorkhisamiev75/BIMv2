

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System;
using System.Linq;



[Transaction(TransactionMode.Manual)]
public class EnhancedStairFinishing : IExternalCommand
{
    private const double Precision = 0.0001;
    private Document _doc;
    private ElementId _floorTypeId;
    private ElementId _wallTypeId;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;
        UIDocument uiDoc = uiApp.ActiveUIDocument;
        _doc = uiDoc.Document;

        try
        {
            // Get user selection
            var stairs = SelectStairs(uiDoc);
            if (stairs == null) return Result.Cancelled;

            // Get finishing types
            _floorTypeId = GetFloorTypeId("0");
            _wallTypeId = GetWallTypeId("0");

            if (_floorTypeId == ElementId.InvalidElementId || _wallTypeId == ElementId.InvalidElementId)
            {
                message = "not found";
                return Result.Failed;
            }

            using (Transaction t = new Transaction(_doc, "Create Stair Finishes"))
            {
                t.Start();

                //создает пол на лестничных клетках
                CreateLandingFinishes(stairs);
                CreateRunFinishes(stairs);

                t.Commit();
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }

    private Stairs SelectStairs(UIDocument uiDoc)
    {
        try
        {
            var reference = uiDoc.Selection.PickObject(ObjectType.Element,
                new StairsSelectionFilter(), "Select a stair element");
            return _doc.GetElement(reference) as Stairs;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return null;
        }
    }

    private void CreateLandingFinishes(Stairs stairs)
    {
        var landings = stairs.GetStairsLandings();
        var baseLevelId = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsElementId();
        var baseOffset = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_OFFSET).AsDouble();
        var level = _doc.GetElement(baseLevelId) as Level;

        foreach (var landingId in landings)
        {
            var landing = _doc.GetElement(landingId) as StairsLanding;
            var elevation = landing.BaseElevation + baseOffset;

            // Get footprint and convert to list of CurveLoops
            var footprint = GetAdjustedFootprint(landing.GetFootprintBoundary(), elevation);
            var curveLoops = new List<CurveLoop> { footprint };

            // Create landing finish
            var floor = Floor.Create(_doc, curveLoops, _floorTypeId, level.Id);
            var floorThickness = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();
            floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(elevation + floorThickness);
        }
    }

    private void CreateRunFinishes(Stairs stairs)
    {
        var runs = stairs.GetStairsRuns();
        var baseLevelId = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsElementId();
        var baseOffset = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_OFFSET).AsDouble();
        var level = _doc.GetElement(baseLevelId) as Level;

        foreach (var runId in runs)
        {
            StairsRun run = _doc.GetElement(runId) as StairsRun;
            double riserHeight = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RISER_HEIGHT).AsDouble();
            double treadDepth = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_TREAD_DEPTH).AsDouble();
            double runWidth = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RUN_WIDTH).AsDouble();
            int treadCount = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_NUMBER_OF_TREADS).AsInteger();
            double baseElevation = run.BaseElevation + baseOffset;

            // Get the run direction vector
            CurveLoop path = run.GetStairsPath();
            XYZ runDirection = (path.FirstOrDefault() as Line)?.Direction ?? XYZ.BasisX;

            // Process run geometry
            (List<Curve> SideBoundaries, List<CurveLoop> TreadBoundaries) boundaries = ProcessRunBoundaries(run.GetFootprintBoundary(), runDirection, runWidth);

            // Create side walls
            CreateRunWalls(boundaries.SideBoundaries, baseLevelId, baseElevation, riserHeight, treadCount, treadDepth, runDirection);

            // Create tread finishes
            //CreateTreadFinishes(boundaries.TreadBoundaries, level.Id, baseElevation, riserHeight, treadCount, treadDepth, runDirection);
        }
    }

    private (List<Curve> SideBoundaries, List<CurveLoop> TreadBoundaries) ProcessRunBoundaries(CurveLoop boundary, XYZ runDirection, double runWidth)
    {
        List<Curve> sideBoundaries = new List<Curve>();
        List<CurveLoop> treadBoundaries = new List<CurveLoop>();

        // Classify curves as sides or ends based on angle to run direction
        foreach (var curve in boundary)
        {
            var line = curve as Line;
            if (line == null) continue;

            var angle = line.Direction.AngleTo(runDirection);
            var isSide = Math.Abs(angle) < Precision || Math.Abs(angle - Math.PI) < Precision;

            if (Math.Abs(line.Length - runWidth) < Precision)
            {
                sideBoundaries.Add(line);
            }
            else
            {
                // Process tread boundaries
                var treadProfile = CreateTreadProfile(line, runDirection, runWidth);
                if (treadProfile != null)
                {
                    treadBoundaries.Add(treadProfile);
                }
            }
        }

        return (sideBoundaries, treadBoundaries);
    }

    private CurveLoop CreateTreadProfile(Curve baseline, XYZ runDirection, double runWidth)
    {
        // Create a rectangular profile for each tread
        var start = baseline.GetEndPoint(0);
        var end = baseline.GetEndPoint(1);

        var widthVector = (end - start).Normalize().CrossProduct(XYZ.BasisZ) * runWidth;
        var p1 = start;
        var p2 = end;
        var p3 = end + widthVector;
        var p4 = start + widthVector;

        var profile = new CurveLoop();
        profile.Append(Line.CreateBound(p1, p2));
        profile.Append(Line.CreateBound(p2, p3));
        profile.Append(Line.CreateBound(p3, p4));
        profile.Append(Line.CreateBound(p4, p1));

        return profile;
    }

    private void CreateRunWalls(List<Curve> sideBoundaries, ElementId levelId, double baseElevation, double riserHeight, int treadCount, double treadDepth, XYZ runDirection)
    {
        foreach (Curve boundary in sideBoundaries)
        {
            Wall wall = Wall.Create(_doc, boundary, levelId, false);
            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseElevation);
            wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(riserHeight);

            // Create wall copies for each tread
            for (int i = 1; i <= treadCount; i++)
            {
                XYZ offset = runDirection * (treadDepth * i) + XYZ.BasisZ * (riserHeight * i);
                ElementId newWallId = ElementTransformUtils.CopyElement(_doc, wall.Id, offset).First();
                Wall newWall = _doc.GetElement(newWallId) as Wall;
                newWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseElevation + riserHeight * i);

            }
        }
    }

    private void CreateTreadFinishes(List<CurveLoop> treadProfiles, ElementId levelId, double baseElevation,
     double riserHeight, int treadCount, double treadDepth, XYZ runDirection)
    {
        foreach (var profile in treadProfiles)
        {
            // Wrap single CurveLoop in a List
            var profileList = new List<CurveLoop> { profile };

            var floor = Floor.Create(_doc, profileList, _floorTypeId, levelId);
            var floorThickness = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();
            floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(baseElevation + riserHeight + floorThickness);

            // Create copies for each tread
            for (int i = 1; i < treadCount; i++)
            {
                var offset = runDirection * (treadDepth * i) + XYZ.BasisZ * (riserHeight * i);
                var newFloorId = ElementTransformUtils.CopyElement(_doc, floor.Id, offset).First();
                var newFloor = _doc.GetElement(newFloorId) as Floor;
                newFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                    .Set(baseElevation + riserHeight * (i + 1) + floorThickness);
            }
        }
    }

    private CurveLoop GetAdjustedFootprint(CurveLoop original, double elevation)
    {
        var adjusted = new CurveLoop();
        foreach (var curve in original)
        {
            var start = curve.GetEndPoint(0);
            var end = curve.GetEndPoint(1);

            // Adjust Z-coordinate to target elevation
            var adjustedStart = new XYZ(start.X, start.Y, elevation);
            var adjustedEnd = new XYZ(end.X, end.Y, elevation);

            if (curve is Line)
            {
                adjusted.Append(Line.CreateBound(adjustedStart, adjustedEnd));
            }
            else if (curve is Arc arc)
            {
                var midpoint = arc.Evaluate(0.5, true);
                var adjustedMid = new XYZ(midpoint.X, midpoint.Y, elevation);
                adjusted.Append(Arc.Create(adjustedStart, adjustedEnd, adjustedMid));
            }
        }
        return adjusted;
    }

    private ElementId GetFloorTypeId(string typeName)
    {
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(FloorType))
            .FirstOrDefault(x => x.Name.Equals(typeName))?.Id ?? ElementId.InvalidElementId;
    }

    private ElementId GetWallTypeId(string typeName)
    {
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(WallType))
            .FirstOrDefault(x => x.Name.Equals(typeName))?.Id ?? ElementId.InvalidElementId;
    }


}

// Filter to only allow stair selection
public class StairsSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem is Stairs;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}