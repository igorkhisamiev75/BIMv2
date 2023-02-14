using System;
using System.Collections;
using System.Collections.Generic;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Application = Autodesk.Revit.ApplicationServices.Application;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class DoorsInformationTransfer : IExternalCommand
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

                var allDoors = GetAllDoorElements(_doc);

                foreach (var door in allDoors)
                {
                    var str=door.LookupParameter("Материал_Полотно изнутри").AsValueString();

                    door.LookupParameter("Полотно изнутри").Set(str);
                }

                //откуда
                //Материал_Полотно изнутри
                //Материал_Полотно фасад
                //Материал_НаличникВнутренний
                //Материал_НаличникНаружный

                //куда
                //Наличник изнутри
                //Наличник фасад
                //Полотно изнутри
                //Полотно фасад

                //
                //Тип полотна

                //Две створки Правая
                //ADSK_Открывание


                TaskDialog.Show("Сделано!", $"посчитано для {allDoors.Count} дверей");


                t.Commit();

            }


            return Result.Succeeded;
        }

        public IList<Element> GetAllDoorElements(Document document)
        {
            // List all Door's
            var collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType();

            IList<Element> elements = collector.ToElements();
            return elements;
        }

        public IList<Element> GetAllMaterials(Document document)
        {
            // List all Door's
            var collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Materials)
                .WhereElementIsNotElementType();

            IList<Element> elements = collector.ToElements();
            return elements;
        }


    }

}
