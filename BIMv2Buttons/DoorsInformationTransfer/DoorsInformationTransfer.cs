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
                var allMaterials= GetAllMaterials(_doc);

                foreach (Element door in allDoors)
                {

                    string materСanvasInside = door.LookupParameter("Материал_Полотно изнутри").AsValueString(); //описание материала
                    string materСanvasOutside = door.LookupParameter("Материал_Полотно фасад").AsValueString();
                    string nalInside = door.LookupParameter("Материал_НаличникВнутренний").AsValueString();
                    string nalOutside = door.LookupParameter("Материал_НаличникНаружный").AsValueString();

                    door.LookupParameter("Полотно изнутри").Set(GetDescriptionInMaterial(materСanvasInside));
                    door.LookupParameter("Полотно фасад").Set(GetDescriptionInMaterial(materСanvasOutside));
                    door.LookupParameter("Наличник изнутри").Set(GetDescriptionInMaterial(nalInside));
                    door.LookupParameter("Наличник фасад").Set(GetDescriptionInMaterial(nalOutside));

                    ElementType typeDoor = _doc.GetElement(door.GetTypeId()) as ElementType;
                    var n1 = typeDoor.LookupParameter("1.1").AsInteger();
                    var n2 = typeDoor.LookupParameter("1.2").AsInteger();
                    var n3 = typeDoor.LookupParameter("2.1").AsInteger();
                    var n4 = typeDoor.LookupParameter("2.2").AsInteger();
                    var n5 = typeDoor.LookupParameter("Равные полотна").AsInteger();

                    var n5 = typeDoor.LookupParameter("Равные полотна").AsInteger();


                    string adsk_otkr = "";

                    if(n1 == 1)
                    {
                        adsk_otkr = "Правая";
                    }

                    if (n2 == 1)
                    {
                        adsk_otkr = "Левая";
                    }

                    if (n3 == 1 && n5!=1)
                    {
                        adsk_otkr = "Правая, две стврки";
                    }

                    if (n4 == 1 && n5 != 1)
                    {
                        adsk_otkr = "Левая, две стврки";
                    }

                    if (n5 == 1)
                    {
                        adsk_otkr = "Равные полотна";
                    }

                    door.LookupParameter("ADSK_Открывание").Set(adsk_otkr);

                    adsk_otkr = "";

                    //тип  полотна




                }

                TaskDialog.Show("Сделано!", $"посчитано для {allDoors.Count} дверей");

                t.Commit();

            }


            return Result.Succeeded;
        }

        public string GetDescriptionInMaterial(string nameMaterial)
        {
            var allMaterials = GetAllMaterials(_doc);
            string opisanie = "";

            foreach (var material in allMaterials)
            {
                if (material.Name == nameMaterial)
                {
                    opisanie = material.LookupParameter("Описание").AsString();
                }
            }

            return opisanie;
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
