//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Architecture;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    public class CreateRailingInRooms : IExternalCommand
//    {
//        // Member variables
//        Application _app;
//        Document _doc;
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            var uiApp = commandData.Application;
//            var uiDoc = uiApp.ActiveUIDocument;

//            IList<Room> roomsInProject = new List<Room>();

//            _app = uiApp.Application;
//            _doc = uiDoc.Document;
            
            
//            using (var t = new Transaction(_doc))
//            {
//                t.Start("Создаем плинтуса");

//                Element room = SelectedElement(_doc); //select room

                
//                string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
//                string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

//                Room room1 = room as Room;

//                var levelId = room1.LevelId;

//                Curve clCurve;

//                //CurveLoop clCurveLoop = new CurveLoop();

//                var segments = room1.GetBoundarySegments(new SpatialElementBoundaryOptions());

//                if (null != segments) //the room may not be bound
//                {
//                    foreach (var segmentList in segments)

//                        foreach (var boundarySegment in segmentList)
//                        {
//                            clCurve = boundarySegment.GetCurve(); //Curve

//                            CurveLoop clCurveLoop = new CurveLoop();

//                            //clCurveLoop.GetEnumerator().Current.
//                            clCurveLoop.Append(clCurve); //CurveLoop


//                            if (clCurveLoop != null)
//                            {
//                                try
//                                {
//                                    CreateRailing(_doc, clCurveLoop, levelId, roomName, roomNumber);


//                                    clCurveLoop.GetCurveLoopIterator().Reset();
//                                    //TaskDialog.Show("Сделано2", "Плинтуса сделаны");
//                                    continue;
//                                    //_doc.Regenerate();

                                   
//                                }

//                                catch
//                                {
//                                    //TaskDialog.Show("Ошибка", "Ошибка");
//                                    continue;
//                                }

//                            }

//                        }
//                }

                    
//                    TaskDialog.Show("Сделано", "Ограждения по контуру помещения смоделированы");

//                t.Commit();

//            }
//            return Result.Succeeded;

//        }

//        public Element SelectedElement(Document sDocument)
//        {

//            try
//            {
//                var uidoc = new UIDocument(_doc);

//                //sDocument.Regenerate();

//                var r = uidoc.Selection.PickObject(ObjectType.Element,
//                    "Выбрать помещение");
//                var elem = uidoc.Document.GetElement(r);
//                return elem;

//            }

//            catch (Exception ex)
//            {
//                TaskDialog.Show("Revit", "Exception" + ex.Message);
//                //subTransaction.RollBack();
//                return null;
//            }

//        }

//        public void CreateRailing(Document document, CurveLoop curveLoop, ElementId eid, string nR, string numR)
//        {

//            var RailingTypeIds = GetCollection(document);

//            ElementId rt = RailingTypeIds.First();

//            foreach (ElementId RailingTypeId in RailingTypeIds)
//            {
//                var el=_doc.GetElement(RailingTypeId);

//                if (el.Name == "02_Плинтус напольный_2000х120мм")
//                {
//                    rt = RailingTypeId;
//                    continue;
//                }

//                //el.Name;

//            }

//            //ElementId elementId = new ElementId(2589207);

//            Railing d = Railing.Create(document, curveLoop, rt, eid);

//            if (null != d)
//            {
//                d.LookupParameter("ADSK_Примечание").Set(numR);
//                d.LookupParameter("Комментарии").Set(nR);

//            }

           

//        }

//        public IList<Element> GetAllRooms(Document doc)
//        {
//            // List all Room's
//            var room_collector = new FilteredElementCollector(doc)
//                .OfCategory(BuiltInCategory.OST_Rooms)
//                .WhereElementIsNotElementType();
//            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
//            var allRooms = room_collector.ToElements();

//            return allRooms;
//        }

//        public ICollection<ElementId> GetCollection(Document doc)
//        {
//            // Find RailingType
//            FilteredElementCollector collector = new FilteredElementCollector(doc);
//            ICollection<ElementId> RailingTypeIds = collector.OfClass(typeof(RailingType)).ToElementIds();

//            return RailingTypeIds;

//        }


//    }
//}
