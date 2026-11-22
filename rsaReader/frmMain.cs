using System.Reflection;
using CefSharp;

namespace rsaReader;

public partial class FormMain : Form {
    private bool _previewOpen;
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
        if (!_previewOpen)
            FocusEditor();
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
        btnPreview.Text = "Preview";
        pnlPreview.Enabled = false;
        pnlEditor.Enabled = true;
        pnlEditor.BringToFront();
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
        btnPreview.Text = "Edit";
        webPreview.Visible = false;
        pnlEditor.Enabled = false;
        pnlPreview.Enabled = true;
        pnlPreview.BringToFront();
    }

    private void btnPreview_Click(object sender, EventArgs e) {
        if (_previewOpen)
            ShowEditor();
        else
            ShowPreview();
    }

    private void ShowPreview() {
        _previewOpen = true;
        btnPreview.Text = "Edit";
        pnlEditor.Enabled = false;
        pnlPreview.Enabled = true;
        pnlPreview.BringToFront();
        webPreview.Visible = _emptyPreviewLoaded;
        EnsurePreview();
        if (_emptyPreviewLoaded)
            RenderPreview();
    }

    private void ShowEditor() {
        _previewOpen = false;
        btnPreview.Text = "Preview";
        pnlPreview.Enabled = false;
        pnlEditor.Enabled = true;
        pnlEditor.BringToFront();
        FocusEditor();
    }

    private void FocusEditor() {
        txtEditor.Focus();
        txtEditor.SelectionStart = 0;
        txtEditor.SelectionLength = 0;
        if (IsHandleCreated)
            BeginInvoke(ClearEditorSelection);
    }

    private void ClearEditorSelection() {
        txtEditor.SelectionStart = 0;
        txtEditor.SelectionLength = 0;
    }

    private void pnlEditor_MouseDown(object sender, MouseEventArgs e) {
        txtEditor.Focus();
    }

    private void txtEditor_TextChanged(object sender, EventArgs e) {
        if (!_suppressDirty)
            btnSave.Enabled = txtEditor.Text != _savedText;
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
        if (_previewOpen)
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
        if (!_previewReady)
            return;

        webPreview.LoadHtml(MarkdownPreview.ToDocument(txtEditor.Text), MarkdownPreview.PreviewUrl);
    }
}
