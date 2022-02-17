#region Namespaces

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using OperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;
#endregion

namespace BIMv2
{
    #region LightPickFilter
    public class LightPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element e)
        {
            return e.Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_LightingFixtures);
        }

        public bool AllowReference(Reference r, XYZ p)
        {
            return false;
        }
    }
    #endregion // LightPickFilter

    [Transaction(TransactionMode.Manual)]
    public class Test : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message,ElementSet elements)
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

            var daf= doc.GetWorksetTable();

            daf.SetActiveWorksetId(newWorkset.Id);

            return Result.Succeeded;
        }
    }
}