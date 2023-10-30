#region Namespace
using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Xml.Linq;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DeletingCollisions : IExternalCommand

    {
        private Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            Document doc = commandData.Application.ActiveUIDocument.Document;
            List<int> ints = new List<int>();

            string pathToFile = @"C:\Users\comp44\Desktop\V FS.xml";

            

            using (var t = new Transaction(doc))
            {

                t.Start("Del collisions");

                XDocument xdoc = XDocument.Load(@"C:\Users\comp44\Desktop\V FS.xml");

                // получаем корневой узел
                //var col2=xdoc.
                var col = xdoc.Elements("clashresult");

                if (col != null)
                {
                    // проходим по всем элементам 
                    foreach (XElement collisium in col.Elements("clashobject"))
                    {

                        var value = collisium.Attribute("value");

                        ints.Add(Convert.ToInt32(value));


                    }
                }


                TaskDialog.Show("2bim.ru", $"{ints.Count}");

               
                //открываем XML находим ID для удаления и в массив
                //убираем дубли
                //пробегаемся по каждому и удаляем

                //TaskDialog.Show("2bim.ru", $"{elem.Name}");

                try
                {
                   
                    t.Commit();
                    return Result.Succeeded;

                }



                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", ex.Message);
                    return Result.Failed;
                }

            }

        }

        

        

    }
}