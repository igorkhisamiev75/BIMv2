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
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            try
            {
                var dlg = new SolarSunPlayerForm();

                while (dlg.ShowDialog() == DialogResult.OK)
                {
                    var tran = new Transaction(commandData.Application.ActiveUIDocument.Document, "Solar Sun Fun");
                    tran.Start("Start");
                    MoveColumn(_doc);
                    tran.Commit();
                }

                return Result.Succeeded;

                //if (null == commandData) throw new ArgumentNullException("commandData");
            }
            catch (Exception exp)
            {
               return Result.Succeeded;
            }
            
            
        }


        public void MoveColumn(Document document)
        {

            string earthId = "54d88471-fd75-4915-9c1b-f310666605a7-00001353";

            Element e = document.GetElement(earthId);

            XYZ point1 = new XYZ(0, 0, 0);
            XYZ point2 = new XYZ(0, 0, 10);


            ElementTransformUtils.RotateElement(_doc, e.Id, Line.CreateBound(point1, point2), Math.PI / 182.5);
            
        }
    }
}
