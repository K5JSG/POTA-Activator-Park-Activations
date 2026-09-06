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
            buttonSaveMap = new Button();
            textBoxWwffDate = new TextBox();
            progressBar1 = new ProgressBar();
            textBoxStatus = new TextBox();
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
            menuStrip1.Size = new Size(1460, 24);
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
            // buttonSaveMap
            //
            buttonSaveMap.Enabled = false;
            buttonSaveMap.Location = new Point(1061, 40);
            buttonSaveMap.Name = "buttonSaveMap";
            buttonSaveMap.Size = new Size(150, 25);
            buttonSaveMap.TabIndex = 2;
            buttonSaveMap.Text = "Save Map...";
            buttonSaveMap.UseVisualStyleBackColor = true;
            buttonSaveMap.Click += buttonSaveMap_Click;
            //
            // textBoxWwffDate
            //
            // A borderless, read-only TextBox rather than a Label - looks the same
            // (colors matched to the form so there's no visible box) but, unlike a
            // Label, its text can be selected and copied to the clipboard.
            textBoxWwffDate.AutoSize = false;
            textBoxWwffDate.BackColor = SystemColors.Control;
            textBoxWwffDate.BorderStyle = BorderStyle.None;
            textBoxWwffDate.ForeColor = SystemColors.ControlText;
            textBoxWwffDate.Location = new Point(901, 70);
            textBoxWwffDate.Name = "textBoxWwffDate";
            textBoxWwffDate.ReadOnly = true;
            textBoxWwffDate.Size = new Size(220, 16);
            textBoxWwffDate.TabIndex = 12;
            textBoxWwffDate.TabStop = false;
            textBoxWwffDate.Text = "WWFF data: not loaded";
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(12, 105);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(1436, 15);
            progressBar1.TabIndex = 2;
            // 
            // textBoxStatus
            //
            // Borderless, read-only TextBox rather than a Label - same reasoning as
            // textBoxWwffDate below: looks identical, but status/result text (park
            // counts, confirmations, error summaries) can now be selected and copied.
            textBoxStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxStatus.AutoSize = false;
            textBoxStatus.BackColor = SystemColors.Control;
            textBoxStatus.BorderStyle = BorderStyle.None;
            textBoxStatus.ForeColor = SystemColors.ControlText;
            textBoxStatus.Location = new Point(12, 125);
            textBoxStatus.Name = "textBoxStatus";
            textBoxStatus.ReadOnly = true;
            textBoxStatus.Size = new Size(1336, 16);
            textBoxStatus.TabIndex = 1;
            textBoxStatus.TabStop = false;
            textBoxStatus.Text = "Ready.";
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
            dataGridView1.Size = new Size(1436, 470);
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
            // Width (both here and MinimumSize below) is sized so the grid
            // never needs a horizontal scrollbar to see every column,
            // including colState (hidden most of the time, but shown for a
            // state with an out-of-state/multi-state Xfer - see
            // buttonLoadParks_Click) - not just at this default size, but at
            // the smallest size the window can be resized down to, so it
            // holds regardless of which state is loaded or how the window
            // gets resized.
            ClientSize = new Size(1460, 639);
            Controls.Add(dataGridView1);
            Controls.Add(textBoxStatus);
            Controls.Add(progressBar1);
            Controls.Add(textBoxWwffDate);
            Controls.Add(buttonSaveMap);
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
            MinimumSize = new Size(1460, 678);
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
        private System.Windows.Forms.Button buttonSaveMap;
        private System.Windows.Forms.TextBox textBoxWwffDate;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.TextBox textBoxStatus;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label labelSearch;
        private System.Windows.Forms.TextBox textBoxSearch;
    }
}
