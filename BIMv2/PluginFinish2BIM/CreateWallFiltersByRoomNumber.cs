using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateWallFiltersByRoomNumber : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();

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

        public static void CreateViewFilter(Document doc, View view)
        {
            List<ElementId> categories = new List<ElementId>();
            categories.Add(new ElementId(BuiltInCategory.OST_Walls));

            List<FilterRule> filterRules = new List<FilterRule>();
            
            // Create filter element assocated to the input categories
            ParameterFilterElement parameterFilterElement = ParameterFilterElement.Create(doc, "Помещение 1", categories);

            // Criterion 1 - wall type Function is "Exterior"
            ElementId exteriorParamId = new ElementId(BuiltInParameter.FUNCTION_PARAM);
            filterRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(exteriorParamId, (int)WallFunction.Exterior));

            // Criterion 2 - wall height > some number
            ElementId lengthId = new ElementId(BuiltInParameter.CURVE_ELEM_LENGTH);
            filterRules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(lengthId, 28.0, 0.0001));

            // Criterion 3 - custom shared parameter value matches string pattern
            // Get the id for the shared parameter - the ElementId is not hardcoded, so we need to get an instance of this type to find it
            Guid spGuid = new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3");

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(Wall));
            Wall wall = collector.FirstElement() as Wall;

                if (wall != null)
                {
                    Parameter sharedParam = wall.get_Parameter(spGuid);
                    ElementId sharedParamId = sharedParam.Id;

                    filterRules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(sharedParamId, "15.", true));
                }

                parameterFilterElement.SetElementFilter(new ElementParameterFilter(filterRules));

                Color color = new Color(0, 0, 0);
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetSurfaceBackgroundPatternColor(color);

                // Apply filter to view
                view.AddFilter(parameterFilterElement.Id);
                view.SetFilterVisibility(parameterFilterElement.Id, false);
                view.SetFilterOverrides(parameterFilterElement.Id, ogs);
               
        }
        
    }
}
