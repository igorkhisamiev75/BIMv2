using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace FinishStairs
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class FinishingOnTheStairs : IExternalCommand
    {
        private Document _doc;
        private ElementId _floorTypeId;
        private ElementId _wallTypeId;
        private FloorType floorTypeGlob;
        private const double Precision = 0.0001;
        private Line line1;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            _doc = uiDoc.Document;

            MainWindow form = new MainWindow(_doc);

            form.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            if (form.ShowDialog() == true)
            {
                using (var tx = new Transaction(_doc, "Отделка лестниц от 2bim.ru"))
                {
                    tx.Start();

                    if (form.ProcessAllStairs)
                    {
                        // Обработка всех лестниц в проекте
                        var allStairs = new FilteredElementCollector(_doc)
                            .OfCategory(BuiltInCategory.OST_Stairs)
                            .WhereElementIsNotElementType();

                        foreach (Element stair in allStairs)
                        {
                            ProcessStair(stair, form.SelectedWallType, form.SelectedFloorType);
                        }
                    }
                    if (!form.ProcessAllStairs)
                    {
                        // Get user selection
                        Stairs stairs = SelectStairs(uiDoc);
                        if (stairs == null) return Result.Cancelled;
                        //TaskDialog.Show("Отделка лестницы2", "Отделка сделана на лестнице");
                        // Обработка выбранных лестниц

                        ProcessStair(stairs, form.SelectedWallType, form.SelectedFloorType);
                        
                    }
                    TaskDialog.Show("Отделка лестницы", "Отделка сделана на лестнице");
                    tx.Commit();
                }
            }

            return Result.Succeeded;
        }

        private void ProcessStair(Element stair, WallType wallType, FloorType floorType)
        {
            //TaskDialog.Show("Отделка лестницы", "Отделка сделана на лестнице");

            // Get finishing types
            _floorTypeId = GetFloorTypeId(floorType.Name);
            _wallTypeId = GetWallTypeId(wallType.Name);
            floorTypeGlob = floorType;

            //создает пол на лестничных клетках
            CreateLandingFinishes((Stairs)stair);
            CreateRunFinishes((Stairs)stair);
            // Здесь реализуйте вашу логику обработки лестницы
            // с использованием выбранных типов стены и перекрытия
        }

        private Stairs SelectStairs(UIDocument uiDoc)
        {
            try
            {
                var reference = uiDoc.Selection.PickObject(ObjectType.Element,
                    new StairsSelectionFilter(), "Select a stair element");
                return _doc.GetElement(reference) as Stairs;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        public class StairsSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Stairs;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }

        private void CreateLandingFinishes(Stairs stairs)

        {
            //Stairs str =GetStairInfo(_doc);
            //уровень лестницы
            ElementId lv = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsElementId();

            //смещение лестницы снизу
            double ofsetStair = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_OFFSET).AsDouble();

            //id лестницы
            string idStairs = stairs.Id.ToString();

            CurveArray temp = new CurveArray();
            CurveArray tempStair = new CurveArray();

            Level level = (Level)_doc.GetElement(lv);

            ICollection<ElementId> landings = stairs.GetStairsLandings();
            ICollection<ElementId> runs = stairs.GetStairsRuns();

            double elevation = 0;

            //создаем перекрытия на каждой площадке
            foreach (ElementId landing in landings)
            {
                try
                {
                    StairsLanding stairsL = _doc.GetElement(landing) as StairsLanding;
                    var cl = stairsL.GetFootprintBoundary();

                    elevation = stairsL.BaseElevation;
                    CurveLoop profile = new CurveLoop();

                    foreach (var item in cl)
                    {
                        temp.Append(item);
                        profile.Append((Line)item);
                    }



#if R2019 || R2020 || R2021 || R2022
                    var nf = _doc.Create.NewFloor(temp, floorTypeGlob, level, false);
#else

                    Floor nf = Floor.Create(_doc, new List<CurveLoop> { profile }, _floorTypeId, level.Id);
#endif
                    var tp = nf.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();

                    temp.Clear();
                    nf.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(elevation + ofsetStair + tp);
                    nf.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(idStairs);



                }
                catch { }


            }

        }

        /// <summary>
        /// Метод создает перекрытия и стены на каждой ступеньке
        /// </summary>
        /// <param name="stairs"></param>
        private void CreateRunFinishes(Stairs stairs)

        {


            //уровень лестницы
            ElementId idLevelStairs = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsElementId();
            Level level = (Level)_doc.GetElement(idLevelStairs);
            //смещение лестницы снизу
            double ofsetStair = stairs.get_Parameter(BuiltInParameter.STAIRS_BASE_OFFSET).AsDouble();
            //id лестницы
            string idStairs = stairs.Id.ToString();


            //ICollection<ElementId> landings = stairs.GetStairsLandings();
            //лестничные пролеты
            ICollection<ElementId> collectionsStairsRuns = stairs.GetStairsRuns();

            //создаем стены

            //WallType wallType;

            //wallType = GetWallTypeInProject("0");

            XYZ mainXYZ = new XYZ();
            XYZ mainXYZ2 = new XYZ();
            XYZ mainXYZ3 = new XYZ();
            XYZ mainXYZ4 = new XYZ();


            foreach (ElementId staitRun in collectionsStairsRuns)
            {

                int index = collectionsStairsRuns
                    .Select((item, idx) => new { Item = item, Index = idx })
                    .FirstOrDefault(x => x.Item == staitRun)?.Index ?? -1;
                //лестничный пролет
                StairsRun stairsRun = _doc.GetElement(staitRun) as StairsRun;

                int iii = stairsRun.ActualRisersNumber;
                int iii2 = stairsRun.ActualTreadsNumber;

                //профиль
                CurveLoop cll2 = GetStairStepCurveLoop(stairs, index, 0);

                //BaseElevation
                var baseElevation = stairsRun.BaseElevation;

                //высота подступенка
                var hightStar = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RISER_HEIGHT).AsDouble();

                //ширина лестницы
                var wightStar = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RUN_WIDTH).AsDouble();

                //ширина протсупни глубина
                var wightStarProst = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_TREAD_DEPTH).AsDouble();

                //число проступней
                int numberTheads = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_NUMBER_OF_TREADS).AsInteger();

                //направление надо найти для правильного копирования
                CurveLoop stirPath = stairsRun.GetStairsPath();
                XYZ runDirection = (stirPath.FirstOrDefault() as Line)?.Direction ?? XYZ.BasisX;

                Line stairLine = stirPath.FirstOrDefault() as Line;

                XYZ dirLine = stairLine.Direction;

                double dixX = dirLine.X;
                double dixY = dirLine.Y;

                Options options = new Options();
                GeometryElement geomElem = stairsRun.get_Geometry(options);


                //попробуем зайти черех Boinding box

                //var bbMax = stairsRun.get_BoundingBox(null).Max;
                //var bbMin = stairsRun.get_BoundingBox(null).Min;

                //рамка марша
                CurveLoop sideBoundaries = stairsRun.GetFootprintBoundary();

                //IList<Curve> curves = new List<Curve>();

                foreach (Curve boundary in sideBoundaries)
                {
                    if (boundary.ApproximateLength == wightStar)

                    {
                        //создаем стены
                        Wall wall = Wall.Create(_doc, boundary, _wallTypeId, idLevelStairs,3.0, 0,  false, false);

                        //создание стены на ступенке
                        wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(hightStar); //установка высоты
                        wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseElevation + ofsetStair);
                        wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(idStairs);

                        //_doc.Regenerate();

                        //граница помещения
                        wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);

                        //TaskDialog.Show("1","1");

                        for (int i = 0; i <= numberTheads; i++)
                        {
                            XYZ vector = new XYZ(wightStarProst * dixX * i, wightStarProst * i * dixY, hightStar * i + ofsetStair);

                            //vector = bbMax - bbMin;

                            ICollection<ElementId> newWall = ElementTransformUtils.CopyElement(_doc, wall.Id, vector);

                            foreach (var el in newWall)
                            {
                                _doc.GetElement(el).get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(hightStar); //установка высоты
                                _doc.GetElement(el).get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(hightStar * i + baseElevation + ofsetStair);
                                _doc.GetElement(el).get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(idStairs + "_" + i);
                                _doc.Regenerate();
                            }


                        }

                        //создаем перекрытия на ступеньках

                        //количество ступенек numberTheads
                        //ширина лестницы wightStar
                        //глубина ступеньки wightStarProst

                        List<Line> lineForFinishDegree = new List<Line>();


                        foreach (Curve item3 in sideBoundaries)

                        {
                            Line line = item3 as Line;

                            //line1= line;
                            //длина длинии рисующую лестницу

                            double lineLighte = line.Length;

                            lineForFinishDegree.Add(line);
                        }



                        line1 = sideBoundaries.First() as Line;

                        CurveLoop profile = CreateTreadProfile(line1, runDirection, wightStarProst, numberTheads);

                        //тут нужно допилить для 21 ревита, елис нужно))рв
                        CurveArray tempStair = new CurveArray();


#if R2019 || R2020 || R2021 || R2022
                        var nf = _doc.Create.NewFloor(tempStair, floorTypeGlob, level, false);
#else

                        var nf = Floor.Create(_doc, new List<CurveLoop> { cll2 }, _floorTypeId, level.Id);
#endif
                        //толщина перекрытия
                        var tp = nf.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();

                        nf.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(baseElevation + hightStar + tp + ofsetStair);
                        nf.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(idStairs);

                        //tempStair.Clear();

                        for (int i = 1; i < numberTheads; i++)
                        {
                            XYZ vector = new XYZ(wightStarProst * dixX * i, wightStarProst * i * dixY, hightStar * i + ofsetStair);

                            //var vector = bbMax - bbMin;

                            var newFloor = ElementTransformUtils.CopyElement(_doc, nf.Id, vector);

                            //_doc.Regenerate();

                            foreach (var el in newFloor)
                            {
                                _doc.GetElement(el).get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(hightStar * i + baseElevation + hightStar + ofsetStair + tp);
                                _doc.GetElement(el).get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(idStairs);
                            }


                        }

                        break;
                    }

                }

            }




        }

        private ElementId GetFloorTypeId(string typeName)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .FirstOrDefault(x => x.Name.Equals(typeName))?.Id ?? ElementId.InvalidElementId;
        }

        private ElementId GetWallTypeId(string typeName)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(WallType))
                .FirstOrDefault(x => x.Name.Equals(typeName))?.Id ?? ElementId.InvalidElementId;
        }

        
        private CurveLoop CreateTreadProfile(Curve baseline, XYZ runDirection, double runWidth, int kolStup)
        {
            //runWidth = runWidth / kolStup;
            // Create a rectangular profile for each tread
            XYZ start = baseline.GetEndPoint(0);
            XYZ end = baseline.GetEndPoint(1);

            XYZ widthVector = (end - start).Normalize().CrossProduct(XYZ.BasisZ) * runWidth;
            XYZ p1 = start;
            XYZ p2 = end;
            XYZ p3 = end + widthVector;
            XYZ p4 = start + widthVector;

            CurveLoop profile = new CurveLoop();
            profile.Append(Line.CreateBound(p1, p2));
            profile.Append(Line.CreateBound(p2, p3));
            profile.Append(Line.CreateBound(p3, p4));
            profile.Append(Line.CreateBound(p4, p1));

            return profile;
        }


        public CurveLoop GetStairStepCurveLoop(Stairs stairs, int runIndex, int stepIndex)
        {
            // Получаем все компоненты лестницы
            ICollection<ElementId> runs = stairs.GetStairsRuns();

            // Проверяем наличие требуемого марша
            if (runIndex >= runs.Count)
            {
                throw new ArgumentOutOfRangeException("runIndex", "Индекс марша выходит за пределы диапазона");
            }

            // Получаем марш лестницы
            StairsRun run = stairs.Document.GetElement(runs.ElementAt(runIndex)) as StairsRun;

            // Получаем геометрию марша
            Options geomOptions = new Options();
            geomOptions.ComputeReferences = true;
            GeometryElement geomElem = run.get_Geometry(geomOptions);

            // Ищем солид, представляющий марш
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    if (solid.Faces.Size > 0)
                    {
                        // Получаем все грани солида
                        foreach (Face face in solid.Faces)
                        {
                            // Проверяем, является ли грань горизонтальной (ступенью)
                            if (face is PlanarFace planarFace)
                            {
                                if (Math.Abs(planarFace.FaceNormal.Z) > 0.9) // Горизонтальная грань
                                {
                                    // Получаем кривую границы грани
                                    CurveLoop curveLoop = planarFace.GetEdgesAsCurveLoops().First();

                                    // Здесь должна быть логика определения нужной ступени
                                    // Это упрощенный пример - в реальности нужно анализировать положение ступени
                                    if (stepIndex == 0) // Для примера возвращаем первую найденную ступень
                                    {
                                        return curveLoop;
                                    }
                                    stepIndex--;
                                }
                            }
                        }
                    }
                }
            }

            throw new Exception("Не удалось найти кривую ступени с указанным индексом");
        }
    }
}
