using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Create3DViewFromViewPlanWPF
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class CreateViewForPlans : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            ViewSet m_allViews = new ViewSet();

            // get list of all ViewPlan
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            FilteredElementIterator itor = collector.OfClass(typeof(ViewPlan)).GetElementIterator();
            itor.Reset();
            while (itor.MoveNext())
            {
                ViewPlan view = itor.Current as ViewPlan;
                // skip view templates because they're invisible in project browser
                if (null == view || view.IsTemplate)
                {
                    continue;
                }
                else
                {
                    ElementType objType = doc.GetElement(view.GetTypeId()) as ElementType;
                    if (null == objType || objType.Name.Equals("Schedule")
                                        || objType.Name.Equals("Drawing Sheet"))
                    {
                        continue;
                    }
                    else
                    {
                        m_allViews.Insert(view);
                        //AssortViews(view.Name, objType.Name);
                    }
                }
            }


            //get Plan new 

            FilteredElementCollector elementCollector = new FilteredElementCollector(doc);
            ICollection<Element> appPlans = elementCollector.OfCategory(BuiltInCategory.OST_Views)
                .WhereElementIsNotElementType()
                .ToElements();

            UserCreate3D userCreate3D = new UserCreate3D(appPlans);
            userCreate3D.ShowDialog();



            return Result.Succeeded;
        }
    }
}
