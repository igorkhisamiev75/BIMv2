using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class PowerFactorElectrical : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            double forFormul = 0.658;
            //ADSK_Коэффициент мощности = Активная расчетная мощность/0,658/Ток-расчетный
            using (var t = new Transaction(_doc))
            {
                t.Start("Create");

                var listAnnotationList=GetAllGenericAnnotationList();
                int i = 0;

                if (listAnnotationList != null)
                {
                   
                    foreach (var el in listAnnotationList)
                    {
                        var activRatedPower = Convert.ToDouble(el.LookupParameter("Активная мощность - Расчётная").AsString());
                        var ratedCurrent = Convert.ToDouble(el.LookupParameter("Ток - Расчётный").AsString());

                        var adskFack = activRatedPower / forFormul/ratedCurrent;

                        adskFack = Math.Round(adskFack, 2);

                        el.LookupParameter("ADSK_Коэффициент мощности").Set(adskFack);
                        
                        i++;
                    }

                    
                }

                

                TaskDialog.Show("Complite!", $"Посчитатано коэффициентов в {i} схемах");
                
                t.Commit();

            }

            return Result.Succeeded;
        }

        public IList<Element> GetAllGenericAnnotationList()
        {
            // List all Room's
            var collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_GenericAnnotation)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = collector.ToElementIds() as IList<ElementId>;
            var annotationList = collector.ToElements();

            return annotationList;
        }
    }
}
