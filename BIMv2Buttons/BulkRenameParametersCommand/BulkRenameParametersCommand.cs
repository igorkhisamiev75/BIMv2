using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

using Form = System.Windows.Forms.Form;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using Panel = System.Windows.Forms.Panel;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]

    public class BulkRenameParametersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                using (BulkRenameParametersForm form = new BulkRenameParametersForm(doc))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        return Result.Succeeded;
                    }
                }

                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }
        }
    }

    public class BulkRenameParametersForm : Form
    {
        private Document _document;
        private Button btnRenameAll;
        private ListBox lstParameters;
        private Label lblInfo;
        private ProgressBar progressBar;

        public BulkRenameParametersForm(Document doc)
        {
            _document = doc;
            InitializeForm();
            LoadParametersList();
        }

        private void InitializeForm()
        {
            this.Text = "Массовое переименование параметров LSR";
            this.Size = new System.Drawing.Size(500, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Padding = new Padding(10);

            // Основная таблица
            TableLayoutPanel mainTable = new TableLayoutPanel();
            mainTable.Dock = DockStyle.Fill;
            mainTable.ColumnCount = 1;
            mainTable.RowCount = 5;
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

            // Заголовок
            Label lblTitle = new Label();
            lblTitle.Text = "Параметры для переименования (LSR_ → LG-B2):";
            lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.Font = new System.Drawing.Font(lblTitle.Font, System.Drawing.FontStyle.Bold);
            mainTable.Controls.Add(lblTitle, 0, 0);

            // ListBox для параметров
            lstParameters = new ListBox();
            lstParameters.Dock = DockStyle.Fill;
            lstParameters.SelectionMode = SelectionMode.MultiExtended;
            mainTable.Controls.Add(lstParameters, 0, 1);

            // ProgressBar
            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Visible = false;
            mainTable.Controls.Add(progressBar, 0, 2);

            // Панель для кнопок
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.Height = 30;
            mainTable.Controls.Add(buttonPanel, 0, 3);

            // Кнопка переименования
            btnRenameAll = new Button();
            btnRenameAll.Text = "Переименовать все";
            btnRenameAll.Size = new System.Drawing.Size(120, 25);
            btnRenameAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRenameAll.Location = new System.Drawing.Point(buttonPanel.Width - 250, 0);
            btnRenameAll.Click += BtnRenameAll_Click;
            buttonPanel.Controls.Add(btnRenameAll);

            // Кнопка выбора
            Button btnSelectAll = new Button();
            btnSelectAll.Text = "Выбрать все";
            btnSelectAll.Size = new System.Drawing.Size(100, 25);
            btnSelectAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSelectAll.Location = new System.Drawing.Point(buttonPanel.Width - 140, 0);
            btnSelectAll.Click += (s, e) =>
            {
                for (int i = 0; i < lstParameters.Items.Count; i++)
                    lstParameters.SetSelected(i, true);
            };
            buttonPanel.Controls.Add(btnSelectAll);

            // Кнопка отмены
            Button btnCancel = new Button();
            btnCancel.Text = "Отмена";
            btnCancel.Size = new System.Drawing.Size(80, 25);
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Location = new System.Drawing.Point(buttonPanel.Width - 50, 0);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            buttonPanel.Controls.Add(btnCancel);

            // Информационная метка
            lblInfo = new Label();
            lblInfo.Text = "Будут переименованы все параметры, начинающиеся с 'LSR_' на 'LG-B2'";
            lblInfo.Dock = DockStyle.Fill;
            lblInfo.ForeColor = System.Drawing.Color.DarkBlue;
            lblInfo.Font = new System.Drawing.Font(lblInfo.Font, System.Drawing.FontStyle.Italic);
            lblInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            mainTable.Controls.Add(lblInfo, 0, 4);

            this.Controls.Add(mainTable);
        }

        private void LoadParametersList()
        {
            try
            {
                var parameters = GetParametersWithPrefix("LSR_");
                lstParameters.Items.Clear();

                foreach (var paramInfo in parameters)
                {
                    string newName = paramInfo.OldName.Replace("LSR_", "LG-B2_");
                    lstParameters.Items.Add($"{paramInfo.OldName} → {newName}");
                }

                if (lstParameters.Items.Count > 0)
                {
                    // Автоматически выбираем все элементы
                    for (int i = 0; i < lstParameters.Items.Count; i++)
                        lstParameters.SetSelected(i, true);

                    lblInfo.Text = $"Найдено параметров для переименования: {lstParameters.Items.Count}";
                }
                else
                {
                    lblInfo.Text = "Параметры с префиксом 'LSR_' не найдены";
                    btnRenameAll.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке параметров: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<ParameterInfo> GetParametersWithPrefix(string prefix)
        {
            List<ParameterInfo> parameters = new List<ParameterInfo>();

            DefinitionFile sharedParamFile = _document.Application.OpenSharedParameterFile();

            if (sharedParamFile == null)
                return parameters;

            foreach (DefinitionGroup group in sharedParamFile.Groups)
            {
                foreach (Definition definition in group.Definitions)
                {
                    if (definition.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        parameters.Add(new ParameterInfo
                        {
                            OldName = definition.Name,
                            Definition = definition,
                            Group = group
                        });
                    }
                }
            }

            return parameters.OrderBy(p => p.OldName).ToList();
        }

        private void BtnRenameAll_Click(object sender, EventArgs e)
        {
            if (lstParameters.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите параметры для переименования!", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedParameters = GetParametersWithPrefix("LSR_");
            if (selectedParameters.Count == 0) return;

            // Подтверждение
            DialogResult confirm = MessageBox.Show(
                $"Вы уверены, что хотите переименовать {lstParameters.SelectedItems.Count} параметров?\n\n" +
                "Старые параметры останутся в проекте. Будет созданы новые параметры с переносом значений.",
                "Подтверждение переименования",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            // Выполнение массового переименования
            try
            {
                Cursor = Cursors.WaitCursor;
                btnRenameAll.Enabled = false;
                progressBar.Visible = true;
                progressBar.Maximum = lstParameters.SelectedItems.Count;
                progressBar.Value = 0;

                int successCount = 0;
                int errorCount = 0;
                List<string> errors = new List<string>();

                using (Transaction transaction = new Transaction(_document, "Массовое переименование параметров"))
                {
                    transaction.Start();

                    foreach (var paramInfo in selectedParameters)
                    {
                        try
                        {
                            string newName = paramInfo.OldName.Replace("LSR_", "LG-B2_");

                            if (RenameSingleParameter(paramInfo.Definition as ExternalDefinition, newName, paramInfo.Group))
                            {
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"{paramInfo.OldName} → {newName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"{paramInfo.OldName}: {ex.Message}");
                        }

                        progressBar.Value++;
                        Application.DoEvents(); // Обновляем UI
                    }

                    transaction.Commit();
                }

                // Показываем результаты
                string resultMessage = $"Переименование завершено!\n\nУспешно: {successCount}\nОшибок: {errorCount}";

                if (errors.Count > 0)
                {
                    resultMessage += $"\n\nОшибки:\n{string.Join("\n", errors.Take(10))}";
                    if (errors.Count > 10)
                        resultMessage += $"\n... и еще {errors.Count - 10} ошибок";
                }

                MessageBox.Show(resultMessage, "Результат",
                    MessageBoxButtons.OK,
                    errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                if (successCount > 0)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла непредвиденная ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                btnRenameAll.Enabled = true;
                progressBar.Visible = false;
            }
        }

        private bool RenameSingleParameter(ExternalDefinition oldDefinition, string newParameterName, DefinitionGroup group)
        {
            try
            {
                // Проверяем, не существует ли уже параметр с новым именем
                if (IsParameterNameExists(newParameterName))
                {
                    return false;
                }

                // Создаем новый параметр в той же группе
                ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(newParameterName, oldDefinition.ParameterType);
                options.UserModifiable = oldDefinition.UserModifiable;

                ExternalDefinition newDefinition = group.Definitions.Create(options) as ExternalDefinition;

                if (newDefinition == null)
                {
                    return false;
                }

                // Получаем привязки старого параметра
                BindingMap bindingMap = _document.ParameterBindings;
                ElementBinding oldBinding = bindingMap.get_Item(oldDefinition) as ElementBinding;

                if (oldBinding != null)
                {
                    // Привязываем новый параметр к тем же категориям
                    CategorySet categories = oldBinding.Categories;
                    Autodesk.Revit.DB.Binding newBinding = null;

                    if (oldBinding is InstanceBinding)
                    {
                        newBinding = _document.Application.Create.NewInstanceBinding(categories);
                    }
                    else if (oldBinding is TypeBinding)
                    {
                        newBinding = _document.Application.Create.NewTypeBinding(categories);
                    }

                    if (newBinding != null)
                    {
                        bindingMap.Insert(newDefinition, newBinding, oldDefinition.GetGroupTypeId());
                    }
                }

                // Переносим значения параметров
                TransferParameterValues(oldDefinition, newDefinition);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void TransferParameterValues(ExternalDefinition oldDefinition, ExternalDefinition newDefinition)
        {
            try
            {
                // Получаем все элементы в проекте
                FilteredElementCollector collector = new FilteredElementCollector(_document);
                var allElements = collector.WhereElementIsNotElementType().ToElements();

                foreach (Element element in allElements)
                {
                    Parameter oldParam = element.get_Parameter(oldDefinition.GUID);
                    if (oldParam != null && oldParam.HasValue && !oldParam.IsReadOnly)
                    {
                        Parameter newParam = element.get_Parameter(newDefinition.GUID);
                        if (newParam != null && !newParam.IsReadOnly)
                        {
                            try
                            {
                                switch (oldParam.StorageType)
                                {
                                    case StorageType.Double:
                                        if (newParam.StorageType == StorageType.Double)
                                            newParam.Set(oldParam.AsDouble());
                                        break;
                                    case StorageType.ElementId:
                                        if (newParam.StorageType == StorageType.ElementId)
                                            newParam.Set(oldParam.AsElementId());
                                        break;
                                    case StorageType.Integer:
                                        if (newParam.StorageType == StorageType.Integer)
                                            newParam.Set(oldParam.AsInteger());
                                        break;
                                    case StorageType.String:
                                        if (newParam.StorageType == StorageType.String)
                                            newParam.Set(oldParam.AsString());
                                        break;
                                }
                            }
                            catch
                            {
                                // Игнорируем ошибки для отдельных элементов
                            }
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки переноса значений
            }
        }

        private bool IsParameterNameExists(string parameterName)
        {
            DefinitionFile sharedParamFile = _document.Application.OpenSharedParameterFile();
            if (sharedParamFile == null) return false;

            foreach (DefinitionGroup group in sharedParamFile.Groups)
            {
                foreach (Definition definition in group.Definitions)
                {
                    if (definition.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    // Вспомогательный класс для хранения информации о параметрах
    public class ParameterInfo
    {
        public string OldName { get; set; }
        public Definition Definition { get; set; }
        public DefinitionGroup Group { get; set; }
    }
}