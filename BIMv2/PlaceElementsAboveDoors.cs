#region Namespace
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;
#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceElementsAboveDoors : IExternalCommand
    {
        private Document _doc;

        public List<XYZ> pointsList = new List<XYZ>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _doc = uiDoc.Document;

            FamilySymbol elementSymbol = GetSymbol(_doc, "ADSK_Обобщенная модель_Перемычка составная", "Перемычка составная");

            if (elementSymbol == null)
            {
                TaskDialog.Show("Error", "Семейство перемычки не загружено");
                return Result.Failed;
            }

            using (var t = new Transaction(_doc))
            {
                t.Start("Расставляем перемычки");

                // Get all doors in the project
                FilteredElementCollector doorCollector = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType();

                foreach (FamilyInstance door in doorCollector)
                {
                    //ВЫСОТА ДВЕРИ
                    var haightDoor=door.LookupParameter("Высота").AsDouble();

                    //haightDoor = UnitUtils.Convert(haightDoor, DisplayUnitType.DUT_MILLIMETERS, DisplayUnitType.DUT_DECIMAL_FEET);

                    if (haightDoor == null)
                    {
                        haightDoor = 0;
                    }

                    Element wall=door.Host;
                    // Get door location and orientation
                    LocationPoint doorLoc = door.Location as LocationPoint;
                    if (doorLoc == null) continue;

                    XYZ doorPosition = doorLoc.Point;
                    //XYZ doorDirection = door.FacingOrientation;

                    //double x2 = doorLoc.Point.X;
                    //double y2 = doorLoc.Point.Y;
                    //double z2 = doorLoc.Point.Z - 4.62467;

                    //XYZ locationWindow = new XYZ(x2, y2, z2);

                    // Calculate position above door
                    XYZ placementPoint = new XYZ(
                        doorPosition.X,
                        doorPosition.Y,
                        doorPosition.Z 
                    );

                    //TaskDialog.Show("точка", $"{placementPoint}");

                    try
                    {
                        // Create the light (aligned with door direction)
                        FamilyInstance light = _doc.Create.NewFamilyInstance(
                            placementPoint,
                            elementSymbol,
                            wall,
                            door.Document.GetElement(door.LevelId) as Level,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                        );

                        light.LookupParameter("Отметка перемычки").Set(haightDoor);
                        _doc.Regenerate();
                        continue;

                    }
                    catch
                    {
                        continue;
                    }

                

               
            }

                TaskDialog.Show("Complite!", $"Размещено  {doorCollector.Count()} перемычек над дверьми");
                t.Commit();

                return Result.Succeeded;
            }
        }


       

        public FamilySymbol GetSymbol(Document document, string familyName, string symbolName)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(Family)).OfType<Family>()
                .FirstOrDefault(f => f.Name.Equals(familyName))?.GetFamilySymbolIds()
                .Select(id => document.GetElement(id)).OfType<FamilySymbol>().FirstOrDefault(symbol => symbol.Name.Equals(symbolName));
        }

    }
}



