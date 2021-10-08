using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;


namespace CreateElementsInRooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class Command : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        private List<Room> m_rooms = new List<Room>();
        private string buttonText = "";

        public virtual Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            
            try
            {
                m_rooms = GetAllRoomsLinked();
                TransactionGroup transactionGroup = new TransactionGroup(commandData.Application.ActiveUIDocument.Document);
                //Create a transaction
                transactionGroup.Start("Create elements");

                // create a new instance of class RoomsData
                RoomsData data = new RoomsData(commandData);

                // create a form to display the information of rooms
                roomsInformationForm infoForm = new roomsInformationForm(data, commandData);
                infoForm.ShowDialog();

                //array selected rooms
                List<string> infoFormChecListRoom = infoForm.checListRoom;

                buttonText = infoForm.ActiveControl.Text.ToString();

                //TaskDialog.Show("выбраны", $"{txt}");

                if (infoFormChecListRoom!=null)
                {
                  CreateAnyElementInLinkedFiles(infoFormChecListRoom);
                }
                
                //TaskDialog.Show("выбраны", $"{s}");

                transactionGroup.Commit();
                return Result.Succeeded;
            }

            catch (Exception ex)
            {
                // If there are something wrong, give error information and return failed
                message = ex.Message;
                return Result.Failed;
            }
            
        }

        public void CreateAnyElementInLinkedFiles(List<string> checListRoom)
        {
            var elem = SelectedElement(_doc); //select element

            var familyInstance = elem as FamilyInstance;
            var familySymbol = familyInstance.Symbol;

            //create new family in rooms

            using (Transaction transaction = new Transaction(_doc/*, Guid.NewGuid().GetHashCode().ToString()*/))

            {
                try
                {
                    transaction.Start("Create");
                    var linkedInstances = FindLinkedInstances(_doc);
                    int j = 0;

                    if (linkedInstances != null)
                        foreach (var linkInstance in linkedInstances)
                        {
                            var linkDocument = linkInstance.GetLinkDocument();
                            var transform = linkInstance.GetTotalTransform();

                            var roomsLink = m_rooms;

                            if (roomsLink != null)
                                foreach (var room in roomsLink)
                                {
                                    var roomName = room.Name;

                                    var justname = "";
                                    for (var i = 0; i < roomName.Length; i++)
                                        if (!char.IsDigit(roomName[i]) && !roomName[i].Equals('.') &&
                                            !roomName[i].Equals(','))
                                            justname += roomName[i];

                                    var FindRoom = checListRoom.Find(item => item == justname);

                                    if (FindRoom != null)
                                    {
                                        double heightRoom = 0;
                                        var areaRoom = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                                        var volumeRoom = room.get_Parameter(BuiltInParameter.ROOM_VOLUME).AsDouble();
                                        if (buttonText == "CEILING")
                                        {
                                            heightRoom = volumeRoom / areaRoom;
                                        }
                                        
                                        

                                        if (areaRoom != 0)
                                        {
                                            var level = room.LevelId;

                                            var level1 = GetLevelFromRoom(level, room);

                                            if (level1 == null) level1 = GetLevelFromRoomByName(room);

                                            var lpPoint = (LocationPoint)room.Location;

                                            var pointFoLigh = lpPoint.Point;

                                            var targetPoint = transform.OfPoint(pointFoLigh);

                                            var placeXyzPoint = new XYZ(targetPoint.X, targetPoint.Y, heightRoom);


                                            var newFamilyInstance = _doc.Create.NewFamilyInstance(
                                                placeXyzPoint, familySymbol, level1, StructuralType.NonStructural);
                                            j++;
                                            //document.Regenerate();
                                        }
                                    }
                                }
                        }

                    TaskDialog.Show("Размещено", $"{j} элементов");

                    transaction.Commit();
                }

                catch (Exception ex)
                {
                    transaction.RollBack();
                    TaskDialog.Show("Revit", "Exception " + ex.Message);
                }

            }

        }

        public Element SelectedElement(Document sDocument)
        {
            //var document = m_revit.ActiveUIDocument.Document;
            //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
            //var elem = document.GetElement(earthId);
            //uiDoc = m_revit.ActiveUIDocument;
            //using (Transaction subTransaction = new Transaction(sDocument, Guid.NewGuid().GetHashCode().ToString()))
            //using (SubTransaction subTransaction = new SubTransaction(sDocument))
            //{
            //subTransaction.Start();
            try
            {
                /*if (*/
                //subTransaction.Start("sdf");/* == TransactionStatus.Started)*/
                //if (subTransaction.Start() == TransactionStatus.Started)
                //if (subTransaction.Start() == TransactionStatus.Started)
                //{

                //var uiDoc = m_revit.ActiveUIDocument;
                //uiDoc.ActiveView;

                var uidoc = new UIDocument(_doc);

                //sDocument.Regenerate();

                var r = uidoc.Selection.PickObject(ObjectType.Element,
                    "Выбрать элемент для размещения");
                var elem = uidoc.Document.GetElement(r);
                return elem;
                //TaskDialog.Show("Revit", "Exception" + elem);

                //var earthId = "942456a3-5e8a-4c24-a195-e6a26edf8b8d-00000d81";
                //var elem = sDocument.GetElement(earthId);


                //if (subTransaction.Commit() == TransactionStatus.Committed)
                //{
                //    return elem;
                //}
                //}
            }

            catch (Exception ex)
            {
                TaskDialog.Show("Revit", "Exception" + ex.Message);
                //subTransaction.RollBack();
                return null;
            }


            //}
        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances(Document document)
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        private Level GetLevelFromRoom(ElementId elementId, Room room)
        {
            var lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (var l in lvlCollection)
                if (l.Id == elementId)
                    return (Level)l;

            return null;
        }

        private Level GetLevelFromRoomByName(Room room)
        {
            var lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (var l in lvlCollection)
                if (l.Name == room.Level.Name)
                    return (Level)l;
            return null;
        }

        public List<Room> GetAllRoomsLinked()
        {
            
            var linkedInstances = FindLinkedInstances(_doc);

            if (linkedInstances != null)
                foreach (var linkInstance in linkedInstances)
                {
                    var linkDocument = linkInstance.GetLinkDocument();
                    var transform = linkInstance.GetTotalTransform();

                    var roomCollect = new FilteredElementCollector(linkDocument);
                    roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
                    Room room = null;
                    m_rooms = new List<Room>();

                    foreach (var elem in roomCollect)
                    {
                        room = elem as Room;
                        if (room != null) m_rooms.Add(room);
                    }
                }

            return m_rooms;
        }

    }
    
}
