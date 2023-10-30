#region Namespace
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DeletingCollisions : IExternalCommand

    {
        private Document _doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            Document doc = commandData.Application.ActiveUIDocument.Document;
            IList<Element> roomsCurrentProject = GetAllRooms(doc);
            IList<Element> roomsLinked = new List<Element>();


            using (var t = new Transaction(doc))
            {

                t.Start("Copy Rooms");

                TaskDialog.Show("2bim.ru", "Выбрать модель для копирования помещений");

                Reference r = uiDoc.Selection.PickObject(ObjectType.Element, "Выбрать модель с помещениями");
                Element elem = uiDoc.Document.GetElement(r);

                //открываем XML находим ID для удаления и в массив
                //убираем дубли
                //пробегаемся по каждому и удаляем

                //TaskDialog.Show("2bim.ru", $"{elem.Name}");


                //получить связанную модель FCD

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();


                //IList<Element> roomsLinked = GetAllRooms(elem);

                foreach (RevitLinkInstance linkedInstance in linkedInstances)
                {

                    if (linkedInstances != null)
                    {
                        foreach (var linkInstance in linkedInstances)
                        {
                            Document linkDocument = linkInstance.GetLinkDocument();
                            roomsLinked = GetAllRooms(linkDocument);

                            TaskDialog.Show("2bim.ru", $"{roomsLinked.Count}");

                        }
                    }
                }

                try
                {
                    // del rooms

                    int k = 0;

                    foreach (var rooms in roomsLinked)
                    {
                        //doc.Create
                    }

                    t.Commit();
                    return Result.Succeeded;

                }



                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", ex.Message);
                    return Result.Failed;
                }

            }

        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }

        public IList<Element> GetAllRooms(Document doc)
        {
            // List all Room's
            var room_collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();
            IList<ElementId> room_eids = room_collector.ToElementIds() as IList<ElementId>;
            var allRooms = room_collector.ToElements();

            return allRooms;
        }

    }
}