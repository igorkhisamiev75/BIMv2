using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using System;
using System.Linq;

namespace DimensionToWall
{
    [Transaction(TransactionMode.Manual)]
    public class MoveDimensionToWallCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Выбираем размер
                var dimRef = uiDoc.Selection.PickObject(ObjectType.Element,
                    new DimensionSelectionFilter(), "Выберите размер, привязанный к двери");

                var dimension = doc.GetElement(dimRef) as Dimension;
                if (dimension == null) return Result.Failed;

                // Получаем линию размера
                if (!(dimension.Curve is Line dimLine))
                {
                    TaskDialog.Show("Ошибка", "Выбранный размер не является линейным");
                    return Result.Failed;
                }

                using (var t = new Transaction(doc, "Перенос привязки на стену"))
                {
                    t.Start();

                    // Создаем новые привязки
                    var newReferences = new ReferenceArray();
                    bool success = true;

                    foreach (Reference reference in dimension.References)
                    {
                        var element = doc.GetElement(reference);

                        if (element is FamilyInstance door &&
                            door.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors)
                        {
                            // Получаем стену-хост
                            if (!(door.Host is Wall wall))
                            {
                                success = false;
                                break;
                            }

                            // Находим грань стены для привязки
                            var wallRef = GetWallReference(wall, door, dimLine.Direction, dimension.View);
                            if (wallRef != null)
                            {
                                newReferences.Append(wallRef);
                                continue;
                            }
                        }

                        // Оставляем другие привязки без изменений
                        newReferences.Append(reference);
                    }

                    if (success && newReferences.Size > 0)
                    {
                        // Создаем новый размер и удаляем старый
                        doc.Create.NewDimension(dimension.View, dimLine, newReferences);
                        doc.Delete(dimension.Id);

                        t.Commit();
                        TaskDialog.Show("Готово", "Привязка успешно перенесена на стену");
                        return Result.Succeeded;
                    }

                    t.RollBack();
                    TaskDialog.Show("Ошибка", "Не удалось перенести привязку на стену");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private Reference GetWallReference(Wall wall, FamilyInstance door, XYZ dimensionDir, View view)
        {
            var options = new Options
            {
                ComputeReferences = true,
                View = view,
                IncludeNonVisibleObjects = false
            };

            // Получаем точку двери
            var doorLoc = (door.Location as LocationPoint)?.Point;
            if (doorLoc == null) return null;

            // Получаем грань стены
            var geometry = wall.get_Geometry(options);
            foreach (var geomObj in geometry)
            {
                if (!(geomObj is Solid solid)) continue;

                foreach (Face face in solid.Faces)
                {
                    if (!(face is PlanarFace planarFace)) continue;

                    // Проверяем ориентацию грани
                    if (!faceNormal.IsPerpendicularTo(dimensionDir)) continue;

                    // Проверяем видимость в виде
                    if (Math.Abs(faceNormal.DotProduct(view.ViewDirection)) > 0.1) continue;

                    // Проверяем проекцию двери на грань
                    if (planarFace.Project(doorLoc) != null)
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
        public bool AllowElement(Element elem) => elem is Dimension;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }

    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel("Размеры");
            panel.AddItem(new PushButtonData(
                "MoveDimToWall",
                "На стену",
                System.Reflection.Assembly.GetExecutingAssembly().Location,
                "DimensionToWall.MoveDimensionToWallCommand"));
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}