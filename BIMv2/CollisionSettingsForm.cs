using System;
using System.Drawing;
using System.Windows.Forms;

namespace BIMv2
{
    public partial class CollisionSettingsForm : Form
    {
        public Color Collision1Color { get; private set; }
        public Color Collision2Color { get; private set; }
        public int Transparency { get; private set; }
        public string ReportFilePath { get; private set; }
        public bool DialogResultOK { get; private set; }

        public CollisionSettingsForm()
        {
            InitializeComponent();
            Collision1Color = Color.Orange;
            Collision2Color = Color.Red;
            Transparency = 80;
            ReportFilePath = string.Empty;
            DialogResultOK = false;
        }

        private void InitializeComponent()
        {
            this.btnOpenReport = new Button();
            this.btnColor1 = new Button();
            this.btnColor2 = new Button();
            this.btnTransparency = new Button();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.lblReport = new Label();
            this.lblColor1 = new Label();
            this.lblColor2 = new Label();
            this.lblTransparency = new Label();
            this.colorDialog = new ColorDialog();
            this.SuspendLayout();

            // btnOpenReport
            this.btnOpenReport.Location = new Point(120, 20);
            this.btnOpenReport.Name = "btnOpenReport";
            this.btnOpenReport.Size = new Size(150, 30);
            this.btnOpenReport.Text = "Открыть отчет";
            this.btnOpenReport.Click += new EventHandler(this.btnOpenReport_Click);

            // btnColor1
            this.btnColor1.Location = new Point(120, 60);
            this.btnColor1.Name = "btnColor1";
            this.btnColor1.Size = new Size(150, 30);
            this.btnColor1.BackColor = Collision1Color;
            this.btnColor1.Text = "Выбрать цвет";
            this.btnColor1.Click += new EventHandler(this.btnColor1_Click);

            // btnColor2
            this.btnColor2.Location = new Point(120, 100);
            this.btnColor2.Name = "btnColor2";
            this.btnColor2.Size = new Size(150, 30);
            this.btnColor2.BackColor = Collision2Color;
            this.btnColor2.Text = "Выбрать цвет";
            this.btnColor2.Click += new EventHandler(this.btnColor2_Click);

            // btnTransparency
            this.btnTransparency.Location = new Point(120, 140);
            this.btnTransparency.Name = "btnTransparency";
            this.btnTransparency.Size = new Size(150, 30); // Исправлено: Size вместо Point
            this.btnTransparency.Text = "Настроить прозрачность";
            this.btnTransparency.Click += new EventHandler(this.btnTransparency_Click);

            // btnOK
            this.btnOK.Location = new Point(50, 190);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new Size(100, 30);
            this.btnOK.Text = "OK";
            this.btnOK.Click += new EventHandler(this.btnOK_Click);

            // btnCancel
            this.btnCancel.Location = new Point(170, 190);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(100, 30);
            this.btnCancel.Text = "Отмена";
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // lblReport
            this.lblReport.Location = new Point(20, 25);
            this.lblReport.Name = "lblReport";
            this.lblReport.Size = new Size(90, 20);
            this.lblReport.Text = "Отчет:";

            // lblColor1
            this.lblColor1.Location = new Point(20, 65);
            this.lblColor1.Name = "lblColor1";
            this.lblColor1.Size = new Size(90, 20);
            this.lblColor1.Text = "Коллизия 1:";

            // lblColor2
            this.lblColor2.Location = new Point(20, 105);
            this.lblColor2.Name = "lblColor2";
            this.lblColor2.Size = new Size(90, 20);
            this.lblColor2.Text = "Коллизия 2:";

            // lblTransparency
            this.lblTransparency.Location = new Point(20, 145);
            this.lblTransparency.Name = "lblTransparency";
            this.lblTransparency.Size = new Size(90, 20);
            this.lblTransparency.Text = "Прозрачность:";

            // Form
            this.ClientSize = new Size(320, 240);
            this.Controls.Add(this.btnOpenReport);
            this.Controls.Add(this.btnColor1);
            this.Controls.Add(this.btnColor2);
            this.Controls.Add(this.btnTransparency);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblReport);
            this.Controls.Add(this.lblColor1);
            this.Controls.Add(this.lblColor2);
            this.Controls.Add(this.lblTransparency);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Настройки коллизий";
            this.ResumeLayout(false);
        }

        private void btnOpenReport_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Выберите XML отчет Navisworks",
                Filter = "XML files|*.xml"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                ReportFilePath = ofd.FileName;
                lblReport.Text = $"Отчет: {System.IO.Path.GetFileName(ReportFilePath)}";
            }
        }

        private void btnColor1_Click(object sender, EventArgs e)
        {
            colorDialog.Color = Collision1Color;
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                Collision1Color = colorDialog.Color;
                btnColor1.BackColor = Collision1Color;
            }
        }

        private void btnColor2_Click(object sender, EventArgs e)
        {
            colorDialog.Color = Collision2Color;
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                Collision2Color = colorDialog.Color;
                btnColor2.BackColor = Collision2Color;
            }
        }

        private void btnTransparency_Click(object sender, EventArgs e)
        {
            using (var transparencyForm = new TransparencySettingsForm(Transparency))
            {
                if (transparencyForm.ShowDialog() == DialogResult.OK)
                {
                    Transparency = transparencyForm.TransparencyValue;
                    lblTransparency.Text = $"Прозрачность: {100 - Transparency}%";
                }
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ReportFilePath))
            {
                MessageBox.Show("Пожалуйста, выберите файл отчета с коллизиями.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResultOK = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private Button btnOpenReport;
        private Button btnColor1;
        private Button btnColor2;
        private Button btnTransparency;
        private Button btnOK;
        private Button btnCancel;
        private Label lblReport;
        private Label lblColor1;
        private Label lblColor2;
        private Label lblTransparency;
        private ColorDialog colorDialog;
    }

    public class TransparencySettingsForm : Form
    {
        public int TransparencyValue { get; private set; }

        private TrackBar trackBar;
        private Label lblValue;

        public TransparencySettingsForm(int currentTransparency)
        {
            TransparencyValue = currentTransparency;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.trackBar = new TrackBar();
            this.lblValue = new Label();
            var btnOK = new Button();
            var btnCancel = new Button();

            // trackBar
            this.trackBar.Location = new Point(20, 20);
            this.trackBar.Size = new Size(250, 45);
            this.trackBar.Minimum = 0;
            this.trackBar.Maximum = 100;
            this.trackBar.Value = TransparencyValue;
            this.trackBar.TickFrequency = 10;
            this.trackBar.ValueChanged += new EventHandler(this.trackBar_ValueChanged);

            // lblValue
            this.lblValue.Location = new Point(280, 25);
            this.lblValue.Size = new Size(50, 20);
            this.lblValue.Text = $"{100 - TransparencyValue}%";

            // btnOK
            btnOK.Location = new Point(70, 80);
            btnOK.Size = new Size(75, 25);
            btnOK.Text = "OK";
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Click += new EventHandler(this.btnOK_Click);

            // btnCancel
            btnCancel.Location = new Point(155, 80);
            btnCancel.Size = new Size(75, 25);
            btnCancel.Text = "Отмена";
            btnCancel.DialogResult = DialogResult.Cancel;

            // Form
            this.ClientSize = new Size(350, 120);
            this.Controls.Add(this.trackBar);
            this.Controls.Add(this.lblValue);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Настройка прозрачности";
        }

        private void trackBar_ValueChanged(object sender, EventArgs e)
        {
            TransparencyValue = trackBar.Value;
            lblValue.Text = $"{100 - TransparencyValue}%";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}