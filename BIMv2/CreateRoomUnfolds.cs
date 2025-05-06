using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;


namespace BIMv2 
{ 

[Transaction(TransactionMode.Manual)]
public class CreateRoomUnfolds : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;
        UIDocument uiDoc = uiApp.ActiveUIDocument;
        Document doc = uiDoc.Document;

        try
        {
            // 1. Получить выбранное помещение
            Reference roomRef = uiDoc.Selection.PickObject(ObjectType.Element, new RoomSelectionFilter(), "Выберите помещение");
            Room room = doc.GetElement(roomRef) as Room;

            if (room == null)
            {
                message = "Выбранный элемент не является помещением";
                return Result.Failed;
            }

            // 2. Получить границы помещения
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;
            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

            if (boundaries == null || boundaries.Count == 0)
            {
                message = "Не удалось получить границы помещения";
                return Result.Failed;
            }

            // 3. Создать сечения по границам
            List<ViewSection> sections = new List<ViewSection>();

            using (Transaction trans = new Transaction(doc, "Создание разверток"))
            {
                trans.Start();

                // Получить уровень помещения
                Level level = doc.GetElement(room.LevelId) as Level;

                // Создать BoundingBox для помещения
                BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
                double roomHeight = roomBox.Max.Z - roomBox.Min.Z;

                foreach (IList<BoundarySegment> boundary in boundaries)
                {
                    foreach (BoundarySegment segment in boundary)
                    {
                        Curve curve = segment.GetCurve();

                        // Создать BoundingBox для сечения
                        BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                        sectionBox.Transform = Transform.Identity;

                        // Настроить направление сечения
                        XYZ curveDirection = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                        XYZ viewDirection = curveDirection.CrossProduct(XYZ.BasisZ).Normalize();

                        sectionBox.Transform.BasisX = viewDirection;
                        sectionBox.Transform.BasisY = XYZ.BasisZ;
                        sectionBox.Transform.BasisZ = viewDirection.CrossProduct(XYZ.BasisZ);

                        // Установить границы сечения
                        XYZ min = curve.GetEndPoint(0) - viewDirection * 2;
                        XYZ max = min + viewDirection * 4 + XYZ.BasisZ * roomHeight;

                        sectionBox.Min = min;
                        sectionBox.Max = max;

                        // Создать сечение
                        ViewSection section = ViewSection.CreateSection(doc,
                            GetSectionViewFamilyType(doc).Id, sectionBox);

                        section.Name = $"Развертка {room.Name} - {sections.Count + 1}";
                        sections.Add(section);
                    }
                }

                trans.Commit();
            }

            // 4. Создать лист и разместить сечения
            if (sections.Count > 0)
            {
                using (Transaction trans = new Transaction(doc, "Размещение на листе"))
                {
                    trans.Start();

                    // Найти семейство листов (используем первый доступный)
                    ViewSheet sheet = null;
                    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    ElementType sheetType = collector.OfClass(typeof(ElementType))
                        .OfCategory(BuiltInCategory.OST_Sheets)
                        .FirstOrDefault() as ElementType;

                    if (sheetType != null)
                    {
                        sheet = ViewSheet.Create(doc, sheetType.Id);
                        sheet.Name = $"Развертки помещения {room.Name}";

                        // Разместить сечения на листе
                        double x = 0.1;
                        double y = 0.1;
                        double offset = 0.3;

                        foreach (ViewSection section in sections)
                        {
                            // Создать viewport на листе
                            Viewport.Create(doc, sheet.Id, section.Id, new XYZ(x, y, 0));

                            // Обновить позицию для следующего viewport
                            x += offset;
                            if (x > 0.9) // Если достигли края листа
                            {
                                x = 0.1;
                                y += offset;
                            }
                        }
                    }

                    trans.Commit();
                }
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }

    // Вспомогательная функция для получения типа семейства сечений
    private ViewFamilyType GetSectionViewFamilyType(Document doc)
    {
        FilteredElementCollector collector = new FilteredElementCollector(doc);
        return collector.OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);
    }
}

// Фильтр для выбора только помещений
//public class RoomSelectionFilter : ISelectionFilter
//{
//    public bool AllowElement(Element elem)
//    {
//        return elem is Room;
//    }

//    public bool AllowReference(Reference reference, XYZ position)
//    {
//        return false;
//    }
//}
}