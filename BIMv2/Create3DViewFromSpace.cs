using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class Create3DViewFromSpace : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public List<Room> ProjectRooms2List = new List<Room>();
        public List<Room> ProjectRoomsList = new List<Room>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
           
            IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances(); //список линкованных файлов
            
            if (linkedInstances != null)
            {
                foreach (var linkInstance in linkedInstances)
                {
                    Document linkDocument = linkInstance.GetLinkDocument();
                   
                    //create room's from linked files
                    var roomsLink = GetAllRoomsLinked(linkDocument);

                    if (roomsLink != null)
                    {
                        foreach (Room room in roomsLink)
                        {
                            if (room.Area != 0 && room.Location != null)
                            {

                                ProjectRoomsList.Add(room);

                            }
                        }
                    }
                }
            }
            
            // get a ViewFamilyType for a 3D View
            ViewFamilyType viewFamilyType =
                (from v in new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                 where v.ViewFamily == ViewFamily.ThreeDimensional
                 select v).First();

            using (Transaction t = new Transaction(_doc, "Create view"))
            {
                t.Start();

                foreach (var room in ProjectRoomsList)
                {
                    BoundingBoxXYZ boundingBoxXYZ = new BoundingBoxXYZ();

                    if (room.Area > 0 )
                    {
                        try
                        {
                            // Create the 3d view
                            View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                            // Set the name of the view
                            view.Name = room.Name + " 3D вид";

                            //t.SetName("Create view " + view.Name);
                            // Create a new BoundingBoxXYZ to define a 3D rectangular space
                            
                            boundingBoxXYZ.Min = room.get_BoundingBox(_doc.ActiveView).Min;
                            boundingBoxXYZ.Max = room.get_BoundingBox(_doc.ActiveView).Max;

                            // Apply this bouding box to the view's section box

                            view.SetSectionBox(boundingBoxXYZ);
                            
                            //view.CropBox = boundingBoxXYZ;
                            
                            // Open the just-created view
                            // There cannot be an open transaction when the active view is set
                            //uiDoc.ActiveView = view;
                        }

                        catch (Exception e)
                        {
                            continue;
                            //TaskDialog.Show("Ошибка", e.Message);
                        }
                    }
                    
                }

                t.Commit();
                TaskDialog.Show("Готово", "Создано " + ProjectRoomsList.Count.ToString()+" видов");
                return Result.Succeeded;

            }
        }

        public List<Room> GetAllRoomsLinked(Document document)
        {
            FilteredElementCollector roomCollect = new FilteredElementCollector(document);
            roomCollect.OfCategory(BuiltInCategory.OST_Rooms);
            Room room = null;
            ProjectRooms2List = new List<Room>();

            foreach (Element elem in roomCollect)
            {
                room = elem as Room;
                if (room != null)
                {
                    ProjectRooms2List.Add(room);
                }
            }

            return ProjectRooms2List;
        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }
    }
}

