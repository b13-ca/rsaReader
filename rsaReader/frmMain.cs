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
    private RealEditorBridge? _realBridge;

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

    private async void btnSave_Click(object sender, EventArgs e) {
        if (_mode == ViewMode.Real)
            await FlushReal();
        TrySave();
    }

    private async void btnLoad_Click(object sender, EventArgs e) {
        if (_mode == ViewMode.Real)
            await FlushReal();
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

    private async void btnEdit_Click(object sender, EventArgs e) {
        if (_mode == ViewMode.Edit)
            return;
        if (_mode == ViewMode.Real)
            await FlushReal();
        ShowEditor();
    }

    private async void btnPreview_Click(object sender, EventArgs e) {
        if (_mode == ViewMode.Preview)
            return;
        if (_mode == ViewMode.Real) {
            await FlushReal();
            await SetEditable(false);
        }
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
        var fromEdit = _mode == ViewMode.Edit;
        _mode = ViewMode.Real;
        ApplyMode();
        EnsurePreview();
        if (!_emptyPreviewLoaded)
            return;
        if (fromEdit)
            RenderPreview();
        _ = SetEditable(true);
    }

    private void ApplyMode() {
        var edit = _mode == ViewMode.Edit;
        pnlTools.Enabled = _mode == ViewMode.Real;
        pnlEditor.Enabled = edit;
        pnlPreview.Enabled = !edit;
        if (edit) {
            splitView.Panel1Collapsed = false;
            splitView.Panel2Collapsed = true;
        }
        else {
            splitView.Panel2Collapsed = false;
            splitView.Panel1Collapsed = true;
        }

        if (edit || lblPreview.Visible)
            webPreview.Visible = false;
        else if (_emptyPreviewLoaded)
            webPreview.Visible = true;

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

    private async void btnBold_Click(object sender, EventArgs e) {
        await RealCommand("cmdBold()");
    }

    private async void btnH1_Click(object sender, EventArgs e) {
        await RealCommand("cmdH1()");
    }

    private async void btnH2_Click(object sender, EventArgs e) {
        await RealCommand("cmdH2()");
    }

    private async void btnBullet_Click(object sender, EventArgs e) {
        await RealCommand("cmdBullet()");
    }

    private async void btnLink_Click(object sender, EventArgs e) {
        await InsertRealTarget(false);
    }

    private async void btnImage_Click(object sender, EventArgs e) {
        await InsertRealTarget(true);
    }

    private async void btnCode_Click(object sender, EventArgs e) {
        await RealCommand("cmdCode()");
    }

    private async void btnBreak_Click(object sender, EventArgs e) {
        await RealCommand("cmdBreak()");
    }

    private async Task InsertRealTarget(bool image) {
        if (!CanRunScript())
            return;

        var selected = "";
        var response = await webPreview.EvaluateScriptAsync("selectionText()");
        if (response.Success && response.Result is string text)
            selected = text;

        var picked = FormMarkdownTarget.Prompt(this, image, selected);
        if (picked is null)
            return;

        var command = image ? "cmdImage" : "cmdLink";
        var script = command
            + "(" + JsonSerializer.Serialize(picked.Value.Definition)
            + "," + JsonSerializer.Serialize(picked.Value.Url) + ")";
        await RealCommand(script);
    }

    private bool CanRunScript() {
        return _mode == ViewMode.Real
            && webPreview.IsBrowserInitialized
            && webPreview.CanExecuteJavascriptInMainFrame;
    }

    private async Task RealCommand(string script) {
        if (!CanRunScript())
            return;

        await webPreview.EvaluateScriptAsync(script);
        if (_mode == ViewMode.Real)
            webPreview.Focus();
    }

    private async Task SetEditable(bool on) {
        if (!webPreview.IsBrowserInitialized || !webPreview.CanExecuteJavascriptInMainFrame)
            return;

        await webPreview.EvaluateScriptAsync(on ? "setEditable(true)" : "setEditable(false)");
        if (on && _mode == ViewMode.Real)
            webPreview.Focus();
    }

    private async Task FlushReal() {
        if (!CanRunScript())
            return;

        var response = await webPreview.EvaluateScriptAsync("flushMarkdown()");
        if (response.Success && response.Result is string markdown)
            ApplyLiveMarkdown(markdown);
    }

    internal void ApplyLiveMarkdown(string? markdown) {
        if (markdown is null || IsDisposed || _mode != ViewMode.Real)
            return;

        var text = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        if (txtEditor.Text == text)
            return;

        txtEditor.Text = text;
    }

    internal void SaveFromEditor() {
        if (IsDisposed)
            return;
        TrySave();
    }

    internal void LoadFromEditor() {
        if (IsDisposed)
            return;
        btnLoad.PerformClick();
    }

    private void pnlEditor_MouseDown(object sender, MouseEventArgs e) {
        txtEditor.Focus();
    }

    private void txtEditor_TextChanged(object sender, EventArgs e) {
        if (_suppressDirty)
            return;

        btnSave.Enabled = txtEditor.Text != _savedText;
    }

    private void EnsurePreview() {
        EnsureRealBridge();
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
        if (_mode == ViewMode.Real)
            _ = SetEditable(true);
    }

    private void EnsureRealBridge() {
        if (_realBridge is not null)
            return;

        _realBridge = new RealEditorBridge(this);
        webPreview.JavascriptObjectRepository.Settings.LegacyBindingEnabled = true;
        webPreview.JavascriptObjectRepository.Register("rsaReal", _realBridge, BindingOptions.DefaultBinder);
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

public sealed class RealEditorBridge {
    private readonly FormMain _form;

    public RealEditorBridge(FormMain form) {
        _form = form;
    }

    public void SyncMarkdown(string markdown) {
        Post(() => _form.ApplyLiveMarkdown(markdown));
    }

    public void Save() {
        Post(() => _form.SaveFromEditor());
    }

    public void Load() {
        Post(() => _form.LoadFromEditor());
    }

    private void Post(Action action) {
        if (_form.IsDisposed)
            return;
        if (_form.InvokeRequired)
            _form.BeginInvoke(action);
        else
            action();
    }
}
