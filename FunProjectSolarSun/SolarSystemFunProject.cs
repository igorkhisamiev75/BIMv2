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
                
                
                DateTime now = DateTime.Now;
                
                DateTime time2 = now.AddSeconds(40);

                DateTime now2 = DateTime.Now;

                do
                {
                    //Transaction tran = new Transaction(_doc, "Solar Sun Fun");
                    
                    //tran.Start("Start");
                    
                    now2 = DateTime.Now;
                    MovePlanets(_doc, uiDoc);

                    uiDoc.RefreshActiveView();
                    
                    //tran.Commit();


                } while (now2.ToString("ss") !=time2.ToString("ss"));

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

            string mercuriiId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-0000226f";
            string veneraId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-00002aac";
            string earthId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-00002ab0";
            string marsId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-00002abf";
            string upiterId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-00002ac2";
            string saturnId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-00002ac5";
            string uranId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-00002ac8";
            string neptunId = "026dd368-736e-4cbb-9868-dea3f9ec68d3-00002acb";

            Move(document, mercuriiId);
            Move(document, veneraId);
            Move(document, earthId);
            Move(document, marsId);
            Move(document, upiterId);
            Move2(document, saturnId);
            Move3(document, uranId);
            Move3(document, neptunId);

            //_doc.Regenerate();
            //uiDoc.RefreshActiveView();
            //object p = _doc.IsModified(true);

            tran.Commit();
            
        }

        public void Move(Document document, string id)
        {

            Element e = document.GetElement(id);

            XYZ point1 = new XYZ(0, 0, 0);
            XYZ point2 = new XYZ(0, 0, 10);

            ElementTransformUtils.RotateElement(_doc, e.Id, Line.CreateBound(point1, point2), Math.PI / 182.5);
        }
        public void Move2(Document document, string id)
        {

            Element e = document.GetElement(id);

            XYZ point1 = new XYZ(0, 0, 0);
            XYZ point2 = new XYZ(0, 0, 100);

            ElementTransformUtils.RotateElement(_doc, e.Id, Line.CreateBound(point1, point2), -Math.PI / 302.5);
        }
        public void Move3(Document document, string id)
        {

            Element e = document.GetElement(id);

            XYZ point1 = new XYZ(0, 0, 0);
            XYZ point2 = new XYZ(0, 0, 100);

            ElementTransformUtils.RotateElement(_doc, e.Id, Line.CreateBound(point1, point2), Math.PI / 900);
        }
    }
}
