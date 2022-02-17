#region Namespaces

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using OperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;
#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class CreateWorksets : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            doc.EnableWorksharing("00_Общие уровни и оси", "00_Рабочий набор 1");

            Workset newWorkset = null;
            // Worksets can only be created in a document with worksharing enabled
            if (doc.IsWorkshared)
            {
                string worksetName = "New Workset";
                // Workset name must not be in use by another workset
                if (WorksetTable.IsWorksetNameUnique(doc, worksetName))
                {
                    using (Transaction worksetTransaction = new Transaction(doc, "Set preview view id"))
                    {
                        worksetTransaction.Start();
                        newWorkset = Workset.Create(doc, worksetName);
                        worksetTransaction.Commit();
                    }
                }
            }

            var daf = doc.GetWorksetTable();

            daf.SetActiveWorksetId(newWorkset.Id);

            return Result.Succeeded;
        }
    }
}