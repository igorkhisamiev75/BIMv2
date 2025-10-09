using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace RvtToNwdConverter
{
    public partial class ProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label labelStatus;
        private Button buttonCancel;
        private int totalFiles;

        public ProgressForm(int totalFiles)
        {
            this.totalFiles = totalFiles;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new System.Drawing.Size(400, 150);
            this.Text = "Конвертация файлов...";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ControlBox = false;

            // Label
            labelStatus = new Label();
            labelStatus.Location = new System.Drawing.Point(20, 20);
            labelStatus.Size = new System.Drawing.Size(350, 20);
            labelStatus.Text = "Подготовка к конвертации...";
            this.Controls.Add(labelStatus);

            // ProgressBar
            progressBar = new ProgressBar();
            progressBar.Location = new System.Drawing.Point(20, 50);
            progressBar.Size = new System.Drawing.Size(350, 23);
            progressBar.Minimum = 0;
            progressBar.Maximum = totalFiles;
            progressBar.Value = 0;
            this.Controls.Add(progressBar);

            // Cancel Button
            buttonCancel = new Button();
            buttonCancel.Location = new System.Drawing.Point(295, 80);
            buttonCancel.Size = new System.Drawing.Size(75, 23);
            buttonCancel.Text = "Отмена";
            buttonCancel.Click += ButtonCancel_Click;
            this.Controls.Add(buttonCancel);
        }

        public void UpdateProgress(string status)
        {
            if (labelStatus.InvokeRequired)
            {
                labelStatus.Invoke(new Action<string>(UpdateProgress), status);
            }
            else
            {
                labelStatus.Text = status;
                Application.DoEvents();
            }
        }

        public void IncrementProgress()
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(IncrementProgress));
            }
            else
            {
                if (progressBar.Value < progressBar.Maximum)
                {
                    progressBar.Value++;
                }
                Application.DoEvents();
            }
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}