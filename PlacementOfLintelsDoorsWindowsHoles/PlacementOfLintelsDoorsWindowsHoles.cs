using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;
using System.Diagnostics;


namespace PlacementOfLintelsDoorsWindowsHoles
{
    [Transaction(TransactionMode.Manual)]
    public class PlacementOfLintelsDoorsWindowsHoles : Window, IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get the Revit application and document
                UIApplication uiApp = commandData.Application;
                Document doc = uiApp.ActiveUIDocument.Document;
                var uiDoc = uiApp.ActiveUIDocument;

                // Collect all doors in the project
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                IList<Element> doors = collector
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .ToElements();

                // Prepare data for WPF window
                List<DoorData> doorDataList = new List<DoorData>();

                foreach (Element door in doors)
                {
                    DoorData data = new DoorData();

                    // Basic door properties
                    data.Id = door.Id.ToString();

                    // Get Mark parameter
                    Parameter markParam = door.get_Parameter(BuiltInParameter.DOOR_NUMBER);
                    if (markParam != null && markParam.HasValue)
                        data.Mark = markParam.AsString();

                    // Get door type name
                    ElementId typeId = door.GetTypeId();
                    ElementType type = doc.GetElement(typeId) as ElementType;
                    if (type != null)
                        data.TypeName = type.Name;

                    // Get level name
                    Parameter levelParam = door.get_Parameter(BuiltInParameter.LEVEL_NAME);
                    if (levelParam != null && levelParam.HasValue)
                        data.LevelName = levelParam.AsString();

                    // Get room name (door is in)
                    data.RoomName = GetRoomName(door, doc);

                    // Get width and height
                    Parameter widthParam = door.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                    if (widthParam != null && widthParam.HasValue)
                        data.Width = UnitUtils.ConvertFromInternalUnits(widthParam.AsDouble(), DisplayUnitType.DUT_MILLIMETERS).ToString("F0") + " mm";

                    Parameter heightParam = door.get_Parameter(BuiltInParameter.DOOR_HEIGHT);
                    if (heightParam != null && heightParam.HasValue)
                        data.Height = UnitUtils.ConvertFromInternalUnits(heightParam.AsDouble(), DisplayUnitType.DUT_MILLIMETERS).ToString("F0") + " mm";

                    doorDataList.Add(data);
                }

                // Show WPF window
                //Application app = new Application();
                //MainWindow window = new MainWindow();
                //window.DoorsDataGrid.ItemsSource = doorDataList;
                //window.ShowDialog ();
                //app.Run(window);

                // Show WPF window
                MainWindow window = new MainWindow();
                window.SetDoorData(doorDataList);

                // Show as modal dialog
                window.ShowDialog();

                using (var t = new Transaction(doc))
                {
                    t.Start("Run");
                    uiApp = commandData.Application;
                    uiDoc = uiApp.ActiveUIDocument;

                    window.SetDoorData(doorDataList);

                    t.Commit();

                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        public string GetRoomName(Element door, Document doc)
        {
            // Get the room that contains the door
            LocationPoint locPoint = door.Location as LocationPoint;
            if (locPoint != null)
            {
                XYZ point = locPoint.Point;
                Room room = doc.GetRoomAtPoint(point);
                if (room != null)
                {
                    Parameter roomNameParam = room.get_Parameter(BuiltInParameter.ROOM_NAME);
                    if (roomNameParam != null && roomNameParam.HasValue)
                        return roomNameParam.AsString();
                }
            }
            return "Not Placed";
        }
    }
}
