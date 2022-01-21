using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateNumberRoomForFloors : IExternalCommand
    {
        public List<Element> projectRooms = new List<Element>();
        public List<Element> AllRooms = new List<Element>();
        public List<Floor> AllF = new List<Floor>();
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("WriteNumberRoomInFloorsComment");

                WriteNumberRoomInFloorsComment(doc);
                var allFloors = GetAllFloorElements(doc);
                
                //TaskDialog.Show("Сделано", "Прописано коментарии в "+$"{allFloors.Count.ToString()}");
                doc.Regenerate();

                t.Commit();

                t.Start("Stage 2");

                //WriteAllRoom(doc);
                //TaskDialog.Show("Сделано", "Сгруппированы перекрытия");

                t.Commit();

            }
            return Result.Succeeded;
            
        }

        public void WriteAllRoom(Document doc)
        {
            var allFloors = GetAllFloorElements(doc);

            foreach (var VARIABLE in allFloors)
            {
                AllF.Add((Floor)VARIABLE);

            }
            
            //группировка по типу пола
            var groups = AllF.GroupBy(p => p.Name);

            //пробегаемся по сгруппированным значениям

            foreach (IGrouping<string, Element> element in groups)
            {
                IList<string> numroomList = new List<string>();
                numroomList.Clear();

                //создаем массив с номерами помещений
                foreach (var t in element)
                {
                    var otd = t.LookupParameter("Номер помещения (отделка)").AsString();
                    string[] subs = otd.Split(' ');
                    numroomList.Add(subs[0]);
                }

                //clean list
                List<string> newList = new List<string>(numroomList.Distinct());

                newList.Sort();

                string numbersAllRoomString = string.Join(", ", newList.ToArray());

                numroomList.Clear();

                //и записываем это массив в каждый номер этого массива

                foreach (var t in element)
                {

                    t.LookupParameter("Комментарии").Set(numbersAllRoomString);
                }

            }

        }

        public void WriteNumberRoomInFloorsComment(Document doc)
        {
            var allFloors = GetAllFloorElements(doc);
           
            foreach (var element in allFloors)
            {
                Room myRoom;
                string nameRoom = "";

                XYZ gXyz = new XYZ();

                Floor floor = (Floor)element;

                ElementId eid = floor.Id;

                Options opts = new Options();

                List<List<XYZ>> polygons = new List<List<XYZ>>();
                
                GeometryElement geo = floor.get_Geometry(opts);

                foreach (GeometryObject obj in geo) // 2013
                {
                    Solid solid = obj as Solid;
                    if (solid != null)
                    {
                        PlanarFace lowest = null;
                        FaceArray faces = solid.Faces;
                        
                        foreach (Face f in faces)
                        {
                            
                            PlanarFace pf = f as PlanarFace;
                            
                            gXyz = pf.Origin;

                            if (pf.FaceNormal.X==0 && pf.FaceNormal.Y == 0 && pf.FaceNormal.Z == 1)
                            {
                                break;
                            }
                            
                        }

                    }
                }

                List<GeometryElement> geometryElements = new List<GeometryElement>(); 

                geometryElements.Add(geo);

                CurveArray profile = new CurveArray();

                foreach (var geometryle in geometryElements)
                {
                    foreach (var obj in geometryle)
                    {
                        
                            profile.Append(obj as Curve);
                        
                    }
                }
                
                XYZ zN = new XYZ(0, 0, 2);

                var newSearchPoint = gXyz+zN;

                myRoom = FindRoom(doc, newSearchPoint);

                if (myRoom != null)
                {
                    nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                               myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                    element.LookupParameter("Комментарии").Set(nameRoom);

                }

                else
                {
                    var linkedInstances = FindLinkedInstances(doc); //get all linked files

                    foreach (var linkInstance in linkedInstances)
                    {
                        var transform = linkInstance.GetTotalTransform().Inverse;
                        var targetPoint = transform.OfPoint(newSearchPoint);
                        var linkDocument = linkInstance.GetLinkDocument();

                        var room = FindRoom(linkDocument, targetPoint);
                        if (room != null)
                        {
                            nameRoom = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                       room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                            element.LookupParameter("Комментарии").Set(nameRoom);
                            break;
                        }
                        else
                        {
                            nameRoom = "";
                            element.LookupParameter("Комментарии").Set(nameRoom);
                        }
                    }
                }

            }
        }
        


        private Room FindRoom(Document doc, XYZ newSearchPoint)
        {
            if (doc != null)
            {
                return doc
                    .Phases
                    .Cast<Phase>()
                    .Select(x => doc.GetRoomAtPoint(newSearchPoint, x))
                    .FirstOrDefault(x => x != null);
            }

            return null;
        }
        
        public IEnumerable<RevitLinkInstance> FindLinkedInstances(Document doc)
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

        public IList<Element>  GetAllFloorElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();
            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

        public List<Element> GetAllRoom(Document doc)
        {
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

            return AllRooms;
        }

    }
}
