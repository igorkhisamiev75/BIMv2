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
    class CreateNumberRoomForFloors : IExternalCommand
    {
        #region Class Variables
        // Revit application
        Autodesk.Revit.ApplicationServices.Application m_application;
        // Current document in Revit
        Document m_document;
        
        public List<Element> projectRooms = new List<Element>();
        public List<Element> AllRooms = new List<Element>();
        #endregion
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            m_document = commandData.Application.ActiveUIDocument.Document;

           

            try
            {

                    // List all Floor's
                    var floor_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();
            IList<Element> allFloors = floor_collector.ToElements();

            //список линкованных файлов
            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances(doc);

            if (linkedInstances != null)
            {

                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                    var transform = linkInstance.GetTotalTransform();

                    List<Element> roomsLink = GetAllRoomsLinked(linkDocument);

                    //сделать проверку на подгруженность файла линк

                    if (roomsLink != null)
                    {
                        foreach (var room in roomsLink) //may be not element, but room come back
                        {
                            var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();

                            if (areaRoom > 0)
                            {
                                AllRooms.Add(room);
                            }

                            

                        }


                    }

                    
                }
            }

            // Represents the criteria for boundary elements to be considered bounding roofs
            LogicalOrFilter categoryFilter = new LogicalOrFilter(new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                new ElementCategoryFilter(BuiltInCategory.OST_FloorsFinish1));

            // Calculator for room/space geometry.
                SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(doc);

            // Stores the resulting room->roof relationships
            Dictionary<Element, List<ElementId>> roomsAndFloors = new Dictionary<Element, List<ElementId>>();

            foreach (var room in AllRooms)
            {
                var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();

                if (areaRoom > 0)
                {
                    // Get room geometry & boundaries          
                    var results = calculator.CalculateSpatialElementGeometry((SpatialElement)room);

                    // Get solid geometry so we can examine each face
                    var geometry = results.GetGeometry();

                    foreach (Face face in geometry.Faces)
                    {
                        // Get list of roof boundary subfaces for a given face
                        var boundaryFaces = results.GetBoundaryFaceInfo(face);

                        foreach (var boundaryFace in boundaryFaces)
                        {
                            var pf = boundaryFace.SubfaceType == SubfaceType.Bottom;
                            
                            // Get boundary element
                            var boundaryElementId = boundaryFace.SpatialBoundaryElement;

                            // Only considering local file room bounding elements
                            var localElementId = boundaryElementId.HostElementId;

                            // Evaluate if element meets criteria using PassesFilter()
                            if (localElementId != ElementId.InvalidElementId &&
                                categoryFilter.PassesFilter(m_document, localElementId))
                            {
                                // Room already has roofs, add more
                                if (roomsAndFloors.ContainsKey(room))
                                {
                                    var roofs = roomsAndFloors[room];
                                    if (!roofs.Contains(localElementId))
                                        roofs.Add(localElementId);
                                    TaskDialog.Show("First", roofs.Count.ToString());
                                }
                                // Room found first roof
                                else
                                {
                                    var roofs = new List<ElementId>();
                                    roofs.Add(localElementId);
                                    roomsAndFloors.Add(room, roofs);
                                    TaskDialog.Show("Second", roofs.Count.ToString());
                                }

                                break;
                            }
                        }
                    }
                }
                
            }

            foreach (var element in allFloors)
            {
                var minPoint = element.get_BoundingBox(doc.ActiveView).Min;
                var maxPoint = element.get_BoundingBox(doc.ActiveView).Max;
                XYZ zN = new XYZ(0, 0, 2);

                var newSearchPoint = (minPoint + maxPoint)/2+ zN;
                TaskDialog.Show("Rooms", newSearchPoint.ToString());


            }

            //foreach (var VARIABLE in allFloors)
            //{
            //    var bb = VARIABLE.get_BoundingBox(doc.ActiveView).Max;
            //    TaskDialog.Show("Element", bb.ToString());
            //}
            //TaskDialog.Show("Rooms", roomsLink.Count.ToString());






            //IList<string> numFinishList = new List<string>();

            //группировка по отделке
            //var roomsGroups = allFloors.GroupBy(p => p.LookupParameter("Тип отделки пола").AsValueString());

            //using (Transaction worksetTransaction = new Transaction(doc, "Set preview view id"))
            //{
            //    worksetTransaction.Start();
            //    //пробегаемся по сгруппированным значениям

            //    foreach (IGrouping<string, Element> element in roomsGroups)
            //    {
            //        IList<string> numroomList = new List<string>();
            //        numroomList.Clear();

            //        //создаем массив с номерами помещений
            //        foreach (var t in element)
            //        {
            //            numroomList.Add(t.LookupParameter("Номер").AsString());
            //        }

            //        string numbersAllRoomString = string.Join(", ", numroomList.ToArray());

            //        numroomList.Clear();


            //        foreach (var t in element)
            //        {

            //            t.LookupParameter("ADSK_Номера помещений для пола").Set(numbersAllRoomString);
            //        }

            //    }

            //    worksetTransaction.Commit();
            //}

            TaskDialog.Show("Rooms", AllRooms.Count.ToString());

            return Result.Succeeded;

            }

            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
            finally
            {
                
            }

            

        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances(Document doc)
        {
            var collector = new FilteredElementCollector(doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        public List<Element> GetAllRoomsLinked(Document document)
        {
            FilteredElementCollector roomCollect = new FilteredElementCollector(document);
            roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
            Room room = null;

            projectRooms = new List<Element>();

            foreach (Element elem in roomCollect)
            {
                room = elem as Room;
                if (room != null)
                {
                    projectRooms.Add(room);
                }
            }

            return projectRooms;
        }

    }
}
