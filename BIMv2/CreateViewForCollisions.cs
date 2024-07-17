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
    public class CreateViewForCollisions : IExternalCommand

    {
        private Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            Document doc = commandData.Application.ActiveUIDocument.Document;
            List<int> ints = new List<int>();

            // get a ViewFamilyType for a 3D View
            ViewFamilyType viewFamilyType =
                (from v in new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                 where v.ViewFamily == ViewFamily.ThreeDimensional
                 select v).First();

            using (var t = new Transaction(doc))
            {

                t.Start("Create view for collisions");



                //XDocument xdoc = XDocument.Load(@"C:\Users\i.khisamiev\Downloads\V FS.xml");

                XmlDocument xmldoc = new XmlDocument();
                xmldoc.Load(@"C:\BIMv2_ClashView.xml");

                //XmlNode node = xmldoc.DocumentElement.SelectSingleNode("/name/value");

                string[] sometheng = xmldoc.InnerText.Split(' '); //объекта7579247ID

                for (int i = 1; i < sometheng.Length; i++)
                {
                    string stroka = sometheng[i];
                    stroka = stroka.Substring(7);
                    stroka = stroka.Substring(0, stroka.Length - 2);

                    try
                    {
                        int x = Convert.ToInt32(stroka);
                        ints.Add(x);
                    }

                    catch { }

                }

                var clearList = ints.Distinct();

                foreach (int x in clearList)
                {
                    try
                    {
                        ElementId elementId = new ElementId(x);

                        var elem=_doc.GetElement(elementId);

                        var par = elem.Category;

                        if (par != null)
                        {
                            BoundingBoxXYZ boundingBoxXYZ = new BoundingBoxXYZ();

                            // Create the 3d view
                            View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                            // Set the name of the view
                            view.Name = "Временны_" + x + " Проблемное место 3D вид";

                            //t.SetName("Create view " + view.Name);
                            // Create a new BoundingBoxXYZ to define a 3D rectangular space

                            boundingBoxXYZ.Min = elem.get_BoundingBox(_doc.ActiveView).Min;
                            boundingBoxXYZ.Max = elem.get_BoundingBox(_doc.ActiveView).Max;

                            // Apply this bouding box to the view's section box

                            view.SetSectionBox(boundingBoxXYZ);
                        }

                        //var bb = elem.Location

                        //if(bb != null)
                        //{
                        //    continue;
                        //}

                        //else
                        //{
                        //    break;
                        //}

                       


                        //view.CropBox = boundingBoxXYZ;



                        //_doc.Regenerate();
                    }

                    catch
                    {
                        continue;
                    }
                }

                TaskDialog.Show("2bim.ru", "3D виды с проблемными местами созданы");

                t.Commit();

                return Result.Succeeded;


            }

        }


    }
}