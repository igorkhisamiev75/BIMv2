using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System;
using System.Linq;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CreateRoomUnfolds : IExternalCommand
{
    // Константа для отступа внутрь помещения (10 мм)
    private const double OffsetInward = 10000; // 10 мм в метрах

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;
        UIDocument uiDoc = uiApp.ActiveUIDocument;
        Document doc = uiDoc.Document;

        try
        {
            // 1. Выбор помещения
            Reference roomRef = uiDoc.Selection.PickObject(
                ObjectType.Element,
                new RoomSelectionFilter(),
                "Выберите помещение");

            Room room = doc.GetElement(roomRef) as Room;
            if (room == null)
            {
                message = "Выбранный элемент не является помещением";
                return Result.Failed;
            }

            // 2. Получение границ помещения
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
                StoreFreeBoundaryFaces = true
            };

            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
            if (boundaries == null || boundaries.Count == 0)
            {
                message = "Не удалось получить границы помещения";
                return Result.Failed;
            }

            // 3. Получение геометрии помещения
            BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
            double roomHeight = roomBox.Max.Z - roomBox.Min.Z;
            Level level = doc.GetElement(room.LevelId) as Level;

            // 4. Создание сечений
            List<ViewSection> sections = new List<ViewSection>();
            ViewFamilyType sectionType = GetSectionViewFamilyType(doc);

            using (Transaction trans = new Transaction(doc, "Создание разрезов с отступом"))
            {
                trans.Start();

                foreach (IList<BoundarySegment> boundary in boundaries)
                {
                    foreach (BoundarySegment segment in boundary)
                    {
                        Curve curve = segment.GetCurve();
                        if (curve == null) continue;

                        //попробуем сами сделать коорлинаты
                        var gg = curve.Tessellate();
                        XYZ curveStart = gg[0];
                        XYZ curveEnd = gg[1];

                        //XYZ translationVector = curve.Tessellate()[0];

                        //TaskDialog.Show("1", $"{curveStart}_{curveEnd}");

                        //XYZ curveStart = curve.GetEndPoint(0);
                        //XYZ curveEnd = curve.GetEndPoint(1);

                        // Вектор вдоль стены
                        XYZ wallDirection = (curveEnd - curveStart).Normalize();

                        // Вектор вида (перпендикулярно стене)
                        XYZ viewDirection = wallDirection.CrossProduct(XYZ.BasisZ).Normalize();

                        // Проверка направления внутрь помещения
                        XYZ testPoint = curveStart + viewDirection * 0.1;
                        if (!IsPointInRoom(room, testPoint))
                        {
                            viewDirection = -viewDirection;
                        }

                        XYZ upDirection = XYZ.BasisZ;
                        XYZ rightDirection = viewDirection.CrossProduct(upDirection);

                        // Создание трансформации
                        Transform transform = Transform.Identity;
                        transform.BasisX = rightDirection;
                        transform.BasisY = upDirection;
                        transform.BasisZ = rightDirection.CrossProduct(upDirection);

                        // Центр сечения - середина стены с отступом 10 мм внутрь
                        XYZ sectionCenter = (curveStart + curveEnd) / 2 + viewDirection * OffsetInward;

                        // Создание BoundingBox
                        BoundingBoxXYZ sectionBox = new BoundingBoxXYZ
                        {
                            Transform = transform,
                            Min = new XYZ(-(curve.Length / 2 + 0.5), -0.5, -0.5),
                            Max = new XYZ(curve.Length / 2 + 0.5, 5.0, roomHeight + 0.5)
                        };

                        // Установка флагов
                        for (int i = 0; i < 3; i++)
                        {
                            sectionBox.set_MinEnabled(i, true);
                            sectionBox.set_MaxEnabled(i, true);
                        }

                        //sectionBox.Transform.Origin = sectionCenter;
                        sectionBox.Transform.Origin.Normalize();
                        // Создание сечения
                        ViewSection section = ViewSection.CreateSection(doc, sectionType.Id, sectionBox);

                       


                        section.Name = $"Разрез {room.Name} - {sections.Count + 1}";

                        // Установка масштаба 1:50
                        Parameter scaleParam = section.get_Parameter(BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC);
                        if (scaleParam != null && !scaleParam.IsReadOnly)
                        {
                            scaleParam.Set(50);
                        }

                        sections.Add(section);
                    }
                }

                trans.Commit();
            }

            //// 5. Создание листа и размещение видов
            //if (sections.Count > 0)
            //{
            //    using (Transaction trans = new Transaction(doc, "Размещение на листе"))
            //    {
            //        trans.Start();

            //        ViewFamilyType sheetType = GetFirstSheetType(doc);
            //        if (sheetType == null)
            //        {
            //            message = "Не найден тип листа";
            //            return Result.Failed;
            //        }

            //        ViewSheet sheet = ViewSheet.Create(doc, sheetType.Id);
            //        sheet.Name = $"Разрезы {room.Name}";

            //        // Параметры размещения
            //        double x = 0.1;
            //        double y = 0.1;
            //        double width = 0.3;
            //        double height = 0.2;
            //        double spacing = 0.05;

            //        foreach (ViewSection section in sections)
            //        {
            //            if (x + width > 0.95)
            //            {
            //                x = 0.1;
            //                y += height + spacing;
            //            }

            //            Viewport viewport = Viewport.Create(doc, sheet.Id, section.Id, new XYZ(x, y, 0));

            //            // Настройка размера viewport
            //            viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).Set(section.Name);

            //            x += width + spacing;
            //        }

            //        trans.Commit();
            //    }
            //}

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            TaskDialog.Show("Ошибка", ex.ToString());
            return Result.Failed;
        }
    }

    private bool IsPointInRoom(Room room, XYZ point)
    {
        // Метод проверки точки в помещении через BoundingBox
        BoundingBoxXYZ bb = room.get_BoundingBox(null);
        if (bb == null) return false;

        return point.X > bb.Min.X && point.X < bb.Max.X &&
               point.Y > bb.Min.Y && point.Y < bb.Max.Y &&
               point.Z > bb.Min.Z && point.Z < bb.Max.Z;
    }

    private ViewFamilyType GetSectionViewFamilyType(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);
    }

    private ViewFamilyType GetFirstSheetType(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(x => x.ViewFamily == ViewFamily.Sheet);
    }
}

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