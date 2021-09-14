#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Application = Autodesk.Revit.ApplicationServices.Application;

#endregion

namespace FunProjectSolarSun
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class SolarSystemFunProject : IExternalCommand
    {
         Application _app;
         Document _doc;
        

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;
            
            try
            {
                var dlg = new SolarSunPlayerForm();
                //_doc.Regenerate();

                
                DateTime now = DateTime.Now;
                
                DateTime time2 = now.AddSeconds(4);

                DateTime now2 = DateTime.Now;

                do
                {
                    //Transaction tran = new Transaction(_doc, "Solar Sun Fun");
                    
                    //tran.Start("Start");
                    
                    now2 = DateTime.Now;
                    MovePlanets(_doc, uiDoc);
                   

                    //tran.Commit();


                } while (now2.ToString("s") !=time2.ToString("s"));

                return Result.Succeeded;

            }
            catch (Exception exp)
            {
                message = exp.Message;
                return Result.Failed;
            }
            
            
        }

       
        public void MovePlanets(Document document, UIDocument uiDoc)
        {
            

            Transaction tran = new Transaction(document, "Solar Sun Fun");
            tran.Start("Start");

            string earthId = "54d88471-fd75-4915-9c1b-f310666605a7-00001353";

            Element e = document.GetElement(earthId);

            XYZ point1 = new XYZ(0, 0, 0);
            XYZ point2 = new XYZ(0, 0, 10);
            
            ElementTransformUtils.RotateElement(_doc, e.Id, Line.CreateBound(point1, point2), Math.PI / 182.5);
            _doc.Regenerate();
            uiDoc.RefreshActiveView();
            //object p = _doc.IsModified(true);

            tran.Commit();
            
            
            //return Result.Succeeded;

        }
    }
}
