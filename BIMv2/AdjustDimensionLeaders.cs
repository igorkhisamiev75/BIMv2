using System;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace DimensionReferencesChanger
{
    [Transaction(TransactionMode.Manual)]
    public class DimensionReferencesChanger : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Выбираем размер
                Reference dimRef = uiDoc.Selection.PickObject(ObjectType.Element, "Выберите параллельный размер");
                Dimension dimension = doc.GetElement(dimRef) as Dimension;

                if (dimension == null)
                {
                    TaskDialog.Show("Ошибка", "Выбранный элемент не является размером");
                    return Result.Failed;
                }

                // Проверяем что размер параллельный
                if (!(dimension.Curve is Line))
                {
                    TaskDialog.Show("Ошибка", "Пожалуйста, выберите параллельный размер");
                    return Result.Failed;
                }

                // Получаем текущие привязки
                ReferenceArray currentReferences = dimension.References;
                ReferenceArray newReferences = new ReferenceArray();

                using (Transaction t = new Transaction(doc, "Изменение привязок размера"))
                {
                    t.Start();

                    foreach (Reference reference in currentReferences)
                    {
                        Element element = doc.GetElement(reference.ElementId);

                        // Если привязка к двери - заменяем на привязку к стене
                        if (element is FamilyInstance door && door.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors)
                        {
                            // Находим стену, к которой принадлежит дверь
                            HostObject host = door.Host as HostObject;
                            if (host != null)
                            {
                                Reference wallReference = GetWallReferenceForDimension(host, door, dimension, doc);
                                if (wallReference != null)
                                {
                                    newReferences.Append(wallReference);
                                    continue;
                                }
                            }
                        }

                        // Для других элементов оставляем как есть
                        newReferences.Append(reference);
                    }

                    // Вместо изменения References создаем новый размер
                    if (newReferences.Size == currentReferences.Size)
                    {
                        // Создаем новый размер с нужными привязками
                        Line dimLine = dimension.Curve as Line;
                        Dimension newDimension = doc.Create.NewDimension(
                            dimension.View,
                            dimLine,
                            newReferences);

                        // Удаляем старый размер
                        doc.Delete(dimension.Id);

                        t.Commit();
                        TaskDialog.Show("Успех", "Привязки размера успешно изменены");
                    }
                    else
                    {
                        t.RollBack();
                        TaskDialog.Show("Ошибка", "Не удалось найти подходящие привязки для всех элементов");
                    }
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

        private Reference GetWallReferenceForDimension(HostObject wall, FamilyInstance door, Dimension dimension, Document doc)
        {
            View view = dimension.View;
            if (view == null) return null;

            // Получаем положение двери
            XYZ doorLocation = (door.Location as LocationPoint)?.Point;
            if (doorLocation == null) return null;

            // Получаем линию размера
            Line dimLine = dimension.Curve as Line;
            if (dimLine == null) return null;

            // Направление размера (перпендикулярно стене)
            XYZ dimensionDirection = dimLine.Direction;

            // Получаем геометрию стены в контексте вида
            Options options = new Options
            {
                ComputeReferences = true,
                View = view,
                IncludeNonVisibleObjects = false
            };

            Reference targetReference = null;
            double minDistance = double.MaxValue;

            foreach (GeometryObject geomObj in wall.get_Geometry(options))
            {
                Solid solid = geomObj as Solid;
                if (solid == null) continue;

                foreach (Face face in solid.Faces)
                {
                    PlanarFace planarFace = face as PlanarFace;
                    if (planarFace == null) continue;

                    // Проверяем что грань примерно перпендикулярна виду
                    XYZ faceNormal = planarFace.FaceNormal;
                    if (Math.Abs(faceNormal.DotProduct(view.ViewDirection)) > 0.1)
                        continue;

                    // Проверяем что грань параллельна направлению размера
                    if (Math.Abs(faceNormal.DotProduct(dimensionDirection)) < 0.1)
                    {
                        // Получаем проекцию двери на грань
                        IntersectionResult ir = planarFace.Project(doorLocation);
                        if (ir != null)
                        {
                            double distance = ir.Distance;
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                targetReference = planarFace.Reference;
                            }
                        }
                    }
                }
            }

            return targetReference;
        }
    }
}