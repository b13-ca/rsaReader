using CefSharp.WinForms;

namespace rsaReader;

partial class FormMain {
    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
        this.pnlBar = new Panel();
        this.pnlSave = new Panel();
        this.btnSave = new Button();
        this.pnlPreviewButton = new Panel();
        this.btnPreview = new Button();
        this.pnlEditor = new Panel();
        this.txtEditor = new TextBox();
        this.pnlPreview = new Panel();
        this.webPreview = new ChromiumWebBrowser();
        this.lblPreview = new Label();
        this.pnlBar.SuspendLayout();
        this.pnlSave.SuspendLayout();
        this.pnlPreviewButton.SuspendLayout();
        this.pnlEditor.SuspendLayout();
        this.pnlPreview.SuspendLayout();
        this.SuspendLayout();
        // 
        // pnlBar
        // 
        this.pnlBar.Controls.Add(this.pnlSave);
        this.pnlBar.Controls.Add(this.pnlPreviewButton);
        this.pnlBar.Dock = DockStyle.Top;
        this.pnlBar.Location = new Point(0, 0);
        this.pnlBar.Name = "pnlBar";
        this.pnlBar.Size = new Size(1037, 40);
        this.pnlBar.TabIndex = 2;
        // 
        // pnlSave
        // 
        this.pnlSave.Controls.Add(this.btnSave);
        this.pnlSave.Dock = DockStyle.Left;
        this.pnlSave.Location = new Point(0, 0);
        this.pnlSave.Name = "pnlSave";
        this.pnlSave.Size = new Size(104, 40);
        this.pnlSave.TabIndex = 1;
        // 
        // btnSave
        // 
        this.btnSave.Enabled = false;
        this.btnSave.Location = new Point(8, 6);
        this.btnSave.Name = "btnSave";
        this.btnSave.Size = new Size(88, 28);
        this.btnSave.TabIndex = 0;
        this.btnSave.Text = "Save";
        this.btnSave.UseVisualStyleBackColor = true;
        this.btnSave.Click += this.btnSave_Click;
        // 
        // pnlPreviewButton
        // 
        this.pnlPreviewButton.Controls.Add(this.btnPreview);
        this.pnlPreviewButton.Dock = DockStyle.Right;
        this.pnlPreviewButton.Location = new Point(911, 0);
        this.pnlPreviewButton.Name = "pnlPreviewButton";
        this.pnlPreviewButton.Size = new Size(126, 40);
        this.pnlPreviewButton.TabIndex = 0;
        // 
        // btnPreview
        // 
        this.btnPreview.Location = new Point(8, 6);
        this.btnPreview.Name = "btnPreview";
        this.btnPreview.Size = new Size(110, 28);
        this.btnPreview.TabIndex = 0;
        this.btnPreview.Text = "Preview";
        this.btnPreview.UseVisualStyleBackColor = true;
        this.btnPreview.Click += this.btnPreview_Click;
        // 
        // pnlEditor
        // 
        this.pnlEditor.BackColor = SystemColors.Window;
        this.pnlEditor.Controls.Add(this.txtEditor);
        this.pnlEditor.Dock = DockStyle.Fill;
        this.pnlEditor.Location = new Point(0, 0);
        this.pnlEditor.Name = "pnlEditor";
        this.pnlEditor.Padding = new Padding(8, 4, 8, 8);
        this.pnlEditor.Size = new Size(1037, 648);
        this.pnlEditor.TabIndex = 0;
        this.pnlEditor.MouseDown += this.pnlEditor_MouseDown;
        // 
        // txtEditor
        // 
        this.txtEditor.AcceptsReturn = true;
        this.txtEditor.AcceptsTab = true;
        this.txtEditor.BorderStyle = BorderStyle.None;
        this.txtEditor.Dock = DockStyle.Fill;
        this.txtEditor.Font = new Font("Consolas", 11F);
        this.txtEditor.HideSelection = false;
        this.txtEditor.Location = new Point(8, 4);
        this.txtEditor.MaxLength = 0;
        this.txtEditor.Multiline = true;
        this.txtEditor.Name = "txtEditor";
        this.txtEditor.ScrollBars = ScrollBars.Both;
        this.txtEditor.Size = new Size(1021, 636);
        this.txtEditor.TabIndex = 0;
        this.txtEditor.WordWrap = false;
        this.txtEditor.TextChanged += this.txtEditor_TextChanged;
        // 
        // pnlPreview
        // 
        this.pnlPreview.BackColor = Color.White;
        this.pnlPreview.Controls.Add(this.webPreview);
        this.pnlPreview.Controls.Add(this.lblPreview);
        this.pnlPreview.Dock = DockStyle.Fill;
        this.pnlPreview.Enabled = false;
        this.pnlPreview.Location = new Point(0, 0);
        this.pnlPreview.Name = "pnlPreview";
        this.pnlPreview.Size = new Size(1037, 648);
        this.pnlPreview.TabIndex = 1;
        // 
        // webPreview
        // 
        this.webPreview.ActivateBrowserOnCreation = false;
        this.webPreview.Dock = DockStyle.Fill;
        this.webPreview.Location = new Point(0, 0);
        this.webPreview.Name = "webPreview";
        this.webPreview.Size = new Size(1037, 648);
        this.webPreview.TabIndex = 2;
        this.webPreview.Visible = false;
        // 
        // lblPreview
        // 
        this.lblPreview.Dock = DockStyle.Fill;
        this.lblPreview.Location = new Point(0, 0);
        this.lblPreview.Name = "lblPreview";
        this.lblPreview.Size = new Size(1037, 648);
        this.lblPreview.TabIndex = 1;
        this.lblPreview.TextAlign = ContentAlignment.MiddleCenter;
        this.lblPreview.Visible = false;
        // 
        // FormMain
        // 
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1037, 648);
        this.Controls.Add(this.pnlBar);
        this.Controls.Add(this.pnlPreview);
        this.Controls.Add(this.pnlEditor);
        this.MinimumSize = new Size(720, 480);
        this.Name = "FormMain";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "rsaReader";
        this.pnlBar.ResumeLayout(false);
        this.pnlSave.ResumeLayout(false);
        this.pnlPreviewButton.ResumeLayout(false);
        this.pnlEditor.ResumeLayout(false);
        this.pnlEditor.PerformLayout();
        this.pnlPreview.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    #endregion

    private Panel pnlBar;
    private Panel pnlSave;
    private Button btnSave;
    private Panel pnlPreviewButton;
    private Button btnPreview;
    private Panel pnlEditor;
    private TextBox txtEditor;
    private Panel pnlPreview;
    private Label lblPreview;
    private ChromiumWebBrowser webPreview;
}
