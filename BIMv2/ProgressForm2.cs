using System;
using System.Windows.Forms;

namespace BIMv2
{
    public partial class ProgressForm2 : Form
    {
        public ProgressBar ProgressBar => progressBar;
        public Label StatusLabel => labelStatus;
        public bool CancelRequested { get; private set; }

        public ProgressForm2()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.progressBar = new ProgressBar();
            this.labelStatus = new Label();
            this.buttonCancel = new Button();

            this.SuspendLayout();

            // progressBar
            this.progressBar.Location = new System.Drawing.Point(15, 30);
            this.progressBar.Size = new System.Drawing.Size(350, 23);
            this.progressBar.Style = ProgressBarStyle.Continuous;

            // labelStatus
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(15, 10);
            this.labelStatus.Text = "Подготовка...";

            // buttonCancel
            this.buttonCancel.Location = new System.Drawing.Point(290, 60);
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.Text = "Отмена";
            this.buttonCancel.Click += (s, e) => { CancelRequested = true; };

            // ProgressForm
            this.ClientSize = new System.Drawing.Size(680, 295);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Обработка коллизий";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private ProgressBar progressBar;
        private Label labelStatus;
        private Button buttonCancel;
    }
}
