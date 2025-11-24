//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Windows;
//using System.Windows.Documents;

//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Architecture;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;

//namespace FinishStairs
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    [Journaling(JournalingMode.NoCommandData)]
//    public class FinishingOnTheStairs : IExternalCommand
//    {
//        private Document _doc;
//        private ElementId _floorTypeId;
//        private ElementId _wallTypeId;
//        private const double Precision = 0.0001;
//        private Line line1;
//        private const double PRECISION = 0.00000001;
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            UIApplication uiApp = commandData.Application;
//            UIDocument uiDoc = uiApp.ActiveUIDocument;
//            _doc = uiDoc.Document;

//            MainWindow form = new MainWindow(_doc);

//            form.WindowStartupLocation = WindowStartupLocation.CenterScreen;

//            if (form.ShowDialog() == true)
//            {
//                using (var tx = new Transaction(_doc, "Отделка лестниц от 2bim.ru"))
//                {
//                    tx.Start();

//                    if (form.ProcessAllStairs)
//                    {
//                        // Обработка всех лестниц в проекте
//                        var allStairs = new FilteredElementCollector(_doc)
//                            .OfCategory(BuiltInCategory.OST_Stairs)
//                            .WhereElementIsNotElementType();

//                        foreach (Element stair in allStairs)
//                        {
//                            ProcessStair(stair, form.SelectedWallType, form.SelectedFloorType);
//                        }
//                    }
//                    if (!form.ProcessAllStairs)
//                    {
//                        // Get user selection
//                        Stairs stairs = SelectStairs(uiDoc);
//                        if (stairs == null) return Result.Cancelled;
//                        //TaskDialog.Show("Отделка лестницы2", "Отделка сделана на лестнице");
//                        // Обработка выбранных лестниц

//                        ProcessStair(stairs, form.SelectedWallType, form.SelectedFloorType);

//                    }
//                    TaskDialog.Show("Отделка лестницы", "Отделка сделана на лестнице");
//                    tx.Commit();
//                }
//            }

//            return Result.Succeeded;
//        }

//        private void ProcessStair(Element stair, WallType wallType, FloorType floorType)
//        {
//            //TaskDialog.Show("Отделка лестницы", "Отделка сделана на лестнице");

//            // Get finishing types
//            _floorTypeId = GetFloorTypeId(floorType.Name);
//            _wallTypeId = GetWallTypeId(wallType.Name);

//            //создает пол на лестничных клетках
//            CreateLandingFinishes((Stairs)stair, floorType);
//            CreateRunFinishes((Stairs)stair, wallType);
//            // Здесь реализуйте вашу логику обработки лестницы
//            // с использованием выбранных типов стены и перекрытия
//        }

//        private Stairs SelectStairs(UIDocument uiDoc)
//        {
//            try
//            {
//                var reference = uiDoc.Selection.PickObject(ObjectType.Element,
//                    new StairsSelectionFilter(), "Select a stair element");
//                return _doc.GetElement(reference) as Stairs;
//            }
//            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
//            {
//                return null;
//            }
//        }

//        public class StairsSelectionFilter : ISelectionFilter
//        {
//            public bool AllowElement(Element elem)
//            {
//                return elem is Stairs;
//            }

//            public bool AllowReference(Reference reference, XYZ position)
//            {
//                return false;
//            }
//        }

//        private void CreateLandingFinishes(Stairs stairs, FloorType floorType)
//        {
//            ElementId levelId = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsElementId();
//            Level level = _doc.GetElement(levelId) as Level;
//            double baseOffset = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_OFFSET).AsDouble();
//            string stairsId = stairs.Id.ToString();

//            ICollection<ElementId> landings = stairs.GetStairsLandings();

//            foreach (ElementId landingId in landings)
//            {
//                try
//                {
//                    StairsLanding landing = _doc.GetElement(landingId) as StairsLanding;
//                    if (landing == null) continue;

//                    double elevation = landing.BaseElevation;

//                    // Для Revit 2020 используем CurveArray вместо CurveLoop
//                    CurveArray footprintCurves = new CurveArray();
//                    var footprint = landing.GetFootprintBoundary();

//                    foreach (Curve curve in footprint)
//                    {
//                        footprintCurves.Append(curve);
//                    }

//                    // Получаем тип перекрытия
//                    FloorType floorType2 = _doc.GetElement(_floorTypeId) as FloorType;
//                    if (floorType == null) continue;

//                    // Создаем перекрытие для площадки (метод для Revit 2020)
//                    Floor landingFloor = _doc.Create.NewFloor(footprintCurves, floorType, level, false);

//                    double floorThickness = landingFloor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();
//                    landingFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(elevation + baseOffset + floorThickness);
//                    landingFloor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(stairsId);
//                }
//                catch (Exception ex)
//                {
//                    TaskDialog.Show("Предупреждение", $"Ошибка создания отделки площадки: {ex.Message}");
//                }
//            }
//        }

//        private void CreateRunFinishes(Stairs stairs, WallType wallType)
//        {
//            ElementId levelId = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsElementId();
//            Level level = _doc.GetElement(levelId) as Level;
//            double baseOffset = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_OFFSET).AsDouble();
//            string stairsId = stairs.Id.ToString();

//            ICollection<ElementId> runs = stairs.GetStairsRuns();

//            foreach (ElementId runId in runs)
//            {
//                try
//                {
//                    StairsRun run = _doc.GetElement(runId) as StairsRun;
//                    if (run == null) continue;

//                    ProcessStairRun(run, levelId, level, baseOffset, stairsId);
//                }
//                catch (Exception ex)
//                {
//                    TaskDialog.Show("Предупреждение", $"Ошибка создания отделки марша: {ex.Message}");
//                }
//            }
//        }

//        private void ProcessStairRun(StairsRun run, ElementId levelId, Level level, double baseOffset, string stairsId)
//        {
//            double baseElevation = run.BaseElevation;
//            double riserHeight = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RISER_HEIGHT).AsDouble();
//            double runWidth = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RUN_WIDTH).AsDouble();
//            double treadDepth = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_TREAD_DEPTH).AsDouble();
//            int treadCount = run.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_NUMBER_OF_TREADS).AsInteger();

//            // Получаем направление марша
//            CurveLoop path = run.GetStairsPath();
//            Line pathLine = path.First() as Line;
//            XYZ runDirection = pathLine?.Direction ?? XYZ.BasisX;

//            // Получаем границы и создаем стены
//            CurveLoop boundaries = run.GetFootprintBoundary();
//            CreateRunWalls(boundaries, levelId, baseElevation, baseOffset, riserHeight, runWidth, treadCount, treadDepth, runDirection, stairsId);

//            // Создаем перекрытия ступеней
//            CreateTreadFloors(run, levelId, level, baseElevation, baseOffset, riserHeight, treadCount, treadDepth, runDirection, stairsId);
//        }

//        private void CreateRunWalls(CurveLoop boundaries, ElementId levelId, double baseElevation, double baseOffset,
//                                   double riserHeight, double runWidth, int treadCount, double treadDepth, XYZ runDirection, string stairsId)
//        {
//            // Получаем тип стены
//            WallType wallType = _doc.GetElement(_wallTypeId) as WallType;
//            if (wallType == null) return;

//            foreach (Curve boundary in boundaries)
//            {
//                if (Math.Abs(boundary.Length - runWidth) < PRECISION)
//                {
//                    // Создаем стену (правильный метод для Revit 2020)
//                    Wall wall = Wall.Create(_doc, boundary, wallType.Id, levelId, riserHeight, baseOffset + baseElevation, false, false);
//                    wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(stairsId);
//                    wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);

//                    // Создаем копии стены для каждой ступени
//                    for (int i = 1; i <= treadCount; i++)
//                    {
//                        XYZ offset = runDirection * (treadDepth * i) + XYZ.BasisZ * (riserHeight * i);
//                        ICollection<ElementId> newWallIds = ElementTransformUtils.CopyElement(_doc, wall.Id, offset);

//                        foreach (ElementId newWallId in newWallIds)
//                        {
//                            Wall newWall = _doc.GetElement(newWallId) as Wall;
//                            newWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseOffset + baseElevation + riserHeight * i);
//                            newWall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set($"{stairsId}_{i}");
//                        }
//                    }
//                    break;
//                }
//            }
//        }

//        private void CreateTreadFloors(StairsRun run, ElementId levelId, Level level, double baseElevation, double baseOffset,
//                                     double riserHeight, int treadCount, double treadDepth, XYZ runDirection, string stairsId)
//        {
//            // Получаем профиль первой ступени
//            CurveArray firstTreadProfile = GetFirstTreadProfile(run);
//            if (firstTreadProfile == null) return;

//            // Получаем тип перекрытия
//            FloorType floorType = _doc.GetElement(_floorTypeId) as FloorType;
//            if (floorType == null) return;

//            // Создаем перекрытие для первой ступени (метод для Revit 2020)
//            Floor firstTreadFloor = _doc.Create.NewFloor(firstTreadProfile, floorType, level, false);
//            double floorThickness = firstTreadFloor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();
//            firstTreadFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(baseOffset + baseElevation + riserHeight + floorThickness);
//            firstTreadFloor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(stairsId);

//            // Создаем копии для остальных ступеней
//            for (int i = 1; i < treadCount; i++)
//            {
//                XYZ offset = runDirection * (treadDepth * i) + XYZ.BasisZ * (riserHeight * i);
//                ICollection<ElementId> newFloorIds = ElementTransformUtils.CopyElement(_doc, firstTreadFloor.Id, offset);

//                foreach (ElementId newFloorId in newFloorIds)
//                {
//                    Floor newFloor = _doc.GetElement(newFloorId) as Floor;
//                    newFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(baseOffset + baseElevation + riserHeight * (i + 1) + floorThickness);
//                    newFloor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set($"{stairsId}_{i}");
//                }
//            }
//        }

//        private CurveArray GetFirstTreadProfile(StairsRun run)
//        {
//            Options options = new Options();
//            options.ComputeReferences = true;
//            GeometryElement geomElem = run.get_Geometry(options);

//            foreach (GeometryObject geomObj in geomElem)
//            {
//                if (geomObj is Solid solid && solid.Faces.Size > 0)
//                {
//                    foreach (Face face in solid.Faces)
//                    {
//                        if (face is PlanarFace planarFace && Math.Abs(planarFace.FaceNormal.Z) > 0.9)
//                        {
//                            // Это горизонтальная грань (ступень)
//                            CurveArray curves = new CurveArray();
//                            foreach (EdgeArray edgeLoop in planarFace.EdgeLoops)
//                            {
//                                foreach (Edge edge in edgeLoop)
//                                {
//                                    curves.Append(edge.AsCurve());
//                                }
//                                break; // Берем только первый контур
//                            }
//                            return curves;
//                        }
//                    }
//                }
//            }
//            return null;
//        }

//        private ElementId GetFloorTypeId(string typeName)
//        {
//            return new FilteredElementCollector(_doc)
//                .OfClass(typeof(FloorType))
//                .FirstOrDefault(x => x.Name.Equals(typeName))?.Id ?? ElementId.InvalidElementId;
//        }

//        private ElementId GetWallTypeId(string typeName)
//        {
//            return new FilteredElementCollector(_doc)
//                .OfClass(typeof(WallType))
//                .FirstOrDefault(x => x.Name.Equals(typeName))?.Id ?? ElementId.InvalidElementId;
//        }



//    }
//}
