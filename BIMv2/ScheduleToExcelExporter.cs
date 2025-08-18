using System;
using System.IO;
using System.Runtime.InteropServices;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Excel = Microsoft.Office.Interop.Excel;

namespace BIMv2
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class ExportScheduleToExcel : IExternalCommand
    {
        public void Export(ViewSchedule schedule, string filePath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                // Initialize Excel
                excelApp = new Excel.Application();
                excelApp.Visible = true;
                workbook = excelApp.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.Sheets[1];

                // Get schedule data
                TableData tableData = schedule.GetTableData();
                TableSectionData body = tableData.GetSectionData(SectionType.Body);

                int rowCount = body.NumberOfRows;
                int colCount = body.NumberOfColumns;

                // Write headers
                for (int col = 0; col < colCount; col++)
                {
                    string header = schedule.GetCellText(SectionType.Body, 0, col);
                    worksheet.Cells[1, col + 1] = header;
                    ((Excel.Range)worksheet.Cells[1, col + 1]).Font.Bold = true;
                }

                // Write data
                for (int row = 1; row < rowCount; row++)
                {
                    for (int col = 0; col < colCount; col++)
                    {
                        string cellValue = schedule.GetCellText(SectionType.Body, row, col);
                        worksheet.Cells[row + 1, col + 1] = cellValue;
                    }
                }

                // Auto-fit columns for better readability
                worksheet.Columns.AutoFit();

                // Save file
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                workbook.SaveAs(filePath);
                workbook.Close(true);
                excelApp.Quit();
            }
            finally
            {
                // Clean up COM objects
                if (worksheet != null) Marshal.ReleaseComObject(worksheet);
                if (workbook != null) Marshal.ReleaseComObject(workbook);
                if (excelApp != null) Marshal.ReleaseComObject(excelApp);
            }
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            View currentView = doc.ActiveView;

            if (!(currentView is ViewSchedule))
            {
                message = "Active view is not a schedule";
                return Result.Failed;
            }

            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, $"{currentView.Name}.xlsx");

                Export((ViewSchedule)currentView, filePath);

                TaskDialog.Show("Export Successful", $"Schedule exported to:\n{filePath}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error: {ex.Message}\n{ex.StackTrace}";
                TaskDialog.Show("Export Failed", message);
                return Result.Failed;
            }
        }
    }
}