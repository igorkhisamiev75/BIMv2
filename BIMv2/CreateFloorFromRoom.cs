//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Architecture;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;

//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    [Journaling(JournalingMode.NoCommandData)]
//    public class CreateFloorFromRoom : IExternalCommand
//    {

//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            // Get Revit document and UI
//            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
//            Document doc = uiDoc.Document;

//            try
//            {
//                // Step 3: Let the user select a room
//                Reference roomRef = uiDoc.Selection.PickObject(ObjectType.Element, new RoomFilter2(), "Select a room");
//                Room room = doc.GetElement(roomRef) as Room;

//                if (room == null)
//                {
//                    message = "Selected element is not a room.";
//                    return Result.Failed;
//                }

//                // Step 4: Get room boundaries
//                IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
//                if (boundaries == null || boundaries.Count == 0)
//                {
//                    message = "No boundary segments found.";
//                    return Result.Failed;
//                }

//                // Step 5: Convert boundaries to CurveLoops
//                List<CurveLoop> curveLoops = new List<CurveLoop>();
//                foreach (IList<BoundarySegment> boundary in boundaries)
//                {
//                    CurveLoop loop = new CurveLoop();
//                    foreach (BoundarySegment segment in boundary)
//                    {
//                        Curve curve = segment.GetCurve();
//                        loop.Append(curve);
//                    }
//                    curveLoops.Add(loop);
//                }

//                // Step 6: Get the first floor type available
//                FloorType floorType = new FilteredElementCollector(doc)
//                    .OfClass(typeof(FloorType))
//                    .Cast<FloorType>()
//                    .FirstOrDefault();

//                if (floorType == null)
//                {
//                    message = "No floor type found.";
//                    return Result.Failed;
//                }

//                // Step 7: Create the floor
//                using (Transaction t = new Transaction(doc, "Create Floor from Room"))
//                {
//                    t.Start();
//                    Floor floor = Floor.Create(doc, curveLoops, floorType.Id, room.LevelId);
//                    t.Commit();
//                }

//                return Result.Succeeded;
//            }
//            catch (Exception ex)
//            {
//                message = ex.Message;
//                return Result.Failed;
//            }
//        }
//    }

//    // Filter to allow only Room selection
//    public class RoomFilter2 : ISelectionFilter
//    {
//        public bool AllowElement(Element elem)
//        {
//            return elem is Room;
//        }

//        public bool AllowReference(Reference reference, XYZ position)
//        {
//            return false;
//        }
//    }
//}
