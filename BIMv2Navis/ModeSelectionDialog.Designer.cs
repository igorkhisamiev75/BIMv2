using RvtToNwdAddIn;

using System;
using System.Windows.Forms;

namespace BIMv2Navis
{
    public  partial class ModeSelectionDialog : Form
    {
        private RadioButton radioSingle;
        private RadioButton radioBatch;
        private Button buttonOk;
        private Button buttonCancel;

        public ConversionMode SelectedMode { get; private set; }

        public ModeSelectionDialog()
        {
            InitializeComponent();
            SelectedMode = ConversionMode.Single;
        }

        private void InitializeComponent()
        {
            this.Text = "Выберите режим конвертации";
            this.Size = new System.Drawing.Size(300, 200);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // RadioButton для одиночного файла
            radioSingle = new RadioButton();
            radioSingle.Text = "Конвертировать один файл";
            radioSingle.Location = new System.Drawing.Point(20, 20);
            radioSingle.Size = new System.Drawing.Size(200, 20);
            radioSingle.Checked = true;
            radioSingle.CheckedChanged += Radio_CheckedChanged;
            this.Controls.Add(radioSingle);

            // RadioButton для пакетной конвертации
            radioBatch = new RadioButton();
            radioBatch.Text = "Пакетная конвертация (несколько файлов)";
            radioBatch.Location = new System.Drawing.Point(20, 50);
            radioBatch.Size = new System.Drawing.Size(250, 20);
            radioBatch.CheckedChanged += Radio_CheckedChanged;
            this.Controls.Add(radioBatch);

            // Кнопка OK
            buttonOk = new Button();
            buttonOk.Text = "OK";
            buttonOk.Location = new System.Drawing.Point(120, 100);
            buttonOk.Size = new System.Drawing.Size(75, 23);
            buttonOk.DialogResult = DialogResult.OK;
            this.Controls.Add(buttonOk);

            // Кнопка Отмена
            buttonCancel = new Button();
            buttonCancel.Text = "Отмена";
            buttonCancel.Location = new System.Drawing.Point(200, 100);
            buttonCancel.Size = new System.Drawing.Size(75, 23);
            buttonCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(buttonCancel);

            this.AcceptButton = buttonOk;
            this.CancelButton = buttonCancel;
        }

        private void Radio_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            if (radio != null && radio.Checked)
            {
                if (radio == radioSingle)
                    SelectedMode = ConversionMode.Single;
                else if (radio == radioBatch)
                    SelectedMode = ConversionMode.Batch;
            }
        }
    }
}
