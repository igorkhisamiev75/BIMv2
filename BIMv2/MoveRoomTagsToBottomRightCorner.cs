using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using System.Collections.Generic;
using System.Linq;

[Transaction(TransactionMode.Manual)]
public class SimpleMoveAllRoomTags : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uiDoc = commandData.Application.ActiveUIDocument;
        Document doc = uiDoc.Document;
        View view = doc.ActiveView;

        // Собираем все марки
        FilteredElementCollector collector = new FilteredElementCollector(doc, view.Id)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsNotElementType();

        List<Element> tags = collector.ToList();

        if (tags.Count == 0)
        {
            TaskDialog.Show("Информация", "Марки помещений не найдены");
            return Result.Succeeded;
        }

        int movedCount = 0;
        int failedCount = 0;
        List<string> failedRooms = new List<string>();

        using (Transaction t = new Transaction(doc, "Перемещение марок"))
        {
            t.Start();

            foreach (Element tag in tags)
            {
                try
                {
                    // Ищем связанное помещение через точку расположения
                    XYZ tagLocation = GetTagLocation(tag);
                    if (tagLocation == null) continue;

                    // Ищем помещение, содержащее эту точку
                    Room room = FindRoomByPoint(doc, tagLocation);
                    if (room == null)
                    {
                        failedCount++;
                        continue;
                    }

                    // Получаем правый нижний угол
                    XYZ newLocation = GetBottomRightCornerSimple(room);
                    if (newLocation == null || !room.IsPointInRoom(newLocation))
                    {
                        failedCount++;
                        failedRooms.Add(GetRoomName(room));
                        continue;
                    }

                    // Перемещаем
                    XYZ moveVector = newLocation - tagLocation;
                    ElementTransformUtils.MoveElement(doc, tag.Id, moveVector);
                    movedCount++;
                }
                catch
                {
                    failedCount++;
                }
            }

            t.Commit();

            // Результаты
            string result = $"Перемещено: {movedCount} марок\n";
            result += $"Не удалось: {failedCount} марок";

            if (failedRooms.Count > 0)
            {
                result += $"\n\nПомещения, где марки не перемещены:";
                foreach (string room in failedRooms.Take(5))
                {
                    result += $"\n• {room}";
                }
                if (failedRooms.Count > 5)
                {
                    result += $"\n... и еще {failedRooms.Count - 5}";
                }
            }

            TaskDialog.Show("Результат", result);
        }

        return Result.Succeeded;
    }

    private Room FindRoomByPoint(Document doc, XYZ point)
    {
        FilteredElementCollector collector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType();

        foreach (Element elem in collector)
        {
            Room room = elem as Room;
            if (room != null && room.IsPointInRoom(point))
            {
                return room;
            }
        }
        return null;
    }

    private XYZ GetBottomRightCornerSimple(Room room)
    {
        try
        {
            BoundingBoxXYZ bbox = room.get_BoundingBox(null);
            if (bbox == null) return null;

            // Правый нижний угол с небольшим отступом
            return new XYZ(bbox.Max.X - 0.3, bbox.Min.Y + 0.3, bbox.Min.Z);
        }
        catch
        {
            return null;
        }
    }

    private XYZ GetTagLocation(Element tag)
    {
        if (tag.Location is LocationPoint lp) return lp.Point;
        return null;
    }

    private string GetRoomName(Room room)
    {
        return !string.IsNullOrEmpty(room.Number) ? room.Number : $"ID:{room.Id}";
    }
}