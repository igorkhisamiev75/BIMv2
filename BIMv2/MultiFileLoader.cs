using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMv2
{
    public class MultiFileLoader
    {
        public Result LoadMultipleFiles(ExternalCommandData commandData, List<string> filePaths)
        {
            UIApplication uiApp = commandData.Application;
            Application app = uiApp.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                using (Transaction trans = new Transaction(doc, "Load Multiple Files"))
                {
                    trans.Start();

                    foreach (string filePath in filePaths)
                    {
                        LoadRevitFileAsLink(doc, filePath);
                    }

                    trans.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Ошибка при загрузке файлов: {ex.Message}");
                return Result.Failed;
            }
        }

        private void LoadRevitFileAsLink(Document doc, string filePath)
        {
            try
            {
                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

                // Создаем опции для загрузки
                WorksetConfiguration worksetConfig = new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets);
                RevitLinkOptions options = new RevitLinkOptions(true, worksetConfig);

                // Загружаем связь - метод возвращает LinkLoadResult
                LinkLoadResult loadResult = RevitLinkType.Create(doc, modelPath, options);

                if (loadResult != null)
                {
                    // Получаем RevitLinkType из результата загрузки
                    RevitLinkType linkType = doc.GetElement(loadResult.ElementId) as RevitLinkType;

                    if (linkType != null)
                    {
                        // Создаем экземпляр связи используя Id
                        RevitLinkInstance linkInstance = RevitLinkInstance.Create(doc, linkType.Id);

                        // Получаем имя файла без расширения для рабочего набора
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                        string worksetName = "_связь_" + fileName;

                        // Создаем рабочий набор для связи
                        CreateWorksetForLink(doc, worksetName, linkInstance);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке файла {filePath}: {ex.Message}");
            }
        }

        private void CreateWorksetForLink(Document doc, string worksetName, RevitLinkInstance linkInstance)
        {
            // Получаем таблицу рабочих наборов
            WorksetTable worksetTable = doc.GetWorksetTable();

            // Проверяем, существует ли уже рабочий набор с таким именем
            FilteredWorksetCollector worksetCollector = new FilteredWorksetCollector(doc);
            Workset existingWorkset = worksetCollector.OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(w => w.Name.Equals(worksetName));

            WorksetId worksetId;

            if (existingWorkset == null)
            {
                // Создаем новый рабочий набор
                Workset newWorkset = Workset.Create(doc, worksetName);
                worksetId = newWorkset.Id;
            }
            else
            {
                worksetId = existingWorkset.Id;
            }

            // Перемещаем связь в созданный рабочий набор
            Parameter worksetParam = linkInstance.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
            if (worksetParam != null && !worksetParam.IsReadOnly)
            {
                worksetParam.Set(worksetId.IntegerValue);
            }
        }
    }

    // Внешняя команда для вызова функциональности
    [Transaction(TransactionMode.Manual)]
    public class LoadMultipleFilesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Диалог для выбора файлов
                var openFileDialog = new System.Windows.Forms.OpenFileDialog();
                openFileDialog.Filter = "Revit Files (*.rvt)|*.rvt";
                openFileDialog.Multiselect = true;
                openFileDialog.Title = "Выберите Revit файлы для загрузки";

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    List<string> selectedFiles = openFileDialog.FileNames.ToList();

                    MultiFileLoader loader = new MultiFileLoader();
                    return loader.LoadMultipleFiles(commandData, selectedFiles);
                }

                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}