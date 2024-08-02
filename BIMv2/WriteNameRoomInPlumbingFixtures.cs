#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
#endregion
namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class WriteNameRoomInPlumbingFixtures : IExternalCommand
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
            using (var t = new Transaction(_doc))
            {
                t.Start("Select all plumbing fixtures");
                WriteNameRoomInElementMethod(uiApp);
                TaskDialog.Show("У тебя получилось", "Выполнено!1");
                t.Commit();
              
            }
            return Result.Succeeded;
        }
        public void WriteNameRoomInElementMethod(UIApplication uiApp)
        {
            var allElements = GetAllElements();

            foreach (var e in allElements)
            {
                try
                {
                    e.get_Parameter(new Guid("a5b27858-5ce0-450c-9c3a-08693b326453")).Set("");
                    e.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038")).Set("");
                }
                catch
                {
                    continue;
                }

            }

            _doc.Regenerate();


            foreach (Element e in allElements)
            {
                try
                {
                    var faz = e.ArePhasesModifiable();

                    if (faz == true)
                    {
                        var loc = e.Location;

                        var L2 = e as FamilyInstance;

                        XYZ verPoint = L2.GetSpatialElementCalculationPoint();

                        LocationPoint locPoint = (LocationPoint)loc;
                        LocationPoint pointPF = locPoint;

                        //if (verPoint != null)


                        var myRoom = FindRoom(_doc, verPoint);

                        var nameRoom = "";
                        var numberRoom = "";

                        var BIM_NumRoom = "7ae9b62e-a182-476b-8297-1b871e262038";
                        var BIM_NameRoom = "a5b27858-5ce0-450c-9c3a-08693b326453";


                        if (myRoom != null)
                        {
                            nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                            numberRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                            e.get_Parameter(new Guid("a5b27858-5ce0-450c-9c3a-08693b326453")).Set(nameRoom);
                            e.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038")).Set(numberRoom);



                        }
                        else
                        {
                            nameRoom = "Проверь точку в семействе и помещение в проекте";
                            numberRoom = "Проверь точку в семействе и помещение в проекте";
                            e.get_Parameter(new Guid("a5b27858-5ce0-450c-9c3a-08693b326453")).Set(nameRoom);
                            e.get_Parameter(new Guid("7ae9b62e-a182-476b-8297-1b871e262038")).Set(numberRoom);


                            //var linkedInstances = FindLinkedInstances();
                            //foreach (var linkInstance in linkedInstances)
                            //{
                            //    var transform = linkInstance.GetTotalTransform().Inverse;
                            //    var targetPoint = transform.OfPoint(pointPF);
                            //    var linkDocument = linkInstance.GetLinkDocument();

                            //    var room = FindRoom(linkDocument, targetPoint);
                            //    if (room != null)
                            //    {
                            //        string[] subs = room.Name.Split(' ');

                            //        e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                            //            .Set(room.Number + " " + subs[0]);
                            //    }
                            //    else
                            //    {
                            //        nameRoom = "It's not in Room";
                            //        e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
                            //            .Set(nameRoom);
                            //    }

                            //}
                        }
                    }



                }
                catch { }

            }
        }
        
        public IList<Element> GetAllElements()
        {
            // List all plumbing fixtures
            var plumbingFixCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
            plumbingFixCollector.OfCategory(BuiltInCategory.OST_PlumbingFixtures);
            var plFixList = plumbingFixCollector.ToElements();
            //// List all generic
            //var genericCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
            //genericCollector.OfCategory(BuiltInCategory.OST_GenericModel);
            //var plFixList2 = genericCollector.ToElements();
            //foreach (var VARIABLE in plFixList2) plFixList.Add(VARIABLE);
            //// List all SE
            //var seCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
            //seCollector.OfCategory(BuiltInCategory.OST_SpecialityEquipment);
            //var plFixList3 = seCollector.ToElements();
            //foreach (var VARIABLE in plFixList3) plFixList.Add(VARIABLE);
            //// List all furniture
            //var fCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
            //fCollector.OfCategory(BuiltInCategory.OST_Furniture);
            //var plFixList4 = fCollector.ToElements();
            //foreach (var VARIABLE in plFixList4) plFixList.Add(VARIABLE);
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
            return document
                .Phases
                .Cast<Phase>()
                .Select(x => document.GetRoomAtPoint(point, x))
                .FirstOrDefault(x => x != null);
        }


    }
}