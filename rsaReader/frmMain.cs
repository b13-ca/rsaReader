using System.Reflection;
using System.Text.Json;
using CefSharp;

// You must do a publish to have a single .exe file with all the dependencies.
// The CefSharp.WinForms package is not enough to run the application, 
// you need to include the CefSharp.Common and CefSharp.WinForms packages as well.
namespace rsaReader;

public partial class FormMain : Form {
    private enum ViewMode {
        Edit,
        Preview,
        Real
    }

    private ViewMode _mode;
    private bool _previewReady;
    private bool _previewInitStarted;
    private bool _emptyPreviewLoaded;
    private bool _emptyPreviewStarted;
    private string? _documentPath;
    private string? _keyHex;
    private string? _aesKeyHex;
    private string _savedText = "";
    private bool _suppressDirty;
    private bool _preparedPreview;
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 100 };

    public FormMain() {
        InitializeComponent();
        _previewTimer.Tick += PreviewTimer_Tick;
        PrepareEditor();
    }

    public FormMain(string? documentPath, string? data, string? keyHex, string? aesKeyHex) {
        InitializeComponent();
        _previewTimer.Tick += PreviewTimer_Tick;
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
        else if (_mode == ViewMode.Real)
            FocusEditor(false);
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
        _previewTimer.Stop();
        _previewTimer.Dispose();
        _keyHex = null;
        _aesKeyHex = null;
        AppSession.Clear();
        base.OnFormClosed(e);
    }

    private void btnSave_Click(object sender, EventArgs e) {
        TrySave();
    }

    private void btnLoad_Click(object sender, EventArgs e) {
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
        webPreview.Visible = false;
    }

    private void btnEdit_Click(object sender, EventArgs e) {
        if (_mode != ViewMode.Edit)
            ShowEditor();
    }

    private void btnPreview_Click(object sender, EventArgs e) {
        if (_mode != ViewMode.Preview)
            ShowPreview();
    }

    private void btnReal_Click(object sender, EventArgs e) {
        if (_mode != ViewMode.Real)
            ShowReal();
    }

    private void ShowPreview() {
        _mode = ViewMode.Preview;
        ApplyMode();
        EnsurePreview();
        if (_emptyPreviewLoaded)
            RenderPreview();
    }

    private void ShowEditor() {
        _mode = ViewMode.Edit;
        ApplyMode();
        FocusEditor(true);
    }

    private void ShowReal() {
        _mode = ViewMode.Real;
        ApplyMode();
        EnsurePreview();
        if (_emptyPreviewLoaded)
            RenderPreview();
        FocusEditor(false);
    }

    private void ApplyMode() {
        var edit = _mode == ViewMode.Edit;
        var preview = _mode == ViewMode.Preview;
        pnlTools.Enabled = _mode == ViewMode.Real;
        pnlEditor.Enabled = !preview;
        pnlPreview.Enabled = !edit;
        if (preview) {
            splitView.Panel2Collapsed = false;
            splitView.Panel1Collapsed = true;
        }
        else if (edit) {
            splitView.Panel1Collapsed = false;
            splitView.Panel2Collapsed = true;
        }
        else {
            splitView.Panel1Collapsed = false;
            splitView.Panel2Collapsed = false;
        }

        if (edit || lblPreview.Visible)
            webPreview.Visible = false;
        else if (_emptyPreviewLoaded)
            webPreview.Visible = true;

        StyleModeButton(btnEdit, edit);
        StyleModeButton(btnPreview, preview);
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
        WrapSelection("**", "**");
    }

    private void btnH1_Click(object sender, EventArgs e) {
        PrefixLines("# ");
    }

    private void btnH2_Click(object sender, EventArgs e) {
        PrefixLines("## ");
    }

    private void btnBullet_Click(object sender, EventArgs e) {
        PrefixLines("- ");
    }

    private void btnLink_Click(object sender, EventArgs e) {
        InsertTarget(false);
    }

    private void btnImage_Click(object sender, EventArgs e) {
        InsertTarget(true);
    }

    private void btnCode_Click(object sender, EventArgs e) {
        InsertCodeFence();
    }

    private void btnBreak_Click(object sender, EventArgs e) {
        InsertHardBreak();
    }

    private void WrapSelection(string prefix, string suffix) {
        var start = txtEditor.SelectionStart;
        var length = txtEditor.SelectionLength;
        txtEditor.SelectedText = prefix + txtEditor.SelectedText + suffix;
        txtEditor.Select(start + prefix.Length, length);
        txtEditor.Focus();
    }

    private void PrefixLines(string marker) {
        var text = txtEditor.Text;
        var selStart = txtEditor.SelectionStart;
        var selLength = txtEditor.SelectionLength;
        var from = LineStart(text, selStart);
        var endIndex = selLength == 0 ? selStart : selStart + selLength;
        if (selLength > 0 && endIndex > from && LineStart(text, endIndex) == endIndex)
            endIndex--;

        var to = LineContentEnd(text, endIndex);
        var lines = text[from..to].Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            var line = lines[i].EndsWith('\r') ? lines[i][..^1] : lines[i];
            lines[i] = marker + line;
        }

        var rebuilt = string.Join("\r\n", lines);
        txtEditor.Select(from, to - from);
        txtEditor.SelectedText = rebuilt;
        if (selLength == 0)
            txtEditor.Select(from + marker.Length, 0);
        else
            txtEditor.Select(from, rebuilt.Length);

        txtEditor.Focus();
    }

    private static int LineStart(string text, int index) {
        var i = Math.Clamp(index, 0, text.Length);
        var newline = text.LastIndexOf('\n', Math.Max(0, i - 1));
        return newline < 0 ? 0 : newline + 1;
    }

    private static int LineContentEnd(string text, int index) {
        var from = Math.Clamp(index, 0, text.Length);
        var newline = text.IndexOf('\n', from);
        if (newline < 0)
            return text.Length;
        if (newline > 0 && text[newline - 1] == '\r')
            return newline - 1;
        return newline;
    }

    private void InsertCodeFence() {
        var text = txtEditor.Text;
        var start = txtEditor.SelectionStart;
        var length = txtEditor.SelectionLength;
        var selected = txtEditor.SelectedText.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        var atLineStart = start == 0 || text[start - 1] == '\n';
        var after = start + length;
        var atLineEnd = after >= text.Length || text[after] == '\r' || text[after] == '\n';
        var insertion = new System.Text.StringBuilder();
        if (!atLineStart)
            insertion.Append("\r\n");
        insertion.Append("```\r\n");
        insertion.Append(selected);
        if (!selected.EndsWith("\r\n", StringComparison.Ordinal))
            insertion.Append("\r\n");
        insertion.Append("```");
        if (!atLineEnd)
            insertion.Append("\r\n");

        var value = insertion.ToString();
        var inner = (atLineStart ? 0 : 2) + "```\r\n".Length;
        txtEditor.SelectedText = value;
        txtEditor.Select(start + inner, selected.Length);
        txtEditor.Focus();
    }

    private void InsertHardBreak() {
        var text = txtEditor.Text;
        var at = txtEditor.SelectionStart + txtEditor.SelectionLength;
        var needsLeadingBreak = at > 0 && text[at - 1] != '\n';
        var insert = (needsLeadingBreak ? "\r\n" : "") + "  \r\n";
        txtEditor.Select(at, 0);
        txtEditor.SelectedText = insert;
        txtEditor.Select(at + insert.Length, 0);
        txtEditor.Focus();
    }

    private void InsertTarget(bool image) {
        var picked = FormMarkdownTarget.Prompt(this, image, txtEditor.SelectedText);
        txtEditor.Focus();
        if (picked is null)
            return;

        var markdown = (image ? "!" : "") + "[" + picked.Value.Definition + "](" + picked.Value.Url + ")";
        var start = txtEditor.SelectionStart;
        txtEditor.SelectedText = markdown;
        txtEditor.Select(start, markdown.Length);
    }

    private void pnlEditor_MouseDown(object sender, MouseEventArgs e) {
        txtEditor.Focus();
    }

    private void txtEditor_TextChanged(object sender, EventArgs e) {
        if (_suppressDirty)
            return;

        btnSave.Enabled = txtEditor.Text != _savedText;
        if (_mode == ViewMode.Real)
            SchedulePreview();
    }

    private void SchedulePreview() {
        if (!_previewTimer.Enabled)
            _previewTimer.Start();
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e) {
        _previewTimer.Stop();
        if (_mode == ViewMode.Real)
            RenderPreview();
    }

    private void EnsurePreview() {
        if (_previewReady || _previewInitStarted)
            return;

        _previewInitStarted = true;
        if (CefHost.StartError is not null || Cef.IsInitialized != true) {
            ShowPreviewError(CefHost.StartError ?? "Preview could not start.");
            return;
        }

        try {
            webPreview.RequestHandler = new PreviewRequestHandler();
            webPreview.BackColor = Color.White;
            webPreview.Visible = false;
            CreatePreviewHandle();
            if (webPreview.IsBrowserInitialized)
                FinishPreviewInit();
            else
                webPreview.IsBrowserInitializedChanged += OnBrowserInitialized;
        }
        catch (Exception ex) {
            ShowPreviewError(ex.Message);
        }
    }

    private void OnBrowserInitialized(object? sender, EventArgs e) {
        if (!webPreview.IsBrowserInitialized)
            return;

        if (InvokeRequired) {
            BeginInvoke(FinishPreviewInit);
            return;
        }

        FinishPreviewInit();
    }

    private void CreatePreviewHandle() {
        if (webPreview.IsHandleCreated)
            return;

        typeof(Control).GetMethod(
            "CreateControl",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(bool) },
            null)!.Invoke(webPreview, new object[] { true });
    }

    private void FinishPreviewInit() {
        _previewReady = true;
        lblPreview.Visible = false;
        webPreview.LoadingStateChanged += OnEmptyPreviewLoaded;
        webPreview.LoadHtml(MarkdownPreview.ToDocument(""), MarkdownPreview.PreviewUrl);
    }

    private void OnEmptyPreviewLoaded(object? sender, LoadingStateChangedEventArgs e) {
        if (e.IsLoading) {
            _emptyPreviewStarted = true;
            return;
        }

        if (!_emptyPreviewStarted)
            return;

        webPreview.LoadingStateChanged -= OnEmptyPreviewLoaded;
        _emptyPreviewLoaded = true;
        if (InvokeRequired) {
            BeginInvoke(ShowLoadedPreview);
            return;
        }

        ShowLoadedPreview();
    }

    private void ShowLoadedPreview() {
        if (_mode != ViewMode.Edit && !lblPreview.Visible)
            webPreview.Visible = true;

        RenderPreview();
    }

    private void ShowPreviewError(string message) {
        webPreview.Visible = false;
        lblPreview.Visible = true;
        lblPreview.Text = "Preview could not start."
            + Environment.NewLine + Environment.NewLine + message;
    }

    private void RenderPreview() {
        if (!_previewReady || !webPreview.IsBrowserInitialized)
            return;

        var body = MarkdownPreview.ToBody(txtEditor.Text);
        if (_emptyPreviewLoaded && webPreview.CanExecuteJavascriptInMainFrame) {
            webPreview.ExecuteScriptAsync("document.body.innerHTML = " + JsonSerializer.Serialize(body) + ";");
            return;
        }

        webPreview.LoadHtml(MarkdownPreview.ToDocument(txtEditor.Text), MarkdownPreview.PreviewUrl);
    }
}
