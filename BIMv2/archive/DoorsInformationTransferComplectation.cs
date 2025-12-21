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
    class DoorsInformationTransferComplectation : IExternalCommand
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
                

                foreach (Element door in allDoors)
                {
                    try
                    {
                        var bimComplictazia = door.LookupParameter("BIM_Комплектация").AsElementId();

                        var BIM_Dovod = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Доводчик").AsString();
                        var BIM_Petl = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Петли").AsString();
                        var BIM_Porog = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Порог").AsString();
                        var BIM_Ruchki = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Ручки").AsString();
                        var BIM_Zamok = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Замок").AsString();
                        var BIM_Skud = _doc.GetElement(bimComplictazia).LookupParameter("BIM_СКУД").AsString();
                        var BIM_SOS = _doc.GetElement(bimComplictazia).LookupParameter("BIM_СОС").AsString();
                        var BIM_Otboinik = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Отбойник").AsString();

                        door.LookupParameter("BIM_Доводчик").Set(BIM_Dovod);
                        door.LookupParameter("BIM_Петли").Set(BIM_Petl);
                        door.LookupParameter("BIM_Порог").Set(BIM_Porog);
                        door.LookupParameter("BIM_Ручки").Set(BIM_Ruchki);
                        door.LookupParameter("BIM_Замок").Set(BIM_Zamok);
                        door.LookupParameter("BIM_СКУД").Set(BIM_Skud);
                        door.LookupParameter("BIM_СОС").Set(BIM_SOS);
                        door.LookupParameter("BIM_Отбойник").Set(BIM_Otboinik);


                    }

                    catch { }

                }

                TaskDialog.Show("Сделано!", $"BIM_Комплектация перенесена для {allDoors.Count} дверей");

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


    }

}
