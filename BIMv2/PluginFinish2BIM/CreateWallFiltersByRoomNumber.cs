using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Microsoft.Office.Interop.Excel;

using Parameter = Autodesk.Revit.DB.Parameter;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateWallFiltersByRoomNumber : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();

        public List<string> AllFinishWall2 = new List<string>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View view = doc.ActiveView;

            using (var t = new Transaction(doc))
            {
                t.Start("Создаем фильтры");

                CreateViewFilter(doc, view);

                TaskDialog.Show("Сделано", "Фильтры созданы");

                t.Commit();

            }
            return Result.Succeeded;

        }

        public void CreateViewFilter(Document doc, View view)
        {
            //собираем все стены отделок

            var allWallElements = GetAllRooms(doc);

            foreach (var element in allWallElements)
            {
                try
                {
                    //var wall2 = (Wall)element;

                    //var descriptionWallType = wall2.WallType.LookupParameter("ADSK_Наименование краткое").AsString();

                    var descriptionWallType2 = element.LookupParameter("Номер").AsString()+" "+
                        element.LookupParameter("Имя").AsString();

                    
                        AllFinishWall2.Add(descriptionWallType2);
                    

                }
                catch (Exception e)
                {
                    continue;
                }
            }

            IList<string> numroomList = new List<string>();

            //foreach (var wall1 in AllFinishWall2)
            //{
            //    //ADSK_Примечание
            //    var otd =wall1.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).AsString();
                
            //    //Comments
            //    //var otd = wall1.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();

            //    //тип
            //    //var otd = wall1.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

            //    numroomList.Add(otd);
            //}

            //clean list
            List<string> newList = new List<string>(AllFinishWall2.Distinct());

            newList.Sort();


            Random rnd = new Random();
            int r = rnd.Next(0, 255);
            int g = rnd.Next(0, 255);
            int b = rnd.Next(0, 255);

            Color color = new Color((byte)r, (byte)g, (byte)b);


            foreach (string nameRoom in AllFinishWall2)
            {
                try
                {
                    if (nameRoom != "" && nameRoom != null)
                    {

                        r = rnd.Next(0, 255);
                        g = rnd.Next(0, 255);
                        b = rnd.Next(0, 255);

                        color = new Color((byte)r, (byte)g, (byte)b);

                        //категории которые используются
                        List<ElementId> categories = new List<ElementId>();
                        categories.Add(new ElementId(BuiltInCategory.OST_Walls));
                        //categories.Add(new ElementId(BuiltInCategory.OST_Floors));
                        //categories.Add(new ElementId(BuiltInCategory.OST_Ceilings));

                        
                        List<FilterRule> filterRules = new List<FilterRule>();

                        //string viewName = view.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();

                        //string nameFilter= nameRoom + " " + viewName;

                       

                        string nameFilter = view.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();


                        // Create filter element assocated to the input categories
                        ParameterFilterElement parameterFilterElement =
                            ParameterFilterElement.Create(doc, "Фильтр пом. " +  nameRoom+ "для вида " + nameFilter, categories);

                        // Criterion 1 - wall type Function is "Exterior"
                        //ElementId exteriorParamId = new ElementId(BuiltInParameter.FUNCTION_PARAM);
                        //filterRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(exteriorParamId, (int)WallFunction.Exterior));

                        // Criterion 2 - wall height > some number
                        //ElementId lengthId = new ElementId(BuiltInParameter.CURVE_ELEM_LENGTH);
                        //filterRules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(lengthId, 28.0, 0.0001));

                        // Criterion 3 - custom shared parameter value matches string pattern
                        // Get the id for the shared parameter - the ElementId is not hardcoded, so we need to get an instance of this type to find it
                        
                        //ADSK_Примечание
                        Guid spGuid = new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3");


                        FilteredElementCollector collector = new FilteredElementCollector(doc);
                        collector.OfClass(typeof(Wall));
                        Wall wall = collector.FirstElement() as Wall;

                        if (wall != null)
                        {
                            Parameter sharedParam = wall.get_Parameter(spGuid);

                            //comments
                            //var parameterComents = wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);

                            //типу стены
                            //.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)

                            //var parameterComents = wall.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM);

                            ElementId sharedParamId = sharedParam.Id;

                            //comments
                            //ElementId elementId =parameterComents.Id;    


                            filterRules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(sharedParamId, nameRoom, true));
                           // filterRules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(elementId, nameRoom, true));

                        }

                        parameterFilterElement.SetElementFilter(new ElementParameterFilter(filterRules));


                        OverrideGraphicSettings ogs = new OverrideGraphicSettings();

                        ogs.SetProjectionLineColor(color); // линии
                        ogs.SetCutForegroundPatternColor(color); //цвет в сечении

                        ogs.SetSurfaceForegroundPatternColor(color); //цвет поверхности

                        FilteredElementCollector elements = new FilteredElementCollector(doc);
                        FillPatternElement solidFillPattern = elements
                            .OfClass(typeof(FillPatternElement))
                            .Cast<FillPatternElement>()
                            .First(a => a.GetFillPattern().IsSolidFill);

                        ogs.SetSurfaceForegroundPatternId(solidFillPattern.Id);

                        ogs.SetSurfaceBackgroundPatternColor(color);
                        ogs.SetSurfaceBackgroundPatternId(solidFillPattern.Id);

                        // Apply filter to view
                        view.AddFilter(parameterFilterElement.Id);
                        view.SetFilterVisibility(parameterFilterElement.Id, true);
                        view.SetFilterOverrides(parameterFilterElement.Id, ogs);
                    }
                }
                catch { }
            }
            

           

        }

        public IList<Element> GetAllWallElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

        public IList<Element> GetAllRooms(Document doc)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

    }
}
