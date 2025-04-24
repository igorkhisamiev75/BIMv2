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
using Autodesk.Revit.DB.Structure;

using System.Xml.Linq;
using System.Linq;
using Microsoft.Office.Interop.Excel;
using System.Globalization;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateClashElements : IExternalCommand

    {
        private Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            Document doc = commandData.Application.ActiveUIDocument.Document;
            //List<int> ints = new List<int>();

            List < pointAndNameClash > nameClashes= new List<pointAndNameClash>();

           

            FamilySymbol symbol = GetSymbol(_doc, "Для подписи пересечений", "Для подписи пересечений");

            // get a ViewFamilyType for a 3D View
            ViewFamilyType viewFamilyType =
                (from v in new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                 where v.ViewFamily == ViewFamily.ThreeDimensional
                 select v).First();

            using (var t = new Transaction(doc))
            {

                t.Start("Create view for collisions");


                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(@"C:\Users\i.khisamiev\Downloads\ОВ2 ВК подвал.xml");
                // получим корневой элемент
                XmlElement xRoot = xDoc.DocumentElement;

                XDocument doci = XDocument.Load(@"C:\Users\i.khisamiev\Downloads\ОВ2 ВК подвал.xml");

                IFormatProvider formatter = new NumberFormatInfo { NumberDecimalSeparator = "." };

                // Then parse like this:
                var clashTests = doci.Descendants("clashtest")
                    .Select(t1 => new ClashTest
                    {
                        Name = t1.Attribute("name")?.Value,
                        TestType = t1.Attribute("test_type")?.Value,
                        Status = t1.Attribute("status")?.Value,
                        Results = t1.Descendants("clashresult")
                            .Select(r => new ClashResult
                            {
                                Name = r.Attribute("name")?.Value,
                                Guid = r.Attribute("guid")?.Value,
                                X = double.Parse(r.Descendants("pos3f").FirstOrDefault()?.Attribute("x")?.Value, formatter) ,
                                Y = double.Parse(r.Descendants("pos3f").FirstOrDefault()?.Attribute("y")?.Value, formatter) ,
                                Z = double.Parse(r.Descendants("pos3f").FirstOrDefault()?.Attribute("z")?.Value, formatter) 
                            })
                            .ToList()
                    })
                    .ToList();


                TaskDialog.Show("'k", $"{clashTests[0].Results.Count }");

                foreach ( var clash in clashTests[0].Results)
                {
                    XYZ pointRoom = new XYZ(clash.X, clash.Y, clash.Y);
                   string name = clash.Name;

                    FamilyInstance familyInstance = _doc.Create.NewFamilyInstance(
                       pointRoom, symbol, StructuralType.NonStructural);
                    familyInstance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(name);

                }






                //XmlNode node = xmldoc.DocumentElement.SelectSingleNode("/name/value");

                //получить координаты и имя конфликта

                //    string[] sometheng = xmldoc.InnerText.Split(' '); //объекта7579247ID

                //for (int i = 1; i < sometheng.Length; i++)
                //{
                //    string stroka = sometheng[i];
                //    stroka = stroka.Substring(7);
                //    stroka = stroka.Substring(0, stroka.Length - 2);

                //    try
                //    {
                //        int x = Convert.ToInt32(stroka);
                //        ints.Add(x);
                //    }

                //    catch { }

                //}

                //var clearList = ints.Distinct();

                //foreach (int x in clearList)
                //{
                //    try
                //    {
                //        ElementId elementId = new ElementId(x);

                //        var elem = _doc.GetElement(elementId);

                //        var par = elem.Category;

                //        if (par != null)
                //        {
                //            BoundingBoxXYZ boundingBoxXYZ = new BoundingBoxXYZ();

                //            // Create the 3d view
                //            View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                //            // Set the name of the view
                //            view.Name = "Временны_" + x + " Проблемное место 3D вид";

                //            //t.SetName("Create view " + view.Name);
                //            // Create a new BoundingBoxXYZ to define a 3D rectangular space

                //            boundingBoxXYZ.Min = elem.get_BoundingBox(_doc.ActiveView).Min;
                //            boundingBoxXYZ.Max = elem.get_BoundingBox(_doc.ActiveView).Max;

                //            // Apply this bouding box to the view's section box

                //            view.SetSectionBox(boundingBoxXYZ);
                //        }

                //        //var bb = elem.Location

                //        //if(bb != null)
                //        //{
                //        //    continue;
                //        //}

                //        //else
                //        //{
                //        //    break;
                //        //}




                //        //view.CropBox = boundingBoxXYZ;



                //        //_doc.Regenerate();
                //    }

                //    catch
                //    {
                //        continue;
                //    }
                //}

                //размещаем элемени



                //TaskDialog.Show("2bim.ru", "Проблемные места расставлены");

                t.Commit();

                return Result.Succeeded;


            }

        }

        public FamilySymbol GetSymbol(Document document, string familyName, string symbolName)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(Family)).OfType<Family>()
                .FirstOrDefault(f => f.Name.Equals(familyName))?.GetFamilySymbolIds()
                .Select(id => document.GetElement(id)).OfType<FamilySymbol>().FirstOrDefault(symbol => symbol.Name.Equals(symbolName));
        }


    }

    class pointAndNameClash
    {
        public string clashName { get; set; }
        public double xPoint { get; set; }
        public double yPoint { get; set; }
        public double zPoint { get; set; }
               
        
    }


    public class ClashTest
    {
        public string Name { get; set; }
        public string TestType { get; set; }
        public string Status { get; set; }
        public List<ClashResult> Results { get; set; } = new List<ClashResult>();
    }

    public class ClashResult
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}