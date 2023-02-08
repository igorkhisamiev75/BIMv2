using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;

using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateNewTypeAnnotation : IExternalCommand
    {
        public List<Wall> AllFinishWall = new List<Wall>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            //Application app = commandData.Application;


            using (var t = new Transaction(doc))
            {
                t.Start("Создаем флажки");

                List<info> listNameAndDescription = new List<info>();
                FamilySymbol neededSymbol = null;

                //get all type floors

                FilteredElementCollector filteredElementCollector = new FilteredElementCollector(doc);
                filteredElementCollector.OfClass(typeof(FloorType));
                List<FloorType> floorTypeCollection = filteredElementCollector.Cast<FloorType>().ToList();

                foreach (var floorType in floorTypeCollection)
                {
                    try
                    {
                        var sostav = floorType.get_Parameter(new Guid("59c583bf-3ce9-41f1-9f7b-7c0fbe14d11d")).AsString();
                        if (sostav != null)
                        {
                            listNameAndDescription.Add(new info(floorType.Name, sostav));
                        }
                    }

                    catch
                    {
                        continue;
                    }
                    
                }

                //пробежаться и составить только с составом

                //get all type roofs 

                FilteredElementCollector filteredElementCollector2 = new FilteredElementCollector(doc);
                filteredElementCollector2.OfClass(typeof(RoofType));
                List<RoofType> roofTypeCollection = filteredElementCollector2.Cast<RoofType>().ToList();

                foreach (var roofType in roofTypeCollection)
                {
                    try
                    {
                        var sostav = roofType.get_Parameter(new Guid("59c583bf-3ce9-41f1-9f7b-7c0fbe14d11d")).AsString();
                    if (sostav != null)
                    {
                        listNameAndDescription.Add(new info(roofType.Name, sostav));
                    }
                    }

                    catch
                    {
                        continue;
                    }
                }


                IEnumerable<ElementType> flagokTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .Cast<ElementType>()
                .Where(x => x.FamilyName.Contains("Флажок"));

                

                var families = new FilteredElementCollector(doc).OfClass(typeof(Family)).ToList();
                foreach (Family family in families)
                {
                    foreach (ElementType type in flagokTypes)
                    {

                        string nameFlagok = type.FamilyName;
                        if(!family.Name.Equals(nameFlagok)) continue;

                        var symbolsId = family.GetFamilySymbolIds();


                        foreach (var symbolId in symbolsId)
                        {

                            var symb = doc.GetElement(symbolId);
                            //TaskDialog.Show("Сделано", $"{symb.Name}  ");
                            //if (!symb.Name.Equals(nameFlagok)) continue;
                            neededSymbol = symb as FamilySymbol;
                        }
                    }

                        
                }



                foreach (ElementType type in flagokTypes)
                {
                    
                    string nameFlagok = type.Name;
                    //string famName = type.FamilyName;

                    //TaskDialog.Show("Сделано", $"{id}  ");

                    foreach (var n in listNameAndDescription)
                    {
                        string nameType = n.Name; //имя типа перекрытия или
                        string sostav = n.Description;




                        if (nameFlagok == nameType)
                        {
                            //число строк

                            int counutLine = n.countLine();

                            //TaskDialog.Show("Сделано", $"{c} - строк ");

                            int count = sostav.Where(x => "\n".IndexOf(x) != -1).Count();

                            if(count == 0)
                            {
                                type.LookupParameter("Число строк").Set(1);
                                type.LookupParameter("01_Текст").Set(sostav);
                                type.LookupParameter("02_Текст").Set("");
                                type.LookupParameter("03_Текст").Set("");

                            }

                            else
                            {
                                type.LookupParameter("Число строк").Set(count+1);
                            }

                        }

                        else
                        {
                            
                            try
                            {
                                 
                                FamilySymbol newFamSym = neededSymbol.Duplicate(nameType) as FamilySymbol;
                                //t.Commit();

                                continue;

                            }

                            catch
                            {
                                continue;
                            }
                       

                        }

                    }

                  
                }

                //TaskDialog.Show("Сделано", $"{listNameAndDescription.Count}  ");


                t.Commit();

            }
            return Result.Succeeded;

        }
       

    }

    class info
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public int countLine ()
        {
            string[] mass = Description.Split('\n');

            return mass.Length;
        }

        public int maxLenghtLine(string Descrtiption)
        {

            int count = 0;

            //for (int i = 0; i < mass.Length; i++)
            //{

            //}


            return count;
        }

        public info(string Name, string Description)
        {
            this.Name= Name;
            this.Description= Description;   
        }




    }

    
}
