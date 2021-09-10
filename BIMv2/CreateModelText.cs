#region Namespace
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion
namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateModelText : IExternalCommand
    {
        private Application _app;
        private Document _doc;
        public ElementId linkDocId = new ElementId(BuiltInCategory.OST_RvtLinks);
        public List<ElementId> roomids = new List<ElementId>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("");

                ModelText sdfv = new ModelText();
                
                CreateModelTextMethod();
                TaskDialog.Show("У тебя получилось", "Complite!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }

        public void CreateModelTextMethod()
        {
            string path = @"C:\text.txt";
            string[] arStr = File.ReadAllLines(path);
            
           
           
        }
        
    }

}