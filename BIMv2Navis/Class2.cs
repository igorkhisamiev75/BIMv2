using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Automation;
using Autodesk.Navisworks.Api.Plugins;

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace RVTtoNWDConverter
{
    public class RVTtoNWDConverterAutomation : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                // Выбор RVT файлов
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Revit Files (*.rvt)|*.rvt",
                    Title = "Выберите RVT файлы для конвертации в NWD",
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return 0;

                // Выбор папки для сохранения
                FolderBrowserDialog folderDialog = new FolderBrowserDialog
                {
                    Description = "Выберите папку для сохранения NWD файлов",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != DialogResult.OK)
                    return 0;

                string outputFolder = folderDialog.SelectedPath;
                List<string> convertedFiles = new List<string>();
                List<string> failedFiles = new List<string>();

                // Используем Automation для создания нового экземпляра Navisworks
                NavisworksApplication automationApp = null;

                try
                {
                    // Создаем новый экземпляр Navisworks
                    automationApp = new NavisworksApplication();
                    automationApp.Visible = false; // Скрываем окно

                    foreach (string rvtFilePath in openFileDialog.FileNames)
                    {
                        try
                        {
                            // Открываем файл в новом документе
                            automationApp.OpenFile(rvtFilePath);

                            // Сохраняем как NWD
                            string fileName = Path.GetFileNameWithoutExtension(rvtFilePath);
                            string nwdFilePath = Path.Combine(outputFolder, fileName + ".nwd");

                            automationApp.SaveFile(nwdFilePath);
                            convertedFiles.Add(Path.GetFileName(rvtFilePath));

                            // Закрываем текущий документ
                           // automationApp.OpenFile
                        }
                        catch (Exception ex)
                        {
                            failedFiles.Add($"{Path.GetFileName(rvtFilePath)}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    // Закрываем приложение
                    automationApp?.Dispose();
                }

                ShowConversionResults(convertedFiles, failedFiles, outputFolder);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при конвертации: {ex.Message}", "Ошибка",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private void ShowConversionResults(List<string> convertedFiles, List<string> failedFiles, string outputFolder)
        {
            string message = $"Результаты конвертации:\n\nПапка назначения: {outputFolder}\n\n";

            if (convertedFiles.Count > 0)
            {
                message += $"Успешно сконвертировано ({convertedFiles.Count}):\n";
                foreach (string file in convertedFiles)
                {
                    message += $"✓ {file}\n";
                }
                message += "\n";
            }

            if (failedFiles.Count > 0)
            {
                message += $"Не удалось сконвертировать ({failedFiles.Count}):\n";
                foreach (string file in failedFiles)
                {
                    message += $"✗ {file}\n";
                }
            }

            MessageBox.Show(message, "Результаты конвертации",
                          MessageBoxButtons.OK,
                          convertedFiles.Count > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }
}