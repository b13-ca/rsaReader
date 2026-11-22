using System.Diagnostics;

namespace rsaReader;

public partial class FormMain : Form {
    private enum ViewMode {
        Edit,
        Preview,
        Real
    }

    private ViewMode _mode;
    private string? _documentPath;
    private string? _keyHex;
    private string? _aesKeyHex;
    private string _savedText = "";
    private bool _suppressDirty;
    private bool _suppressRich;
    private bool _preparedPreview;

    public FormMain() {
        InitializeComponent();
        PrepareEditor();
    }

    public FormMain(string? documentPath, string? data, string? keyHex, string? aesKeyHex) {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(documentPath) && data is not null) {
            _keyHex = keyHex;
            _aesKeyHex = aesKeyHex;
            PreparePreview(data, documentPath);
        }
        else
            PrepareEditor(documentPath);
    }

    internal void ShowPrepared() {
        if (_preparedPreview)
            ShowPreview();
        else
            ShowEditor();
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        if (_mode == ViewMode.Edit)
            FocusEditor(true);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        if (keyData == (Keys.Control | Keys.O)) {
            btnLoad.PerformClick();
            return true;
        }

        if (keyData == (Keys.Control | Keys.S)) {
            btnSave.PerformClick();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        _keyHex = null;
        _aesKeyHex = null;
        AppSession.Clear();
        base.OnFormClosed(e);
    }

    private void btnSave_Click(object sender, EventArgs e) {
        FlushRich();
        TrySave();
    }

    private void btnLoad_Click(object sender, EventArgs e) {
        FlushRich();
        if (!ConfirmReplace())
            return;

        using var dialog = new OpenFileDialog {
            Filter = "rsaReader (*.rsa)|*.rsa",
            DefaultExt = "rsa",
            CheckFileExists = true
        };
        if (_documentPath is not null) {
            var directory = Path.GetDirectoryName(_documentPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                dialog.InitialDirectory = directory;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        OpenDocument(dialog.FileName);
    }

    private bool ConfirmReplace() {
        if (!btnSave.Enabled)
            return true;

        var choice = MessageBox.Show(
            this,
            "Save changes before loading a file?",
            "rsaReader",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (choice == DialogResult.Cancel)
            return false;
        if (choice == DialogResult.No)
            return true;

        return TrySave();
    }

    private void OpenDocument(string path) {
        if (File.Exists(path) && new FileInfo(path).Length == 0) {
            OpenEmpty(path);
            return;
        }

        var opened = ReadDocument(this, path);
        if (opened is null)
            return;

        _keyHex = opened.Value.KeyHex;
        _aesKeyHex = opened.Value.AesKeyHex;
        PreparePreview(opened.Value.Data, path);
        ShowPreview();
    }

    private void OpenEmpty(string path) {
        _keyHex = null;
        _aesKeyHex = null;
        _suppressDirty = true;
        txtEditor.Clear();
        _savedText = "";
        _suppressDirty = false;
        btnSave.Enabled = false;
        _documentPath = path;
        _preparedPreview = false;
        Text = "rsaReader - " + Path.GetFileName(path);
        ShowEditor();
    }

    internal static (string Data, string KeyHex, string AesKeyHex)? ReadDocument(IWin32Window? owner, string path) {
        RsaFile.Package package;
        try {
            package = RsaFile.Read(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) {
            ShowReadError(owner, ex.Message);
            return null;
        }

        var keyHex = package.Key ?? "";
        string aesKeyHex;
        try {
            if (PhraseHash.HexEquals(keyHex, KeyStore.ReadKey()))
                aesKeyHex = AppSession.DecryptPassword;
            else {
                var phrase = FormFileKey.Prompt(owner!, keyHex, Path.GetFileName(path));
                if (phrase is null)
                    return null;
                aesKeyHex = PhraseHash.DecryptPassword(phrase);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
            ShowReadError(owner, ex.Message);
            return null;
        }

        try {
            return (RsaFile.Decrypt(package, aesKeyHex), keyHex, aesKeyHex);
        }
        catch (InvalidDataException ex) {
            ShowReadError(owner, ex.Message);
            return null;
        }
    }

    private static void ShowReadError(IWin32Window? owner, string message) {
        if (owner is null)
            MessageBox.Show(message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
        else
            MessageBox.Show(owner, message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private bool TrySave() {
        string keyHex;
        string aesKeyHex;
        if ((ModifierKeys & Keys.Shift) == Keys.Shift || _keyHex is null || _aesKeyHex is null) {
            var choice = FormSaveKey.Prompt(this);
            if (choice is null)
                return false;

            try {
                if (choice.Value.UseDefault) {
                    keyHex = KeyStore.ReadKey();
                    aesKeyHex = AppSession.DecryptPassword;
                }
                else {
                    keyHex = PhraseHash.Of(choice.Value.Phrase);
                    aesKeyHex = PhraseHash.DecryptPassword(choice.Value.Phrase);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
                MessageBox.Show(this, ex.Message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        else {
            keyHex = _keyHex;
            aesKeyHex = _aesKeyHex;
        }

        var path = _documentPath;
        if (path is null) {
            using var dialog = new SaveFileDialog {
                Filter = "rsaReader (*.rsa)|*.rsa",
                DefaultExt = "rsa",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;
            path = EnsureRsaExtension(dialog.FileName);
        }

        try {
            RsaFile.Write(path, keyHex, aesKeyHex, txtEditor.Text);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) {
            MessageBox.Show(this, ex.Message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _keyHex = keyHex;
        _aesKeyHex = aesKeyHex;
        MarkSaved(path);
        return true;
    }

    private void MarkSaved(string path) {
        _savedText = txtEditor.Text;
        _documentPath = path;
        btnSave.Enabled = false;
        Text = "rsaReader - " + Path.GetFileName(path);
    }

    private static string EnsureRsaExtension(string path) {
        if (path.EndsWith(".rsa", StringComparison.OrdinalIgnoreCase))
            return path;
        return path + ".rsa";
    }

    private void PrepareEditor(string? path = null) {
        _preparedPreview = false;
        if (string.IsNullOrWhiteSpace(path))
            return;

        _documentPath = path;
        _savedText = "";
        Text = "rsaReader - " + Path.GetFileName(path);
    }

    private void PreparePreview(string markdown, string path) {
        _preparedPreview = true;
        _suppressDirty = true;
        txtEditor.Text = markdown;
        _savedText = txtEditor.Text;
        _documentPath = path;
        btnSave.Enabled = false;
        Text = "rsaReader - " + Path.GetFileName(path);
        _suppressDirty = false;
    }

    private void btnEdit_Click(object sender, EventArgs e) {
        if (_mode == ViewMode.Edit)
            return;
        FlushRich();
        ShowEditor();
    }

    private void btnPreview_Click(object sender, EventArgs e) {
        if (_mode == ViewMode.Preview)
            return;
        FlushRich();
        ShowPreview();
    }

    private void btnReal_Click(object sender, EventArgs e) {
        if (_mode != ViewMode.Real)
            ShowReal();
    }

    private void ShowPreview() {
        _mode = ViewMode.Preview;
        ApplyMode();
        RenderRich();
    }

    private void ShowEditor() {
        _mode = ViewMode.Edit;
        ApplyMode();
        FocusEditor(true);
    }

    private void ShowReal() {
        var fromEdit = _mode == ViewMode.Edit;
        _mode = ViewMode.Real;
        ApplyMode();
        if (fromEdit)
            RenderRich();
        rtbPreview.Focus();
    }

    private void ApplyMode() {
        var edit = _mode == ViewMode.Edit;
        pnlTools.Enabled = _mode == ViewMode.Real;
        pnlEditor.Enabled = edit;
        pnlPreview.Enabled = !edit;
        rtbPreview.ReadOnly = _mode != ViewMode.Real;
        rtbPreview.BackColor = Color.White;
        if (edit) {
            splitView.Panel1Collapsed = false;
            splitView.Panel2Collapsed = true;
        }
        else {
            splitView.Panel2Collapsed = false;
            splitView.Panel1Collapsed = true;
        }

        StyleModeButton(btnEdit, edit);
        StyleModeButton(btnPreview, _mode == ViewMode.Preview);
        StyleModeButton(btnReal, _mode == ViewMode.Real);
    }

    private static void StyleModeButton(Button button, bool active) {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = active ? Color.FromArgb(70, 120, 180) : SystemColors.ControlDark;
        button.FlatAppearance.BorderSize = active ? 2 : 1;
        button.BackColor = active ? Color.FromArgb(220, 232, 246) : SystemColors.Control;
    }

    private void FocusEditor(bool moveToStart) {
        txtEditor.Focus();
        if (!moveToStart)
            return;

        txtEditor.SelectionStart = 0;
        txtEditor.SelectionLength = 0;
        if (IsHandleCreated)
            BeginInvoke(ClearEditorSelection);
    }

    private void ClearEditorSelection() {
        if (_mode != ViewMode.Edit)
            return;

        txtEditor.SelectionStart = 0;
        txtEditor.SelectionLength = 0;
    }

    private void pnlTools_Paint(object sender, PaintEventArgs e) {
        using var pen = new Pen(SystemColors.ControlDark);
        e.Graphics.DrawLine(pen, 0, 0, pnlTools.Width, 0);
    }

    private void btnBold_Click(object sender, EventArgs e) {
        ToggleStyle(FontStyle.Bold);
    }

    private void btnH1_Click(object sender, EventArgs e) {
        FormatParagraph(22f, FontStyle.Bold, false);
    }

    private void btnH2_Click(object sender, EventArgs e) {
        FormatParagraph(16f, FontStyle.Bold, false);
    }

    private void btnBullet_Click(object sender, EventArgs e) {
        if (_mode != ViewMode.Real)
            return;
        rtbPreview.SelectionBullet = !rtbPreview.SelectionBullet;
        rtbPreview.Focus();
    }

    private void btnLink_Click(object sender, EventArgs e) {
        InsertRichTarget(false);
    }

    private void btnImage_Click(object sender, EventArgs e) {
        InsertRichTarget(true);
    }

    private void btnCode_Click(object sender, EventArgs e) {
        if (_mode != ViewMode.Real)
            return;
        rtbPreview.SelectionFont = new Font("Consolas", 11f, FontStyle.Regular);
        rtbPreview.SelectionBackColor = Color.FromArgb(243, 243, 243);
        rtbPreview.SelectionColor = Color.Black;
        rtbPreview.Focus();
    }

    private void btnBreak_Click(object sender, EventArgs e) {
        if (_mode != ViewMode.Real)
            return;
        rtbPreview.SelectedRtf = "{\\rtf1\\ansi\\line}";
        rtbPreview.Focus();
    }

    private void ToggleStyle(FontStyle style) {
        if (_mode != ViewMode.Real)
            return;
        var current = rtbPreview.SelectionFont;
        var family = current?.FontFamily.Name ?? "Segoe UI";
        var size = current?.Size ?? 12f;
        var next = current is null ? style : current.Style ^ style;
        rtbPreview.SelectionFont = new Font(family, size, next);
        rtbPreview.Focus();
    }

    private void FormatParagraph(float size, FontStyle style, bool bullet) {
        if (_mode != ViewMode.Real)
            return;
        var text = rtbPreview.Text;
        var start = rtbPreview.SelectionStart;
        var length = rtbPreview.SelectionLength;
        var from = LineStart(text, start);
        var endIndex = length == 0 ? start : start + length;
        if (length > 0 && endIndex > from && LineStart(text, endIndex) == endIndex)
            endIndex--;
        var to = LineEnd(text, endIndex);
        rtbPreview.Select(from, Math.Max(0, to - from));
        rtbPreview.SelectionBullet = bullet;
        rtbPreview.SelectionBackColor = Color.White;
        rtbPreview.SelectionColor = Color.Black;
        rtbPreview.SelectionFont = new Font("Segoe UI", size, style);
        rtbPreview.Focus();
    }

    private static int LineStart(string text, int index) {
        var i = Math.Clamp(index, 0, text.Length);
        var newline = text.LastIndexOf('\n', Math.Max(0, i - 1));
        return newline < 0 ? 0 : newline + 1;
    }

    private static int LineEnd(string text, int index) {
        var from = Math.Clamp(index, 0, text.Length);
        var newline = text.IndexOf('\n', from);
        return newline < 0 ? text.Length : newline;
    }

    private void InsertRichTarget(bool image) {
        if (_mode != ViewMode.Real)
            return;
        var selected = rtbPreview.SelectedText.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        var picked = FormMarkdownTarget.Prompt(this, image, selected);
        rtbPreview.Focus();
        if (picked is null)
            return;
        var snippet = image
            ? RichMarkdown.ImageRtf(picked.Value.Definition, picked.Value.Url)
            : RichMarkdown.LinkRtf(picked.Value.Definition, picked.Value.Url);
        rtbPreview.SelectedRtf = snippet;
    }

    private void pnlEditor_MouseDown(object sender, MouseEventArgs e) {
        txtEditor.Focus();
    }

    private void txtEditor_TextChanged(object sender, EventArgs e) {
        if (_suppressDirty)
            return;

        btnSave.Enabled = txtEditor.Text != _savedText;
    }

    private void rtbPreview_TextChanged(object sender, EventArgs e) {
        if (_suppressRich || _mode != ViewMode.Real)
            return;
        ApplyLiveMarkdown(RichMarkdown.FromRtf(rtbPreview.Rtf));
    }

    private void rtbPreview_LinkClicked(object sender, LinkClickedEventArgs e) {
        if (_mode != ViewMode.Preview || string.IsNullOrWhiteSpace(e.LinkText))
            return;
        try {
            Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) {
        }
    }

    private void RenderRich() {
        _suppressRich = true;
        try {
            rtbPreview.Rtf = RichMarkdown.ToRtf(txtEditor.Text);
            rtbPreview.BackColor = Color.White;
        }
        finally {
            _suppressRich = false;
        }
    }

    private void FlushRich() {
        if (_mode != ViewMode.Real)
            return;
        ApplyLiveMarkdown(RichMarkdown.FromRtf(rtbPreview.Rtf));
    }

    private void ApplyLiveMarkdown(string? markdown) {
        if (markdown is null || IsDisposed)
            return;
        var text = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        if (txtEditor.Text == text)
            return;
        txtEditor.Text = text;
    }
}
