namespace rsaReader;

partial class FormFileKey {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing) {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent() {
        this.lblIntro = new Label();
        this.txtPhrase = new TextBox();
        this.chkShow = new CheckBox();
        this.btnOk = new Button();
        this.btnCancel = new Button();
        this.SuspendLayout();
        // 
        // lblIntro
        // 
        this.lblIntro.AutoSize = false;
        this.lblIntro.Location = new Point(16, 16);
        this.lblIntro.Name = "lblIntro";
        this.lblIntro.Size = new Size(388, 36);
        this.lblIntro.Text = "Enter the passphrase for this file.";
        // 
        // txtPhrase
        // 
        this.txtPhrase.Location = new Point(16, 60);
        this.txtPhrase.Name = "txtPhrase";
        this.txtPhrase.Size = new Size(388, 23);
        this.txtPhrase.TabIndex = 0;
        this.txtPhrase.UseSystemPasswordChar = true;
        // 
        // chkShow
        // 
        this.chkShow.AutoSize = true;
        this.chkShow.Location = new Point(16, 92);
        this.chkShow.Name = "chkShow";
        this.chkShow.Size = new Size(97, 19);
        this.chkShow.TabIndex = 1;
        this.chkShow.Text = "Show phrase";
        this.chkShow.CheckedChanged += this.chkShow_CheckedChanged;
        // 
        // btnOk
        // 
        this.btnOk.Location = new Point(216, 132);
        this.btnOk.Name = "btnOk";
        this.btnOk.Size = new Size(88, 28);
        this.btnOk.TabIndex = 2;
        this.btnOk.Text = "Open";
        this.btnOk.UseVisualStyleBackColor = true;
        this.btnOk.Click += this.btnOk_Click;
        // 
        // btnCancel
        // 
        this.btnCancel.DialogResult = DialogResult.Cancel;
        this.btnCancel.Location = new Point(312, 132);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new Size(88, 28);
        this.btnCancel.TabIndex = 3;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;
        // 
        // FormFileKey
        // 
        this.AcceptButton = this.btnOk;
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.CancelButton = this.btnCancel;
        this.ClientSize = new Size(420, 176);
        this.Controls.Add(this.lblIntro);
        this.Controls.Add(this.txtPhrase);
        this.Controls.Add(this.chkShow);
        this.Controls.Add(this.btnOk);
        this.Controls.Add(this.btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "FormFileKey";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Open .rsa";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Label lblIntro;
    private TextBox txtPhrase;
    private CheckBox chkShow;
    private Button btnOk;
    private Button btnCancel;
}
