#region Namespace
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

#endregion
namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateModelText : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
           
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                CreateModelTextMethod();
                TaskDialog.Show("У тебя получилось", "Complite!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }

        public void CreateModelTextMethod()
        {
            //string path = @"C:\text.txt";
            //string[] arStr = File.ReadAllLines(path);

            //UniqueId элемента
            string earthId = "c30ec930-30e2-4f3a-8f98-7767e782e4c8-0005496f";
            Element e = _doc.GetElement(earthId);

            XYZ point1 = new XYZ(0, -10, 0);

            //e.get_Parameter(BuiltInParameter.TEXT_TEXT).Set("123");

            //string f=e.get_Parameter(BuiltInParameter.TEXT_TEXT).AsString();

            //create copy element
            ICollection<ElementId> newElement = ElementTransformUtils.CopyElement(_doc, e.Id, point1);

            newElement.




            //TaskDialog.Show("1", f);


        }
        
    }

}