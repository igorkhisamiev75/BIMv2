#region Namespace

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion
namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateModelTextRoomNameInRoom : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            XYZ pointRoom = new XYZ();

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                
                CreateModelTextMethod(uiDoc);
                TaskDialog.Show("Готово", "Complite!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }

        public void CreateModelTextMethod(UIDocument uiDocument)
        {
            //Get All Rooms
            var rooms = GetRoomsOnCurrentProject(_doc);
            
            FamilySymbol symbol = GetSymbol(_doc, "Для подписи помещений", "Для подписи помещений");

            //create model text in Rooms
            foreach (var room in rooms)
            {
                //get room name and number
                var roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                var roomNumber= room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                var textModel = roomNumber + "\n" + roomName;

                //level
                var level = room.LevelId;
                Level level1 = GetLevelFromRoomById(level);


                LocationPoint lpPoint = (LocationPoint)room.Location;
                
                XYZ pointRoom = lpPoint.Point;

                XYZ placeXyzPoint = new XYZ(pointRoom.X, pointRoom.Y, 0);

                FamilyInstance familyInstance = _doc.Create.NewFamilyInstance(
                    placeXyzPoint, symbol, level1, StructuralType.NonStructural);

                familyInstance.get_Parameter(new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43")).Set(roomNumber);
                familyInstance.get_Parameter(new Guid("9c98831b-9450-412d-b072-7d69b39f4029")).Set(roomName);

                
            }
            
        }

        public int GetId(ICollection<ElementId> newElement)
        {
            int a = 0;

            foreach (var VARIABLE in newElement)
            {
                a = VARIABLE.IntegerValue;
            }

            return a;
        }

        public IList<Element> GetRoomsOnCurrentProject(Document doc)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            //IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            IList<Element> allRooms = room_collector.ToElements();

            return allRooms;

        }

        public FamilySymbol GetSymbol(Document document, string familyName, string symbolName)
        {
            return new FilteredElementCollector(document).OfClass(typeof(Family)).OfType<Family>().FirstOrDefault(f => f.Name.Equals(familyName))?.GetFamilySymbolIds().Select(id => document.GetElement(id)).OfType<FamilySymbol>().FirstOrDefault(symbol => symbol.Name.Equals(symbolName));
        }

        public Level GetLevelFromRoomById(ElementId elementId)
        {
            FilteredElementCollector lvlCollector = new FilteredElementCollector(_doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (Element l in lvlCollection)
            {
                if (l.Id == elementId)
                {
                    return (Level)l;
                }

            }
            return null;

        }
    }

}