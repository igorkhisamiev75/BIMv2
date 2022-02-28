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
    class ConvertGrids3Dto2D : IExternalCommand
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

                ConvertTo2D(_doc, uiDoc);
                TaskDialog.Show("Готово", "Оси переведены в 2D");
                t.Commit();

            }


            return Result.Succeeded;
        }


        public void ConvertTo2D(Document doc, UIDocument uiDoc)
        {
            var grids = GetAllGridsElements(doc);

            List<bool> list = new List<bool>();


            foreach (var VARIABLE in grids)
            {
                Grid gr = VARIABLE as Grid;


                double grl1 = gr.Curve.GetEndPoint(0).X + gr.Curve.GetEndPoint(0).Y;
                double grl2 = gr.Curve.GetEndPoint(1).X + gr.Curve.GetEndPoint(1).Y;

                gr.SetDatumExtentType(DatumEnds.End0,doc.ActiveView,DatumExtentType.ViewSpecific);
                gr.SetDatumExtentType(DatumEnds.End1, doc.ActiveView, DatumExtentType.ViewSpecific);

                //show and hide bubble
                //if (!gr.IsBubbleVisibleInView(DatumEnds.End0, doc.ActiveView) && !gr.IsBubbleVisibleInView(DatumEnds.End1, doc.ActiveView))
                //{
                //    gr.ShowBubbleInView(DatumEnds.End0, doc.ActiveView);
                //    gr.ShowBubbleInView(DatumEnds.End1, doc.ActiveView);
                //}
                ////all ON then turn all OFF
                //else if (gr.IsBubbleVisibleInView(DatumEnds.End0, doc.ActiveView) && gr.IsBubbleVisibleInView(DatumEnds.End1, doc.ActiveView))
                //{
                //    gr.HideBubbleInView(DatumEnds.End0, doc.ActiveView);
                //    gr.HideBubbleInView(DatumEnds.End1, doc.ActiveView);
                //}
                ////if one is ON then Turn all ON.
                //else if ((gr.IsBubbleVisibleInView(DatumEnds.End0, doc.ActiveView) && !gr.IsBubbleVisibleInView(DatumEnds.End1, doc.ActiveView)) || ((!gr.IsBubbleVisibleInView(DatumEnds.End0, doc.ActiveView) && gr.IsBubbleVisibleInView(DatumEnds.End1, doc.ActiveView))))
                //{
                //    gr.ShowBubbleInView(DatumEnds.End0, doc.ActiveView);
                //    gr.ShowBubbleInView(DatumEnds.End1, doc.ActiveView);
                //}

            }

        }

        public IList<Element> GetAllGridsElements(Document document)
        {
            // List all Grids's
            var grid_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Grids)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = grid_collector.ToElements();
            return allFloors;
        }
    }

}
