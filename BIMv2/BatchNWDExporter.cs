using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Form = System.Windows.Forms.Form;
using Button = System.Windows.Forms.Button;
using ListBox = System.Windows.Forms.ListBox;
using TextBox = System.Windows.Forms.TextBox;

[Transaction(TransactionMode.Manual)]
public class AdvancedNavisworksExport : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;

        using (var form = new ExportForm(uiApp))
        {
            if (form.ShowDialog() == DialogResult.OK)
            {
                TaskDialog.Show("Успех", "Экспорт завершен успешно!");
                return Result.Succeeded;
            }
        }

        return Result.Cancelled;
    }
}

// Форма для настройки экспорта
public class ExportForm : Form
{
    private UIApplication uiApp;
    private ListBox fileListBox;
    private Button addButton;
    private Button removeButton;
    private Button exportButton;
    private TextBox savePathTextBox;
    private Button browseSaveButton;

    public ExportForm(UIApplication uiApplication)
    {
        this.uiApp = uiApplication;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Экспорт в Navisworks NWD";
        this.Size = new System.Drawing.Size(600, 400);
        this.StartPosition = FormStartPosition.CenterScreen;

        // Список файлов
        fileListBox = new ListBox();
        fileListBox.Location = new System.Drawing.Point(10, 40);
        fileListBox.Size = new System.Drawing.Size(400, 200);
        this.Controls.Add(fileListBox);

        // Кнопка добавления файлов
        addButton = new Button();
        addButton.Text = "Добавить модели";
        addButton.Location = new System.Drawing.Point(420, 40);
        addButton.Size = new System.Drawing.Size(150, 30);
        addButton.Click += AddButton_Click;
        this.Controls.Add(addButton);

        // Кнопка удаления файлов
        removeButton = new Button();
        removeButton.Text = "Удалить выбранное";
        removeButton.Location = new System.Drawing.Point(420, 80);
        removeButton.Size = new System.Drawing.Size(150, 30);
        removeButton.Click += RemoveButton_Click;
        this.Controls.Add(removeButton);

        // Поле пути сохранения
        savePathTextBox = new TextBox();
        savePathTextBox.Location = new System.Drawing.Point(10, 260);
        savePathTextBox.Size = new System.Drawing.Size(400, 20);
        savePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Export.nwd");
        this.Controls.Add(savePathTextBox);

        // Кнопка обзора пути сохранения
        browseSaveButton = new Button();
        browseSaveButton.Text = "Обзор...";
        browseSaveButton.Location = new System.Drawing.Point(420, 260);
        browseSaveButton.Size = new System.Drawing.Size(150, 25);
        browseSaveButton.Click += BrowseSaveButton_Click;
        this.Controls.Add(browseSaveButton);

        // Кнопка экспорта
        exportButton = new Button();
        exportButton.Text = "Экспорт в NWD";
        exportButton.Location = new System.Drawing.Point(10, 300);
        exportButton.Size = new System.Drawing.Size(560, 40);
        exportButton.Click += ExportButton_Click;
        this.Controls.Add(exportButton);
    }

    private void AddButton_Click(object sender, EventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "Revit Files (*.rvt)|*.rvt";
        openFileDialog.Multiselect = true;

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            foreach (string file in openFileDialog.FileNames)
            {
                if (!fileListBox.Items.Contains(file))
                    fileListBox.Items.Add(file);
            }
        }
    }

    private void RemoveButton_Click(object sender, EventArgs e)
    {
        if (fileListBox.SelectedIndex != -1)
        {
            fileListBox.Items.RemoveAt(fileListBox.SelectedIndex);
        }
    }

    private void BrowseSaveButton_Click(object sender, EventArgs e)
    {
        SaveFileDialog saveDialog = new SaveFileDialog();
        saveDialog.Filter = "Navisworks Files (*.nwd)|*.nwd";
        saveDialog.FileName = "Export.nwd";

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            savePathTextBox.Text = saveDialog.FileName;
        }
    }

    private void ExportButton_Click(object sender, EventArgs e)
    {
        if (fileListBox.Items.Count == 0)
        {
            MessageBox.Show("Не выбраны файлы для экспорта!");
            return;
        }

        if (string.IsNullOrEmpty(savePathTextBox.Text))
        {
            MessageBox.Show("Не выбран путь для сохранения!");
            return;
        }

        try
        {
            Document doc = uiApp.ActiveUIDocument.Document;
            string[] sourceFiles = fileListBox.Items.Cast<string>().ToArray();

            ExportToNavisworks(doc, sourceFiles, savePathTextBox.Text);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при экспорте: {ex.Message}");
        }
    }

    private void ExportToNavisworks(Document doc, string[] sourceFiles, string exportPath)
    {
        using (Transaction trans = new Transaction(doc, "Navisworks Export"))
        {
            trans.Start();

            NavisworksExportOptions options = new NavisworksExportOptions();

            // Настройка опций экспорта
            options.ExportScope = NavisworksExportScope.View;
            options.ViewId = doc.ActiveView.Id;
            options.ExportLinks = true;
            options.ConvertElementProperties = true;
            options.ExportUrls = true;
            options.Coordinates = NavisworksCoordinates.Shared;

            // Дополнительные настройки
            options.ExportRoomGeometry = false;
            options.ExportRoomAsAttribute = true;
            options.ExportParts = true;
            options.FindMissingMaterials = true;

            // Выполнение экспорта
            doc.Export(Path.GetDirectoryName(exportPath),
                      Path.GetFileNameWithoutExtension(exportPath),
                      options);

            trans.Commit();
        }
    }
}