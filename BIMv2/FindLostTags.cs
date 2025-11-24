//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Linq;

//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Architecture;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    public class FindLostTags : IExternalCommand
//    {
//        // Member variables
//        Application _app;
//        Document _doc;
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            var uiApp = commandData.Application;
//            var uiDoc = uiApp.ActiveUIDocument;

//            IList<Room> roomsInProject = new List<Room>();

//            _app = uiApp.Application;
//            _doc = uiDoc.Document;


//            using (var t = new Transaction(_doc))
//            {
//                t.Start("Отредактировать размеры");

                

//                List<Element> genericModels = new FilteredElementCollector(_doc)
//                    .OfCategory(BuiltInCategory.OST_GenericModelTags).WhereElementIsNotElementType().ToList();

//                List<IndependentTag> listIndependentTags = new List<IndependentTag>();

//                int i = 0;

//                Reference referenceToTag = new Reference(genericModels[0]);

//                foreach (var gm in genericModels)
//                {
//                    var it = gm as IndependentTag;

//                    if (it!=null && it.IsOrphaned == true)
//                    {
//                        listIndependentTags.Add(it);
//                    }

//                    if (it != null && it.IsOrphaned == false)
//                    {
//#if R2019 || R2020 || R2021 || R2022
//                        referenceToTag = it.GetTaggedReference(); 
//#else
//                        referenceToTag = (Reference)it.GetTaggedReferences();
//#endif
//                    }


//                }

//                foreach (var it in listIndependentTags)
//                {

//                    var symId = it.GetTypeId();
//                    var view = it.OwnerViewId;
//                    //TagMode tagMode = TagMode.TM_ADDBY_CATEGORY;
//                    //Reference referenceToTag = it.GetTaggedReference();

//                    var tagOrientation = TagOrientation.Horizontal;
//                    var pnt = it.TagHeadPosition;
//                    //var pnt = it.LeaderEnd;

//                    try 
//                    {
//                        IndependentTag newTag = IndependentTag.Create(_doc, symId, view, referenceToTag, true, tagOrientation, pnt);

//                        if (newTag != null)
//                        {
//                            //TaskDialog.Show("Сделано", $"{it.Name}");
//                        }
//                        newTag.HasLeader = false;

//                        _doc.Delete(it.Id);
//                        //_doc.Regenerate();
//                    }
//                    catch {
//                        //_doc.Delete(it.Id); 
//                        continue; }
                    

//                }


//                //TaskDialog.Show("Сделано", $"{genericModels.Count}");
//                //TaskDialog.Show("Сделано", "Сделано");


//                //var el = _doc.GetElement(ID);

//                //if (el.Category.Id == ID2)
//                //{
//                //   // el.
//                //}




//                //TaskDialog.Show("Сделано", "Ограждения по контуру помещения смоделированы");

//                t.Commit();

//            }
//            return Result.Succeeded;

//        }


//        public IList<Element> GetAllGenericModelTags(Document doc)
//        {
//            //// 
//            //var room_collector = new FilteredElementCollector(doc)
//            //    .OfClass(typeof(IndependentTag)).Cast<IndependentTag>();
//            //    //.Where(tag => tag.IsOrphaned == true);

//            //IList<ElementId> room_eids = room_collector.ToElement();

//            //var allRooms = room_collector.ToElements();

//            //return allRooms;

//            var floor_collector = new FilteredElementCollector(doc)
//               .OfCategory(BuiltInCategory.OST_GenericModelTags)
//               .WhereElementIsNotElementType();

//            IList<Element> allFloors = floor_collector.ToElements();
//            return allFloors;


//        }

       


//    }
//}
