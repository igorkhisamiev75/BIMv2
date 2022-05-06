using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateRailingInRooms : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;



            using (var t = new Transaction(doc))
            {
                t.Start("Записываем суммарную площадь отделки");

                CreateRailing(doc, null);

                //TaskDialog.Show("Сделано", "Стены ОТДЕЛКА посчитаны");

                t.Commit();

            }
            return Result.Succeeded;

        }

        private void CreateRailing(Document document, Stairs stairs)
        {
            

                // Delete existing railings
                ICollection<ElementId> railingIds = stairs.GetAssociatedRailings();

                foreach (ElementId railingId in railingIds)
                {
                    document.Delete(railingId);
                }
                // Find RailingType
                FilteredElementCollector collector = new FilteredElementCollector(document);
                ICollection<ElementId> RailingTypeIds = collector.OfClass(typeof(RailingType)).ToElementIds();
                Railing.Create(document, stairs.Id, RailingTypeIds.First(), RailingPlacementPosition.Treads);
                
        }



    }
}
