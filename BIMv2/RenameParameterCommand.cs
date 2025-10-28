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

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class RenameParameterCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            using (RenameParameterForm form = new RenameParameterForm(doc))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    TaskDialog.Show("Успех", "Параметр успешно переименован!");
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

public class RenameParameterForm : Form
{
    private Document _document;
    private ComboBox cmbExistingParams;
    private TextBox txtOldParameterName;
    private TextBox txtNewParameterName;
    private Button btnRename;

    public RenameParameterForm(Document doc)
    {
        _document = doc;
        InitializeForm();
        LoadSharedParameters();
    }

    private void InitializeForm()
    {
        this.Text = "Переименование общего параметра";
        this.Size = new System.Drawing.Size(850, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Padding = new Padding(10);

        // Создаем таблицу расположения
        TableLayoutPanel mainTable = new TableLayoutPanel();
        mainTable.Dock = DockStyle.Fill;
        mainTable.ColumnCount = 2;
        mainTable.RowCount = 5;
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 25f));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 25f));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 80f));

        // Метка для существующих параметров
        Label lblExistingParams = new Label();
        lblExistingParams.Text = "Существующие параметры:";
        lblExistingParams.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        lblExistingParams.Dock = DockStyle.Fill;
        mainTable.Controls.Add(lblExistingParams, 0, 0);

        // ComboBox для выбора существующего параметра
        cmbExistingParams = new ComboBox();
        cmbExistingParams.Dock = DockStyle.Fill;
        cmbExistingParams.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbExistingParams.SelectedIndexChanged += CmbExistingParams_SelectedIndexChanged;
        mainTable.Controls.Add(cmbExistingParams, 1, 0);

        // Метка для старого имени
        Label lblOldName = new Label();
        lblOldName.Text = "Старое имя параметра:";
        lblOldName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        lblOldName.Dock = DockStyle.Fill;
        mainTable.Controls.Add(lblOldName, 0, 1);

        // TextBox для старого имени
        txtOldParameterName = new TextBox();
        txtOldParameterName.Dock = DockStyle.Fill;
        mainTable.Controls.Add(txtOldParameterName, 1, 1);

        // Метка для нового имени
        Label lblNewName = new Label();
        lblNewName.Text = "Новое имя параметра:";
        lblNewName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        lblNewName.Dock = DockStyle.Fill;
        mainTable.Controls.Add(lblNewName, 0, 2);

        // TextBox для нового имени
        txtNewParameterName = new TextBox();
        txtNewParameterName.Dock = DockStyle.Fill;
        mainTable.Controls.Add(txtNewParameterName, 1, 2);

        // Панель для кнопок
        Panel buttonPanel = new Panel();
        buttonPanel.Dock = DockStyle.Fill;
        buttonPanel.Height = 30;
        mainTable.SetColumnSpan(buttonPanel, 2);
        mainTable.Controls.Add(buttonPanel, 0, 3);

        // Кнопка переименования
        btnRename = new Button();
        btnRename.Text = "Переименовать";
        btnRename.Size = new System.Drawing.Size(100, 25);
        btnRename.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRename.Location = new System.Drawing.Point(buttonPanel.Width - 210, 0);
        btnRename.Click += BtnRename_Click;
        buttonPanel.Controls.Add(btnRename);

        // Кнопка отмены
        Button btnCancel = new Button();
        btnCancel.Text = "Отмена";
        btnCancel.Size = new System.Drawing.Size(80, 25);
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.Location = new System.Drawing.Point(buttonPanel.Width - 100, 0);
        btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        buttonPanel.Controls.Add(btnCancel);

        // Информационная метка
        Label lblInfo = new Label();
        lblInfo.Text = "Внимание: Создается новый параметр с переносом значений. Старый параметр остается в проекте.";
        lblInfo.Dock = DockStyle.Fill;
        lblInfo.ForeColor = System.Drawing.Color.DarkOrange;
        lblInfo.Font = new System.Drawing.Font(lblInfo.Font, System.Drawing.FontStyle.Italic);
        lblInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        mainTable.SetColumnSpan(lblInfo, 2);
        mainTable.Controls.Add(lblInfo, 0, 4);

        this.Controls.Add(mainTable);
    }

    private void CmbExistingParams_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbExistingParams.SelectedItem != null)
        {
            txtOldParameterName.Text = cmbExistingParams.SelectedItem.ToString();
        }
    }

    private void LoadSharedParameters()
    {
        try
        {
            string[] parameters = GetAllSharedParameters();
            cmbExistingParams.Items.Clear();

            foreach (string paramName in parameters)
            {
                cmbExistingParams.Items.Add(paramName);
            }

            if (cmbExistingParams.Items.Count > 0)
            {
                cmbExistingParams.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке параметров: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string[] GetAllSharedParameters()
    {
        DefinitionFile sharedParamFile = _document.Application.OpenSharedParameterFile();

        if (sharedParamFile == null)
            return new string[0];

        List<string> parameterNames = new List<string>();

        foreach (DefinitionGroup group in sharedParamFile.Groups)
        {
            foreach (Definition definition in group.Definitions)
            {
                parameterNames.Add(definition.Name);
            }
        }

        return parameterNames.OrderBy(name => name).ToArray();
    }

    private bool RenameSharedParameter(string oldParameterName, string newParameterName, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            using (Transaction transaction = new Transaction(_document, $"Переименование параметра {oldParameterName} в {newParameterName}"))
            {
                transaction.Start();

                // Получаем файл общих параметров
                DefinitionFile sharedParamFile = _document.Application.OpenSharedParameterFile();

                if (sharedParamFile == null)
                {
                    errorMessage = "Файл общих параметров не открыт или недоступен";
                    return false;
                }

                // Ищем старый параметр
                ExternalDefinition oldDefinition = null;
                DefinitionGroup foundGroup = null;

                foreach (DefinitionGroup group in sharedParamFile.Groups)
                {
                    foreach (Definition definition in group.Definitions)
                    {
                        if (definition.Name.Equals(oldParameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            oldDefinition = definition as ExternalDefinition;
                            foundGroup = group;
                            break;
                        }
                    }
                    if (oldDefinition != null) break;
                }

                if (oldDefinition == null)
                {
                    errorMessage = $"Параметр '{oldParameterName}' не найден в файле общих параметров";
                    return false;
                }

                // Проверяем, не существует ли уже параметр с новым именем
                if (IsParameterNameExists(sharedParamFile, newParameterName))
                {
                    errorMessage = $"Параметр с именем '{newParameterName}' уже существует";
                    return false;
                }

                // Создаем новый параметр
                bool success = CreateNewParameterWithValues(oldDefinition, newParameterName, foundGroup, out errorMessage);

                if (success)
                {
                    transaction.Commit();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Ошибка при переименовании параметра: {ex.Message}";
            return false;
        }
    }

    private bool CreateNewParameterWithValues(ExternalDefinition oldDefinition, string newParameterName, DefinitionGroup group, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            // Создаем новый параметр в той же группе
            ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(newParameterName, oldDefinition.ParameterType);
            options.UserModifiable = oldDefinition.UserModifiable;

            ExternalDefinition newDefinition = group.Definitions.Create(options) as ExternalDefinition;

            if (newDefinition == null)
            {
                errorMessage = "Не удалось создать новый параметр";
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
        catch (Exception ex)
        {
            errorMessage = $"Ошибка при создании нового параметра: {ex.Message}";
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
                                case StorageType.None:
                                    break;
                            }
                        }
                        catch
                        {
                            // Игнорируем ошибки установки значений для отдельных элементов
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не прерываем выполнение
            System.Diagnostics.Debug.WriteLine($"Ошибка при переносе значений: {ex.Message}");
        }
    }

    private bool IsParameterNameExists(DefinitionFile sharedParamFile, string parameterName)
    {
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

    private void BtnRename_Click(object sender, EventArgs e)
    {
        string oldName = txtOldParameterName.Text.Trim();
        string newName = txtNewParameterName.Text.Trim();

        // Валидация
        if (string.IsNullOrEmpty(oldName))
        {
            MessageBox.Show("Введите имя существующего параметра!", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtOldParameterName.Focus();
            return;
        }

        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("Введите новое имя параметра!", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtNewParameterName.Focus();
            return;
        }

        if (oldName == newName)
        {
            MessageBox.Show("Старое и новое имена параметра не должны совпадать!", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtNewParameterName.Focus();
            return;
        }

        // Выполнение переименования
        try
        {
            Cursor = Cursors.WaitCursor;
            btnRename.Enabled = false;

            bool success = RenameSharedParameter(oldName, newName, out string errorMessage);

            if (success)
            {
                MessageBox.Show($"Параметр '{oldName}' успешно переименован в '{newName}'", "Успех",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show(errorMessage, "Ошибка переименования",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            btnRename.Enabled = true;
        }
    }
}