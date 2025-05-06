using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;

namespace RoomSectionCreator
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
                IList<Room> rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .ToList();

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "В проекте не найдены помещения.");
                    return Result.Cancelled;
                }

                // Начинаем транзакцию
                using (Transaction trans = new Transaction(doc, "Создание разрезов помещений"))
                {
                    trans.Start();

                    // Создаем секции для каждого помещения
                    foreach (Room room in rooms)
                    {
                        CreateSectionsAroundRoom(doc, room);
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
            // Получаем границы помещения
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;
            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

            if (boundaries == null || boundaries.Count == 0)
                return;

            // Получаем уровень помещения
            Level level = doc.GetElement(room.LevelId) as Level;

            // Получаем высоту помещения (берем высоту уровня выше или стандартную 3м)
            double roomHeight = 3000 / 304.8; // 3 метра в футах
            ElementId nextLevelId = level.GetDependentElements(new ElementCategoryFilter(BuiltInCategory.OST_Levels))
                .Select(x => doc.GetElement(x))
                .OfType<Level>()
                .Where(l => l.Elevation > level.Elevation)
                .OrderBy(l => l.Elevation)
                .Select(l => l.Id)
                .FirstOrDefault();

            if (nextLevelId != null)
            {
                Level nextLevel = doc.GetElement(nextLevelId) as Level;
                roomHeight = nextLevel.Elevation - level.Elevation;
            }

            // Получаем ограничивающую рамку помещения
            BoundingBoxXYZ bb = room.get_BoundingBox(null);
            XYZ roomCenter = (bb.Max + bb.Min) * 0.5;

            // Создаем секции для каждой границы
            foreach (IList<BoundarySegment> boundary in boundaries)
            {
                foreach (BoundarySegment segment in boundary)
                {
                    Element boundaryElement = doc.GetElement(segment.ElementId);
                    if (boundaryElement == null)
                        continue;

                    // Получаем кривую границы
                    Curve curve = segment.GetCurve();
                    if (curve == null)
                        continue;

                    // Создаем смещение на 10 мм (0.0328 фута)
                    XYZ offsetDirection = GetOffsetDirection(curve, roomCenter);
                    double offset = 0.0328; // 10 мм в футах
                    Curve offsetCurve = curve.CreateOffset(offset, offsetDirection);

                    // Генерируем уникальное имя для вида
                    string baseName = $"Развертка {room.Name} ";
                    string viewName = GetUniqueName(doc, baseName, typeof(ViewSection));

                    //Создание объекта для генерации чисел
                    Random rnd = new Random();
                    int value = rnd.Next();

                    // Создаем секцию
                    CreateSectionView(doc, offsetCurve, room.Number + "_" + boundaryElement.Id.ToString() + viewName+value, level, roomHeight);
                    
                }
            }
        }

        private XYZ GetOffsetDirection(Curve curve, XYZ roomCenter)
        {
            XYZ curveCenter = (curve.GetEndPoint(0) + curve.GetEndPoint(1)) * 0.5;
            XYZ direction = (roomCenter - curveCenter).Normalize();

            // Для вертикальных стен проверяем направление
            if (Math.Abs(curve.GetEndPoint(0).Z - curve.GetEndPoint(1).Z) > 0.001)
            {
                return XYZ.BasisZ;
            }

            // Для горизонтальных стен возвращаем вектор, перпендикулярный кривой
            XYZ tangent = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            return new XYZ(-tangent.Y, tangent.X, 0);
        }

        private void CreateSectionView(Document doc, Curve curve, string viewName, Level level, double height)
        {
            // Создаем BoundingBoxXYZ для секции
            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();

            // Устанавливаем направление секции
            XYZ curveDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            XYZ viewDirection = new XYZ(-curveDirection.Y, curveDirection.X, 0);

            // Устанавливаем преобразование координат
            Transform transform = Transform.Identity;
            transform.BasisX = viewDirection;
            transform.BasisY = XYZ.BasisZ;
            transform.BasisZ = curveDirection;
            sectionBox.Transform = transform;

            // Устанавливаем границы секции
            XYZ min = curve.GetEndPoint(0);
            XYZ max = curve.GetEndPoint(1);

            // Добавляем отступы
            double padding = 1.0; // 1 фут (~300 мм) отступ от границы
            sectionBox.Min = new XYZ(-padding, level.Elevation, -padding);
            sectionBox.Max = new XYZ(padding, level.Elevation + height, Math.Max(min.DistanceTo(max) + padding, 2 * padding));

           

            // Создаем вид секции
            ViewSection sectionView = ViewSection.CreateSection(doc, doc.GetDefaultElementTypeId(ElementTypeGroup.ViewTypeSection), sectionBox);

            // Настраиваем имя вида
            sectionView.Name = "Разрез_" + viewName;

            // Настраиваем масштаб
            sectionView.Scale = 50;
        }

        // Метод для генерации уникального имени
        private string GetUniqueName(Document doc, string baseName, Type elementType)
        {
            string name = baseName;
            int counter = 1;

            // Получаем все элементы указанного типа в проекте
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(elementType);

            // Проверяем, существует ли уже имя
            while (collector.Any(x => x.Name == name))
            {
                name = $"{baseName} ({counter++})";
            }

            return name;
        }
    }
}