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
    public class BIMStandart : IExternalCommand

    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            try
            {
                System.Diagnostics.Process.Start(
                    "https://docs.google.com/document/d/1RsZPgiTs4nRGBuE5QLrPcAl8irX_CnGdLxYQ_pAfrrI/edit?usp=sharing");
                return Result.Succeeded;
            }

            catch (Exception ex)
            {
                return Result.Failed;
            }

        }
    }
}