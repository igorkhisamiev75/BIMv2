using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace BIMv2Navis
{
    [Plugin("RvtToNwdAddIn", "ADSK", DisplayName = "RVT to NWD Converter")]
    public class RvtToNwdAddIn : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                // Показываем диалог выбора режима работы
                using (ModeSelectionDialog dialog = new ModeSelectionDialog())
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        if (dialog.SelectedMode == ConversionMode.Single)
                        {
                            ConvertSingleFile();
                        }
                        else
                        {
                            BatchConvertFiles();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return 0;
        }

        private void ConvertSingleFile()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Revit Files (*.rvt)|*.rvt";
                openFileDialog.Title = "Выберите RVT файл для конвертации";
                openFileDialog.Multiselect = false;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string rvtFilePath = openFileDialog.FileName;
                    ProcessFile(rvtFilePath);
                }
            }
        }

        private void BatchConvertFiles()
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Выберите папку с RVT файлами";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFolder = folderDialog.SelectedPath;

                    // Ищем все RVT файлы в папке и подпапках
                    string[] rvtFiles = Directory.GetFiles(selectedFolder, "*.rvt", SearchOption.AllDirectories);

                    if (rvtFiles.Length == 0)
                    {
                        MessageBox.Show("RVT файлы не найдены в выбранной папке", "Информация",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    // Показываем диалог выбора файлов
                    using (FileSelectionDialog fileDialog = new FileSelectionDialog(rvtFiles))
                    {
                        if (fileDialog.ShowDialog() == DialogResult.OK)
                        {
                            List<string> selectedFiles = fileDialog.SelectedFiles;
                            ProcessBatchConversion(selectedFiles);
                        }
                    }
                }
            }
        }

        private void ProcessBatchConversion(List<string> files)
        {
            int successCount = 0;
            int totalFiles = files.Count;

            using (ProgressDialog progressDialog = new ProgressDialog(totalFiles))
            {
                progressDialog.Show();

                foreach (string filePath in files)
                {
                    if (progressDialog.Cancelled) break;

                    try
                    {
                        progressDialog.UpdateStatus($"Обработка: {Path.GetFileName(filePath)}");

                        if (ProcessFile(filePath, false))
                        {
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        progressDialog.AddLog($"Ошибка: {Path.GetFileName(filePath)} - {ex.Message}");
                    }

                    progressDialog.IncrementProgress();
                }

                progressDialog.Close();
            }

            MessageBox.Show($"Конвертация завершена!\nУспешно: {successCount} из {totalFiles} файлов",
                          "Результат",
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool ProcessFile(string rvtFilePath, bool showMessages = true)
        {
            Document doc = null;
            try
            {
                if (!File.Exists(rvtFilePath))
                {
                    if (showMessages)
                        MessageBox.Show($"Файл не найден: {rvtFilePath}", "Ошибка",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                string nwdFilePath = Path.Combine(
                    Path.GetDirectoryName(rvtFilePath),
                    Path.GetFileNameWithoutExtension(rvtFilePath) + ".nwd"
                );

                // Проверяем существование NWD файла
                if (File.Exists(nwdFilePath))
                {
                    DialogResult result = MessageBox.Show(
                        $"Файл уже существует: {Path.GetFileName(nwdFilePath)}\nПерезаписать?",
                        "Файл существует",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (result != DialogResult.Yes)
                    {
                        // Предлагаем выбрать новое расположение
                        using (SaveFileDialog saveDialog = new SaveFileDialog())
                        {
                            saveDialog.Filter = "Navisworks Files (*.nwd)|*.nwd";
                            saveDialog.Title = "Сохранить NWD файл как";
                            saveDialog.FileName = Path.GetFileName(nwdFilePath);

                            if (saveDialog.ShowDialog() == DialogResult.OK)
                            {
                                nwdFilePath = saveDialog.FileName;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }

                // Создаем новый документ
                doc = new Document();

                // Открываем RVT файл
                doc.OpenFile(rvtFilePath);

                // Сохраняем как NWD
                doc.SaveAs(nwdFilePath);

                if (showMessages)
                {
                    MessageBox.Show($"Файл успешно конвертирован!\n{nwdFilePath}",
                                  "Готово",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (showMessages)
                {
                    MessageBox.Show($"Ошибка при конвертации {Path.GetFileName(rvtFilePath)}:\n{ex.Message}",
                                  "Ошибка",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return false;
            }
            finally
            {
                doc?.Dispose();
            }
        }
    }

    public enum ConversionMode
    {
        Single,
        Batch
    }
}