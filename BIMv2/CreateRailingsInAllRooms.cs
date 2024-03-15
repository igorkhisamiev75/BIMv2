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
    public class CreateRailingsInAllRooms : IExternalCommand
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

                    string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                    string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                    Curve clCurve;

                    //CurveLoop clCurveLoop = new CurveLoop();

                    var segments = room.GetBoundarySegments(new SpatialElementBoundaryOptions());

                    if (null != segments) //the room may not be bound
                    {
                        foreach (var segmentList in segments)

                            foreach (var boundarySegment in segmentList)
                            {
                                clCurve = boundarySegment.GetCurve(); //Curve

                                CurveLoop clCurveLoop = new CurveLoop();

                                //clCurveLoop.GetEnumerator().Current.
                                clCurveLoop.Append(clCurve); //CurveLoop


                                if (clCurveLoop != null)
                                {
                                    CreateRailing(_doc, clCurveLoop, levelId, roomName, roomNumber);
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

        private void CreateRailing(Document document, CurveLoop curveLoop, ElementId eid, string nR, string numR)
        {

            var RailingTypeIds = GetCollection(document);
            var rt = RailingTypeIds.First();

            foreach (ElementId RailingTypeId in RailingTypeIds)
            {
                var el = _doc.GetElement(RailingTypeId);

                if (el.Name == "02_Плинтус напольный_2000х120мм")
                {
                    rt = RailingTypeId;
                    continue;
                }

                //el.Name;

            }

            Railing d = Railing.Create(document, curveLoop, rt, eid);

            if (null != d)
            {
                d.LookupParameter("ADSK_Примечание").Set(numR);
                d.LookupParameter("Комментарии").Set(nR);

            }


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