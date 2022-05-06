using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateRailingInRooms : IExternalCommand
    {
        // Member variables
        Application _app;
        Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            IList<Room> roomsInProject = new List<Room>();

            _app = uiApp.Application;
            _doc = uiDoc.Document;
            
            
            using (var t = new Transaction(_doc))
            {
                t.Start("Создаем плинтуса");

                var rooms = GetAllRooms(_doc);
                
                foreach (var element in rooms)
                {
                    roomsInProject.Add((Room)element);
                }

                foreach (var room in roomsInProject)
                {
                    
                    var levelId = room.LevelId;

                    Curve clCurve;

                    //CurveLoop clCurveLoop = new CurveLoop();

                    var segments = room.GetBoundarySegments(new SpatialElementBoundaryOptions());

                    if (null != segments) //the room may not be bound
                    {
                        foreach (var segmentList in segments)
                            
                        foreach (var boundarySegment in segmentList)
                        {
                            clCurve = boundarySegment.GetCurve (); //Curve

                            CurveLoop clCurveLoop = new CurveLoop();

                                //clCurveLoop.GetEnumerator().Current.
                            clCurveLoop.Append(clCurve); //CurveLoop


                            if (clCurveLoop != null)
                            {
                                CreateRailing(_doc, clCurveLoop, levelId);
                                clCurveLoop.GetCurveLoopIterator().Reset();
                                    continue;
                            }
                            
                            //clCurveLoop.GetEnumerator().Dispose();

                        } 
                        
                    }
                }

                

                TaskDialog.Show("Сделано", "Плинтуса сделаны");

                t.Commit();

            }
            return Result.Succeeded;

        }

        private void CreateRailing(Document document, CurveLoop curveLoop, ElementId eid)
        {

            var RailingTypeIds = GetCollection(document);
            var rt = RailingTypeIds.First();



            Railing.Create(document, curveLoop, rt, eid);
        }

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

        public ICollection<ElementId> GetCollection(Document doc)
        {
            // Find RailingType
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<ElementId> RailingTypeIds = collector.OfClass(typeof(RailingType)).ToElementIds();

            return RailingTypeIds;

        }


    }
}
