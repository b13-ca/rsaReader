using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace rsaReader;

internal static class RichMarkdown {
    private const string HardBreakMark = "\\v br:\\v0 ";

    public const string HardBreakRtf = "{\\rtf1\\ansi\\v br:\\v0\\line}";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    private static Color _pictureBack = Color.White;

    public static string ToRtf(string? markdown, bool dark = false) {
        var previous = _pictureBack;
        _pictureBack = dark ? Color.Black : Color.White;
        try {
            return BuildRtf(markdown, dark);
        }
        finally {
            _pictureBack = previous;
        }
    }

    public static int SourceIndex(string? actualText, int caret, int markdownLength) {
        return _map.SourceIndex(actualText ?? "", caret, markdownLength);
    }

    public static int ControlCaret(string? actualText, int markdownIndex) {
        return _map.ControlCaret(actualText ?? "", markdownIndex);
    }

    private static string BuildRtf(string? markdown, bool dark) {
        var text = markdown ?? "";
        _note = text;
        var source = KeepVisibleBreaks(text, out var origin);
        _origin = origin;
        _originLength = text.Length;
        _carets = new CaretMap();
        try {
            return BuildDocument(source, dark);
        }
        finally {
            _map = _carets ?? new CaretMap();
            _carets = null;
        }
    }

    private static string BuildDocument(string source, bool dark) {
        var document = Markdown.Parse(source, Pipeline);
        var rtf = new StringBuilder();
        var map = new SourceMap(source);
        var codeBack = dark ? "\\red46\\green46\\blue46" : "\\red243\\green243\\blue243";
        rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0\\deflang1033");
        rtf.Append("{\\fonttbl{\\f0\\fnil\\fcharset0 Segoe UI;}{\\f1\\fnil\\fcharset0 Consolas;}}");
        rtf.Append("{\\colortbl ;\\red11\\green87\\blue208;").Append(codeBack).Append(";}");
        rtf.Append("\\viewkind4\\uc1");
        var covered = 0;
        var any = false;
        foreach (var block in document) {
            var start = block.Span.IsEmpty ? covered : BlockStart(block);
            EmitEmptyLines(rtf, any ? covered : 0, start);
            RenderBlock(rtf, map, block, 0);
            if (!block.Span.IsEmpty)
                covered = BlockEnd(block);
            any = true;
        }

        EmitEmptyLines(rtf, covered, _note.Length);
        if (!any && _note.IndexOf('\n') < 0)
            rtf.Append("\\pard\\f0\\fs24\\par");
        rtf.Append('}');
        return rtf.ToString();
    }

    private static void EmitEmptyLines(StringBuilder rtf, int from, int to) {
        if (from < 0)
            from = 0;
        if (to > _note.Length)
            to = _note.Length;
        for (var i = from; i < to; i++) {
            if (_note[i] != '\n')
                continue;
            if (i > 0 && _note[i - 1] != '\n')
                continue;
            if (_carets?.HasBreak(i) == true)
                continue;
            rtf.Append("\\pard\\f0\\fs24\\par");
            _carets?.Add('\n', i, false);
        }
    }

    private static int[] _origin = Array.Empty<int>();
    private static int _originLength;
    private static string _note = "";
    private static CaretMap? _carets;
    private static CaretMap _map = new();

    private static int Here(int renderIndex) {
        if (renderIndex < 0)
            return 0;
        if ((uint)renderIndex >= (uint)_origin.Length)
            return _originLength;
        return _origin[renderIndex];
    }

    private static int BlockStart(MarkdownObject block) {
        return block.Span.IsEmpty ? 0 : Here(block.Span.Start);
    }

    private static int BlockEnd(Block block) {
        return block.Span.IsEmpty ? _originLength : Here(block.Span.End + 1);
    }

    public static string FromRtf(string? rtf) {
        if (string.IsNullOrWhiteSpace(rtf))
            return "";
        return new RtfReader(rtf).Read();
    }

    public static string LinkRtf(string text, string url) {
        var rtf = new StringBuilder();
        rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0{\\fonttbl{\\f0\\fnil\\fcharset0 Segoe UI;}}{\\colortbl ;\\red11\\green87\\blue208;}\\f0\\fs24");
        RenderLink(rtf, text, url);
        rtf.Append('}');
        return rtf.ToString();
    }

    public static string ImageRtf(string alt, string url, bool dark = false) {
        var previous = _pictureBack;
        _pictureBack = dark ? Color.Black : Color.White;
        try {
            var rtf = new StringBuilder();
            rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0{\\fonttbl{\\f0\\fnil\\fcharset0 Segoe UI;}}{\\colortbl ;\\red11\\green87\\blue208;}\\f0\\fs24 ");
            RenderImage(rtf, alt, url);
            rtf.Append('}');
            return rtf.ToString();
        }
        finally {
            _pictureBack = previous;
        }
    }

    private static void RenderBlock(StringBuilder rtf, SourceMap map, Block block, int depth) {
        switch (block) {
            case HeadingBlock heading:
                RenderParagraph(rtf, heading.Inline, HeadingSize(heading.Level), FontStyleKind.Bold, false, 0, null, map.Take(heading), BlockStart(heading), BlockEnd(heading));
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, false, depth, null, map.Take(paragraph), BlockStart(paragraph), BlockEnd(paragraph));
                break;
            case ListBlock list:
                RenderList(rtf, map, list, depth);
                break;
            case FencedCodeBlock or CodeBlock:
                RenderCode(rtf, (LeafBlock)block, map.Take(block));
                break;
            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderQuote(rtf, map, child, depth + 1, 1);
                break;
            case ThematicBreakBlock:
                rtf.Append("\\pard\\f0\\fs24 ");
                AppendGap(rtf, map.Take(block), BlockStart(block));
                WritePlain(rtf, "---", BlockStart(block));
                EndBlock(rtf, BlockEnd(block));
                break;
            default:
                RenderKept(rtf, map, block);
                break;
        }
    }

    private static void RenderQuote(StringBuilder rtf, SourceMap map, Block block, int depth, int quotes) {
        if (block is QuoteBlock nested) {
            foreach (var child in nested)
                RenderQuote(rtf, map, child, depth + 1, quotes + 1);
            return;
        }

        if (block is ParagraphBlock paragraph) {
            RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, false, depth, new string('>', quotes) + " ", map.Take(paragraph), BlockStart(paragraph), BlockEnd(paragraph));
            return;
        }

        RenderBlock(rtf, map, block, depth);
    }

    private static void RenderList(StringBuilder rtf, SourceMap map, ListBlock list, int depth) {
        var number = 1;
        if (list.IsOrdered && int.TryParse(list.OrderedStart, out var ordered))
            number = ordered;

        foreach (var item in list.OfType<ListItemBlock>()) {
            var task = TryTask(item, out var taskChecked);
            var marker = !list.IsOrdered ? null : number + ". ";
            var first = true;
            foreach (var child in item) {
                if (child is ParagraphBlock paragraph && first) {
                    RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, !list.IsOrdered, depth, task ? (taskChecked ? "1" : "0") : marker, map.Take(paragraph), BlockStart(paragraph), BlockEnd(paragraph));
                    first = false;
                }
                else if (child is ListBlock nested)
                    RenderList(rtf, map, nested, depth + 1);
                else
                    RenderBlock(rtf, map, child, depth);
            }

            if (list.IsOrdered)
                number++;
        }
    }

    private static bool TryTask(ListItemBlock item, out bool isChecked) {
        isChecked = false;
        if (item.Count == 0 || item[0] is not ParagraphBlock paragraph)
            return false;
        if (paragraph.Inline?.FirstChild is not Markdig.Extensions.TaskLists.TaskList task)
            return false;
        isChecked = task.Checked;
        return true;
    }

    private static void RenderCode(StringBuilder rtf, LeafBlock block, int? gap) {
        rtf.Append("\\pard\\highlight2\\cb2\\f1\\fs22 ");
        AppendGap(rtf, gap, BlockStart(block));
        var lines = block.Lines;
        for (var i = 0; i < lines.Count; i++) {
            if (i > 0) {
                var previous = lines.Lines[i - 1];
                WriteChar(rtf, '\n', Here(previous.Position + previous.ToString().Length));
            }

            var line = lines.Lines[i];
            var text = line.ToString();
            for (var c = 0; c < text.Length; c++)
                WriteChar(rtf, text[c], Here(line.Position + c));
        }

        rtf.Append("\\highlight0\\cb0\\f0\\fs24");
        EndBlock(rtf, BlockEnd(block));
    }

    private static void RenderKept(StringBuilder rtf, SourceMap map, Block block) {
        var text = Source(map.Text, block);
        if (text.Length == 0)
            return;
        var start = BlockStart(block);
        rtf.Append("\\pard\\f0\\fs24 ");
        AppendGap(rtf, map.Take(block), start);
        rtf.Append("\\v keep:\\v0 ");
        _carets?.Add("keep:", start, true);
        for (var c = 0; c < text.Length; c++)
            WriteChar(rtf, text[c], Here(block.Span.Start + c));
        EndBlock(rtf, BlockEnd(block));
    }

    private static void RenderParagraph(StringBuilder rtf, ContainerInline? inline, int halfPoints, FontStyleKind style, bool bullet, int depth, string? marker, int? gap, int start, int end) {
        var indent = 360 * Math.Max(depth, bullet ? 1 : 0);
        rtf.Append("\\pard");
        if (halfPoints >= 26)
            rtf.Append("\\sb240\\sa150");
        if (bullet) {
            rtf.Append("{\\*\\pn\\pnlvlblt\\pnf0\\pnindent0{\\pntxtb\\'B7}}\\fi-360");
            indent = Math.Max(indent, 720);
        }

        if (indent > 0)
            rtf.Append("\\li").Append(indent);
        rtf.Append("\\f0\\fs").Append(halfPoints);
        if (style == FontStyleKind.Bold)
            rtf.Append("\\b");
        if (style == FontStyleKind.Italic)
            rtf.Append("\\i");
        rtf.Append(' ');
        AppendGap(rtf, gap, start);
        if (marker is "0" or "1") {
            var label = "task:" + marker;
            rtf.Append("\\v ").Append(label).Append("\\v0 ");
            _carets?.Add(label, start, true);
        }
        else if (!string.IsNullOrEmpty(marker))
            WritePlain(rtf, marker, start);
        if (inline is not null)
            RenderInline(rtf, inline, style == FontStyleKind.Bold, style == FontStyleKind.Italic);
        if (style == FontStyleKind.Bold)
            rtf.Append("\\b0");
        if (style == FontStyleKind.Italic)
            rtf.Append("\\i0");
        EndBlock(rtf, end);
    }

    private static void RenderInline(StringBuilder rtf, ContainerInline inline, bool bold, bool italic) {
        for (var child = inline.FirstChild; child is not null; child = child.NextSibling)
            RenderOne(rtf, child, bold, italic);
    }

    private static void RenderOne(StringBuilder rtf, Inline inline, bool bold, bool italic) {
        switch (inline) {
            case LiteralInline literal:
                WriteSpan(rtf, literal.Content.ToString(), literal.Span);
                break;
            case LineBreakInline line:
                var at = Here(line.Span.Start);
                if (line.IsHard && !Draws(line.NextSibling) && EmptyLineFollows(at))
                    AppendParagraphBreak(rtf, at);
                else if (line.IsHard)
                    AppendHardBreak(rtf, at);
                else
                    WriteChar(rtf, '\n', at);
                break;
            case CodeInline code:
                rtf.Append("\\f1\\fs22\\highlight2\\cb2 ");
                WriteCode(rtf, code);
                rtf.Append("\\highlight0\\cb0\\f0\\fs24 ");
                if (bold)
                    rtf.Append("\\b");
                if (italic)
                    rtf.Append("\\i");
                break;
            case EmphasisInline emphasis:
                var strong = emphasis.DelimiterCount >= 2;
                var emph = !strong;
                if (strong)
                    rtf.Append("\\b ");
                if (emph)
                    rtf.Append("\\i ");
                RenderInline(rtf, emphasis, bold || strong, italic || emph);
                if (strong)
                    rtf.Append("\\b0 ");
                if (emph)
                    rtf.Append("\\i0 ");
                if (bold)
                    rtf.Append("\\b ");
                if (italic)
                    rtf.Append("\\i ");
                break;
            case LinkInline link when link.IsImage:
                RenderImage(rtf, InlineText(link), link.Url ?? "", BlockStart(link));
                break;
            case LinkInline link:
                RenderLink(rtf, link, BlockStart(link));
                break;
            case Markdig.Extensions.TaskLists.TaskList:
                break;
            case AutolinkInline autolink:
                RenderLink(rtf, autolink.Url, autolink.Url, BlockStart(autolink));
                break;
            case HtmlEntityInline entity:
                WritePlain(rtf, entity.Transcoded.ToString(), BlockStart(entity));
                break;
            case HtmlInline html when html.Tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase):
                AppendHardBreak(rtf, Here(html.Span.End + 1));
                break;
            case ContainerInline container:
                RenderInline(rtf, container, bold, italic);
                break;
        }
    }

    private static void RenderLink(StringBuilder rtf, LinkInline link, int index) {
        var text = new StringBuilder();
        CollectPlain(link, text);
        RenderLink(rtf, text.ToString(), link.Url ?? "", index);
    }

    private static void RenderLink(StringBuilder rtf, string text, string url, int index = 0) {
        if (text.Length == 0)
            text = url;
        AppendMarker(rtf, "lnk:", text, url, index);
        AppendLinkField(rtf, text, url, index);
    }

    private static void AppendLinkField(StringBuilder rtf, string text, string url, int index) {
        rtf.Append("{\\field{\\*\\fldinst{HYPERLINK \"");
        AppendText(rtf, url);
        rtf.Append("\"}}{\\fldrslt{\\ul\\cf1 ");
        WritePlain(rtf, text, index);
        rtf.Append("\\ul0\\cf0}}}");
    }

    private static void AppendMarker(StringBuilder rtf, string kind, string text, string url, int index) {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text + "\n" + url));
        var label = kind + payload;
        rtf.Append("\\v ").Append(label).Append("\\v0 ");
        _carets?.Add(label, index, true);
    }

    private static void RenderImage(StringBuilder rtf, string alt, string url, int index = 0) {
        AppendMarker(rtf, "img:", alt, url, index);
        var picture = TryPicture(url);
        if (picture is not null) {
            rtf.Append(picture);
            _carets?.Add('\uFFFC', index, false);
        }
        else
            AppendLinkField(rtf, string.IsNullOrEmpty(alt) ? url : alt, url, index);
    }

    private static string? TryPicture(string url) {
        var bytes = LoadBytes(url);
        if (bytes is null || bytes.Length == 0)
            return null;
        try {
            using var input = new MemoryStream(bytes);
            using var image = Image.FromStream(input);
            var width = Math.Min(image.Width, 520);
            var height = Math.Max(1, image.Height * width / Math.Max(1, image.Width));
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var draw = Graphics.FromImage(bitmap)) {
                draw.Clear(_pictureBack);
                draw.DrawImage(image, 0, 0, width, height);
            }

            return MetafilePicture(bitmap, width, height);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or ExternalException) {
            return null;
        }
    }

    private static string? MetafilePicture(Image image, int width, int height) {
        using var stream = new MemoryStream();
        using var screen = Graphics.FromHwnd(IntPtr.Zero);
        var hdc = screen.GetHdc();
        Metafile metafile;
        try {
            metafile = new Metafile(stream, hdc, new Rectangle(0, 0, width, height), MetafileFrameUnit.Pixel, EmfType.EmfOnly);
        }
        finally {
            screen.ReleaseHdc(hdc);
        }

        using (var draw = Graphics.FromImage(metafile))
            draw.DrawImage(image, 0, 0, width, height);

        var handle = metafile.GetHenhmetafile();
        try {
            var size = GdipEmfToWmfBits(handle, 0, null, MmAnisotropic, 0);
            if (size <= 0)
                return null;
            var buffer = new byte[size];
            if (GdipEmfToWmfBits(handle, (uint)size, buffer, MmAnisotropic, 0) <= 0)
                return null;
            var picw = width * 2540 / 96;
            var pich = height * 2540 / 96;
            var goalW = width * 1440 / 96;
            var goalH = height * 1440 / 96;
            var rtf = new StringBuilder(buffer.Length * 2 + 64);
            rtf.Append("{\\pict\\wmetafile8\\picw").Append(picw);
            rtf.Append("\\pich").Append(pich);
            rtf.Append("\\picwgoal").Append(goalW);
            rtf.Append("\\pichgoal").Append(goalH).Append(' ');
            for (var i = 0; i < buffer.Length; i++) {
                rtf.Append(buffer[i].ToString("x2"));
                if (i % 64 == 63)
                    rtf.Append('\n');
            }

            rtf.Append('}');
            return rtf.ToString();
        }
        finally {
            DeleteEnhMetaFile(handle);
            metafile.Dispose();
        }
    }

    private const int MmAnisotropic = 8;

    [DllImport("gdiplus.dll")]
    private static extern int GdipEmfToWmfBits(IntPtr hemf, uint bufferSize, byte[]? buffer, int mappingMode, int flags);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteEnhMetaFile(IntPtr hemf);

    private static byte[]? LoadBytes(string url) {
        try {
            if (File.Exists(url))
                return File.ReadAllBytes(url);
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile && File.Exists(uri.LocalPath))
                return File.ReadAllBytes(uri.LocalPath);
            if (uri is null || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return null;
            return Task.Run(async () => {
                using var response = await Http.GetAsync(uri).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;
                var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return data.Length > 5_000_000 ? null : data;
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException or UnauthorizedAccessException or InvalidOperationException) {
            return null;
        }
    }

    private static void CollectPlain(ContainerInline inline, StringBuilder text) {
        for (var child = inline.FirstChild; child is not null; child = child.NextSibling) {
            switch (child) {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.ToString());
                    break;
                case ContainerInline container:
                    CollectPlain(container, text);
                    break;
            }
        }
    }

    private static string InlineText(ContainerInline inline) {
        var text = new StringBuilder();
        CollectPlain(inline, text);
        return text.ToString();
    }

    private static void AppendHardBreak(StringBuilder rtf, int index) {
        rtf.Append(HardBreakMark);
        rtf.Append("\\line ");
        _carets?.Add("br:", index, true);
        _carets?.Add('\n', index, false, true);
    }

    private static void AppendParagraphBreak(StringBuilder rtf, int index) {
        rtf.Append("\\par");
        _carets?.Add('\n', index, false);
    }

    private static bool EmptyLineFollows(int newlineIndex) {
        var next = newlineIndex + 1;
        return next < _note.Length && _note[next] == '\n';
    }

    private static bool Draws(Inline? inline) {
        for (var child = inline; child is not null; child = child.NextSibling) {
            switch (child) {
                case LiteralInline literal:
                    foreach (var c in literal.Content.ToString())
                        if (c != '\u200B' && c != '\r' && !char.IsWhiteSpace(c))
                            return true;
                    break;
                case ContainerInline container:
                    if (Draws(container.FirstChild))
                        return true;
                    break;
                default:
                    return true;
            }
        }

        return false;
    }

    private static void AppendGap(StringBuilder rtf, int? gap, int index) {
        if (gap is int count) {
            var label = "gap:" + count;
            rtf.Append("\\v ").Append(label).Append("\\v0 ");
            _carets?.Add(label, index, true);
        }
    }

    private static void EndBlock(StringBuilder rtf, int index) {
        rtf.Append("\\par");
        _carets?.Paragraph(index);
    }

    private static void WriteCode(StringBuilder rtf, CodeInline code) {
        var content = code.Content;
        var span = code.Span;
        var start = 0;
        if (!span.IsEmpty) {
            start = span.Start;
            var width = span.End - span.Start + 1;
            if (width > content.Length)
                start += (width - content.Length) / 2;
        }

        for (var i = 0; i < content.Length; i++)
            WriteChar(rtf, content[i], Here(start + i));
    }

    private static void WriteSpan(StringBuilder rtf, string text, SourceSpan span) {
        if (text.Length == 0)
            return;
        if (span.IsEmpty || span.End < span.Start || span.End - span.Start + 1 != text.Length) {
            WritePlain(rtf, text, span.IsEmpty ? 0 : Here(span.Start));
            return;
        }

        for (var i = 0; i < text.Length; i++)
            WriteChar(rtf, text[i], Here(span.Start + i));
    }

    private static void WritePlain(StringBuilder rtf, string text, int index) {
        foreach (var c in text)
            WriteChar(rtf, c, index);
    }

    private static void WriteChar(StringBuilder rtf, char c, int index) {
        switch (c) {
            case '\\' or '{' or '}':
                rtf.Append('\\').Append(c);
                _carets?.Add(c, index, false);
                break;
            case '\n':
                rtf.Append("\\line ");
                _carets?.Add('\n', index, false);
                break;
            case '\r':
            case '\u200B':
                break;
            default:
                if (c < 128)
                    rtf.Append(c);
                else
                    rtf.Append("\\u").Append((short)c).Append('?');
                _carets?.Add(c, index, false);
                break;
        }
    }

    private static void AppendText(StringBuilder rtf, string text) {
        foreach (var c in text) {
            switch (c) {
                case '\\' or '{' or '}':
                    rtf.Append('\\').Append(c);
                    break;
                case '\n':
                    rtf.Append("\\line ");
                    break;
                case '\r':
                case '\u200B':
                    break;
                default:
                    if (c < 128)
                        rtf.Append(c);
                    else
                        rtf.Append("\\u").Append((short)c).Append('?');
                    break;
            }
        }
    }

    private sealed class SourceMap {
        public SourceMap(string text) {
            Text = text;
        }

        public string Text { get; }
        private int _previousEnd = -1;

        public int? Take(Block block) {
            var span = block.Span;
            if (span.IsEmpty || span.Start < 0 || span.End >= Text.Length)
                return null;
            int? gap = 0;
            if (_previousEnd >= 0 && span.Start > _previousEnd) {
                var count = 0;
                for (var i = _previousEnd + 1; i < span.Start && i < Text.Length; i++)
                    if (Text[i] == '\n')
                        count++;
                gap = count;
            }

            if (span.End > _previousEnd)
                _previousEnd = span.End;
            return gap;
        }
    }

    private sealed class CaretMap {
        private readonly List<Slot> _slots = new();
        private bool _pending;
        private int _pendingIndex;

        public void Add(char c, int index, bool hidden, bool hard = false) {
            Flush();
            _slots.Add(new Slot(c, index, hidden, hard));
        }

        public void Add(string text, int index, bool hidden, bool hard = false) {
            foreach (var c in text)
                Add(c, index, hidden, hard && c == '\n');
        }

        public void Paragraph(int index) {
            _pending = true;
            _pendingIndex = index;
        }

        public bool HasBreak(int index) {
            if (_pending && _pendingIndex == index)
                return true;
            foreach (var slot in _slots)
                if (!slot.Hidden && slot.Char == '\n' && slot.Index == index)
                    return true;
            return false;
        }

        private void Flush() {
            if (!_pending)
                return;
            _pending = false;
            _slots.Add(new Slot('\n', _pendingIndex, false, false));
        }

        public int SourceIndex(string actual, int caret, int markdownLength) {
            if (_slots.Count == 0)
                return caret <= 0 ? 0 : markdownLength;
            caret = Math.Clamp(caret, 0, actual.Length);
            if (Same(actual))
                return caret >= _slots.Count ? markdownLength : _slots[caret].Index;
            var slot = Align(actual, caret);
            if (caret >= actual.Length || slot >= _slots.Count)
                return markdownLength;
            return _slots[slot].Index;
        }

        public int ControlCaret(string actual, int markdownIndex) {
            markdownIndex = Math.Clamp(markdownIndex, 0, _note.Length);
            var line = LineOf(_note, markdownIndex);
            var predicted = _slots.Count;
            for (var i = 0; i < _slots.Count; i++) {
                var slot = _slots[i];
                if (slot.Hidden)
                    continue;
                if (LineOf(_note, slot.Index) > line) {
                    predicted = i;
                    break;
                }

                if (slot.Index >= markdownIndex) {
                    predicted = i;
                    break;
                }
            }

            if (Same(actual))
                return Math.Min(predicted, actual.Length);
            return MapPredicted(actual, predicted);
        }

        private static int LineOf(string text, int index) {
            if (index < 0)
                return 0;
            if (index > text.Length)
                index = text.Length;
            var line = 0;
            for (var i = 0; i < index; i++)
                if (text[i] == '\n')
                    line++;
            return line;
        }

        private bool Same(string actual) {
            if (actual.Length != _slots.Count)
                return false;
            for (var i = 0; i < actual.Length; i++)
                if (actual[i] != _slots[i].Char)
                    return false;
            return true;
        }

        private int Align(string actual, int caret) {
            var actualAt = 0;
            var slot = 0;
            while (actualAt < caret && actualAt < actual.Length && slot < _slots.Count) {
                if (actual[actualAt] == _slots[slot].Char) {
                    actualAt++;
                    slot++;
                    continue;
                }

                var nextSlot = FindSlot(slot + 1, actual[actualAt]);
                var nextActual = FindChar(actual, actualAt + 1, _slots[slot].Char);
                if (nextSlot >= 0 && (nextActual < 0 || nextSlot - slot <= nextActual - actualAt))
                    slot = nextSlot;
                else if (nextActual >= 0)
                    actualAt = nextActual;
                else {
                    actualAt++;
                    slot++;
                }
            }

            return slot;
        }

        private int MapPredicted(string actual, int predicted) {
            var actualAt = 0;
            var slot = 0;
            while (slot < predicted && slot < _slots.Count && actualAt < actual.Length) {
                if (actual[actualAt] == _slots[slot].Char) {
                    actualAt++;
                    slot++;
                    continue;
                }

                var nextSlot = FindSlot(slot + 1, actual[actualAt]);
                var nextActual = FindChar(actual, actualAt + 1, _slots[slot].Char);
                if (nextSlot >= 0 && nextSlot <= predicted && (nextActual < 0 || nextSlot - slot <= nextActual - actualAt))
                    slot = nextSlot;
                else if (nextActual >= 0)
                    actualAt = nextActual;
                else {
                    actualAt++;
                    slot++;
                }
            }

            return Math.Min(actualAt, actual.Length);
        }

        private int FindSlot(int start, char wanted) {
            var end = Math.Min(_slots.Count, start + 32);
            for (var i = start; i < end; i++)
                if (_slots[i].Char == wanted)
                    return i;
            return -1;
        }

        private static int FindChar(string text, int start, char wanted) {
            var end = Math.Min(text.Length, start + 32);
            for (var i = start; i < end; i++)
                if (text[i] == wanted)
                    return i;
            return -1;
        }

        private readonly record struct Slot(char Char, int Index, bool Hidden, bool Hard);
    }

    private static string KeepVisibleBreaks(string markdown, out int[] origin) {
        var source = markdown;
        int[]? raw = null;
        if (markdown.IndexOf('\r') >= 0) {
            var clean = new StringBuilder(markdown.Length);
            var map = new List<int>(markdown.Length);
            for (var n = 0; n < markdown.Length; n++) {
                if (markdown[n] == '\r')
                    continue;
                clean.Append(markdown[n]);
                map.Add(n);
            }

            source = clean.ToString();
            raw = map.ToArray();
        }

        var lines = source.Split('\n');
        var pieces = new List<Piece>(lines.Length);
        var fence = "";
        var cursor = 0;
        for (var i = 0; i < lines.Length; i++) {
            var line = lines[i];
            var lineStart = cursor;
            cursor += line.Length;
            var newlineIndex = cursor;
            var hasNewline = i < lines.Length - 1;
            if (cursor < source.Length && source[cursor] == '\n')
                cursor++;

            var trimmed = line.TrimStart();
            var inside = fence.Length > 0;
            if (inside) {
                if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                    fence = "";
            }
            else if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
                fence = trimmed[..3];

            var hard = !inside && fence.Length == 0 && line.EndsWith("  ", StringComparison.Ordinal);
            if (inside)
                hard = false;
            pieces.Add(new Piece(line, lineStart, hard && line.Trim().Length == 0, false, lineStart, hasNewline, newlineIndex));
            if (!hard)
                continue;
            var next = i + 1 < lines.Length ? lines[i + 1] : null;
            if (next is null || next.Length == 0 || StartsNewBlock(next)) {
                var at = hasNewline ? newlineIndex + 1 : source.Length;
                pieces.Add(new Piece("\u200B", at, false, true, at, false, at));
            }
        }

        var render = new StringBuilder();
        var mapped = new List<int>();
        for (var p = 0; p < pieces.Count; p++) {
            if (p > 0) {
                var previous = pieces[p - 1];
                var newlineAt = !previous.Synthetic && previous.HadNewline
                    ? previous.NewlineIndex
                    : pieces[p].Synthetic ? pieces[p].At : pieces[p].Start;
                render.Append('\n');
                mapped.Add(Original(newlineAt));
            }

            var piece = pieces[p];
            if (piece.Synthetic) {
                render.Append('\u200B');
                mapped.Add(Original(piece.At));
                continue;
            }

            if (piece.Prefix) {
                render.Append('\u200B');
                mapped.Add(Original(piece.Start));
            }

            for (var c = 0; c < piece.Text.Length; c++) {
                render.Append(piece.Text[c]);
                mapped.Add(Original(piece.Start + c));
            }
        }

        origin = mapped.ToArray();
        return render.ToString();

        int Original(int index) {
            if (index < 0)
                return 0;
            if (raw is null)
                return Math.Min(index, markdown.Length);
            if (index >= raw.Length)
                return markdown.Length;
            return raw[index];
        }
    }

    private readonly record struct Piece(string Text, int Start, bool Prefix, bool Synthetic, int At, bool HadNewline, int NewlineIndex);

    private static bool StartsNewBlock(string line) {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
            return false;
        if (trimmed[0] is '#' or '>' or '-' or '*' or '+')
            return true;
        if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            return true;
        var dot = trimmed.IndexOf('.');
        return dot > 0 && trimmed[..dot].All(char.IsDigit);
    }

    private static string Source(string markdown, Block block) {
        var span = block.Span;
        if (span.IsEmpty || span.Start < 0 || span.End >= markdown.Length)
            return "";
        return markdown.Substring(span.Start, span.End - span.Start + 1);
    }

    private static int HeadingSize(int level) {
        return level switch {
            1 => 44,
            2 => 32,
            _ => 26
        };
    }

    private enum FontStyleKind {
        None,
        Bold,
        Italic
    }

    private sealed class RtfReader {
        private readonly string _rtf;
        private int _i;
        private int _uc = 1;
        private bool _bold;
        private bool _italic;
        private bool _hidden;
        private bool _bullet;
        private bool _keep;
        private bool _hardBreak;
        private int _font;
        private int _fs = 24;
        private string? _link;
        private string? _imageMarkdown;
        private string? _pendingMarkdown;
        private string[]? _echoes;
        private readonly StringBuilder _echo = new();
        private readonly Dictionary<int, string> _fonts = new();
        private readonly StringBuilder _run = new();
        private readonly List<Run> _runs = new();
        private readonly StringBuilder _output = new();
        private bool _lastWasCode;
        private int? _gap;

        public RtfReader(string rtf) {
            _rtf = rtf;
        }

        public string Read() {
            ParseGroup(false);
            FlushParagraph();
            if (_lastWasCode)
                _output.Append("\n```");
            return Tidy(_output.ToString());
        }

        private void ParseGroup(bool skipping) {
            while (_i < _rtf.Length) {
                var c = _rtf[_i++];
                if (c == '{') {
                    var word = PeekWord();
                    if (word == "fonttbl") {
                        ParseFontTable();
                        continue;
                    }

                    if (word is "pntext" or "listtext") {
                        _bullet = true;
                        SkipGroup();
                        continue;
                    }

                    if (word is "colortbl" or "stylesheet" or "info" or "pict" or "object" or "generator" or "datastore") {
                        SkipGroup();
                        continue;
                    }

                    if (word == "*") {
                        var star = PeekStarWord();
                        if (star == "fldinst") {
                            ReadFieldInstruction();
                            continue;
                        }

                        if (star == "pn") {
                            ParseGroup(true);
                            continue;
                        }

                        SkipGroup();
                        continue;
                    }

                    if (word == "field") {
                        ParseField();
                        continue;
                    }

                    ParseGroup(skipping);
                    continue;
                }

                if (c == '}')
                    return;
                if (c == '\r' || c == '\n')
                    continue;
                if (c == '\\')
                    Control();
                else if (!skipping)
                    Emit(c);
            }
        }

        private void ParseField() {
            var saved = _link;
            while (_i < _rtf.Length && _rtf[_i] != '}') {
                if (_rtf[_i] != '{') {
                    _i++;
                    continue;
                }

                _i++;
                var word = PeekWord();
                if (word == "*") {
                    if (PeekStarWord() == "fldinst")
                        ReadFieldInstruction();
                    else
                        SkipGroup();
                }
                else if (word == "fldrslt") {
                    if (_imageMarkdown is not null) {
                        EmitRaw(_imageMarkdown);
                        _imageMarkdown = null;
                        SkipGroup();
                    }
                    else
                        ParseGroup(false);
                }
                else
                    ParseGroup(false);
            }

            if (_i < _rtf.Length && _rtf[_i] == '}')
                _i++;
            FlushRun();
            _link = saved;
        }

        private void ReadFieldInstruction() {
            if (_i < _rtf.Length && _rtf[_i] == '{')
                _i++;
            var text = ReadGroupText();
            var marker = "HYPERLINK";
            var at = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
                return;
            at += marker.Length;
            while (at < text.Length && text[at] == ' ')
                at++;
            if (at >= text.Length)
                return;
            string url;
            if (text[at] == '"') {
                var end = text.IndexOf('"', at + 1);
                if (end <= at + 1)
                    return;
                url = text[(at + 1)..end];
            }
            else {
                var end = at;
                while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '}')
                    end++;
                if (end <= at)
                    return;
                url = text[at..end];
            }

            _link = url.Replace("\\\\", "\\");
        }

        private string ReadGroupText() {
            var text = new StringBuilder();
            var depth = 1;
            while (_i < _rtf.Length && depth > 0) {
                var c = _rtf[_i++];
                if (c == '\\') {
                    if (_i < _rtf.Length)
                        text.Append(_rtf[_i++]);
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;
                else
                    text.Append(c);
            }

            return text.ToString();
        }

        private void ParseFontTable() {
            while (_i < _rtf.Length && _rtf[_i] != '}') {
                if (_rtf[_i] == '{') {
                    _i++;
                    ParseFontEntry();
                }
                else
                    _i++;
            }

            if (_i < _rtf.Length && _rtf[_i] == '}')
                _i++;
        }

        private void ParseFontEntry() {
            var id = 0;
            var name = new StringBuilder();
            var naming = false;
            while (_i < _rtf.Length) {
                var c = _rtf[_i++];
                if (c == '}')
                    break;
                if (c == '{') {
                    SkipGroup();
                    continue;
                }

                if (c == '\\') {
                    var word = ReadControl(out var number, out var hasNumber);
                    if (word == "f" && hasNumber)
                        id = number;
                    naming = word is "fnil" or "fswiss" or "froman" or "fmodern" or "fscript" or "fdecor" or "ftech" or "fbidi";
                    continue;
                }

                if (c == ';') {
                    naming = false;
                    continue;
                }

                if (naming || name.Length > 0 || char.IsLetter(c))
                    name.Append(c);
            }

            _fonts[id] = name.ToString().Trim();
        }

        private void Control() {
            if (_i >= _rtf.Length)
                return;
            var c = _rtf[_i];
            if (!char.IsLetter(c)) {
                _i++;
                if (c is '\\' or '{' or '}')
                    Emit(c);
                else if (c == '~')
                    Emit(' ');
                else if (c is '-' or '_')
                    Emit('-');
                else if (c == '\'')
                    HexChar();
                return;
            }

            var word = ReadControl(out var number, out var hasNumber);
            switch (word) {
                case "par":
                    FlushParagraph();
                    _hidden = false;
                    _hardBreak = false;
                    break;
                case "line":
                    FlushRun();
                    var hard = _hardBreak;
                    _hardBreak = false;
                    _runs.Add(new Run(hard ? "  \n" : "\n", _bold, _italic, IsCodeFont(_font), _link, false, _fs));
                    break;
                case "pard":
                    FlushRun();
                    _bullet = false;
                    _hidden = false;
                    _font = 0;
                    _fs = 24;
                    break;
                case "b":
                    FlushRun();
                    _bold = !hasNumber || number != 0;
                    break;
                case "i":
                    FlushRun();
                    _italic = !hasNumber || number != 0;
                    break;
                case "v":
                    FlushRun();
                    _hidden = !hasNumber || number != 0;
                    break;
                case "ul":
                case "ulnone":
                    break;
                case "f":
                    FlushRun();
                    if (hasNumber)
                        _font = number;
                    break;
                case "fs":
                    FlushRun();
                    if (hasNumber)
                        _fs = number;
                    break;
                case "pnlvlblt":
                    _bullet = true;
                    break;
                case "tab":
                    if (_runs.Count > 0 || _run.Length > 0)
                        Emit('\t');
                    break;
                case "uc":
                    if (hasNumber)
                        _uc = number;
                    break;
                case "u":
                    var code = number < 0 ? number + 65536 : number;
                    Emit((char)code);
                    SkipFallback();
                    break;
                case "plain":
                    FlushRun();
                    _bold = false;
                    _italic = false;
                    _hidden = false;
                    _font = 0;
                    _fs = 24;
                    break;
            }
        }

        private void HexChar() {
            if (_i + 1 >= _rtf.Length)
                return;
            var hex = _rtf.Substring(_i, 2);
            _i += 2;
            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
                Emit((char)value);
        }

        private string ReadControl(out int number, out bool hasNumber) {
            var start = _i;
            while (_i < _rtf.Length && char.IsLetter(_rtf[_i]))
                _i++;
            var word = _rtf[start.._i];
            var sign = 1;
            hasNumber = false;
            number = 0;
            if (_i < _rtf.Length && _rtf[_i] == '-') {
                sign = -1;
                _i++;
                hasNumber = true;
            }

            if (_i < _rtf.Length && char.IsDigit(_rtf[_i])) {
                hasNumber = true;
                var n = 0;
                while (_i < _rtf.Length && char.IsDigit(_rtf[_i]))
                    n = n * 10 + (_rtf[_i++] - '0');
                number = sign * n;
            }

            if (_i < _rtf.Length && _rtf[_i] == ' ')
                _i++;
            return word;
        }

        private void SkipFallback() {
            for (var n = 0; n < _uc && _i < _rtf.Length; n++) {
                if (_rtf[_i] == '\\' && _i + 1 < _rtf.Length && _rtf[_i + 1] == '\'') {
                    _i += 4;
                    continue;
                }

                if (_rtf[_i] is '\\' or '{' or '}')
                    break;
                _i++;
            }
        }

        private string PeekWord() {
            if (_i >= _rtf.Length || _rtf[_i] != '\\')
                return "";
            if (_i + 1 < _rtf.Length && _rtf[_i + 1] == '*')
                return "*";
            var at = _i + 1;
            var start = at;
            while (at < _rtf.Length && char.IsLetter(_rtf[at]))
                at++;
            return _rtf[start..at];
        }

        private string PeekStarWord() {
            var at = _i;
            while (at < _rtf.Length && _rtf[at] is '\\' or '*' or ' ' or '\r' or '\n')
                at++;
            if (at >= _rtf.Length || _rtf[at] != '\\')
                return "";
            at++;
            var start = at;
            while (at < _rtf.Length && char.IsLetter(_rtf[at]))
                at++;
            return _rtf[start..at];
        }

        private void SkipGroup() {
            var depth = 1;
            while (_i < _rtf.Length && depth > 0) {
                var c = _rtf[_i++];
                if (c == '\\') {
                    if (_i < _rtf.Length)
                        _i++;
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;
            }
        }

        private void Emit(char c) {
            if (_hidden) {
                _run.Append(c);
                return;
            }

            if (_echoes is not null && TakeEcho(c))
                return;

            if (_imageMarkdown is not null) {
                EmitRaw(_imageMarkdown);
                _imageMarkdown = null;
            }

            _run.Append(c);
        }

        private bool TakeEcho(char c) {
            var next = _echo.ToString() + c;
            var extends = false;
            var exact = false;
            var longer = false;
            foreach (var candidate in _echoes!) {
                if (!candidate.StartsWith(next, StringComparison.Ordinal))
                    continue;
                extends = true;
                if (candidate.Length == next.Length)
                    exact = true;
                else
                    longer = true;
            }

            if (extends) {
                _echo.Append(c);
                if (exact && !longer)
                    CommitPending();
                return true;
            }

            var buffered = _echo.ToString();
            if (buffered.Length == 0) {
                var markdown = _pendingMarkdown;
                ClearPending();
                if (!string.IsNullOrEmpty(markdown))
                    EmitRaw(markdown);
                return false;
            }

            if (IsEcho(buffered)) {
                CommitPending();
                return false;
            }

            AbandonPending(buffered);
            return false;
        }

        private void BeginEcho(string markdown, string text, string url) {
            FinishPending();
            _pendingMarkdown = markdown;
            _echoes = text == url
                ? new[] { "<" + url + ">", url + " <" + url + ">", url }
                : new[] { text + " <" + url + ">", text };
            _echo.Clear();
        }

        private void FinishPending() {
            if (_pendingMarkdown is null && _echoes is null)
                return;
            var buffered = _echo.ToString();
            if (buffered.Length == 0 || IsEcho(buffered))
                CommitPending();
            else
                AbandonPending(buffered);
        }

        private bool IsEcho(string buffered) {
            if (_echoes is null)
                return false;
            foreach (var candidate in _echoes)
                if (candidate == buffered)
                    return true;
            return false;
        }

        private void CommitPending() {
            var markdown = _pendingMarkdown;
            ClearPending();
            if (!string.IsNullOrEmpty(markdown))
                EmitRaw(markdown);
        }

        private void AbandonPending(string buffered) {
            ClearPending();
            foreach (var c in buffered)
                _run.Append(c);
        }

        private void ClearPending() {
            _pendingMarkdown = null;
            _echoes = null;
            _echo.Clear();
        }

        private void EmitRaw(string markdown) {
            FlushRun();
            _runs.Add(new Run(markdown, false, false, false, null, false, _fs, true));
        }

        private void FlushRun() {
            if (_hidden) {
                var hidden = _run.ToString().Trim();
                _run.Clear();
                if (hidden.StartsWith("img:", StringComparison.Ordinal) && TryPair(hidden[4..], out var alt, out var imageUrl))
                    BeginEcho(ImageMarkdown(alt, imageUrl), string.IsNullOrEmpty(alt) ? imageUrl : alt, imageUrl);
                else if (hidden.StartsWith("lnk:", StringComparison.Ordinal) && TryPair(hidden[4..], out var label, out var linkUrl))
                    BeginEcho(LinkMarkdown(label, linkUrl), label, linkUrl);
                else if (hidden == "keep:")
                    _keep = true;
                else if (hidden == "br:")
                    _hardBreak = true;
                else if (hidden.StartsWith("gap:", StringComparison.Ordinal) && int.TryParse(hidden.AsSpan(4), out var gap))
                    _gap = gap;
                else if (hidden.StartsWith("task:", StringComparison.Ordinal))
                    _runs.Add(new Run(hidden, false, false, false, null, true, _fs));
                return;
            }

            if (_run.Length == 0)
                return;
            var code = IsCodeFont(_font);
            _runs.Add(new Run(_run.ToString(), _bold, _italic, code, _link, false, _fs));
            _run.Clear();
        }

        private static bool TryPair(string payload, out string left, out string right) {
            left = "";
            right = "";
            try {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var split = decoded.Split('\n', 2);
                left = split[0];
                right = split.Length > 1 ? split[1] : "";
                return true;
            }
            catch (FormatException) {
                return false;
            }
        }

        private static string ImageMarkdown(string alt, string url) {
            return "![" + alt + "](" + url + ")";
        }

        private static string LinkMarkdown(string text, string url) {
            var label = text.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]");
            var target = url.IndexOfAny([' ', '(', ')']) >= 0 ? "<" + url.Replace(">", "%3E") + ">" : url;
            return "[" + label + "](" + target + ")";
        }

        private bool IsCodeFont(int font) {
            return _fonts.TryGetValue(font, out var name)
                && name.Contains("Consolas", StringComparison.OrdinalIgnoreCase);
        }

        private void FlushParagraph() {
            FinishPending();
            FlushRun();
            _hardBreak = false;
            if (_imageMarkdown is not null) {
                _runs.Add(new Run(_imageMarkdown, false, false, false, null, false, _fs, true));
                _imageMarkdown = null;
            }

            var task = "";
            var visible = new List<Run>();
            foreach (var run in _runs) {
                if (run.Hidden && run.Text.StartsWith("task:", StringComparison.Ordinal))
                    task = run.Text;
                else if (!run.Hidden)
                    visible.Add(run);
            }

            _runs.Clear();
            var bullet = _bullet;
            var keep = _keep;
            _bullet = false;
            _keep = false;
            if (keep) {
                var kept = string.Concat(visible.Select(run => run.Text)).Trim('\r', '\n');
                if (kept.Length > 0) {
                    Separate();
                    _output.Append(kept);
                }

                _lastWasCode = false;
                return;
            }

            var allCode = visible.Count > 0 && visible.TrueForAll(run => run.Code || run.Text.Trim().Length == 0);
            var text = RenderRuns(visible, allCode, out var size);
            if (text.Length == 0) {
                if (_lastWasCode)
                    _output.Append("\n```");
                else if (_output.Length > 0 && !EndsWithBlankLine())
                    _output.Append('\n');
                _gap = null;
                _lastWasCode = false;
                return;
            }

            if (allCode && !bullet && string.IsNullOrEmpty(task)) {
                if (!_lastWasCode) {
                    Separate();
                    _output.Append("```\n");
                }
                else
                    _output.Append('\n');
                _output.Append(text);
                _lastWasCode = true;
                return;
            }

            if (_lastWasCode)
                _output.Append("\n```");

            var prefix = "";
            if (bullet) {
                prefix = task switch {
                    "task:1" => "- [x] ",
                    "task:0" => "- [ ] ",
                    _ => "- "
                };
            }
            else if (size >= 40)
                prefix = "# ";
            else if (size >= 30)
                prefix = "## ";
            else if (size >= 26 && visible.TrueForAll(run => run.Raw || run.Bold || run.Text.Trim().Length == 0))
                prefix = "### ";

            Separate();
            _output.Append(prefix).Append(text);
            _lastWasCode = false;
        }

        private static string RenderRuns(List<Run> runs, bool allCode, out int size) {
            size = 24;
            var heading = !allCode && runs.Exists(run => run.Size >= 26 && run.Text.Trim().Length > 0)
                && runs.TrueForAll(run => run.Raw || run.Bold || run.Text.Trim().Length == 0);
            var text = new StringBuilder();
            foreach (var run in runs) {
                if (run.Text.Trim().Length > 0)
                    size = run.Size;
                if (run.Raw) {
                    text.Append(run.Text);
                    continue;
                }

                var piece = run.Text.Replace("\r", "");
                if (piece is "\n" or "  \n") {
                    if (allCode)
                        text.Append('\n');
                    else {
                        var hard = piece == "  \n" || EndsWithTwoSpaces(text);
                        if (EndsWithTwoSpaces(text))
                            text.Length -= 2;
                        text.Append(hard ? "  \n" : "\n");
                    }
                    continue;
                }

                if (allCode) {
                    text.Append(piece);
                    continue;
                }

                if (run.Code)
                    piece = "`" + piece.Replace("`", "") + "`";
                else if (run.Link is null)
                    piece = Escape(piece);
                else
                    piece = "[" + Escape(piece) + "](" + run.Link + ")";
                if (run.Bold && !heading)
                    piece = "**" + piece + "**";
                else if (run.Italic && run.Link is null)
                    piece = "*" + piece + "*";
                text.Append(piece);
            }

            var markdown = text.ToString();
            if (markdown.StartsWith("  \n", StringComparison.Ordinal))
                return markdown;
            return markdown.TrimStart();
        }

        private static bool EndsWithTwoSpaces(StringBuilder text) {
            return text.Length >= 2 && text[^1] == ' ' && text[^2] == ' ';
        }

        private static string Escape(string text) {
            return text.Replace("\\", "\\\\").Replace("`", "\\`").Replace("*", "\\*").Replace("_", "\\_").Replace("[", "\\[").Replace("\u00A0", "&nbsp;");
        }

        private void Separate() {
            var gap = _gap;
            _gap = null;
            if (_output.Length == 0 || _lastWasCode)
                return;
            if (gap is int count) {
                if (count > 0)
                    _output.Append('\n', count);
                return;
            }

            _output.Append('\n');
        }

        private bool EndsWithBlankLine() {
            return _output.Length >= 2 && _output[^1] == '\n' && _output[^2] == '\n';
        }

        private static string Tidy(string markdown) {
            while (markdown.Contains("\n\n\n", StringComparison.Ordinal))
                markdown = markdown.Replace("\n\n\n", "\n\n");
            if (markdown.EndsWith("\n```", StringComparison.Ordinal))
                markdown += "\n";
            return KeepHardBreaks(markdown);
        }

        private static string KeepHardBreaks(string markdown) {
            var start = 0;
            var end = markdown.Length;
            while (start < end && markdown[start] is '\n' or '\r')
                start++;
            while (end > start && markdown[end - 1] == '\r')
                end--;
            while (end > start && markdown[end - 1] == '\n') {
                if (end - start >= 3 && markdown[end - 2] == ' ' && markdown[end - 3] == ' ')
                    break;
                end--;
            }

            return markdown[start..end];
        }

        private sealed record Run(string Text, bool Bold, bool Italic, bool Code, string? Link, bool Hidden, int Size, bool Raw = false);
    }
}
