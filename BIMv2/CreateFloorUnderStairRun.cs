//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.DB.Architecture;
//using System.Collections.Generic;
//using System.Linq;
//using Autodesk.Revit.UI.Selection;
//using System;

//public class CreateFloorsUnderStairRuns : IExternalCommand
//{
//    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//    {
//        UIApplication uiApp = commandData.Application;
//        UIDocument uiDoc = uiApp.ActiveUIDocument;
//        Document doc = uiDoc.Document;

//        try
//        {
//            // 1. Выбор лестницы
//            Reference stairRef = uiDoc.Selection.PickObject(ObjectType.Element, "Выберите лестницу");
//            Stairs stair = doc.GetElement(stairRef) as Stairs;
//            if (stair == null)
//            {
//                message = "Выбранный элемент не является лестницей";
//                return Result.Failed;
//            }

//            // 2. Получаем все марши
//            ICollection<ElementId> runIds = stair.GetStairsRuns();
//            if (runIds.Count == 0)
//            {
//                message = "У лестницы не найдены марши";
//                return Result.Failed;
//            }

//            // 3. Получаем уровень
//            Level baseLevel = doc.GetElement(stair.GetBaseLevel()) as Level;
//            if (baseLevel == null)
//            {
//                message = "Не удалось определить базовый уровень";
//                return Result.Failed;
//            }

//            // 4. Выбираем тип перекрытия
//            FloorType floorType = new FilteredElementCollector(doc)
//                .OfClass(typeof(FloorType))
//                .FirstOrDefault(x => x.Name.Contains("Железобетон")) as FloorType
//                ?? new FilteredElementCollector(doc).OfClass(typeof(FloorType)).FirstElement() as FloorType;

//            // 5. Создаем транзакцию
//            using (Transaction trans = new Transaction(doc, "Создание перекрытий под маршами"))
//            {
//                trans.Start();

//                int createdFloors = 0;
//                foreach (ElementId runId in runIds)
//                {
//                    StairsRun run = doc.GetElement(runId) as StairsRun;
//                    if (run == null) continue;

//                    // 6. Получаем геометрию марша
//                    GeometryElement geomElem = run.get_Geometry(new Options()
//                    {
//                        ComputeReferences = true,
//                        DetailLevel = ViewDetailLevel.Fine,
//                        IncludeNonVisibleObjects = false
//                    });

//                    // 7. Находим нижнюю грань
//                    List<CurveLoop> profile = GetBottomProfile(geomElem);
//                    if (profile.Count == 0) continue;

//                    // 8. Создаем перекрытие
//                    Floor floor = Floor.Create(doc, profile, floorType.Id, baseLevel.Id);

//                    // 9. Смещаем немного вниз
//                    ElementTransformUtils.MoveElement(doc, floor.Id, new XYZ(0, 0, -0.05));
//                    createdFloors++;
//                }

//                trans.Commit();
//                TaskDialog.Show("Результат", $"Создано перекрытий: {createdFloors}");
//            }

//            return Result.Succeeded;
//        }
//        catch (Exception ex)
//        {
//            message = $"Ошибка: {ex.Message}\n{ex.StackTrace}";
//            return Result.Failed;
//        }
//    }

//    private List<CurveLoop> GetBottomProfile(GeometryElement geomElem)
//    {
//        List<CurveLoop> result = new List<CurveLoop>();
//        PlanarFace bestFace = null;
//        double maxArea = 0;

//        foreach (GeometryObject geomObj in geomElem)
//        {
//            if (!(geomObj is Solid solid) || !solid.Faces.Any()) continue;

//            foreach (Face face in solid.Faces)
//            {
//                if (!(face is PlanarFace planarFace)) continue;

//                XYZ normal = planarFace.FaceNormal;
//                if (normal.Z > -0.1) continue; // Отбрасываем не нижние грани

//                double area = planarFace.Area;
//                if (area > maxArea)
//                {
//                    maxArea = area;
//                    bestFace = planarFace;
//                }
//            }
//        }

//        if (bestFace != null)
//        {
//            foreach (CurveLoop loop in bestFace.GetEdgesAsCurveLoops())
//            {
//                result.Add(loop);
//            }
//        }

//        return result;
//    }
//}