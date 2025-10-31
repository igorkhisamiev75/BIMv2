//using System;
//using System.Collections.Generic;
//using System.Linq;

//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Structure;
//using Autodesk.Revit.UI;

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    class ClearDependencies : IExternalCommand
//    {
//        private Application _app;
//        private Document _doc;

//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            var uiApp = commandData.Application;
//            var uiDoc = uiApp.ActiveUIDocument;
//            _app = uiApp.Application;
//            _doc = uiDoc.Document;

//            IList<ElementId> dependenciesList = new List<ElementId>();
//            ElementClassFilter filter = new ElementClassFilter(typeof(Dimension));

//            //получить все светильники

//            var allElements = GetAllLightElements(_doc);
//            //найти у них зависимости

//            //foreach (var element in allElements) {

//            //    if(element.GetDependentElements(filter).FirstOrDefault()==Alig)
//            //    dependenciesList.Add(element.GetDependentElements(filter).FirstOrDefault());


            
//            //}


//            //пройтись по ним и удалить


//            //active view
//            //var notForDeleetedId = _doc.ActiveView.Id;

//            //ViewSet m_allViews = new ViewSet();

//            ////IEnumerable<View> views = new FilteredElementCollector(_doc).
//            ////    OfClass(typeof(View))
//            ////    .WhereElementIsNotElementType()
//            ////    .Cast<View>();

//            //List<ElementId> viewsForDel = new FilteredElementCollector(_doc).
//            //    OfClass(typeof(View)).ToElements().Cast<View>().
//            //    Select(y => y.Id).ToList();

//            //viewsForDel.Remove(notForDeleetedId);

//            var idElement=GetConstrains(_doc);

//            TaskDialog.Show("Error", $"Зависимостей {idElement.Count()}");

//            using (Transaction t = new Transaction(_doc))
//            {
//                t.Start("Del aligment");



//                foreach (var n in idElement)
//                {
//                    try
//                    {
//                        _doc.Delete(n);
//                    }

//                    catch
//                    {
//                        //TaskDialog.Show("Error", $"Тут трабл с{n}");
//                    }
//                }


               


//                t.Commit();

//            }


//            return Result.Succeeded;




//        }

//        public IList<Element> GetAllLightElements(Document document)
//        {
//            // List all Floor's
//            var floor_collector = new FilteredElementCollector(document)
//                .OfCategory(BuiltInCategory.OST_LightingFixtures)
//                .WhereElementIsNotElementType();

//            IList<Element> allFloors = floor_collector.ToElements();
//            return allFloors;
//        }

//        public IList<ElementId> GetConstrains(Document document)
//        {
//            // List all Floor's
//            var floor_collector = new FilteredElementCollector(document)
//                .OfCategory(BuiltInCategory.OST_Constraints).ToElementIds().ToList();

           
//            return floor_collector;
//        }
//    }
//}

