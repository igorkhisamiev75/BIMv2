#region Namespaces

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using System.Collections.Generic;
using System.Linq;

using OperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;
#endregion

namespace BIMv2
{
    #region LightPickFilter
    public class LightPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element e)
        {
            return e.Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_LightingFixtures);
        }

        public bool AllowReference(Reference r, XYZ p)
        {
            return false;
        }
    }
    #endregion // LightPickFilter

    [Transaction(TransactionMode.Manual)]
    public class Test : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message,ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document _doc = uidoc.Document;

            List<string> _elements = new List<string>();

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
               

                //get all elements on ActiveView
                var col = new FilteredElementCollector(_doc, _doc.ActiveView.Id).Cast<Element>().ToList();

                foreach (var element in col)
                {
                    if(element.Category != null)
                    {
                        var g = element.Category.Name;
                        _elements.Add(g);
                    }
                    
                    
                }

                List<string> distinct2 = _elements.Distinct().ToList() ;

                _elements.Distinct().ToList();


                var filtersInViewsId2 = col.GroupBy(x => x.Category);

                filtersInViewsId2.Distinct().ToList();


                TaskDialog.Show("111", distinct2.Count().ToString());

                //TaskDialog.Show("123", "123");

                //del filters checked
                //ViewFiltersDelete(_doc, (List<Element>)checkFilters);
                t.Commit();

            }


            return Result.Succeeded;
        }
    }
}