using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Application = Autodesk.Revit.ApplicationServices.Application;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class FindDoorsMirror : IExternalCommand
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
                t.Start("Поиск отзеркаленных");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                var allDoors = GetAllDoorElements(_doc);
                int i = 0;

                var allWindows = GetAllWindowsElements(_doc);

                foreach (var door in allDoors)
                {
                    try
                    {
                        FamilyInstance doorFamilyInstance = door as FamilyInstance;

                        bool mirFI = doorFamilyInstance.Mirrored;


                        if (mirFI == true)
                        {
                            i++;

                            door.LookupParameter("BIM_Зeркальный").Set(1);

                        }

                        else
                        {
                            door.LookupParameter("BIM_Зeркальный").Set(0);
                        }

                    }

                    catch { }


                }

                foreach (var door in allWindows)
                {
                    try
                    {
                        FamilyInstance doorFamilyInstance = door as FamilyInstance;

                        bool mirFI = doorFamilyInstance.Mirrored;


                        if (mirFI == true)
                        {
                            i++;

                            door.LookupParameter("BIM_Зeркальный").Set(1);

                        }

                        else
                        {
                            door.LookupParameter("BIM_Зeркальный").Set(0);
                        }

                    }

                    catch { }


                }

                TaskDialog.Show("Сделано!", $"Информация перенесена для {i} отзеркаленных дверей и окон");

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

        public IList<Element> GetAllWindowsElements(Document document)
        {
            // List all Door's
            var collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType();

            IList<Element> elements = collector.ToElements();
            return elements;
        }



    }

}
