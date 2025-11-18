//#region Namespace
//using System;
//using System.Collections.Generic;
//using System.Xml;
//using System.Linq;

//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;
//using System.Xml.Linq;

//#endregion

//namespace BIMv2
//{
//    [Transaction(TransactionMode.Manual)]
//    [Regeneration(RegenerationOption.Manual)]
//    public class DeletingCollisions : IExternalCommand

//    {
//        private Document _doc;
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

//        {

//            var uiApp = commandData.Application;
//            var uiDoc = uiApp.ActiveUIDocument;

//            _doc = uiDoc.Document;

//            Document doc = commandData.Application.ActiveUIDocument.Document;
//            List<int> ints = new List<int>();

//            string pathToFile = @"C:\Users\comp44\Desktop\V FS.xml";

            

//            using (var t = new Transaction(doc))
//            {

//                t.Start("Del collisions");

//                //XDocument xdoc = XDocument.Load(@"C:\Users\i.khisamiev\Downloads\V FS.xml");

//                XmlDocument xmldoc = new XmlDocument();
//                xmldoc.Load(@"C:\Users\i.khisamiev\Downloads\V ES.xml");

//                //XmlNode node = xmldoc.DocumentElement.SelectSingleNode("/name/value");

//                string[] sometheng = xmldoc.InnerText.Split(' '); //объекта7579247ID

//                for (int i = 1; i < sometheng.Length; i++)
//                {
//                    string stroka = sometheng[i];
//                    stroka = stroka.Substring(7);
//                    stroka = stroka.Substring(0, stroka.Length - 2);

//                    try
//                    {
//                        int x = Convert.ToInt32(stroka);
//                        ints.Add(x);
//                    }

//                    catch { }
                    
//                }

//                var clearList = ints.Distinct();

//                foreach (int x in clearList)
//                {
//                    try
//                    {
//                        ElementId elementId = new ElementId(x);

//                        _doc.Delete(elementId);
//                        _doc.Regenerate();
//                    }

//                    catch {
//                        continue;
//                    }   
//                }

//                TaskDialog.Show("Почищено", "Почишено");

//                t.Commit();
                
//                return Result.Succeeded;


//            }

//        }


//    }
//}