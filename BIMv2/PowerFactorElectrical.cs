using System;
using System.Collections.Generic;
using System.Globalization;
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
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            double forFormul1 = 0.23;
            double forFormul3 = 0.66;
            double k;
            //ADSK_Коэффициент мощности = Активная расчетная мощность/0,658/Ток-расчетный
            //ADSK_Коэффициент мощности = Активная расчетная мощность/0,22/Ток-расчетный
            using (Transaction t = new Transaction(_doc))
            {
                t.Start("Create");

                IList<Element> listAnnotationList=GetAllGenericAnnotationList();
                int i = 0;
                IFormatProvider formatter = new NumberFormatInfo { NumberDecimalSeparator = "." };

                if (listAnnotationList != null)
                {
                    foreach (Element el in listAnnotationList)
                    {
                        if (el.LookupParameter("Семейство").AsValueString() == "_Силовой щит - Групповая линия (М)"|| el.LookupParameter("Семейство").AsValueString()=="# Reports - Схема - Силовой щит - Данные")
                        {
                            var activRatedPower2 = (el.LookupParameter("Активная мощность - Расчётная").AsString());

                            if (el.LookupParameter("Число фаз").AsValueString() == "1")
                            {
                                k = forFormul1;
                            }

                            else
                            {
                                k = forFormul3;
                            }
                            
                            if (activRatedPower2!="")
                            {
                                try
                                {
                                    double activRatedPower =
                                        double.Parse((el.LookupParameter("Активная мощность - Расчётная").AsString()),
                                            formatter);
                                    double ratedCurrent =
                                        double.Parse((el.LookupParameter("Ток - Расчётный").AsString()), formatter);

                                    if (activRatedPower != null)
                                    {
                                        double adskFack = activRatedPower / k / ratedCurrent;

                                        adskFack = Math.Round(adskFack, 2);

                                        if (adskFack > 1)
                                        {
                                            adskFack = 0.99;
                                        }

                                        string adskFack2 = (Convert.ToString(adskFack)).Replace(",", ".");

                                        el.LookupParameter("ADSK_Коэффициент мощности").Set(adskFack2);

                                        i++;
                                    }
                                }

                                catch (Exception ex)
                                {
                                    //TaskDialog.Show("Revit", "Exception " + ex.Message);
                                }
                                
                            }
                            
                        }
                       

                        
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

            return collector.ToElements();

            
        }
    }
}
