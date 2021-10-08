
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(roomsInformationForm));
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.btnCeiling = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.btnFloor = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnAllRooms = new System.Windows.Forms.Button();
            this.btnSelectNone = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView1.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column1,
            this.Column2});
            this.dataGridView1.Location = new System.Drawing.Point(52, 38);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 72;
            this.dataGridView1.RowTemplate.Height = 31;
            this.dataGridView1.Size = new System.Drawing.Size(855, 1256);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.MultiSelectChanged += new System.EventHandler(this.dataGridView1_MultiSelectChanged);
            this.dataGridView1.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView1_CellMouseDown);
            this.dataGridView1.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridView1_CurrentCellDirtyStateChanged);
            this.dataGridView1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.dataGridView1_MouseDown);
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
            this.Column2.FalseValue = "0";
            this.Column2.HeaderText = "Select Rooms";
            this.Column2.IndeterminateValue = "";
            this.Column2.MinimumWidth = 9;
            this.Column2.Name = "Column2";
            this.Column2.TrueValue = "1";
            // 
            // btnCeiling
            // 
            this.btnCeiling.Location = new System.Drawing.Point(36, 55);
            this.btnCeiling.Name = "btnCeiling";
            this.btnCeiling.Size = new System.Drawing.Size(214, 87);
            this.btnCeiling.TabIndex = 1;
            this.btnCeiling.Text = "CEILING";
            this.btnCeiling.UseVisualStyleBackColor = true;
            this.btnCeiling.Click += new System.EventHandler(this.btnCeiling_Click);
            // 
            // btnExit
            // 
            this.btnExit.Location = new System.Drawing.Point(971, 1200);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(214, 94);
            this.btnExit.TabIndex = 2;
            this.btnExit.Text = "EXIT";
            this.btnExit.UseVisualStyleBackColor = true;
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
            // 
            // btnFloor
            // 
            this.btnFloor.Location = new System.Drawing.Point(971, 205);
            this.btnFloor.Name = "btnFloor";
            this.btnFloor.Size = new System.Drawing.Size(214, 88);
            this.btnFloor.TabIndex = 3;
            this.btnFloor.Text = "FLOOR";
            this.btnFloor.UseVisualStyleBackColor = true;
            this.btnFloor.Click += new System.EventHandler(this.btnFloor_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Controls.Add(this.btnCeiling);
            this.groupBox1.Location = new System.Drawing.Point(935, 38);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(274, 417);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "placement";
            // 
            // btnAllRooms
            // 
            this.btnAllRooms.Location = new System.Drawing.Point(971, 658);
            this.btnAllRooms.Name = "btnAllRooms";
            this.btnAllRooms.Size = new System.Drawing.Size(214, 88);
            this.btnAllRooms.TabIndex = 5;
            this.btnAllRooms.Text = "SELECT ALL";
            this.btnAllRooms.UseVisualStyleBackColor = true;
            this.btnAllRooms.Click += new System.EventHandler(this.btnAllRooms_Click);
            // 
            // btnSelectNone
            // 
            this.btnSelectNone.Location = new System.Drawing.Point(971, 772);
            this.btnSelectNone.Name = "btnSelectNone";
            this.btnSelectNone.Size = new System.Drawing.Size(214, 88);
            this.btnSelectNone.TabIndex = 6;
            this.btnSelectNone.Text = "SELECT NONE";
            this.btnSelectNone.UseVisualStyleBackColor = true;
            this.btnSelectNone.Click += new System.EventHandler(this.btnSelectNone_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Location = new System.Drawing.Point(935, 609);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(274, 285);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "select";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(36, 287);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(214, 87);
            this.button1.TabIndex = 2;
            this.button1.Text = "WALL";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // roomsInformationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1235, 1331);
            this.Controls.Add(this.btnSelectNone);
            this.Controls.Add(this.btnAllRooms);
            this.Controls.Add(this.btnFloor);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "roomsInformationForm";
            this.Text = "RoomInfoForm";
            this.Load += new System.EventHandler(this.roomsInformationForm_Load);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.roomsInformationForm_MouseDown);
            this.MouseEnter += new System.EventHandler(this.roomsInformationForm_MouseEnter);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.roomsInformationForm_MouseMove);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnCeiling;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.Button btnFloor;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Column2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnAllRooms;
        private System.Windows.Forms.Button btnSelectNone;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.GroupBox groupBox2;
    }
}