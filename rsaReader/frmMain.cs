using System.Runtime.InteropServices;

namespace rsaReader;

public partial class FormMain : Form {
    private enum ViewMode {
        Edit,
        Real
    }

    private ViewMode _mode;
    private string? _documentPath;
    private string? _keyHex;
    private string? _aesKeyHex;
    private string _markdown = "";
    private string _savedText = "";
    private bool _suppress;
    private bool _live;
    private bool _realEdited;
    private string _shownRtf = "";
    private bool _openAsReal;
    private int _keptPreviewCaret = -1;
    private int _keptMarkdownCaret = -1;
    private Point _keptPreviewScroll;
    private bool _dark;
    private readonly List<TitleMark> _titleMarks = new();
    private bool _drawingTitles;
    private TitleRuleWindow? _titleWindow;
    private readonly Font _plainFont = new("Consolas", 11F);
    private readonly Font _richFont = new("Segoe UI", 12F);

    public FormMain() {
        InitializeComponent();
        AttachTitleRules();
        PrepareEditor();
    }

    public FormMain(string? documentPath, string? data, string? keyHex, string? aesKeyHex) {
        InitializeComponent();
        AttachTitleRules();
        if (!string.IsNullOrWhiteSpace(documentPath) && data is not null) {
            _keyHex = keyHex;
            _aesKeyHex = aesKeyHex;
            PrepareDocument(data, documentPath);
        }
        else
            PrepareEditor(documentPath);
    }

    internal void ShowPrepared() {
        if (_openAsReal)
            ShowReal();
        else
            ShowEditor();
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        if (_mode == ViewMode.Edit)
            rtbPreview.Focus();
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
        CaptureMarkdown();
        TrySave();
    }

    private void btnLoad_Click(object sender, EventArgs e) {
        CaptureMarkdown();
        if (!ConfirmReplace())
            return;

        using var dialog = new OpenFileDialog {
            Filter = "rsaReader (*.rsa;*.md;*.txt)|*.rsa;*.md;*.txt|Markdown (*.md)|*.md|Text (*.txt)|*.txt|rsaReader (*.rsa)|*.rsa",
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

        if (IsPlainNote(path)) {
            var text = ReadPlain(this, path);
            if (text is null)
                return;
            _keyHex = null;
            _aesKeyHex = null;
            PrepareDocument(text, path);
            ShowReal();
            return;
        }

        var opened = ReadDocument(this, path);
        if (opened is null)
            return;

        _keyHex = opened.Value.KeyHex;
        _aesKeyHex = opened.Value.AesKeyHex;
        PrepareDocument(opened.Value.Data, path);
        ShowReal();
    }

    private void OpenEmpty(string path) {
        _keyHex = null;
        _aesKeyHex = null;
        _markdown = "";
        _savedText = "";
        btnSave.Enabled = false;
        _documentPath = path;
        _openAsReal = false;
        Text = "rsaReader - " + Path.GetFileName(path);
        ShowEditor();
    }

    internal static bool IsPlainNote(string path) {
        var extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ReadPlain(IWin32Window? owner, string path) {
        try {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
            ShowReadError(owner, path + Environment.NewLine + Environment.NewLine + ex.Message);
            return null;
        }
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
        var converted = path is not null && IsPlainNote(path);
        if (converted)
            path = EnsureRsaExtension(path!);
        if (converted && File.Exists(path)) {
            var replace = MessageBox.Show(
                this,
                Path.GetFileName(path) + " already exists. Replace it?",
                "rsaReader",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (replace != DialogResult.Yes)
                return false;
        }

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
            RsaFile.Write(path, keyHex, aesKeyHex, _markdown);
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
        _savedText = _markdown;
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
        _openAsReal = false;
        if (string.IsNullOrWhiteSpace(path))
            return;

        _documentPath = path;
        _savedText = "";
        Text = "rsaReader - " + Path.GetFileName(path);
    }

    private void PrepareDocument(string markdown, string path) {
        _openAsReal = true;
        _markdown = Normalize(markdown);
        _savedText = _markdown;
        _keptPreviewCaret = -1;
        _documentPath = path;
        btnSave.Enabled = false;
        Text = "rsaReader - " + Path.GetFileName(path);
    }

    private void btnMode_Click(object sender, EventArgs e) {
        var text = rtbPreview.Text ?? "";
        var start = Math.Clamp(rtbPreview.SelectionStart, 0, text.Length);
        var scroll = Point.Empty;
        if (rtbPreview.IsHandleCreated)
            SendMessage(rtbPreview.Handle, 0x04DD, IntPtr.Zero, ref scroll);
        CaptureMarkdown();
        if (_mode == ViewMode.Edit) {
            ShowReal();
            PlacePreviewCaret(PlainIndex(text, start));
            return;
        }

        var markdownCaret = SnapMarkers(_markdown, RichMarkdown.SourceIndex(text, start, _markdown.Length));
        _keptPreviewCaret = start;
        _keptMarkdownCaret = markdownCaret;
        _keptPreviewScroll = scroll;
        ShowEditor();
        PlaceEditCaret(markdownCaret);
    }

    private void PlaceEditCaret(int markdownCaret) {
        _suppress = true;
        try {
            rtbPreview.Focus();
            rtbPreview.Select(Math.Clamp(markdownCaret, 0, rtbPreview.TextLength), 0);
        }
        finally {
            _suppress = false;
        }
    }

    private void PlacePreviewCaret(int markdownCaret) {
        var samePlace = _keptPreviewCaret >= 0 && markdownCaret == _keptMarkdownCaret;
        var caret = samePlace
            ? _keptPreviewCaret
            : RichMarkdown.ControlCaret(rtbPreview.Text, markdownCaret);
        _suppress = true;
        try {
            rtbPreview.Focus();
            rtbPreview.Select(Math.Clamp(caret, 0, rtbPreview.TextLength), 0);
            if (samePlace && rtbPreview.IsHandleCreated)
                SendMessage(rtbPreview.Handle, 0x04DE, IntPtr.Zero, ref _keptPreviewScroll);
        }
        finally {
            _suppress = false;
        }
    }

    private static int PlainIndex(string text, int index) {
        index = Math.Clamp(index, 0, text.Length);
        var plain = 0;
        for (var i = 0; i < index; i++)
            if (text[i] != '\r')
                plain++;
        return plain;
    }

    private void ShowEditor() {
        _live = false;
        _mode = ViewMode.Edit;
        _titleMarks.Clear();
        ApplyMode();
        ShowPlain();
        _live = true;
        rtbPreview.Focus();
    }

    private void ShowReal() {
        _live = false;
        _mode = ViewMode.Real;
        ApplyMode();
        ShowRich();
        _realEdited = false;
        _live = true;
        CollectTitleRules();
        rtbPreview.Focus();
    }

    private void ApplyMode() {
        pnlTools.Enabled = true;
        rtbPreview.ReadOnly = false;
        btnMode.Text = _mode == ViewMode.Edit ? "Preview" : "Edit";
    }

    private void pnlTools_Paint(object sender, PaintEventArgs e) {
        using var pen = new Pen(SystemColors.ControlDark);
        e.Graphics.DrawLine(pen, 0, 0, pnlTools.Width, 0);
    }

    private void btnBold_Click(object sender, EventArgs e) {
        RunButton(() => {
            if (_mode == ViewMode.Edit)
                ToggleMarkdownWrap("**");
            else
                ToggleStyle(FontStyle.Bold);
        });
    }

    private void btnH1_Click(object sender, EventArgs e) {
        RunButton(() => {
            if (_mode == ViewMode.Edit)
                ToggleHeading("# ");
            else
                FormatParagraph(22f, FontStyle.Bold, false);
        });
    }

    private void btnH2_Click(object sender, EventArgs e) {
        RunButton(() => {
            if (_mode == ViewMode.Edit)
                ToggleHeading("## ");
            else
                FormatParagraph(16f, FontStyle.Bold, false);
        });
    }

    private void btnBullet_Click(object sender, EventArgs e) {
        RunButton(() => {
            if (_mode == ViewMode.Edit)
                ToggleBullet();
            else {
                SelectNextWord();
                rtbPreview.SelectionBullet = !rtbPreview.SelectionBullet;
            }
        });
    }

    private void btnLink_Click(object sender, EventArgs e) {
        RunButton(() => {
            SelectNextWord();
            InsertRichTarget(false);
        });
    }

    private void btnImage_Click(object sender, EventArgs e) {
        RunButton(() => InsertRichTarget(true));
    }

    private void btnInlineCode_Click(object sender, EventArgs e) {
        RunButton(() => ApplyCodeButton(false));
    }

    private void btnCode_Click(object sender, EventArgs e) {
        RunButton(() => ApplyCodeButton(true));
    }

    private void btnBreak_Click(object sender, EventArgs e) {
        if (_mode == ViewMode.Real) {
            BeginInvoke(() => InsertPreviewText("  \n"));
            return;
        }

        RunButton(() => {
            var start = rtbPreview.SelectionStart;
            rtbPreview.SelectedText = "  \n";
            rtbPreview.Select(start + 3, 0);
        });
    }

    private void InsertPreviewText(string insertion) {
        var text = rtbPreview.Text ?? "";
        var start = Math.Clamp(rtbPreview.SelectionStart, 0, text.Length);
        var end = Math.Clamp(start + Math.Max(0, rtbPreview.SelectionLength), start, text.Length);
        if (_realEdited)
            _markdown = Normalize(RichMarkdown.FromRtf(rtbPreview.Rtf));
        var from = RichMarkdown.SourceIndex(text, start, _markdown.Length);
        var to = end == start ? from : RichMarkdown.SourceIndex(text, end, _markdown.Length);
        from = SnapMarkers(_markdown, from);
        to = SnapMarkers(_markdown, to);
        if (to < from)
            (from, to) = (to, from);
        from = Math.Clamp(from, 0, _markdown.Length);
        to = Math.Clamp(to, from, _markdown.Length);
        _markdown = _markdown.Remove(from, to - from).Insert(from, insertion);
        ReloadPreview(text, start, end);
    }

    private void ReloadPreview(string before, int start, int end) {
        var scroll = Point.Empty;
        if (rtbPreview.IsHandleCreated)
            SendMessage(rtbPreview.Handle, 0x04DD, IntPtr.Zero, ref scroll);
        _live = false;
        _realEdited = false;
        _titleMarks.Clear();
        try {
            ShowRich();
        }
        finally {
            _live = true;
        }

        CollectTitleRules();
        var after = rtbPreview.Text ?? "";
        var caret = CaretAfterInsert(before, after, start, end);
        caret = RetreatOutsideMarkers(after, caret);
        _suppress = true;
        try {
            rtbPreview.Focus();
            rtbPreview.Select(Math.Clamp(caret, 0, rtbPreview.TextLength), 0);
            if (rtbPreview.IsHandleCreated)
                SendMessage(rtbPreview.Handle, 0x04DE, IntPtr.Zero, ref scroll);
        }
        finally {
            _suppress = false;
        }

        rtbPreview.Invalidate();
        rtbPreview.Update();
        btnSave.Enabled = _markdown != _savedText;
    }

    private static int CaretAfterInsert(string before, string after, int start, int end) {
        start = Math.Clamp(start, 0, before.Length);
        end = Math.Clamp(end, start, before.Length);
        var prefix = 0;
        var head = Math.Min(start, after.Length);
        while (prefix < head && before[prefix] == after[prefix])
            prefix++;
        var suffix = 0;
        var tail = before.Length - end;
        while (suffix < tail && suffix < after.Length - prefix
            && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
            suffix++;
        var caret = after.Length - suffix;
        if (caret < prefix)
            caret = prefix;
        return Math.Clamp(caret, 0, after.Length);
    }

    private int RetreatOutsideMarkers(string preview, int caret) {
        caret = Math.Clamp(caret, 0, preview.Length);
        var snapped = SnapMarkers(_markdown, RichMarkdown.SourceIndex(preview, caret, _markdown.Length));
        while (caret > 0) {
            var at = RichMarkdown.SourceIndex(preview, caret, _markdown.Length);
            if (SnapMarkers(_markdown, at) == at && at <= snapped)
                return caret;
            caret--;
        }

        return 0;
    }

    private static int SnapMarkers(string markdown, int index) {
        index = Math.Clamp(index, 0, markdown.Length);
        var lineStart = index == 0 ? 0 : markdown.LastIndexOf('\n', index - 1) + 1;
        var lineEnd = markdown.IndexOf('\n', index);
        if (lineEnd < 0)
            lineEnd = markdown.Length;
        var line = markdown[lineStart..lineEnd];
        var local = index - lineStart;
        local = SnapPair(line, local, "**");
        local = SnapPair(line, local, "__");
        local = SnapPair(line, local, "~~");
        return lineStart + local;
    }

    private static int SnapPair(string line, int index, string mark) {
        var search = 0;
        while (search < line.Length) {
            var open = line.IndexOf(mark, search, StringComparison.Ordinal);
            if (open < 0 || open >= index)
                return index;
            var close = line.IndexOf(mark, open + mark.Length, StringComparison.Ordinal);
            if (close < 0)
                return index;
            var end = close + mark.Length;
            if (index >= end) {
                search = end;
                continue;
            }

            return index - open <= end - index ? open : end;
        }

        return index;
    }

    private void RunButton(Action action) {
        var hadSelection = rtbPreview.SelectionLength > 0;
        var caret = rtbPreview.SelectionStart;
        var before = rtbPreview.Text;
        action();
        if (!hadSelection) {
            var restored = MapCaret(before, rtbPreview.Text, caret);
            rtbPreview.Select(Math.Clamp(restored, 0, rtbPreview.TextLength), 0);
        }

        rtbPreview.Focus();
    }

    private static int MapCaret(string before, string after, int caret) {
        var prefix = 0;
        var limit = Math.Min(before.Length, after.Length);
        while (prefix < limit && before[prefix] == after[prefix])
            prefix++;

        var suffix = 0;
        var beforeRemain = before.Length - prefix;
        var afterRemain = after.Length - prefix;
        while (suffix < beforeRemain && suffix < afterRemain
            && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
            suffix++;

        var beforeEnd = before.Length - suffix;
        if (caret <= prefix)
            return caret;
        if (caret >= beforeEnd)
            return caret + after.Length - before.Length;
        return prefix + Math.Min(caret - prefix, Math.Max(0, after.Length - suffix - prefix));
    }

    private void ApplyCodeButton(bool fence) {
        var text = rtbPreview.Text;
        var start = rtbPreview.SelectionStart;
        var length = rtbPreview.SelectionLength;
        var anchor = length == 0 ? start : Math.Min(text.Length - 1, start + length - 1);
        var from = LineStart(text, start);
        var to = LineEnd(text, Math.Max(0, anchor));
        if (length > 0 && text[from..to].Contains('\n')) {
            ToggleFence(from, to);
            return;
        }

        if (fence && length == 0) {
            ToggleWholeLine(from, to);
            return;
        }

        if (!fence && length == 0)
            SelectNextWord();
        if (_mode == ViewMode.Edit)
            WrapSelection(fence ? "```" : "`");
        else
            ApplyCodeStyle();
    }

    private void ToggleWholeLine(int from, int to) {
        var line = rtbPreview.Text[from..to];
        if (LineHasCodeMarks(line)) {
            var inner = StripCodeMarks(line);
            rtbPreview.Select(from, to - from);
            rtbPreview.SelectedText = inner;
            rtbPreview.Select(from, inner.Length);
            if (_mode == ViewMode.Real)
                ClearCodeStyle();
            return;
        }

        var wrapped = "```" + line + "```";
        rtbPreview.Select(from, to - from);
        rtbPreview.SelectedText = wrapped;
        rtbPreview.Select(from, wrapped.Length);
        if (_mode == ViewMode.Real)
            ApplyCodeStyle();
    }

    private void ToggleFence(int from, int to) {
        if (_mode == ViewMode.Real) {
            rtbPreview.Select(from, to - from);
            ApplyCodeStyle();
            return;
        }

        var block = rtbPreview.Text[from..to];
        string next;
        if (block.StartsWith("```\n", StringComparison.Ordinal) && block.EndsWith("\n```", StringComparison.Ordinal) && block.Length >= 8)
            next = block[4..^4];
        else
            next = "```\n" + block.TrimEnd('\n') + "\n```";
        rtbPreview.Select(from, to - from);
        rtbPreview.SelectedText = next;
        rtbPreview.Select(from, next.Length);
    }

    private void ApplyCodeStyle() {
        rtbPreview.SelectionFont = new Font("Consolas", 11f, FontStyle.Regular);
        rtbPreview.SelectionBackColor = _dark ? Color.FromArgb(46, 46, 46) : Color.FromArgb(243, 243, 243);
        rtbPreview.SelectionColor = _dark ? Color.White : Color.FromArgb(28, 28, 28);
    }

    private static bool LineHasCodeMarks(string line) {
        var core = line.Trim();
        return core.Length >= 6 && core.StartsWith("```", StringComparison.Ordinal) && core.EndsWith("```", StringComparison.Ordinal);
    }

    private static string StripCodeMarks(string line) {
        var lead = line.Length - line.TrimStart().Length;
        var core = line.Trim();
        return line[..lead] + core[3..^3] + line[(lead + core.Length)..];
    }

    private void ClearCodeStyle() {
        rtbPreview.SelectionFont = new Font(_mode == ViewMode.Edit ? "Consolas" : "Segoe UI", _mode == ViewMode.Edit ? 11f : 12f, FontStyle.Regular);
        rtbPreview.SelectionBackColor = _dark && _mode == ViewMode.Real ? Color.Black : Color.White;
        rtbPreview.SelectionColor = _dark && _mode == ViewMode.Real ? Color.White : Color.FromArgb(28, 28, 28);
    }

    private void ToggleMarkdownWrap(string mark) {
        SelectNextWord();
        WrapSelection(mark);
    }

    private void WrapSelection(string mark) {
        var selected = rtbPreview.SelectedText.Replace("\r\n", "\n").Replace('\r', '\n');
        var start = rtbPreview.SelectionStart;
        var text = rtbPreview.Text;
        if (selected.StartsWith(mark, StringComparison.Ordinal)
            && selected.EndsWith(mark, StringComparison.Ordinal)
            && selected.Length >= mark.Length * 2) {
            var inner = selected[mark.Length..^mark.Length];
            rtbPreview.SelectedText = inner;
            rtbPreview.Select(start, inner.Length);
        }
        else if (start >= mark.Length
            && start + rtbPreview.SelectionLength + mark.Length <= text.Length
            && text.Substring(start - mark.Length, mark.Length) == mark
            && text.Substring(start + rtbPreview.SelectionLength, mark.Length) == mark) {
            var length = rtbPreview.SelectionLength;
            rtbPreview.Select(start - mark.Length, length + mark.Length * 2);
            rtbPreview.SelectedText = selected;
            rtbPreview.Select(start - mark.Length, selected.Length);
        }
        else {
            if (mark == "**")
                RemoveHeadingPrefix();
            start = rtbPreview.SelectionStart;
            selected = rtbPreview.SelectedText.Replace("\r\n", "\n").Replace('\r', '\n');
            rtbPreview.SelectedText = mark + selected + mark;
            rtbPreview.Select(start + mark.Length, selected.Length);
        }

        rtbPreview.Focus();
    }

    private void RemoveHeadingPrefix() {
        var text = rtbPreview.Text;
        var start = rtbPreview.SelectionStart;
        var length = rtbPreview.SelectionLength;
        var from = LineStart(text, start);
        var i = from;
        while (i < text.Length && text[i] == ' ')
            i++;
        var hashes = 0;
        while (i + hashes < text.Length && text[i + hashes] == '#' && hashes < 3)
            hashes++;
        if (hashes is not 1 and not 2)
            return;
        if (i + hashes >= text.Length || text[i + hashes] != ' ')
            return;
        var removeLen = hashes + 1;
        if (start < i + removeLen)
            return;
        rtbPreview.Select(i, removeLen);
        rtbPreview.SelectedText = "";
        rtbPreview.Select(start - removeLen, length);
    }

    private void ToggleHeading(string prefix) {
        var level = prefix.Trim().Length;
        ReplaceLines(line => {
            var lead = line.Length - line.TrimStart().Length;
            var core = line.TrimStart();
            var hashes = 0;
            while (hashes < core.Length && core[hashes] == '#')
                hashes++;
            var heading = hashes > 0 && hashes < core.Length && core[hashes] == ' ';
            var body = StripBold(heading ? core[(hashes + 1)..] : core);
            if (heading && hashes == level)
                return line[..lead] + body;
            return line[..lead] + prefix + body;
        });
    }

    private static string StripBold(string text) {
        return text.Replace("**", "").Replace("__", "").Trim();
    }

    private void ToggleBullet() {
        ReplaceLines(line => {
            var lead = line.Length - line.TrimStart().Length;
            var core = line.TrimStart();
            if (core.StartsWith("- ", StringComparison.Ordinal))
                return line[..lead] + core[2..];
            return line[..lead] + "- " + core;
        });
    }

    private void ReplaceLines(Func<string, string> transform) {
        var text = rtbPreview.Text;
        var start = rtbPreview.SelectionStart;
        var length = rtbPreview.SelectionLength;
        var from = LineStart(text, start);
        var endIndex = length == 0 ? start : Math.Max(start, start + length - 1);
        var to = LineEnd(text, endIndex);
        var changed = string.Join("\n", text[from..to].Split('\n').Select(transform));
        rtbPreview.Select(from, to - from);
        rtbPreview.SelectedText = changed;
        rtbPreview.Select(from, changed.Length);
        rtbPreview.Focus();
    }

    private void ToggleStyle(FontStyle style) {
        if (_mode != ViewMode.Real)
            return;
        SelectNextWord();
        if (style == FontStyle.Bold && SelectionIsHeading())
            ClearHeadingStyle();
        var current = rtbPreview.SelectionFont;
        var family = current?.FontFamily.Name ?? "Segoe UI";
        var size = current?.Size ?? 12f;
        var next = current is null ? style : current.Style ^ style;
        rtbPreview.SelectionFont = new Font(family, size, next);
        rtbPreview.Focus();
    }

    private bool SelectionIsHeading() {
        var savedStart = rtbPreview.SelectionStart;
        var savedLength = rtbPreview.SelectionLength;
        if (savedLength == 0)
            return false;
        rtbPreview.Select(savedStart, 1);
        var size = rtbPreview.SelectionFont?.Size ?? 0f;
        rtbPreview.Select(savedStart, savedLength);
        return size >= 16f;
    }

    private void ClearHeadingStyle() {
        var text = rtbPreview.Text;
        var wordStart = rtbPreview.SelectionStart;
        var wordLength = rtbPreview.SelectionLength;
        var from = LineStart(text, wordStart);
        var to = LineEnd(text, wordStart);
        rtbPreview.Select(from, Math.Max(0, to - from));
        rtbPreview.SelectionFont = new Font("Segoe UI", 12f, FontStyle.Regular);
        rtbPreview.SelectionBackColor = _dark ? Color.Black : Color.White;
        rtbPreview.SelectionColor = _dark ? Color.White : Color.FromArgb(28, 28, 28);
        var format = new TitleParagraph {
            rgxTabs = new int[32],
            dwMask = 0x40 | 0x80,
            dySpaceBefore = 0,
            dySpaceAfter = 0
        };
        format.cbSize = Marshal.SizeOf(format);
        SendMessage(rtbPreview.Handle, 0x447, IntPtr.Zero, ref format);
        rtbPreview.Select(wordStart, wordLength);
    }

    private void FormatParagraph(float size, FontStyle style, bool bullet) {
        if (_mode != ViewMode.Real)
            return;
        SelectNextWord();
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
        rtbPreview.SelectionBackColor = _dark ? Color.Black : Color.White;
        rtbPreview.SelectionColor = _dark ? Color.White : Color.FromArgb(28, 28, 28);
        rtbPreview.SelectionFont = new Font("Segoe UI", size, style);
        if (size >= 16f)
            ApplyTitleRule();
        rtbPreview.Focus();
    }

    private void ApplyTitleRule() {
        var format = new TitleParagraph {
            rgxTabs = new int[32],
            dwMask = 0x40 | 0x80,
            dySpaceBefore = 240,
            dySpaceAfter = 150
        };
        format.cbSize = Marshal.SizeOf(format);
        SendMessage(rtbPreview.Handle, 0x447, IntPtr.Zero, ref format);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TitleParagraph {
        public int cbSize;
        public uint dwMask;
        public short wNumbering;
        public short wEffects;
        public int dxStartIndent;
        public int dxRightIndent;
        public int dxOffset;
        public short wAlignment;
        public short cTabCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public int[] rgxTabs;
        public int dySpaceBefore;
        public int dySpaceAfter;
        public int dyLineSpacing;
        public short sStyle;
        public byte bLineSpacingRule;
        public byte bOutlineLevel;
        public short wShadingWeight;
        public short wShadingStyle;
        public short wNumberingStart;
        public short wNumberingStyle;
        public short wNumberingTab;
        public short wBorderSpace;
        public short wBorderWidth;
        public short wBorders;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref TitleParagraph format);

    [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point point);

    [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void SelectNextWord() {
        if (rtbPreview.SelectionLength > 0)
            return;
        var text = rtbPreview.Text;
        var start = Math.Clamp(rtbPreview.SelectionStart, 0, text.Length);
        if (start < text.Length && IsWordChar(text[start])) {
            while (start > 0 && IsWordChar(text[start - 1]))
                start--;
        }
        else {
            while (start < text.Length && !IsWordChar(text[start]))
                start++;
        }

        if (start >= text.Length)
            return;
        var end = start + 1;
        while (end < text.Length && IsWordChar(text[end]))
            end++;
        rtbPreview.Select(start, end - start);
    }

    private static bool IsWordChar(char c) {
        return char.IsLetterOrDigit(c) || c is '_' or '\'' or '-';
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
        var selected = rtbPreview.SelectedText.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        var picked = FormMarkdownTarget.Prompt(this, image, selected);
        rtbPreview.Focus();
        if (picked is null)
            return;
        if (_mode == ViewMode.Edit) {
            var definition = picked.Value.Definition.Replace("]", "\\]");
            var url = picked.Value.Url;
            rtbPreview.SelectedText = image
                ? "![" + definition + "](" + url + ")"
                : "[" + definition + "](" + url + ")";
            return;
        }

        var snippet = image
            ? RichMarkdown.ImageRtf(picked.Value.Definition, picked.Value.Url, _dark)
            : RichMarkdown.LinkRtf(picked.Value.Definition, picked.Value.Url);
        rtbPreview.SelectedRtf = snippet;
    }

    private void rtbPreview_KeyDown(object sender, KeyEventArgs e) {
        if (_mode != ViewMode.Real || e.KeyCode != Keys.Enter || e.Control || e.Alt)
            return;
        e.SuppressKeyPress = true;
        e.Handled = true;
        BeginInvoke(() => InsertPreviewText("\n"));
    }

    private void rtbPreview_TextChanged(object sender, EventArgs e) {
        if (_suppress || !_live)
            return;
        if (_mode == ViewMode.Edit)
            _markdown = Normalize(rtbPreview.Text);
        else if (_mode == ViewMode.Real) {
            if (rtbPreview.Rtf != _shownRtf) {
                _realEdited = true;
                _shownRtf = rtbPreview.Rtf ?? "";
                _markdown = Normalize(RichMarkdown.FromRtf(rtbPreview.Rtf));
            }

            CollectTitleRules();
        }
        else
            return;

        btnSave.Enabled = _markdown != _savedText;
    }

    private void AttachTitleRules() {
        void Attach() {
            _titleWindow?.ReleaseHandle();
            _titleWindow = new TitleRuleWindow(DrawTitleRules);
            _titleWindow.AssignHandle(rtbPreview.Handle);
        }

        if (rtbPreview.IsHandleCreated)
            Attach();
        rtbPreview.HandleCreated += (_, _) => Attach();
        rtbPreview.HandleDestroyed += (_, _) => _titleWindow?.ReleaseHandle();
    }

    private void CollectTitleRules() {
        _titleMarks.Clear();
        if (_mode != ViewMode.Real || !rtbPreview.IsHandleCreated) {
            rtbPreview.Invalidate();
            return;
        }

        var text = rtbPreview.Text;
        var start = rtbPreview.SelectionStart;
        var length = rtbPreview.SelectionLength;
        var scroll = Point.Empty;
        SendMessage(rtbPreview.Handle, 0x04DD, IntPtr.Zero, ref scroll);
        SendMessage(rtbPreview.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
        _suppress = true;
        try {
            var at = 0;
            while (at < text.Length) {
                var end = text.IndexOf('\n', at);
                if (end < 0)
                    end = text.Length;
                if (end > at) {
                    rtbPreview.Select(at, 1);
                    var font = rtbPreview.SelectionFont;
                    if (font is { Bold: true, Size: >= 12.5f })
                        _titleMarks.Add(new TitleMark(end, font.Height));
                }

                if (end >= text.Length)
                    break;
                at = end + 1;
            }
        }
        finally {
            SendMessage(rtbPreview.Handle, 0x04DE, IntPtr.Zero, ref scroll);
            SendMessage(rtbPreview.Handle, 0x000B, new IntPtr(1), IntPtr.Zero);
            rtbPreview.Select(start, length);
            _suppress = false;
            rtbPreview.Invalidate();
        }
    }

    private void DrawTitleRules() {
        if (_drawingTitles || _mode != ViewMode.Real || _titleMarks.Count == 0 || !rtbPreview.IsHandleCreated)
            return;
        _drawingTitles = true;
        try {
            using var graphics = Graphics.FromHwnd(rtbPreview.Handle);
            graphics.PageUnit = GraphicsUnit.Pixel;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            var dark = Color.FromArgb(45, 50, 60);
            var gray = Color.FromArgb(168, 168, 168);
            using var top = new SolidBrush(_dark ? gray : dark);
            using var bottom = new SolidBrush(_dark ? dark : gray);
            var area = new Rect();
            SendMessage(rtbPreview.Handle, 0x00B2, IntPtr.Zero, ref area);
            if (area.Right <= area.Left)
                area = new Rect { Right = rtbPreview.ClientSize.Width, Bottom = rtbPreview.ClientSize.Height };
            var margin = pnlPreview.Padding.Right;
            var left = Math.Max(0, area.Left);
            var right = Math.Min(area.Right, rtbPreview.ClientSize.Width) - margin + 2;
            var limit = rtbPreview.ClientSize.Height;
            if (right <= left)
                return;
            graphics.SetClip(rtbPreview.ClientRectangle);
            var zoom = rtbPreview.ZoomFactor;
            if (zoom <= 0f)
                zoom = 1f;
            var pad = Math.Max(2, (int)Math.Round(8 * zoom));
            foreach (var mark in _titleMarks) {
                if (mark.Index < 0 || mark.Index > rtbPreview.TextLength)
                    continue;
                var origin = rtbPreview.GetPositionFromCharIndex(Math.Max(0, mark.Index - 1));
                var textBottom = origin.Y + (int)Math.Round(mark.Height * zoom);
                var y = textBottom + Math.Max(1, (int)Math.Round(2 * zoom));
                if (mark.Index < rtbPreview.TextLength) {
                    var next = rtbPreview.GetPositionFromCharIndex(mark.Index + 1);
                    if (next.Y > textBottom)
                        y = next.Y - pad;
                }

                if (y < textBottom)
                    y = textBottom;

                if (y < 0 || y + 1 >= limit)
                    continue;
                var span = right - left;
                graphics.FillRectangle(top, left, y, span, 1);
                graphics.FillRectangle(bottom, left, y + 1, span, 1);
            }
        }
        finally {
            _drawingTitles = false;
        }
    }

    private readonly record struct TitleMark(int Index, int Height);

    private sealed class TitleRuleWindow : NativeWindow {
        private readonly Action _paint;

        public TitleRuleWindow(Action paint) {
            _paint = paint;
        }

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if (m.Msg == 0x000F)
                _paint();
        }
    }

    private void CaptureMarkdown() {
        if (_mode == ViewMode.Edit)
            _markdown = Normalize(rtbPreview.Text);
        else if (_mode == ViewMode.Real && _realEdited)
            _markdown = Normalize(RichMarkdown.FromRtf(rtbPreview.Rtf));
    }

    private void ShowPlain() {
        _suppress = true;
        try {
            rtbPreview.WordWrap = false;
            rtbPreview.Font = _plainFont;
            rtbPreview.Text = _markdown;
            ApplyEditorColors(false);
        }
        finally {
            _suppress = false;
        }
    }

    private void ShowRich() {
        _suppress = true;
        try {
            rtbPreview.WordWrap = true;
            rtbPreview.Font = _richFont;
            ApplyEditorColors(true);
            rtbPreview.Clear();
            rtbPreview.Rtf = RichMarkdown.ToRtf(_markdown, _dark);
            ApplyEditorColors(true);
            _shownRtf = rtbPreview.Rtf ?? "";
        }
        finally {
            _suppress = false;
        }
    }

    private void btnTheme_Click(object sender, EventArgs e) {
        _dark = !_dark;
        btnTheme.Text = _dark ? "DARK" : "WHITE";
        if (_mode != ViewMode.Real)
            return;
        var start = rtbPreview.SelectionStart;
        var length = rtbPreview.SelectionLength;
        var scroll = Point.Empty;
        SendMessage(rtbPreview.Handle, 0x04DD, IntPtr.Zero, ref scroll);
        ShowRich();
        rtbPreview.Select(start, length);
        SendMessage(rtbPreview.Handle, 0x04DE, IntPtr.Zero, ref scroll);
        rtbPreview.Invalidate();
    }

    private void ApplyEditorColors(bool real) {
        var back = real && _dark ? Color.Black : Color.White;
        var fore = real && _dark ? Color.White : Color.FromArgb(28, 28, 28);
        pnlPreview.BackColor = back;
        rtbPreview.BackColor = back;
        rtbPreview.ForeColor = fore;
        if (!real || rtbPreview.TextLength == 0)
            return;
        var start = rtbPreview.SelectionStart;
        var length = rtbPreview.SelectionLength;
        rtbPreview.SelectAll();
        rtbPreview.SelectionColor = fore;
        rtbPreview.Select(start, length);
    }

    private static string Normalize(string? text) {
        return (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
