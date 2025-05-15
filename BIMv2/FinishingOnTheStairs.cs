#region Namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;


#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]

    public class FinishingOnTheStairs : IExternalCommand
    {
        Application _app;
        Document _doc;
        private const double PRECISION = 0.00000001;
        private CurveArray profile;
        //private Hashtable floorTypes;
        private List<string> floorTypesName;
        public FloorType floorType;
        private Level level;
        private bool structural;
        private Autodesk.Revit.Creation.Application creApp;
        private ElementId _floorTypeId;
        private ElementId _wallTypeId;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            creApp = commandData.Application.Application.Create;

            try {

                // Get user selection
                var stairs = SelectStairs(uiDoc);
                if (stairs == null) return Result.Cancelled;

                // Get finishing types
                _floorTypeId = GetFloorTypeId("0");
                _wallTypeId = GetWallTypeId("0");

                if (_floorTypeId == ElementId.InvalidElementId || _wallTypeId == ElementId.InvalidElementId)
                {
                    message = "Не найдет тип отделки";
                    return Result.Failed;
                }

                using (Transaction t = new Transaction(_doc, "Создать отделку лестницы"))
                {
                    t.Start();

                    //создает пол на лестничных клетках
                    CreateLandingFinishes(stairs);
                    CreateRunFinishes(stairs);

                    //TaskDialog.Show("Отделка лестницы", "Отделка сделана на лестнице");
                    _doc.Regenerate();
                    t.Commit();
                }

                return Result.Succeeded;


            }
            catch(Exception ex) 
            
            {
                message = ex.Message;
                return Result.Failed;
            }


           

        }

        
        // an array of planar lines and arcs that represent the horizontal profile of the floor
        public CurveArray Profile
        {
            get
            {
                return profile;
            }
            set
            {
                profile = value;
            }
        }

        public FloorType GetFloorTypeInProject(string nameTypeWall)
        {
            FloorType wallType;
            //// Get a floor type for floor creation
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(FloorType));
            wallType = collector.Where(x => x.Name == nameTypeWall) as FloorType;
            //as WallType;

            FilteredElementCollector a = new FilteredElementCollector(_doc).OfClass(typeof(FloorType));
            foreach (FloorType wallType2 in a)
            {
                if (wallType2.Name == nameTypeWall)
                {
                    return wallType2;
                }
            }

            return wallType;

        }

        public WallType GetWallTypeInProject(string nameTypeWall)
        {
            WallType wallType;
            //// Get a floor type for floor creation
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(WallType));
            wallType = collector.Where(x => x.Name == nameTypeWall) as WallType;
            //as WallType;

            FilteredElementCollector a = new FilteredElementCollector(_doc).OfClass(typeof(WallType));
            foreach (WallType wallType2 in a)
            {
                if (wallType2.Name == nameTypeWall)
                {
                    return wallType2;
                }
            }

            return wallType;
        }

        private CurveLoop CreateTreadProfile(Curve baseline, XYZ runDirection, double runWidth)
        {
            // Create a rectangular profile for each tread
            var start = baseline.GetEndPoint(0);
            var end = baseline.GetEndPoint(1);

            var widthVector = (end - start).Normalize().CrossProduct(XYZ.BasisZ) * runWidth;
            var p1 = start;
            var p2 = end;
            var p3 = end + widthVector;
            var p4 = start + widthVector;

            var profile = new CurveLoop();
            profile.Append(Line.CreateBound(p1, p2));
            profile.Append(Line.CreateBound(p2, p3));
            profile.Append(Line.CreateBound(p3, p4));
            profile.Append(Line.CreateBound(p4, p1));

            return profile;
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

        /// <summary>
        /// Метод создает перекрытия на каждой лестничной площадке
        /// </summary>
        /// <param name="stairs"></param>
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
                    var nf = _doc.Create.NewFloor(temp, floorType, level, false);
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
                //лестничный пролет
                StairsRun stairsRun = _doc.GetElement(staitRun) as StairsRun;

                //BaseElevation
                var baseElevation = stairsRun.BaseElevation;

                //высота подступенка
                var hightStar = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RISER_HEIGHT).AsDouble();

                //ширина лестницы
                var wightStar = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RUN_WIDTH).AsDouble();

                //ширина протсупни глубина
                var wightStarProst = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_TREAD_DEPTH).AsDouble();

                //число проступней
                var numberTheads = stairsRun.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_NUMBER_OF_TREADS).AsInteger();

                //направление надо найти для правильного копирования
                CurveLoop stirPath = stairsRun.GetStairsPath();
                XYZ runDirection = (stirPath.FirstOrDefault() as Line)?.Direction ?? XYZ.BasisX;

                Line cl2 = stirPath.FirstOrDefault() as Line;

                XYZ dirLine = cl2.Direction;

                double dixX = dirLine.X;
                double dixY = dirLine.Y;

                Options options = new Options();
                GeometryElement geom = stairsRun.get_Geometry(options);

                //попробуем зайти черех Boinding box

                var bbMax = stairsRun.get_BoundingBox(null).Max;
                var bbMin = stairsRun.get_BoundingBox(null).Min;

                //рамка марша
                CurveLoop sideBoundaries = stairsRun.GetFootprintBoundary();

                //IList<Curve> curves = new List<Curve>();

                foreach (Curve boundary in sideBoundaries)
                {
                    if (boundary.ApproximateLength == wightStar)

                    {
                        //создаем стены
                        Wall wall = Wall.Create(_doc, boundary, idLevelStairs, false);

                        //создание стены на ступенке
                        wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(hightStar); //установка высоты
                        wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseElevation + ofsetStair);

                        wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(idStairs);

                        _doc.Regenerate();

                        //граница помещения
                        wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);

                        for (int i = 1; i <= numberTheads; i++)
                        {
                            XYZ vector = new XYZ(wightStarProst * dixX * i, wightStarProst * i * dixY, hightStar * i + ofsetStair);

                            //vector = bbMax - bbMin;

                            ICollection<ElementId> newWall = ElementTransformUtils.CopyElement(_doc, wall.Id, vector);

                            //_doc.Regenerate();

                            foreach (var el in newWall)
                            {
                                _doc.GetElement(el).get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(hightStar * i + baseElevation + ofsetStair);
                                _doc.GetElement(el).get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(idStairs);
                            }


                        }

                        //создаем перекрытия

                        //количество ступенек numberTheads
                        //ширина лестницы wightStar
                        //глубина ступеньки wightStarProst

                        List<Line> lineForFinishDegree = new List<Line>();

                        foreach (Curve item3 in sideBoundaries)

                        {
                            Line line = item3 as Line;

                            //длина длинии рисующую лестницу

                            double lineLighte = line.Length;

                            lineForFinishDegree.Add(line);

                        }

                        List<XYZ> newlistXYZ = new List<XYZ>();

                        IList<XYZ> l1 = lineForFinishDegree[0].Tessellate();

                        XYZ x1 = l1.First();
                        XYZ x2 = l1.Last();

                        int x1XInt = (int)x1.X;
                        int x2XInt = (int)x2.X;

                        int y1XInt = (int)x1.Y;
                        int y2XInt = (int)x2.Y;


                        //TaskDialog.Show("Отделка лестницы", $"{x1XInt} {x2XInt} {y1XInt} {y2XInt}"); ;

                        int k = 0;

                        if (x1XInt > x2XInt && y1XInt == y2XInt)
                        {

                            mainXYZ = x2;
                            k = 1;
                        }

                        if (x1XInt == x2XInt && y1XInt < y2XInt)
                        {

                            mainXYZ = l1[1];
                            k = 2;
                        }

                        if (x1XInt == x2XInt && y1XInt > y2XInt) //сделано
                        {

                            mainXYZ = l1[1];
                            k = 3;
                        }

                        if (x1XInt < x2XInt && y1XInt == y2XInt)
                        {

                            mainXYZ = l1[1];
                            k = 4;
                        }


                        if (k == 1) //все в одном
                        {
                            XYZ xYZ = new XYZ(mainXYZ.X + wightStarProst, mainXYZ.Y, mainXYZ.Z);
                            mainXYZ2 = xYZ;
                            XYZ xYZ1 = new XYZ(mainXYZ.X, mainXYZ.Y - wightStar, mainXYZ.Z);
                            mainXYZ3 = xYZ1;
                            XYZ xYZ2 = new XYZ(mainXYZ.X + wightStarProst, mainXYZ.Y - wightStar, mainXYZ.Z);
                            mainXYZ4 = xYZ2;

                        }

                        if (k == 2) //все в одном
                        {
                            XYZ xYZ = new XYZ(mainXYZ.X, mainXYZ.Y - wightStarProst, mainXYZ.Z);
                            mainXYZ2 = xYZ;
                            XYZ xYZ1 = new XYZ(mainXYZ.X - wightStar, mainXYZ.Y, mainXYZ.Z);
                            mainXYZ3 = xYZ1;
                            XYZ xYZ2 = new XYZ(mainXYZ.X - wightStar, mainXYZ.Y - wightStarProst, mainXYZ.Z);
                            mainXYZ4 = xYZ2;

                        }

                        if (k == 3) //норм
                        {
                            XYZ xYZ = new XYZ(mainXYZ.X, mainXYZ.Y + wightStarProst, mainXYZ.Z);
                            mainXYZ2 = xYZ;
                            XYZ xYZ1 = new XYZ(mainXYZ.X + wightStar, mainXYZ.Y, mainXYZ.Z);
                            mainXYZ3 = xYZ1;
                            XYZ xYZ2 = new XYZ(mainXYZ.X + wightStar, mainXYZ.Y + wightStarProst, mainXYZ.Z);
                            mainXYZ4 = xYZ2;

                        }

                        if (k == 4) //все в одном
                        {
                            XYZ xYZ = new XYZ(mainXYZ.X, mainXYZ.Y + wightStar, mainXYZ.Z);
                            mainXYZ2 = xYZ;

                            XYZ xYZ1 = new XYZ(mainXYZ.X - wightStarProst, mainXYZ.Y, mainXYZ.Z);
                            mainXYZ3 = xYZ1;

                            XYZ xYZ2 = new XYZ(mainXYZ.X - wightStarProst, mainXYZ.Y + wightStar, mainXYZ.Z);
                            mainXYZ4 = xYZ2;

                        }

                        if (k == 0) //все в одном
                        {
                            TaskDialog.Show("Не сработало", "Пока такая лестница в работе у программиста https://2bim.ru");

                        }



                        Line line1 = Line.CreateBound(mainXYZ, mainXYZ2);
                        Line line2 = Line.CreateBound(mainXYZ2, mainXYZ4);
                        Line line3 = Line.CreateBound(mainXYZ4, mainXYZ3);
                        Line line4 = Line.CreateBound(mainXYZ3, mainXYZ);




                        //tempStair.Append(line1);
                        //tempStair.Append(line2);
                        //tempStair.Append(line3);
                        //tempStair.Append(line4);

                        CurveLoop profile = new CurveLoop();
                        profile.Append(line1);
                        profile.Append(line2);
                        profile.Append(line3);
                        profile.Append(line4);

#if R2019 || R2020 || R2021 || R2022
                            var nf = _doc.Create.NewFloor(tempStair, floorType, level, false);
#else

                        var nf = Floor.Create(_doc, new List<CurveLoop> { profile }, _floorTypeId, level.Id);
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

    }

    
}

