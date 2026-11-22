namespace rsaReader;

partial class FormMain {
    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
        this.pnlChrome = new Panel();
        this.pnlBar = new Panel();
        this.pnlSave = new Panel();
        this.btnLoad = new Button();
        this.btnSave = new Button();
        this.btnTheme = new Button();
        this.pnlModes = new Panel();
        this.btnEdit = new Button();
        this.btnReal = new Button();
        this.pnlTools = new Panel();
        this.btnBold = new Button();
        this.btnH1 = new Button();
        this.btnH2 = new Button();
        this.btnBullet = new Button();
        this.btnLink = new Button();
        this.btnImage = new Button();
        this.btnCode = new Button();
        this.btnBreak = new Button();
        this.pnlPreview = new Panel();
        this.rtbPreview = new RichTextBox();
        this.pnlChrome.SuspendLayout();
        this.pnlBar.SuspendLayout();
        this.pnlSave.SuspendLayout();
        this.pnlModes.SuspendLayout();
        this.pnlTools.SuspendLayout();
        this.pnlPreview.SuspendLayout();
        this.SuspendLayout();
        // 
        // pnlChrome
        // 
        this.pnlChrome.Controls.Add(this.pnlBar);
        this.pnlChrome.Controls.Add(this.pnlTools);
        this.pnlChrome.Dock = DockStyle.Top;
        this.pnlChrome.Location = new Point(0, 0);
        this.pnlChrome.Name = "pnlChrome";
        this.pnlChrome.Size = new Size(1037, 80);
        this.pnlChrome.TabIndex = 2;
        // 
        // pnlBar
        // 
        this.pnlBar.Controls.Add(this.pnlSave);
        this.pnlBar.Controls.Add(this.pnlModes);
        this.pnlBar.Dock = DockStyle.Top;
        this.pnlBar.Location = new Point(0, 0);
        this.pnlBar.Name = "pnlBar";
        this.pnlBar.Size = new Size(1037, 40);
        this.pnlBar.TabIndex = 0;
        // 
        // pnlSave
        // 
        this.pnlSave.Controls.Add(this.btnLoad);
        this.pnlSave.Controls.Add(this.btnSave);
        this.pnlSave.Controls.Add(this.btnTheme);
        this.pnlSave.Dock = DockStyle.Left;
        this.pnlSave.Location = new Point(0, 0);
        this.pnlSave.Name = "pnlSave";
        this.pnlSave.Size = new Size(288, 40);
        this.pnlSave.TabIndex = 1;
        // 
        // btnLoad
        // 
        this.btnLoad.Location = new Point(8, 6);
        this.btnLoad.Name = "btnLoad";
        this.btnLoad.Size = new Size(88, 28);
        this.btnLoad.TabIndex = 0;
        this.btnLoad.Text = "Load";
        this.btnLoad.UseVisualStyleBackColor = true;
        this.btnLoad.Click += this.btnLoad_Click;
        // 
        // btnSave
        // 
        this.btnSave.Enabled = false;
        this.btnSave.Location = new Point(104, 6);
        this.btnSave.Name = "btnSave";
        this.btnSave.Size = new Size(88, 28);
        this.btnSave.TabIndex = 1;
        this.btnSave.Text = "Save";
        this.btnSave.UseVisualStyleBackColor = true;
        this.btnSave.Click += this.btnSave_Click;
        // 
        // btnTheme
        // 
        this.btnTheme.Location = new Point(200, 6);
        this.btnTheme.Name = "btnTheme";
        this.btnTheme.Size = new Size(80, 28);
        this.btnTheme.TabIndex = 2;
        this.btnTheme.Text = "WHITE";
        this.btnTheme.UseVisualStyleBackColor = true;
        this.btnTheme.Click += this.btnTheme_Click;
        // 
        // pnlModes
        // 
        this.pnlModes.Controls.Add(this.btnEdit);
        this.pnlModes.Controls.Add(this.btnReal);
        this.pnlModes.Dock = DockStyle.Right;
        this.pnlModes.Location = new Point(853, 0);
        this.pnlModes.Name = "pnlModes";
        this.pnlModes.Size = new Size(184, 40);
        this.pnlModes.TabIndex = 0;
        // 
        // btnEdit
        // 
        this.btnEdit.Location = new Point(8, 6);
        this.btnEdit.Name = "btnEdit";
        this.btnEdit.Size = new Size(80, 28);
        this.btnEdit.TabIndex = 0;
        this.btnEdit.Text = "Edit";
        this.btnEdit.UseVisualStyleBackColor = false;
        this.btnEdit.Click += this.btnEdit_Click;
        // 
        // btnReal
        // 
        this.btnReal.Location = new Point(96, 6);
        this.btnReal.Name = "btnReal";
        this.btnReal.Size = new Size(80, 28);
        this.btnReal.TabIndex = 1;
        this.btnReal.Text = "Real";
        this.btnReal.UseVisualStyleBackColor = false;
        this.btnReal.Click += this.btnReal_Click;
        // 
        // pnlTools
        // 
        this.pnlTools.Controls.Add(this.btnBold);
        this.pnlTools.Controls.Add(this.btnH1);
        this.pnlTools.Controls.Add(this.btnH2);
        this.pnlTools.Controls.Add(this.btnBullet);
        this.pnlTools.Controls.Add(this.btnLink);
        this.pnlTools.Controls.Add(this.btnImage);
        this.pnlTools.Controls.Add(this.btnCode);
        this.pnlTools.Controls.Add(this.btnBreak);
        this.pnlTools.Dock = DockStyle.Bottom;
        this.pnlTools.Enabled = false;
        this.pnlTools.Location = new Point(0, 40);
        this.pnlTools.Name = "pnlTools";
        this.pnlTools.Size = new Size(1037, 40);
        this.pnlTools.TabIndex = 1;
        this.pnlTools.Paint += this.pnlTools_Paint;
        // 
        // btnBold
        // 
        this.btnBold.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.btnBold.Location = new Point(8, 6);
        this.btnBold.Name = "btnBold";
        this.btnBold.Size = new Size(40, 28);
        this.btnBold.TabIndex = 0;
        this.btnBold.Text = "B";
        this.btnBold.UseVisualStyleBackColor = true;
        this.btnBold.Click += this.btnBold_Click;
        // 
        // btnH1
        // 
        this.btnH1.Location = new Point(52, 6);
        this.btnH1.Name = "btnH1";
        this.btnH1.Size = new Size(44, 28);
        this.btnH1.TabIndex = 1;
        this.btnH1.Text = "H1";
        this.btnH1.UseVisualStyleBackColor = true;
        this.btnH1.Click += this.btnH1_Click;
        // 
        // btnH2
        // 
        this.btnH2.Location = new Point(100, 6);
        this.btnH2.Name = "btnH2";
        this.btnH2.Size = new Size(44, 28);
        this.btnH2.TabIndex = 2;
        this.btnH2.Text = "H2";
        this.btnH2.UseVisualStyleBackColor = true;
        this.btnH2.Click += this.btnH2_Click;
        // 
        // btnBullet
        // 
        this.btnBullet.Location = new Point(148, 6);
        this.btnBullet.Name = "btnBullet";
        this.btnBullet.Size = new Size(40, 28);
        this.btnBullet.TabIndex = 3;
        this.btnBullet.Text = "•";
        this.btnBullet.UseVisualStyleBackColor = true;
        this.btnBullet.Click += this.btnBullet_Click;
        // 
        // btnLink
        // 
        this.btnLink.Location = new Point(192, 6);
        this.btnLink.Name = "btnLink";
        this.btnLink.Size = new Size(40, 28);
        this.btnLink.TabIndex = 4;
        this.btnLink.Text = "A";
        this.btnLink.UseVisualStyleBackColor = true;
        this.btnLink.Click += this.btnLink_Click;
        // 
        // btnImage
        // 
        this.btnImage.Font = new Font("Segoe MDL2 Assets", 11F);
        this.btnImage.Location = new Point(236, 6);
        this.btnImage.Name = "btnImage";
        this.btnImage.Size = new Size(40, 28);
        this.btnImage.TabIndex = 5;
        this.btnImage.Text = "";
        this.btnImage.UseVisualStyleBackColor = true;
        this.btnImage.Click += this.btnImage_Click;
        // 
        // btnCode
        // 
        this.btnCode.Location = new Point(280, 6);
        this.btnCode.Name = "btnCode";
        this.btnCode.Size = new Size(56, 28);
        this.btnCode.TabIndex = 6;
        this.btnCode.Text = "```";
        this.btnCode.UseVisualStyleBackColor = true;
        this.btnCode.Click += this.btnCode_Click;
        // 
        // btnBreak
        // 
        this.btnBreak.Location = new Point(340, 6);
        this.btnBreak.Name = "btnBreak";
        this.btnBreak.Size = new Size(44, 28);
        this.btnBreak.TabIndex = 7;
        this.btnBreak.Text = "br";
        this.btnBreak.UseVisualStyleBackColor = true;
        this.btnBreak.Click += this.btnBreak_Click;
        // 
        // pnlPreview
        // 
        this.pnlPreview.BackColor = Color.White;
        this.pnlPreview.Controls.Add(this.rtbPreview);
        this.pnlPreview.Dock = DockStyle.Fill;
        this.pnlPreview.Location = new Point(0, 0);
        this.pnlPreview.Name = "pnlPreview";
        this.pnlPreview.Padding = new Padding(16, 12, 16, 16);
        this.pnlPreview.Size = new Size(1037, 648);
        this.pnlPreview.TabIndex = 0;
        // 
        // rtbPreview
        // 
        this.rtbPreview.AcceptsTab = true;
        this.rtbPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.rtbPreview.BackColor = Color.White;
        this.rtbPreview.BorderStyle = BorderStyle.None;
        this.rtbPreview.DetectUrls = false;
        this.rtbPreview.Font = new Font("Consolas", 11F);
        this.rtbPreview.HideSelection = false;
        this.rtbPreview.Location = new Point(12, 89);
        this.rtbPreview.Name = "rtbPreview";
        this.rtbPreview.Size = new Size(1013, 550);
        this.rtbPreview.TabIndex = 0;
        this.rtbPreview.Text = "";
        this.rtbPreview.WordWrap = false;
        this.rtbPreview.KeyDown += this.rtbPreview_KeyDown;
        this.rtbPreview.TextChanged += this.rtbPreview_TextChanged;
        // 
        // FormMain
        // 
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1037, 648);
        this.Controls.Add(this.pnlChrome);
        this.Controls.Add(this.pnlPreview);
        this.MinimumSize = new Size(720, 480);
        this.Name = "FormMain";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "rsaReader";
        this.pnlChrome.ResumeLayout(false);
        this.pnlBar.ResumeLayout(false);
        this.pnlSave.ResumeLayout(false);
        this.pnlModes.ResumeLayout(false);
        this.pnlTools.ResumeLayout(false);
        this.pnlPreview.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    #endregion

    private Panel pnlChrome;
    private Panel pnlBar;
    private Panel pnlSave;
    private Button btnLoad;
    private Button btnSave;
    private Button btnTheme;
    private Panel pnlModes;
    private Button btnEdit;
    private Button btnReal;
    private Panel pnlTools;
    private Button btnBold;
    private Button btnH1;
    private Button btnH2;
    private Button btnBullet;
    private Button btnLink;
    private Button btnImage;
    private Button btnCode;
    private Button btnBreak;
    private Panel pnlPreview;
    private RichTextBox rtbPreview;
}
