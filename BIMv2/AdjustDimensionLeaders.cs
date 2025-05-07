using System;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace WallDimensionReferences
{
    [Transaction(TransactionMode.Manual)]
    public class AdjustDimensionLeaders : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Выбираем размер
                Reference dimRef = uiDoc.Selection.PickObject(ObjectType.Element,
                    new DimensionSelectionFilter(), "Выберите параллельный размер");
                Dimension dimension = doc.GetElement(dimRef) as Dimension;

                if (dimension == null || !(dimension.Curve is Line))
                {
                    TaskDialog.Show("Ошибка", "Пожалуйста, выберите линейный размер");
                    return Result.Failed;
                }

                using (Transaction t = new Transaction(doc, "Привязка к стенам"))
                {
                    t.Start();

                    // Получаем геометрию размера
                    Line dimLine = dimension.Curve as Line;
                    XYZ direction = dimLine.Direction;

                    // Создаем новые привязки
                    ReferenceArray newReferences = new ReferenceArray();
                    bool allReferencesValid = true;

                    foreach (Reference reference in dimension.References)
                    {
                        Element element = doc.GetElement(reference);

                        if (element is FamilyInstance instance &&
                            instance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors)
                        {
                            // Для дверей ищем стену
                            HostObject host = instance.Host as HostObject;
                            if (host == null)
                            {
                                allReferencesValid = false;
                                break;
                            }

                            // Получаем грань стены
                            Reference wallRef = GetWallReference(host, instance, direction, dimension.View);
                            if (wallRef != null)
                            {
                                newReferences.Append(wallRef);
                                continue;
                            }
                        }

                        // Для других элементов оставляем как есть
                        newReferences.Append(reference);
                    }

                    if (allReferencesValid && newReferences.Size > 0)
                    {
                        // Создаем новый размер и удаляем старый
                        Dimension newDim = doc.Create.NewDimension(dimension.View, dimLine, newReferences);
                        doc.Delete(dimension.Id);

                        t.Commit();
                        TaskDialog.Show("Готово", "Привязки успешно изменены на стены");
                        return Result.Succeeded;
                    }

                    t.RollBack();
                    TaskDialog.Show("Ошибка", "Не удалось найти подходящие привязки к стенам");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private Reference GetWallReference(HostObject wall, FamilyInstance door, XYZ dimensionDirection, View view)
        {
            Options options = new Options
            {
                ComputeReferences = true,
                View = view,
                IncludeNonVisibleObjects = false
            };

            // Получаем точку двери
            XYZ doorPoint = (door.Location as LocationPoint)?.Point;
            if (doorPoint == null) return null;

            // Ищем подходящую грань стены
            foreach (GeometryObject geom in wall.get_Geometry(options))
            {
                if (!(geom is Solid solid)) continue;

                foreach (Face face in solid.Faces)
                {
                    if (!(face is PlanarFace planarFace)) continue;

                    // Проверяем ориентацию грани
                    if (Math.Abs(planarFace.FaceNormal.DotProduct(dimensionDirection)) > 0.1)
                        continue;

                    // Проверяем видимость в виде
                    if (Math.Abs(planarFace.FaceNormal.DotProduct(view.ViewDirection)) > 0.1)
                        continue;

                    // Проверяем что точка двери проецируется на грань
                    IntersectionResult proj = planarFace.Project(doorPoint);
                    if (proj != null)
                    {
                        return planarFace.Reference;
                    }
                }
            }

            return null;
        }
    }

    public class DimensionSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Dimension;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}