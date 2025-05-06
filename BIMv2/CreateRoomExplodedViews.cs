using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIMv2
{

    [Transaction(TransactionMode.Manual)]
    public class CreateRoomExplodedViews : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. Выбор помещения
                Reference roomRef = uiDoc.Selection.PickObject(ObjectType.Element, new RoomSelectionFilter(), "Выберите помещение");
                Room room = doc.GetElement(roomRef) as Room;

                if (room == null)
                {
                    message = "Выбранный элемент не является помещением";
                    return Result.Failed;
                }

                // 2. Получение границ помещения
                IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                if (boundaries == null || boundaries.Count == 0)
                {
                    message = "Не удалось получить границы помещения";
                    return Result.Failed;
                }

                // 3. Создание разверток (по одной на каждую стену)
                List<ViewSection> sections = new List<ViewSection>();

                using (Transaction trans = new Transaction(doc, "Создание разверток"))
                {
                    trans.Start();

                    // Создаем лист для размещения видов
                    ViewSheet sheet = CreateSheet(doc, $"Развертки помещения {room.Name}");

                    XYZ origin = GetRoomCenter(room);

                    // Создаем виды для каждой стены
                    foreach (IList<BoundarySegment> boundary in boundaries)
                    {
                        foreach (BoundarySegment segment in boundary)
                        {
                            Element wall = doc.GetElement(segment.ElementId);
                            if (wall is Wall)
                            {
                                ViewSection section = CreateWallSection(doc, room, wall as Wall, origin);
                                sections.Add(section);
                            }
                        }
                    }

                    // 4. Размещение видов на листе
                    PlaceViewsOnSheet(doc, sheet, sections);

                    trans.Commit();
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private XYZ GetRoomCenter(Room room)
        {
            LocationPoint locPoint = room.Location as LocationPoint;
            return locPoint != null ? locPoint.Point : room.get_BoundingBox(null).Min;
        }

        private ViewSheet CreateSheet(Document doc, string baseSheetName)
        {
            // Находим доступное семейство TitleBlock (рамки листа)
            FilteredElementCollector titleBlocks = new FilteredElementCollector(doc);
            titleBlocks.OfCategory(BuiltInCategory.OST_TitleBlocks);
            ElementId titleBlockId = titleBlocks.FirstElementId();

            if (titleBlockId == null || titleBlockId == ElementId.InvalidElementId)
                throw new Exception("В проекте не найдены семейства TitleBlock (рамки листа)");

            // Генерируем уникальное имя для листа
            string sheetName = GetUniqueName(doc, baseSheetName, typeof(ViewSheet));

            // Создаем лист
            ViewSheet sheet = ViewSheet.Create(doc, titleBlockId);
            sheet.Name = sheetName;

            return sheet;
        }

        private ViewSection CreateWallSection(Document doc, Room room, Wall wall, XYZ origin)
        {
            // Получаем расположение стены
            LocationCurve wallLocation = wall.Location as LocationCurve;
            Curve wallCurve = wallLocation.Curve;

            XYZ wallDirection = wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0);
            XYZ viewDirection = wallDirection.CrossProduct(XYZ.BasisZ).Normalize();

            // Определяем BoundingBox стены
            BoundingBoxXYZ wallBox = wall.get_BoundingBox(null);

            // Создаем BoundingBox для вида
            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
            sectionBox.Transform = Transform.Identity;

            // Устанавливаем границы вида
            sectionBox.Min = new XYZ(-5, wallBox.Min.Z - 1, -5); // Отступы
            sectionBox.Max = new XYZ(5, wallBox.Max.Z + 1, 5);   // Отступы

            // Генерируем уникальное имя для вида
            string baseName = $"Развертка {room.Name} - {wall.Name}";
            string viewName = GetUniqueName(doc, baseName, typeof(ViewSection));

            // Создаем вид в разрезе
            ViewSection section = ViewSection.CreateSection(doc,
                GetViewFamilyTypeId(doc, ViewFamily.Section),
                sectionBox);

            section.Name = viewName;
            // ... остальные настройки вида ...

            return section;
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

        private ElementId GetViewFamilyTypeId(Document doc, ViewFamily viewFamily)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(ViewFamilyType));

            ViewFamilyType viewType = collector
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == viewFamily);

            return viewType?.Id ?? throw new Exception($"Не найден тип вида для {viewFamily}");
        }

        private void PlaceViewsOnSheet(Document doc, ViewSheet sheet, List<ViewSection> views)
        {
            double x = 20; // Начальная позиция по X (мм)
            double y = 20; // Начальная позиция по Y (мм)
            double delta = 250; // Расстояние между видами (мм)

            foreach (ViewSection view in views)
            {
                // Проверяем, можно ли разместить вид на листе
                if (!Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
                    continue;

                // Конвертируем мм в футы (Revit использует футы)
                XYZ location = new XYZ(UnitUtils.ConvertToInternalUnits(x, DisplayUnitType.DUT_MILLIMETERS),
                                     UnitUtils.ConvertToInternalUnits(y, DisplayUnitType.DUT_MILLIMETERS),
                                     0);

                // Создаем вид на листе
                Viewport.Create(doc, sheet.Id, view.Id, location);

                // Переходим к следующей позиции
                x += delta;
                if (x > UnitUtils.ConvertToInternalUnits(800, DisplayUnitType.DUT_MILLIMETERS))
                {
                    x = 20;
                    y += delta;
                }
            }
        }

        //private ViewSection CreateRoomSection(Document doc, Room room, XYZ viewDirection)
        //{
        //    // Получаем границы помещения
        //    BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
        //    if (roomBox == null) return null;

        //    // Создаем BoundingBox для вида
        //    BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();

        //    // Устанавливаем границы вида на основе помещения
        //    sectionBox.Min = new XYZ(
        //        roomBox.Min.X - 2,  // Отступ от стены
        //        roomBox.Min.Y - 2,  // Отступ от стены
        //        roomBox.Min.Z - 1   // Отступ ниже пола
        //    );

        //    sectionBox.Max = new XYZ(
        //        roomBox.Max.X + 2,  // Отступ от стены
        //        roomBox.Max.Y + 2,  // Отступ от стены
        //        roomBox.Max.Z + 1   // Отступ выше потолка
        //    );

        //    // Настраиваем направление вида
        //    Transform transform = Transform.Identity;
        //    transform.Origin = (roomBox.Min + roomBox.Max) / 2;
        //    transform.BasisX = -viewDirection.CrossProduct(XYZ.BasisZ).Normalize();
        //    transform.BasisY = XYZ.BasisZ;
        //    transform.BasisZ = viewDirection.Normalize();

        //    sectionBox.Transform = transform;

        //    // Создаем вид в разрезе
        //    ViewSection section = ViewSection.CreateSection(
        //        doc,
        //        GetViewFamilyTypeId(doc, ViewFamily.Section),
        //        sectionBox
        //    );

        //    // Настраиваем вид
        //    section.Scale = 50; // Масштаб 1:50
        //    section.Name = GetUniqueName(doc, $"Развертка {room.Name}", typeof(ViewSection));

        //    // Включаем отображение всех стен помещения
        //    SetViewDisplaySettings(doc, section, room);

        //    return section;
        //}

        //private void SetViewDisplaySettings(Document doc, ViewSection section, Room room)
        //{
        //    // Настраиваем графический отображение вида
        //    using (Transaction t = new Transaction(doc, "Настройка вида"))
        //    {
        //        t.Start();

        //        // Включаем видимость стен
        //        section.SetCategoryHidden(BuiltInCategory.OST_Walls, false);

        //        // Включаем видимость помещений
        //        section.SetCategoryHidden(BuiltInCategory.OST_Rooms, true);

        //        // Настраиваем глубину обрезки
        //        Parameter farClip = section.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
        //        if (farClip != null && farClip.IsReadOnly == false)
        //        {
        //            farClip.Set(UnitUtils.ConvertToInternalUnits(5, DisplayUnitType.DUT_METERS));
        //        }

        //t.Commit();
        //    }

}

// Фильтр для выбора только помещений
public class RoomSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Room;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}