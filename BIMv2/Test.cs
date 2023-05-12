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
    //#region LightPickFilter
    //public class LightPickFilter : ISelectionFilter
    //{
    //    public bool AllowElement(Element e)
    //    {
    //        return e.Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_LightingFixtures);
    //    }

    //    public bool AllowReference(Reference r, XYZ p)
    //    {
    //        return false;
    //    }
    //}
    //#endregion // LightPickFilter

    [Transaction(TransactionMode.Manual)]
    public class Test : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message,ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document _doc = uidoc.Document;

            List<string> listAllElementsOnView = new List<string>();
                       
            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
               

                //get all elements on ActiveView
                var allElementOnView = new FilteredElementCollector(_doc, _doc.ActiveView.Id).Cast<Element>().ToList();

                foreach (var element in allElementOnView)
                {
                    if(element.Category != null)
                    {
                        var g = element.Category.Name;
                        listAllElementsOnView.Add(g);
                    }
                    
                    
                }

                //TaskDialog.Show("Общий", listAllElementsOnView.Count().ToString());

                List<string> distinctListElement = listAllElementsOnView.Distinct().ToList() ;

                //TaskDialog.Show("Почищенный", distinctListElement.Count().ToString());

                DataCategory dataCategory = new DataCategory();


                foreach(var el in distinctListElement)
                {
                    if(el=="Стены")
                    foreach (var element in allElementOnView)
                    {
                        if (element.Category != null)
                        {
                            string g = element.Category.Name;

                            if (element.Category != null && el == g)
                            {
                                dataCategory.Name = element.Category.Name;

                                ParameterSet pSet = element.Parameters;

                                IList<Parameter> ps = element.GetOrderedParameters();

                                foreach (var param in ps)
                                {

                                    if (param != null)
                                    {
                                        try
                                        {
                                                var st = param.Definition.Name.ToString();
                                                TaskDialog.Show("Имя параметра", st);

                                                //dataCategory.parametersCategory.Add(st);
                                        }

                                        catch
                                        {

                                        }


                                    }

                                }



                            }


                        }
                           
                    }

                }


                //string Something = string.Join(",", dataCategory.parametersCategory);


                TaskDialog.Show("Почищенный", "1");


                t.Commit();

            }


            return Result.Succeeded;
        }
    }


    public class DataCategory
    {
        public string Name { get; set; }
        public List<string> parametersCategory { get; set; }


    }



}