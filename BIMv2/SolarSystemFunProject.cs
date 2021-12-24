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
    public class SolarSystemFunProject : IExternalCommand
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

                string earthId = "54d88471-fd75-4915-9c1b-f310666605a7-00001353";

                Element e = _doc.GetElement(earthId);

                XYZ point1 = new XYZ(0, 0, 0);
                XYZ point2 = new XYZ(0, 0, 10);
                

                ElementTransformUtils.RotateElement(_doc, e.Id, Line.CreateBound(point1, point2), Math.PI/182.5);
                
                //MoveColumn(_doc, e);

                //TaskDialog.Show("У тебя получилось", "Complite!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }

        public void MoveColumn(Document document, Element element)
        {

            // get the column current location
            //LocationPoint elementLocation = element.Location as LocationPoint;

            //XYZ oldPlace = elementLocation.Point;

            // Move the column to new location.
            XYZ newPlace = new XYZ(0, 0, 2);

            ElementTransformUtils.MoveElement(document, element.Id, newPlace);

            // now get the column's new location
            //elementLocation = element.Location as LocationPoint;
            //XYZ newActual = elementLocation.Point;

            //string info = "Original Z location: " + oldPlace.Z +
            //              "\nNew Z location: " + newActual.Z;

            //TaskDialog.Show("Revit", info);
        }

        //public void WriteLightingFixturesillumination(UIApplication uiApp)
        //{
        //    var allElements = GetAllLightFix();

        //    foreach (var e in allElements)
        //    {
        //        var loc = e.Location;
        //        var locPoint = (LocationPoint)loc;
        //        var pointPF = locPoint.Point;
        //        var myRoom = FindRoom(_doc, pointPF);
        //        var nameRoom = "";

        //        if (myRoom != null)
        //        {
        //            nameRoom = myRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString() + " " +
        //                           myRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();

        //            e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
        //                .Set(nameRoom);

        //        }
        //        else
        //        {
        //            var linkedInstances = FindLinkedInstances();
        //            foreach (var linkInstance in linkedInstances)
        //            {
        //                var transform = linkInstance.GetTotalTransform().Inverse;
        //                var targetPoint = transform.OfPoint(pointPF);
        //                var linkDocument = linkInstance.GetLinkDocument();

        //                var room = FindRoom(linkDocument, targetPoint);
        //                if (room != null)
        //                {
        //                    string[] subs = room.Name.Split(' ');

        //                    e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
        //                        .Set(room.Number + " " + subs[0]);
        //                }
        //                else
        //                {
        //                    nameRoom = "It's not in Room";
        //                    e.get_Parameter(new Guid("c78f0a7d-b68b-4d21-a247-1c8c6ced8bc5"))
        //                        .Set(nameRoom);
        //                }

        //            }
        //        }
        //    }
        //}
        //public IList<Element> GetAllLightFix()
        //{
        //    // List all plumbing fixtures
        //    var plumbingFixCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
        //    plumbingFixCollector.OfCategory(BuiltInCategory.OST_PlumbingFixtures);
        //    var plFixList = plumbingFixCollector.ToElements();
        //    // List all generic
        //    var genericCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
        //    genericCollector.OfCategory(BuiltInCategory.OST_GenericModel);
        //    var plFixList2 = genericCollector.ToElements();
        //    foreach (var VARIABLE in plFixList2) plFixList.Add(VARIABLE);
        //    // List all SE
        //    var seCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
        //    seCollector.OfCategory(BuiltInCategory.OST_SpecialityEquipment);
        //    var plFixList3 = seCollector.ToElements();
        //    foreach (var VARIABLE in plFixList3) plFixList.Add(VARIABLE);
        //    // List all furniture
        //    var fCollector = new FilteredElementCollector(_doc).OfClass(typeof(FamilyInstance));
        //    fCollector.OfCategory(BuiltInCategory.OST_Furniture);
        //    var plFixList4 = fCollector.ToElements();
        //    foreach (var VARIABLE in plFixList4) plFixList.Add(VARIABLE);
        //    return plFixList;
        //}
        //private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        //{
        //    var collector = new FilteredElementCollector(_doc);
        //    return collector
        //        .OfClass(typeof(RevitLinkInstance))
        //        .Cast<RevitLinkInstance>()
        //        .ToList();
        //}
        //private static Room FindRoom(Document document, XYZ point)
        //{
        //    return document
        //        .Phases
        //        .Cast<Phase>()
        //        .Select(x => document.GetRoomAtPoint(point, x))
        //        .FirstOrDefault(x => x != null);
        //}


    }
}