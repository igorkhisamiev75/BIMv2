
namespace CreateElementsInRooms
{
    partial class roomsInformationForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.btnCeiling = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.btnFloor = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView1.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column1,
            this.Column2});
            this.dataGridView1.Location = new System.Drawing.Point(84, 50);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 72;
            this.dataGridView1.RowTemplate.Height = 31;
            this.dataGridView1.Size = new System.Drawing.Size(855, 1256);
            this.dataGridView1.TabIndex = 0;
            // 
            // Column1
            // 
            this.Column1.HeaderText = "Room Name";
            this.Column1.MinimumWidth = 9;
            this.Column1.Name = "Column1";
            this.Column1.ReadOnly = true;
            // 
            // Column2
            // 
            this.Column2.HeaderText = "Select Rooms";
            this.Column2.MinimumWidth = 9;
            this.Column2.Name = "Column2";
            // 
            // btnCeiling
            // 
            this.btnCeiling.Location = new System.Drawing.Point(1019, 50);
            this.btnCeiling.Name = "btnCeiling";
            this.btnCeiling.Size = new System.Drawing.Size(214, 87);
            this.btnCeiling.TabIndex = 1;
            this.btnCeiling.Text = "CEILING";
            this.btnCeiling.UseVisualStyleBackColor = true;
            this.btnCeiling.Click += new System.EventHandler(this.btnCeiling_Click);
            // 
            // btnExit
            // 
            this.btnExit.Location = new System.Drawing.Point(1019, 385);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(214, 94);
            this.btnExit.TabIndex = 2;
            this.btnExit.Text = "EXIT";
            this.btnExit.UseVisualStyleBackColor = true;
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
            // 
            // btnFloor
            // 
            this.btnFloor.Location = new System.Drawing.Point(1019, 217);
            this.btnFloor.Name = "btnFloor";
            this.btnFloor.Size = new System.Drawing.Size(214, 88);
            this.btnFloor.TabIndex = 3;
            this.btnFloor.Text = "FLOOR";
            this.btnFloor.UseVisualStyleBackColor = true;
            this.btnFloor.Click += new System.EventHandler(this.btnFloor_Click);
            // 
            // roomsInformationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1287, 1505);
            this.Controls.Add(this.btnFloor);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.btnCeiling);
            this.Controls.Add(this.dataGridView1);
            this.Name = "roomsInformationForm";
            this.Text = "RoomInfoForm";
            this.Load += new System.EventHandler(this.roomsInformationForm_Load);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.roomsInformationForm_MouseDown);
            this.MouseEnter += new System.EventHandler(this.roomsInformationForm_MouseEnter);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.roomsInformationForm_MouseMove);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnCeiling;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Column2;
        private System.Windows.Forms.Button btnFloor;
    }
}