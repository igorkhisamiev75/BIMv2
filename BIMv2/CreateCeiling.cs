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

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]

    public class CreateCeiling : IExternalCommand
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

            ElementId roomId = new ElementId(282347);

            Element room = _doc.GetElement(roomId);

            //get room boundary
            SpatialElementBoundaryOptions options =new SpatialElementBoundaryOptions();

            options.SpatialElementBoundaryLocation =SpatialElementBoundaryLocation.Finish;

            IList<IList<BoundarySegment>> roomBoundary =(room as SpatialElement).GetBoundarySegments(options);


            //ceiling
            ElementId ceilingId = new ElementId(282885);

            Element ceiling = _doc.GetElement(ceilingId);

            //get ceiling sketch
            ElementClassFilter filter =
                new ElementClassFilter(typeof(Sketch));

            ElementId sketchId =
                ceiling.GetDependentElements(filter).First();

            Sketch ceilingSketch =
                _doc.GetElement(sketchId) as Sketch;

            CurveArrArray ceilingProfile = ceilingSketch.Profile;

            filter = new ElementClassFilter(typeof(CurveElement));

            IEnumerable curves = ceiling.GetDependentElements(filter).Select(id => _doc.GetElement(id));

            //IEnumerable modelLines = curves.Where(e => e is ModelLine).Cast();//target

            IEnumerable modelLines = curves.Cast<ModelLine>();

            //if (curves.Count() != modelLines.Count())
            //    throw new Exception("The ceiling contains" +
            //                        " non straight lines");

            IList<IList<ModelLine>> editableSketch = new List<IList<ModelLine>>();

            foreach (CurveArray loop in ceilingProfile)
            {
                List<ModelLine> newLoop = new List<ModelLine>();

                foreach (Curve edge in loop)
                {

                    foreach (ModelLine modelLine in modelLines)
                    {

                        Curve currentLine = ((modelLine as ModelLine)
                          .Location as LocationCurve).Curve;

                        if (currentLine.Intersect(edge)
                             == SetComparisonResult.Equal)
                        {

                            newLoop.Add(modelLine);
                            break;
                        }

                        editableSketch.Add(newLoop);
                    }
                }
            }

            IList<BoundarySegment> roomPerimeter = roomBoundary[0];

            roomBoundary = roomBoundary.Skip(1)
               .OrderByDescending(s => s.Count()).ToList();

            roomBoundary.Insert(0, roomPerimeter);

            //sort ceiling boundary            
            IList<ModelLine> ceilingPerimeter = editableSketch[0];

            editableSketch = editableSketch.Skip(1)
               .OrderByDescending(s => s.Count()).ToList();

            editableSketch.Insert(0, ceilingPerimeter);

            //compare the number of loops and edges
            if (roomBoundary.Count() > editableSketch.Count()
              || Enumerable.Range(0, Math.Min(roomBoundary.Count(),
                                              editableSketch.Count()))
              .Any(i => roomBoundary[0].Count() > editableSketch[0].Count()))
            {
                throw new Exception("The ceiling's sketch cannot be "+ "adapted to the room's shape.");
            }


            using (Transaction t = new Transaction(_doc, "Edit ceiling sketch"))
            {
                t.Start();

                for (int i = 0; i < editableSketch.Count(); i++)
                {
                    IList<ModelLine> ceilingLoop = editableSketch[i];

                    if (i < roomBoundary.Count())
                    {
                        //edit the current loop
                        IList<BoundarySegment> roomLoop = roomBoundary[i];

                        for (int j = 0; j < ceilingLoop.Count(); j++)
                        {
                            ModelLine currentEdge = ceilingLoop[j];

                            if (j < roomLoop.Count())
                            {
                                //edit the current edge
                                BoundarySegment roomEdge = roomLoop[j];

                                Curve newEdge = roomEdge.GetCurve().Clone();

                                if (!(newEdge is Line))
                                    throw new Exception(
                                       "The room contains non straight lines");

                                (currentEdge.Location as LocationCurve).Curve =newEdge;
                            }
                            else
                            {
                                //remove the current edge
                                _doc.Delete(currentEdge.Id);
                            }
                        }
                    }
                    else
                    {
                        //remove the current loop
                        foreach (ModelLine line in ceilingLoop)
                        {
                            _doc.Delete(line.Id);
                        }
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;

        }



    }

}

