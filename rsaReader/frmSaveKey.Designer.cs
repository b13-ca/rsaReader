namespace rsaReader;

partial class FormSaveKey {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing) {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent() {
        this.lblIntro = new Label();
        this.radDefault = new RadioButton();
        this.radPhrase = new RadioButton();
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
        this.lblIntro.Text = "Choose the key used to encrypt this file.";
        // 
        // radDefault
        // 
        this.radDefault.AutoSize = true;
        this.radDefault.Checked = true;
        this.radDefault.Location = new Point(16, 60);
        this.radDefault.Name = "radDefault";
        this.radDefault.Size = new Size(94, 19);
        this.radDefault.TabIndex = 0;
        this.radDefault.TabStop = true;
        this.radDefault.Text = "Default key";
        // 
        // radPhrase
        // 
        this.radPhrase.AutoSize = true;
        this.radPhrase.Location = new Point(16, 88);
        this.radPhrase.Name = "radPhrase";
        this.radPhrase.Size = new Size(88, 19);
        this.radPhrase.TabIndex = 1;
        this.radPhrase.Text = "Passphrase";
        this.radPhrase.CheckedChanged += this.radPhrase_CheckedChanged;
        // 
        // txtPhrase
        // 
        this.txtPhrase.Enabled = false;
        this.txtPhrase.Location = new Point(36, 116);
        this.txtPhrase.Name = "txtPhrase";
        this.txtPhrase.Size = new Size(368, 23);
        this.txtPhrase.TabIndex = 2;
        this.txtPhrase.UseSystemPasswordChar = true;
        // 
        // chkShow
        // 
        this.chkShow.AutoSize = true;
        this.chkShow.Enabled = false;
        this.chkShow.Location = new Point(36, 148);
        this.chkShow.Name = "chkShow";
        this.chkShow.Size = new Size(97, 19);
        this.chkShow.TabIndex = 3;
        this.chkShow.Text = "Show phrase";
        this.chkShow.CheckedChanged += this.chkShow_CheckedChanged;
        // 
        // btnOk
        // 
        this.btnOk.Location = new Point(216, 184);
        this.btnOk.Name = "btnOk";
        this.btnOk.Size = new Size(88, 28);
        this.btnOk.TabIndex = 4;
        this.btnOk.Text = "Save";
        this.btnOk.UseVisualStyleBackColor = true;
        this.btnOk.Click += this.btnOk_Click;
        // 
        // btnCancel
        // 
        this.btnCancel.DialogResult = DialogResult.Cancel;
        this.btnCancel.Location = new Point(312, 184);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new Size(88, 28);
        this.btnCancel.TabIndex = 5;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;
        // 
        // FormSaveKey
        // 
        this.AcceptButton = this.btnOk;
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.CancelButton = this.btnCancel;
        this.ClientSize = new Size(420, 228);
        this.Controls.Add(this.lblIntro);
        this.Controls.Add(this.radDefault);
        this.Controls.Add(this.radPhrase);
        this.Controls.Add(this.txtPhrase);
        this.Controls.Add(this.chkShow);
        this.Controls.Add(this.btnOk);
        this.Controls.Add(this.btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "FormSaveKey";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Save";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Label lblIntro;
    private RadioButton radDefault;
    private RadioButton radPhrase;
    private TextBox txtPhrase;
    private CheckBox chkShow;
    private Button btnOk;
    private Button btnCancel;
}
