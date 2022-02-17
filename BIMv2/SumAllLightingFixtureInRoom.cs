#region Namespace

using System;
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
    public class SumAllLightingFixtureInRoom : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            Selection selection = uiDoc.Selection;
            IList<Element> sElements = selection.PickElementsByRectangle();


            using (var t = new Transaction(_doc))
            {
                t.Start("Select all lighting fixtures");
                
                WriteNameRoomInElementMethod(uiApp, sElements);
                SumAllLightningFixInRoom();

                TaskDialog.Show("Complite!", "Complite!  ☺☺☺");

                t.Commit();
               
            }

            return Result.Succeeded;
        }

        public void WriteNameRoomInElementMethod(UIApplication uiApp, IList<Element> sElements) //проставляем помещения в светильники
        {
            IList<Element> allElements = new List<Element>();

            //Selection selection = uiApp.ActiveUIDocument.Selection;
            //IList<Element> sElements2 = selection.PickElementsByRectangle();
            
            if (sElements == null)
            {
                allElements = GetAllElements(); //get all lighting
            }
            else
            {
                allElements = sElements;
            }

            

            foreach (var e in allElements)
            {
                Room myRoom;
                var nameRoom = "";
                var familyInstance = (FamilyInstance)e;

                if (familyInstance.HasSpatialElementCalculationPoint) // if calculation point == true
                {
                    var scp = familyInstance.GetSpatialElementCalculationPoint();
                    myRoom = FindRoom(_doc, scp);

                    if (myRoom != null)
                    {
                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                   myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                        e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                            .Set(nameRoom);
                        break;
                    }

                    else
                    {
                        var linkedInstances = FindLinkedInstances(); //get all linked files
                        foreach (var linkInstance in linkedInstances)
                        {
                            var transform = linkInstance.GetTotalTransform().Inverse;
                            var targetPoint = transform.OfPoint(scp);
                            var linkDocument = linkInstance.GetLinkDocument();

                            var room = FindRoom(linkDocument, targetPoint);
                            if (room != null)
                            {
                                nameRoom = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                           room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                                //var subs = room.Name.Split(' ');

                                //e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                //    .Set(room.Number + " " + subs[0]);

                                e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                    .Set(nameRoom);
                                
                                break;
                            }
                            else
                            {
                                nameRoom = "It's not in Room";
                                e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                    .Set(nameRoom);
                            }
                        }
                    }
                    
                }

                else
                {
                    var loc = e.Location;
                    var locPoint = (LocationPoint)loc;
                    var pointPF = locPoint.Point;

                    myRoom = FindRoom(_doc, pointPF);
                    if (myRoom != null)
                    {
                        nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                   myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                        e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                            .Set(nameRoom);
                    }

                    else
                    {
                        var linkedInstances = FindLinkedInstances(); //get all linked files
                        foreach (var linkInstance in linkedInstances)
                        {
                            var transform = linkInstance.GetTotalTransform().Inverse;
                            var targetPoint = transform.OfPoint(pointPF);
                            var linkDocument = linkInstance.GetLinkDocument();

                            var room = FindRoom(linkDocument, targetPoint);
                            if (room != null)
                            {
                                nameRoom = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
                                           room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

                                //var subs = room.Name.Split(' ');

                                //e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                //    .Set(room.Number + " " + subs[0]);
                                e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                    .Set(nameRoom);
                                break;

                            }
                            else
                            {
                                nameRoom = "It's not in Room";
                                e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                                    .Set(nameRoom);
                            }
                        }

                    }
                }
                
                
            }
        }

        public void SumAllLightningFixInRoom() // sum and set count lightning
        {
            //все светильники в которые прописали ADSK_Зона bd940efd-bae8-43c6-a839-454b44ff6baa

            var listLightFixtturel = GetAllElements();
            var sharedGUIDZONA = "c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5";
            var sharedGUIDCOUNTLIGHTFIX = "bd940efd-bae8-43c6-a839-454b44ff6baa";
            
            foreach (var svet in listLightFixtturel)
            {
                var countSvet = 0;

                foreach (var element in listLightFixtturel)
                    if (svet.get_Parameter(new Guid(sharedGUIDZONA)).AsString() == 
                        element.get_Parameter(new Guid(sharedGUIDZONA)).AsString() &&
                        getFamilyName(svet) == getFamilyName(element))
                        countSvet++;

                svet.get_Parameter(new Guid(sharedGUIDCOUNTLIGHTFIX))
                    .Set(countSvet.ToString());
            }
        }

        public string getFamilyName(Element e)
        {
            var sharedADSKNaimenovanie = "e6e0f5cd-3e26-485b-9342-23882b20eb43";
            //var eId = e.GetTypeId();
            //var elementType = (ElementType)_doc.GetElement(eId);
            //var familyName = elementType.FamilyName;
            var nameADSKNaimenovanie = e.GetTypeId().ToString();
            return nameADSKNaimenovanie;
        }

        public IList<Element> GetAllElements()
        {
            // List all lighting fixtures
            var lightingFixCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
            lightingFixCollector.OfCategory(BuiltInCategory.OST_LightingFixtures);
            var plFixList = lightingFixCollector.ToElements();

            return plFixList;
        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        private static Room FindRoom(Document document, XYZ point)
        {
            if (document != null)
            {
                return document
                    .Phases
                    .Cast<Phase>()
                    .Select(x => document.GetRoomAtPoint(point, x))
                    .FirstOrDefault(x => x != null);
            }

            return null;
        }
    }
}