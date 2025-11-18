//using System;
//using System.Drawing;
//using System.Windows.Forms;

//namespace BIMv2
//{
//    public partial class CollisionSettingsForm : Form
//    {
//        public Color Collision1Color { get; set; }
//        public Color Collision2Color { get; set; }
//        public int Transparency { get; set; }
//        public string ReportFilePath { get; set; }

//        private Button btnOpenReport;
//        private Button btnColor1;
//        private Button btnColor2;
//        private Button btnTransparency;
//        private Button btnOK;
//        private Button btnCancel;
//        private Label lblReport;
//        private Label lblColor1;
//        private Label lblColor2;
//        private Label lblTransparency;
//        private ColorDialog colorDialog;

//        public CollisionSettingsForm()
//        {
//            InitializeComponent();
//            Collision1Color = Color.Orange;
//            Collision2Color = Color.Red;
//            Transparency = 80;
//            ReportFilePath = string.Empty;
//        }

//        private void InitializeComponent()
//        {
//            this.btnOpenReport = new Button();
//            this.btnColor1 = new Button();
//            this.btnColor2 = new Button();
//            this.btnTransparency = new Button();
//            this.btnOK = new Button();
//            this.btnCancel = new Button();
//            this.lblReport = new Label();
//            this.lblColor1 = new Label();
//            this.lblColor2 = new Label();
//            this.lblTransparency = new Label();
//            this.colorDialog = new ColorDialog();

//            this.btnOpenReport.Location = new System.Drawing.Point(120, 20);
//            this.btnOpenReport.Name = "btnOpenReport";
//            this.btnOpenReport.Size = new System.Drawing.Size(150, 30);
//            this.btnOpenReport.Text = "Открыть отчет";
//            this.btnOpenReport.Click += new EventHandler(this.btnOpenReport_Click);

//            this.btnColor1.Location = new System.Drawing.Point(120, 60);
//            this.btnColor1.Name = "btnColor1";
//            this.btnColor1.Size = new System.Drawing.Size(150, 30);
//            this.btnColor1.BackColor = Collision1Color;
//            this.btnColor1.Text = "Выбрать цвет";
//            this.btnColor1.Click += new EventHandler(this.btnColor1_Click);

//            this.btnColor2.Location = new System.Drawing.Point(120, 100);
//            this.btnColor2.Name = "btnColor2";
//            this.btnColor2.Size = new System.Drawing.Size(150, 30);
//            this.btnColor2.BackColor = Collision2Color;
//            this.btnColor2.Text = "Выбрать цвет";
//            this.btnColor2.Click += new EventHandler(this.btnColor2_Click);

//            this.btnTransparency.Location = new System.Drawing.Point(120, 140);
//            this.btnTransparency.Name = "btnTransparency";
//            this.btnTransparency.Size = new System.Drawing.Size(150, 30);
//            this.btnTransparency.Text = "Настроить прозрачность";
//            this.btnTransparency.Click += new EventHandler(this.btnTransparency_Click);

//            this.btnOK.Location = new System.Drawing.Point(50, 190);
//            this.btnOK.Name = "btnOK";
//            this.btnOK.Size = new System.Drawing.Size(100, 30);
//            this.btnOK.Text = "OK";
//            this.btnOK.Click += new EventHandler(this.btnOK_Click);

//            this.btnCancel.Location = new System.Drawing.Point(170, 190);
//            this.btnCancel.Name = "btnCancel";
//            this.btnCancel.Size = new System.Drawing.Size(100, 30);
//            this.btnCancel.Text = "Отмена";
//            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

//            this.lblReport.Location = new System.Drawing.Point(20, 25);
//            this.lblReport.Name = "lblReport";
//            this.lblReport.Size = new System.Drawing.Size(90, 20);
//            this.lblReport.Text = "Отчет:";

//            this.lblColor1.Location = new System.Drawing.Point(20, 65);
//            this.lblColor1.Name = "lblColor1";
//            this.lblColor1.Size = new System.Drawing.Size(90, 20);
//            this.lblColor1.Text = "Коллизия 1:";

//            this.lblColor2.Location = new System.Drawing.Point(20, 105);
//            this.lblColor2.Name = "lblColor2";
//            this.lblColor2.Size = new System.Drawing.Size(90, 20);
//            this.lblColor2.Text = "Коллизия 2:";

//            this.lblTransparency.Location = new System.Drawing.Point(20, 145);
//            this.lblTransparency.Name = "lblTransparency";
//            this.lblTransparency.Size = new System.Drawing.Size(90, 20);
//            this.lblTransparency.Text = "Прозрачность:";

//            this.ClientSize = new System.Drawing.Size(320, 240);
//            this.Controls.Add(this.btnOpenReport);
//            this.Controls.Add(this.btnColor1);
//            this.Controls.Add(this.btnColor2);
//            this.Controls.Add(this.btnTransparency);
//            this.Controls.Add(this.btnOK);
//            this.Controls.Add(this.btnCancel);
//            this.Controls.Add(this.lblReport);
//            this.Controls.Add(this.lblColor1);
//            this.Controls.Add(this.lblColor2);
//            this.Controls.Add(this.lblTransparency);
//            this.FormBorderStyle = FormBorderStyle.FixedDialog;
//            this.MaximizeBox = false;
//            this.MinimizeBox = false;
//            this.Text = "Настройки коллизий";
//            this.StartPosition = FormStartPosition.CenterScreen;
//        }

//        private void btnOpenReport_Click(object sender, EventArgs e)
//        {
//            OpenFileDialog ofd = new OpenFileDialog
//            {
//                Title = "Выберите XML отчет Navisworks",
//                Filter = "XML files|*.xml"
//            };

//            if (ofd.ShowDialog() == DialogResult.OK)
//            {
//                ReportFilePath = ofd.FileName;
//                lblReport.Text = $"Отчет: {System.IO.Path.GetFileName(ReportFilePath)}";
//            }
//        }

//        private void btnColor1_Click(object sender, EventArgs e)
//        {
//            colorDialog.Color = Collision1Color;
//            if (colorDialog.ShowDialog() == DialogResult.OK)
//            {
//                Collision1Color = colorDialog.Color;
//                btnColor1.BackColor = Collision1Color;
//            }
//        }

//        private void btnColor2_Click(object sender, EventArgs e)
//        {
//            colorDialog.Color = Collision2Color;
//            if (colorDialog.ShowDialog() == DialogResult.OK)
//            {
//                Collision2Color = colorDialog.Color;
//                btnColor2.BackColor = Collision2Color;
//            }
//        }

//        private void btnTransparency_Click(object sender, EventArgs e)
//        {
//            using (var transparencyForm = new TransparencySettingsForm(Transparency))
//            {
//                if (transparencyForm.ShowDialog() == DialogResult.OK)
//                {
//                    Transparency = transparencyForm.TransparencyValue;
//                    lblTransparency.Text = $"Прозрачность: {100 - Transparency}%";
//                }
//            }
//        }

//        private void btnOK_Click(object sender, EventArgs e)
//        {
//            if (string.IsNullOrEmpty(ReportFilePath))
//            {
//                MessageBox.Show("Пожалуйста, выберите файл отчета с коллизиями.", "Внимание",
//                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
//                return;
//            }

//            this.DialogResult = DialogResult.OK;
//            this.Close();
//        }

//        private void btnCancel_Click(object sender, EventArgs e)
//        {
//            this.DialogResult = DialogResult.Cancel;
//            this.Close();
//        }
//    }

//    public class TransparencySettingsForm : Form
//    {
//        public int TransparencyValue { get; private set; }

//        private TrackBar trackBar;
//        private Label lblValue;
//        private Button btnOK;
//        private Button btnCancel;

//        public TransparencySettingsForm(int currentTransparency)
//        {
//            TransparencyValue = currentTransparency;
//            InitializeComponent();
//        }

//        private void InitializeComponent()
//        {
//            this.trackBar = new TrackBar();
//            this.lblValue = new Label();
//            this.btnOK = new Button();
//            this.btnCancel = new Button();

//            // trackBar
//            this.trackBar.Location = new System.Drawing.Point(20, 20);
//            this.trackBar.Size = new System.Drawing.Size(250, 45);
//            this.trackBar.Minimum = 0;
//            this.trackBar.Maximum = 100;
//            this.trackBar.Value = TransparencyValue;
//            this.trackBar.TickFrequency = 10;
//            this.trackBar.ValueChanged += new EventHandler(this.trackBar_ValueChanged);

//            // lblValue
//            this.lblValue.Location = new System.Drawing.Point(280, 25);
//            this.lblValue.Size = new System.Drawing.Size(50, 20);
//            this.lblValue.Text = $"{100 - TransparencyValue}%";

//            // btnOK
//            this.btnOK.Location = new System.Drawing.Point(70, 80);
//            this.btnOK.Size = new System.Drawing.Size(75, 25);
//            this.btnOK.Text = "OK";
//            this.btnOK.Click += new EventHandler(this.btnOK_Click);

//            // btnCancel
//            this.btnCancel.Location = new System.Drawing.Point(155, 80);
//            this.btnCancel.Size = new System.Drawing.Size(75, 25);
//            this.btnCancel.Text = "Отмена";
//            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

//            // Form
//            this.ClientSize = new System.Drawing.Size(350, 120);
//            this.Controls.Add(this.trackBar);
//            this.Controls.Add(this.lblValue);
//            this.Controls.Add(this.btnOK);
//            this.Controls.Add(this.btnCancel);
//            this.FormBorderStyle = FormBorderStyle.FixedDialog;
//            this.MaximizeBox = false;
//            this.MinimizeBox = false;
//            this.Text = "Настройка прозрачности";
//            this.StartPosition = FormStartPosition.CenterScreen;
//        }

//        private void trackBar_ValueChanged(object sender, EventArgs e)
//        {
//            TransparencyValue = trackBar.Value;
//            lblValue.Text = $"{100 - TransparencyValue}%";
//        }

//        private void btnOK_Click(object sender, EventArgs e)
//        {
//            this.DialogResult = DialogResult.OK;
//            this.Close();
//        }

//        private void btnCancel_Click(object sender, EventArgs e)
//        {
//            this.DialogResult = DialogResult.Cancel;
//            this.Close();
//        }
//    }
//}