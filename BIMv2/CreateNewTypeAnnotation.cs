using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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


                List<string> names = new List<string>();    

                foreach (ElementType type in flagokTypes)
                { 
                    names.Add(type.Name);
                }


                //цикл для переноса параметров
                foreach (info n in listNameAndDescription)
                {
                    

                    foreach (ElementType type in flagokTypes)
                    {
                        
                        if (type.Name == n.Name)
                        {
                            //число строк

                            int counutLine = n.countLine();

                            //максимальная длина строки 

                            double maxCountLine = n.maxLenghtLine();

                            double maxDoubble = maxCountLine / 132;

                            //TaskDialog.Show("Сделано", $"{maxCountLine} - число строк ");

                            //int count = sostav.Where(x => "\n".IndexOf(x) != -1).Count();

                            var gg=n.massLines;


                            type.LookupParameter("Число строк").Set(counutLine);

                            type.LookupParameter("Ширина").Set(maxDoubble);

                            for(int i = 1; i <= counutLine; i++)
                            {
                                string nameParam = "";

                                if (counutLine <= 9)
                                {
                                    nameParam = "0" + i + "_Текст";
                                }

                                if (counutLine > 9)
                                {
                                    nameParam = i + "_Текст";
                                }

                                type.LookupParameter(nameParam).Set(n.massLines[i-1]);

                            }

                            
                        }
                        else
                        {
                            if(!names.Contains(n.Name))

                                try
                                {

                                    FamilySymbol newFamSym = neededSymbol.Duplicate(n.Name) as FamilySymbol;
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

        public List<string> massLines 
        {

            get { return MassLines(); }

            set { }
        
        }

        public int countLine () //сколько слоев
        {
            string[] mass = Description.Split('\n');

            return mass.Length;
        }

        public double maxLenghtLine() //максимальная длина строки
        {
            double maxCountLine = 0;

            List<string> mass = Description.Split('\n').ToList();

            foreach(string line in mass)
            {
                double strLenght =line.Length;

                if (strLenght > maxCountLine)
                {
                    maxCountLine = strLenght;
                }

            }
            
            return maxCountLine;
        }

        public List<string> MassLines()
        {
            List<string> newString = new List<string>();

            List<string> mass = Description.Split('\n').ToList(); //разделение описания на отдельные строки

            foreach (string item in mass) //заходим в строку
            {

                string[] words = item.Split(' '); //строка разбита на слова

                int n = words.Length; //количество слов

                string newStr = "";

                if (words[n - 1] == "мм\r" || words[n - 1] == "мм") //если в строке есть мм то удаляем 3 слова - 350 мм
                {

                    for(int i = 0; i < n-3; i++) //del 3 words
                    {
                        newStr += words[i]+" ";
                    }


                }

                else
                {
                    newStr = item.Replace("\n", ""); 
                    
                }

                newString.Add(newStr);

            }

            

            return newString;
        }

        public info(string Name, string Description)
        {
            this.Name= Name;
            this.Description= Description;   
        }




    }

    
}
