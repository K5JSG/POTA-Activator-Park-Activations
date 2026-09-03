namespace PotaActivatorParkActivations
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            labelState = new Label();
            comboBoxState = new ComboBox();
            buttonLoadParks = new Button();
            buttonLoadAdif = new Button();
            buttonExportCsv = new Button();
            buttonExportExcel = new Button();
            buttonShowMap = new Button();
            labelWwffDate = new Label();
            progressBar1 = new ProgressBar();
            labelStatus = new Label();
            dataGridView1 = new DataGridView();
            labelSearch = new Label();
            textBoxSearch = new TextBox();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { helpToolStripMenuItem, aboutToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1244, 24);
            menuStrip1.TabIndex = 10;
            menuStrip1.Text = "menuStrip1";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(52, 20);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "Help";
            helpToolStripMenuItem.Click += helpToolStripMenuItem_Click;
            // 
            // labelState
            // 
            labelState.AutoSize = true;
            labelState.Location = new Point(12, 43);
            labelState.Name = "labelState";
            labelState.Size = new Size(36, 16);
            labelState.TabIndex = 9;
            labelState.Text = "State:";
            // 
            // comboBoxState
            // 
            comboBoxState.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxState.Location = new Point(55, 40);
            comboBoxState.Name = "comboBoxState";
            comboBoxState.Size = new Size(200, 24);
            comboBoxState.TabIndex = 8;
            // 
            // buttonLoadParks
            // 
            buttonLoadParks.Location = new Point(265, 39);
            buttonLoadParks.Name = "buttonLoadParks";
            buttonLoadParks.Size = new Size(150, 25);
            buttonLoadParks.TabIndex = 7;
            buttonLoadParks.Text = "Load Parks for State";
            buttonLoadParks.UseVisualStyleBackColor = true;
            buttonLoadParks.Click += buttonLoadParks_Click;
            // 
            // buttonLoadAdif
            // 
            buttonLoadAdif.Location = new Point(425, 39);
            buttonLoadAdif.Name = "buttonLoadAdif";
            buttonLoadAdif.Size = new Size(150, 25);
            buttonLoadAdif.TabIndex = 6;
            buttonLoadAdif.Text = "Load ADIF File...";
            buttonLoadAdif.UseVisualStyleBackColor = true;
            buttonLoadAdif.Click += buttonLoadAdif_Click;
            // 
            // buttonExportCsv
            // 
            buttonExportCsv.Location = new Point(585, 39);
            buttonExportCsv.Name = "buttonExportCsv";
            buttonExportCsv.Size = new Size(150, 25);
            buttonExportCsv.TabIndex = 5;
            buttonExportCsv.Text = "Export CSV...";
            buttonExportCsv.UseVisualStyleBackColor = true;
            buttonExportCsv.Click += buttonExportCsv_Click;
            // 
            // buttonExportExcel
            // 
            buttonExportExcel.Location = new Point(745, 39);
            buttonExportExcel.Name = "buttonExportExcel";
            buttonExportExcel.Size = new Size(150, 25);
            buttonExportExcel.TabIndex = 4;
            buttonExportExcel.Text = "Export Excel...";
            buttonExportExcel.UseVisualStyleBackColor = true;
            buttonExportExcel.Click += buttonExportExcel_Click;
            // 
            // buttonShowMap
            // 
            buttonShowMap.Location = new Point(901, 40);
            buttonShowMap.Name = "buttonShowMap";
            buttonShowMap.Size = new Size(150, 25);
            buttonShowMap.TabIndex = 3;
            buttonShowMap.Text = "Show Map...";
            buttonShowMap.UseVisualStyleBackColor = true;
            buttonShowMap.Click += buttonShowMap_Click;
            // 
            // labelWwffDate
            // 
            labelWwffDate.AutoSize = true;
            labelWwffDate.Location = new Point(901, 70);
            labelWwffDate.Name = "labelWwffDate";
            labelWwffDate.Size = new Size(130, 16);
            labelWwffDate.TabIndex = 12;
            labelWwffDate.Text = "WWFF data: not loaded";
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(12, 105);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(1220, 15);
            progressBar1.TabIndex = 2;
            // 
            // labelStatus
            // 
            labelStatus.AutoSize = true;
            labelStatus.Location = new Point(12, 125);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(42, 16);
            labelStatus.TabIndex = 1;
            labelStatus.Text = "Ready.";
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridView1.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText;
            dataGridView1.Location = new Point(12, 150);
            dataGridView1.MultiSelect = true;
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersWidth = 25;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dataGridView1.Size = new Size(1220, 470);
            dataGridView1.TabIndex = 0;
            // 
            // labelSearch
            // 
            labelSearch.AutoSize = true;
            labelSearch.Location = new Point(12, 75);
            labelSearch.Name = "labelSearch";
            labelSearch.Size = new Size(45, 16);
            labelSearch.TabIndex = 10;
            labelSearch.Text = "Search:";
            // 
            // textBoxSearch
            // 
            textBoxSearch.Enabled = false;
            textBoxSearch.Location = new Point(65, 72);
            textBoxSearch.Name = "textBoxSearch";
            textBoxSearch.Size = new Size(300, 23);
            textBoxSearch.TabIndex = 11;
            textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            // 
            // Form1
            // 
            ClientSize = new Size(1244, 639);
            Controls.Add(dataGridView1);
            Controls.Add(labelStatus);
            Controls.Add(progressBar1);
            Controls.Add(labelWwffDate);
            Controls.Add(buttonShowMap);
            Controls.Add(buttonExportExcel);
            Controls.Add(buttonExportCsv);
            Controls.Add(buttonLoadAdif);
            Controls.Add(buttonLoadParks);
            Controls.Add(comboBoxState);
            Controls.Add(labelState);
            Controls.Add(labelSearch);
            Controls.Add(textBoxSearch);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(970, 678);
            Name = "Form1";
            Text = "POTA Activator Park Activations";
            Load += Form1_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.Label labelState;
        private System.Windows.Forms.ComboBox comboBoxState;
        private System.Windows.Forms.Button buttonLoadParks;
        private System.Windows.Forms.Button buttonLoadAdif;
        private System.Windows.Forms.Button buttonExportCsv;
        private System.Windows.Forms.Button buttonExportExcel;
        private System.Windows.Forms.Button buttonShowMap;
        private System.Windows.Forms.Label labelWwffDate;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label labelSearch;
        private System.Windows.Forms.TextBox textBoxSearch;
    }
}
