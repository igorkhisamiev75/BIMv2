//#region Namespaces
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;

//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Architecture;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;

//#endregion

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    [Journaling(JournalingMode.NoCommandData)]

//    public class JoinWalls : IExternalCommand
//    {
//        Application _app;
//        Document _doc;
//        private const double PRECISION = 0.00000001;
//        private CurveArray profile;

//        private Autodesk.Revit.Creation.Application creApp;
//        public List<Wall> AllFinishWall = new List<Wall>();
//        public List<Wall> wallWithoutFinish = new List<Wall>();


//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {

//            UIApplication uiApp = commandData.Application;
//            UIDocument uiDoc = uiApp.ActiveUIDocument;
//            _app = uiApp.Application;
//            _doc = uiDoc.Document;
//            creApp = commandData.Application.Application.Create;


//            using (Transaction t = new Transaction(_doc))
//            {
//                t.Start("Join walls");

//                //get all walls
//                IList<Element> allWallElements = GetAllWallElements(_doc);

//                foreach (Element wallElement in allWallElements)
//                {
//                    try
//                    {
//                        var wall = (Wall)wallElement;

//                        var descriptionWallType = wall.WallType.LookupParameter("Описание").AsString();

//                        if (descriptionWallType == "Отделка")
//                        {
//                            AllFinishWall.Add(wall);
//                        }

//                    }
//                    catch (Exception e)
//                    {
//                        continue;
//                    }
//                }

//                foreach (Element wallElement in allWallElements)
//                {
//                    foreach(var e in AllFinishWall)
//                    {
//                        if (wallElement.Name == e.Name)
//                        {
//                            continue;
//                        }
//                        wallWithoutFinish.Add(e);

//                    }
//                }

//                foreach (Wall wall1 in AllFinishWall)
//                {
//                    var level1 = wall1.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsValueString();

//                  foreach (Wall wall2 in wallWithoutFinish)
//                    {
//                        var level2 = wall2.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsValueString();
                       
//                        if (wall1 != wall2 && level1==level2)
//                        {
//                            BoundingBoxXYZ boundingBoxXYZ = wall1.get_BoundingBox(null);
                           
//                            BoundingBoxXYZ boundingBoxXYZ2 = wall2.get_BoundingBox(null);

//                            if (bbIntersect(boundingBoxXYZ, boundingBoxXYZ2))
//                            {
//                                if (!JoinGeometryUtils.AreElementsJoined(_doc, wall1, wall2))
//                                {
//                                    try
//                                    {
//                                        JoinGeometryUtils.JoinGeometry(_doc, wall1, wall2);
//                                        continue;
//                                    }
//                                    catch (Autodesk.Revit.Exceptions.ArgumentException a)
//                                    {
//                                        //Debug.Print("Ошибка" + a.ToString());
//                                    }
//                                }

//                            }

//                        }

                        

//                    }
//                }

//                #region
//                //join walls
//                //foreach (var wall1 in allWallElements)
//                //{
//                //    //BoundingBoxXYZ g1 = wall1.get_BoundingBox(_doc.ActiveView);
//                //    //Element f = g1.;
//                //    foreach (var wall2 in allWallElements)
//                //    {
//                //        //BoundingBoxXYZ g2 = wall2.get_BoundingBox(_doc.ActiveView);
//                //        if (wall1 != wall2)
//                //        {
//                //            try
//                //            {
//                //                if (JoinGeometryUtils.AreElementsJoined(_doc, wall1, wall2)||
//                //                    JoinGeometryUtils.AreElementsJoined(_doc, wall2, wall1))
//                //                { continue; }
//                //                else
//                //                {
//                //                    JoinGeometryUtils.JoinGeometry(_doc, wall1, wall2);
//                //                    _doc.Regenerate();
//                //                    continue;

//                //                }

//                //            }
//                //            catch { continue; }

//                //        }

//                //        else { continue; }

//                //    }


//                //}

//                //_doc.Regenerate();

//                //var warnings= _doc.GetWarnings();

//                //TaskDialog.Show("2bim.ru", $"{warnings.Count}");

//                //foreach (var warning in warnings)
//                //{
//                //    ICollection<ElementId> elementsWarnings = warning.GetFailingElements();

//                //    foreach (var el2 in elementsWarnings)
//                //        foreach (var el3 in elementsWarnings)
//                //        {
//                //            try
//                //            {
//                //                if (el2 != el3)
//                //                {
//                //                    Element firstElement = _doc.GetElement(el2);
//                //                    Element secondElement = _doc.GetElement(el3);

//                //                    JoinGeometryUtils.UnjoinGeometry(_doc, firstElement, secondElement);
//                //                    _doc.Regenerate();
//                //                }
//                //            }

//                //            catch
//                //            {
//                //                continue;
//                //            }

//                //        }
//                //    {

//                //    }
//                //}

//                #endregion

//                //TaskDialog.Show("2bim.ru", "Стены соеденены ☺☺☺");
//                _doc.Regenerate();
//                t.Commit();

//            }

//            return Result.Succeeded;

//        }


//        public bool bbIntersect(BoundingBoxXYZ BoxXYZ, BoundingBoxXYZ BoxXYZ2)
//        {
//            if (BoxXYZ != null && BoxXYZ2 != null)
//            {
//                Outline outline1 = new Outline(BoxXYZ.Min, BoxXYZ.Max);
//                Outline outline2 = new Outline(BoxXYZ2.Min, BoxXYZ2.Max);

//                if (outline1.Intersects(outline2,0))
//                {
//                    return true;
//                }
//            }

//            return false;
            
//        }


//        public IList<Element> GetAllWallElements(Document document)
//        {
//            // List all Floor's
//            var floor_collector = new FilteredElementCollector(document)
//                .OfCategory(BuiltInCategory.OST_Walls)
//                .WhereElementIsNotElementType();

//            IList<Element> allWalls = floor_collector.ToElements();
//            return allWalls;
//        }


//    }

       

//}

