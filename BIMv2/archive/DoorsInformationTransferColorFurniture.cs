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
    class DoorsInformationTransferColorFurniture : IExternalCommand
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
                        var bimColorFurnit = door.LookupParameter("BIM_Цвет фурнитуры").AsElementId();
                       


                        //фурнитура

                        var BIM_DovodColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Доводчик_Цвет").AsString();
                        var BIM_PetlColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Петли_Цвет").AsString();
                        var BIM_RuchkiColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Ручки_Цвет").AsString();
                        var BIM_RuchkiMat = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Ручки_Материал").AsElementId();
                        var BIM_ZamokColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Замок_Цвет").AsString();
                        var BIM_PorogColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Порог_Цвет").AsString();

                        door.LookupParameter("BIM_Доводчик_Цвет").Set(BIM_DovodColor);
                        door.LookupParameter("BIM_Петли_Цвет").Set(BIM_PetlColor);
                        door.LookupParameter("BIM_Ручки_Цвет").Set(BIM_RuchkiColor);
                        door.LookupParameter("BIM_Ручки_Материал").Set(BIM_RuchkiMat);
                        door.LookupParameter("BIM_Замок_Цвет").Set(BIM_ZamokColor);
                        door.LookupParameter("BIM_Порог_Цвет").Set(BIM_PorogColor);

                      


                    }

                    catch { }





                }

                TaskDialog.Show("Сделано!", $"Информация перенесена для {allDoors.Count} дверей");

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
