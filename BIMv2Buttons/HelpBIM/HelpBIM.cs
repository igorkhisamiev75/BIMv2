#region Namespace

using System;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HelpBIM : IExternalCommand

    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            try
            {
                System.Diagnostics.Process.Start(
                    "https://chat.whatsapp.com/KHpWyVsfYQE3fVIdBS2XDe?text=У%20меня%20проблемы%20с%20Revit,%20_помоги!");
                return Result.Succeeded;
            }

            catch (Exception ex)
            {
                return Result.Failed;
            }

        }
    }
}