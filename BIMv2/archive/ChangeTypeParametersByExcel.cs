#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]

    public class ChangeTypeParametersByExcel : IExternalCommand
    {
      
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            var _app = uiApp.Application;
            var _doc = uiDoc.Document;
         


            using (Transaction t = new Transaction(_doc))
            {
                t.Start("Edit Family");

                //get all walls
                IList<Element> allElements = GetAllElements(_doc);
                //TaskDialog.Show("2bim.ru", $"{allElements.Count}");

                //удаляем дубликаты
                allElements = allElements.GroupBy(x => x.Name).Select(x => x.First()).ToList();

                //TaskDialog.Show("2bim.ru", $"{allElements.Count}");

                //int i = 1;

                foreach (var element in allElements)
                {
                    FamilyInstance famInst = element as FamilyInstance;

                    famInst.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_MANUFACTURER).Set("rur");

                   
                }

                t.Commit();

                //TaskDialog.Show("2bim.ru", "Стены соеденены ☺☺☺");
                //_doc.Regenerate();


            }

            return Result.Succeeded;

        }


        public IList<Element> GetAllElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .WhereElementIsNotElementType();

            IList<Element> allWalls = floor_collector.ToElements();
            return allWalls;
        }


    }



}

