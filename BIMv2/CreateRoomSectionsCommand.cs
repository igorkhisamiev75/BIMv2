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
    public class CreateRoomSectionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Получаем все помещения в проекте
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .ToList();

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "В проекте не найдены помещения.");
                    return Result.Cancelled;
                }

                using (Transaction trans = new Transaction(doc, "Создание разрезов помещений"))
                {
                    trans.Start();

                    foreach (Room room in rooms)
                    {
                        try
                        {
                            CreateSectionsAroundRoom(doc, room);
                        }

                        catch { }
                        
                    }

                    trans.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private void CreateSectionsAroundRoom(Document doc, Room room)
        {
            var options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };

            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
            if (boundaries == null || boundaries.Count == 0) return;

            Level level = doc.GetElement(room.LevelId) as Level;
            double roomHeight = GetRoomHeight(doc, room, level);
            XYZ roomCenter = GetRoomCenter(room);

            foreach (var boundary in boundaries)
            {
                foreach (var segment in boundary)
                {
                    Element boundaryElement = doc.GetElement(segment.ElementId);
                    if (boundaryElement == null) continue;

                    Curve curve = segment.GetCurve();
                    if (curve == null) continue;

                    XYZ offsetDirection = GetOffsetDirection(curve, roomCenter);
                    double offset = 0.0328; // 10 мм в футах
                    Curve offsetCurve = curve.CreateOffset(offset, offsetDirection);

                    CreateSectionView(doc, offsetCurve, room, level, roomHeight);
                }
            }
        }

        private XYZ GetRoomCenter(Room room)
        {
            LocationPoint locPoint = room.Location as LocationPoint;
            if (locPoint != null) return locPoint.Point;

            BoundingBoxXYZ bb = room.get_BoundingBox(null);
            return (bb.Max + bb.Min) * 0.5;
        }

        private double GetRoomHeight(Document doc, Room room, Level level)
        {
            // Попробуем получить высоту из параметра помещения
            Parameter heightParam = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
            if (heightParam != null && heightParam.HasValue)
            {
                return heightParam.AsDouble();
            }

            // Или вычисляем по следующему уровню
            var nextLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Where(l => l.Elevation > level.Elevation)
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

            return nextLevel != null ?
                nextLevel.Elevation - level.Elevation :
                3.0 / 0.3048; // 3 метра по умолчанию
        }

        private XYZ GetOffsetDirection(Curve curve, XYZ roomCenter)
        {
            XYZ curveCenter = (curve.GetEndPoint(0) + curve.GetEndPoint(1)) * 0.5;
            XYZ direction = (roomCenter - curveCenter).Normalize();

            if (Math.Abs(curve.GetEndPoint(0).Z - curve.GetEndPoint(1).Z) > 0.001)
            {
                return XYZ.BasisZ;
            }

            XYZ tangent = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            return new XYZ(-tangent.Y, tangent.X, 0);
        }

        private void CreateSectionView(Document doc, Curve curve, Room room, Level level, double height)
        {
            XYZ curveMidPoint = (curve.GetEndPoint(0) + curve.GetEndPoint(1)) * 0.5;
            XYZ curveDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            XYZ viewDirection = new XYZ(-curveDirection.Y, curveDirection.X, 0);

            var sectionBox = new BoundingBoxXYZ
            {
                Transform = Transform.CreateTranslation(curveMidPoint) *
                           Transform.CreateRotation(XYZ.BasisZ, Math.Atan2(viewDirection.Y, viewDirection.X))
            };

            double sectionWidth = curve.Length + 2.0;
            double sectionDepth = 1.0;

            sectionBox.Min = new XYZ(-sectionDepth, 0, -sectionWidth / 2);
            sectionBox.Max = new XYZ(sectionDepth, height, sectionWidth / 2);

            ViewSection sectionView = ViewSection.CreateSection(doc,
                doc.GetDefaultElementTypeId(ElementTypeGroup.ViewTypeSection),
                sectionBox);

            //Создание объекта для генерации чисел
            Random rnd = new Random();
            int value = rnd.Next();

            sectionView.Name = $"Разрез_{room.Number}_{room.Id}_{value.ToString()}";
            sectionView.Scale = 50;

            doc.Regenerate();
        }
    }
}