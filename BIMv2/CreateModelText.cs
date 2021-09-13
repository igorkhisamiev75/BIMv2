#region Namespace
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
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

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
           
            _app = uiApp.Application;
            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;
                Reference r = uiDoc.Selection.PickObject(ObjectType.Element, "Выбрать текст модели");

                CreateModelTextMethod(r, uiDoc);
                TaskDialog.Show("Готово", "Complite!!!☺☺☺");
                t.Commit();

            }
            return Result.Succeeded;
        }

        public void CreateModelTextMethod(Reference r, UIDocument uiDocument)
        {
            //reed txt file
            string path = @"C:\text.txt";
            string[] arStr = File.ReadAllLines(path, Encoding.GetEncoding(1251));

            //UniqueId элемента
            //string earthId = "c30ec930-30e2-4f3a-8f98-7767e782e4c8-0005496f";


            //Element e = _doc.GetElement(earthId);

            // (1) select an object on a screen. (We'll come back to the selection in the UI Lab later.)
           
            // We have picked something.
            Element e = uiDocument.Document.GetElement(r);

            XYZ point1 = new XYZ(0, -10, 0);


            foreach (var s in arStr)
            {
                //create copy element
                e.get_Parameter(BuiltInParameter.TEXT_TEXT).Set(s);
                ICollection<ElementId> newElement = ElementTransformUtils.CopyElement(_doc, e.Id, point1);

                e = _doc.GetElement(new ElementId(GetId(newElement)));

            }

            

           

            //e.get_Parameter(BuiltInParameter.TEXT_TEXT).Set("123");

            //string f=e.get_Parameter(BuiltInParameter.TEXT_TEXT).AsString();

            

           //string g= newElement.GetEnumerator().Current.IntegerValue.ToString();

           //int g = GetId(newElement);

           //Element gg = _doc.GetElement(new ElementId(g));

           //gg.get_Parameter(BuiltInParameter.TEXT_TEXT).Set("123");

           // ICollection<ElementId> newElement2 = ElementTransformUtils.CopyElement(_doc, gg.Id, point1);

            //TaskDialog.Show("1", g.ToString());


        }

        public int GetId(ICollection<ElementId> newElement)
        {
            int a=0;

            foreach (var VARIABLE in newElement)
            {
                a=VARIABLE.IntegerValue;
            }

            return a;
        }
        
    }

}