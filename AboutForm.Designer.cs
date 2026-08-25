namespace POTA_Check
{
    partial class AboutForm
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
            this.labelAppName = new System.Windows.Forms.Label();
            this.labelVersion = new System.Windows.Forms.Label();
            this.labelPublished = new System.Windows.Forms.Label();
            this.textBoxLicense = new System.Windows.Forms.TextBox();
            this.buttonOk = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelAppName
            //
            this.labelAppName.AutoSize = true;
            this.labelAppName.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelAppName.Location = new System.Drawing.Point(20, 20);
            this.labelAppName.Name = "labelAppName";
            this.labelAppName.Size = new System.Drawing.Size(340, 27);
            this.labelAppName.Text = "POTA Activator Park Activations";
            //
            // labelVersion
            //
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(22, 62);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(90, 15);
            this.labelVersion.Text = "Version 0.0.0";
            //
            // labelPublished
            //
            this.labelPublished.AutoSize = true;
            this.labelPublished.Location = new System.Drawing.Point(22, 82);
            this.labelPublished.Name = "labelPublished";
            this.labelPublished.Size = new System.Drawing.Size(90, 15);
            this.labelPublished.Text = "Published: ";
            //
            // textBoxLicense
            //
            this.textBoxLicense.Location = new System.Drawing.Point(22, 108);
            this.textBoxLicense.Multiline = true;
            this.textBoxLicense.Name = "textBoxLicense";
            this.textBoxLicense.ReadOnly = true;
            this.textBoxLicense.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxLicense.Size = new System.Drawing.Size(440, 150);
            this.textBoxLicense.TabIndex = 3;
            //
            // buttonOk
            //
            this.buttonOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOk.Location = new System.Drawing.Point(387, 272);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Size = new System.Drawing.Size(75, 25);
            this.buttonOk.Text = "OK";
            this.buttonOk.UseVisualStyleBackColor = true;
            //
            // AboutForm
            //
            this.AcceptButton = this.buttonOk;
            this.ClientSize = new System.Drawing.Size(484, 310);
            this.Controls.Add(this.buttonOk);
            this.Controls.Add(this.textBoxLicense);
            this.Controls.Add(this.labelPublished);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.labelAppName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About POTA Activator Park Activations";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label labelAppName;
        private System.Windows.Forms.Label labelVersion;
        private System.Windows.Forms.Label labelPublished;
        private System.Windows.Forms.TextBox textBoxLicense;
        private System.Windows.Forms.Button buttonOk;
    }
}