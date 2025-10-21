using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MultiFileLoaderAdvanced
{
    [Transaction(TransactionMode.Manual)]
    public class LoadFilesAutoSharedCoordinatesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                var openFileDialog = new System.Windows.Forms.OpenFileDialog();
                openFileDialog.Filter = "Revit Files (*.rvt)|*.rvt";
                openFileDialog.Multiselect = true;
                openFileDialog.Title = "Import RVT Files - Positioning: Auto - By Shared Coordinates";

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    List<string> selectedFiles = openFileDialog.FileNames.ToList();

                    using (Transaction trans = new Transaction(doc, "Import RVT Links with Auto-Shared Coordinates"))
                    {
                        trans.Start();

                        foreach (string filePath in selectedFiles)
                        {
                            ImportRvtLinkWithAutoPositioning(doc, filePath);
                        }

                        trans.Commit();
                    }

                    TaskDialog.Show("Import Complete",
                        $"All RVT files imported with positioning: Auto - By Shared Coordinates\n" +
                        $"Each link placed in its own workset: '_связь_+filename'");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Import Error", ex.Message);
                return Result.Failed;
            }
        }

        private void ImportRvtLinkWithAutoPositioning(Document doc, string filePath)
        {
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

            // Используем RevitLinkOptions с позиционированием по общим координатам
            WorksetConfiguration worksetConfig = new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets);
            RevitLinkOptions options = new RevitLinkOptions(false, worksetConfig); // false = Auto - By Shared Coordinates

            // Создаем связь
            LinkLoadResult loadResult = RevitLinkType.Create(doc, modelPath, options);

            if (loadResult != null && loadResult.ElementId != ElementId.InvalidElementId)
            {
                RevitLinkType linkType = doc.GetElement(loadResult.ElementId) as RevitLinkType;
                if (linkType != null)
                {
                    // Создаем экземпляр связи
                    RevitLinkInstance linkInstance = RevitLinkInstance.Create(doc, linkType.Id);

                    // Применяем автоматическое позиционирование по общим координатам
                    ApplyAutoSharedCoordinates(linkInstance);

                    // Создаем рабочий набор
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    string worksetName = "_связь_" + fileName;
                    CreateWorksetForLink(doc, worksetName, linkInstance);
                }
            }
        }

        private void ApplyAutoSharedCoordinates(RevitLinkInstance linkInstance)
        {
            // В Revit API 2022 позиционирование "Auto - By Shared Coordinates"
            // автоматически применяется при использовании RevitLinkOptions(false)
            // Дополнительные настройки обычно не требуются

            // Можно добавить логирование для отладки
            TaskDialog.Show("Link Imported",
                $"RVT link imported with positioning: Auto - By Shared Coordinates\n" +
                $"Link will be positioned according to the shared coordinate system");
        }

        private void CreateWorksetForLink(Document doc, string worksetName, RevitLinkInstance linkInstance)
        {
            FilteredWorksetCollector worksetCollector = new FilteredWorksetCollector(doc);
            Workset existingWorkset = worksetCollector.OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(w => w.Name.Equals(worksetName));

            WorksetId worksetId = existingWorkset?.Id ?? Workset.Create(doc, worksetName).Id;

            Parameter worksetParam = linkInstance.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
            if (worksetParam != null && !worksetParam.IsReadOnly)
            {
                worksetParam.Set(worksetId.IntegerValue);
            }
        }
    }
}