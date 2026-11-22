using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace rsaReader;

internal static class RichMarkdown {
    private const string HardBreakMark = "\\v br:\\v0 ";

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

    private static string BuildRtf(string? markdown, bool dark) {
        var source = markdown ?? "";
        var document = Markdown.Parse(source, Pipeline);
        var rtf = new StringBuilder();
        var map = new SourceMap(source);
        var codeBack = dark ? "\\red46\\green46\\blue46" : "\\red243\\green243\\blue243";
        rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0\\deflang1033");
        rtf.Append("{\\fonttbl{\\f0\\fnil\\fcharset0 Segoe UI;}{\\f1\\fnil\\fcharset0 Consolas;}}");
        rtf.Append("{\\colortbl ;\\red11\\green87\\blue208;").Append(codeBack).Append(";}");
        rtf.Append("\\viewkind4\\uc1");
        foreach (var block in document)
            RenderBlock(rtf, map, block, 0);
        if (document.Count == 0)
            rtf.Append("\\pard\\f0\\fs24\\par");
        rtf.Append('}');
        return rtf.ToString();
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
                RenderParagraph(rtf, heading.Inline, HeadingSize(heading.Level), FontStyleKind.Bold, false, 0, null, map.Take(heading));
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, false, depth, null, map.Take(paragraph));
                break;
            case ListBlock list:
                RenderList(rtf, map, list, depth);
                break;
            case FencedCodeBlock or CodeBlock:
                RenderCode(rtf, block is LeafBlock leaf ? leaf.Lines.ToString() : "", map.Take(block));
                break;
            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderQuote(rtf, map, child, depth + 1, 1);
                break;
            case ThematicBreakBlock:
                rtf.Append("\\pard\\f0\\fs24 ");
                AppendGap(rtf, map.Take(block));
                rtf.Append("---\\par");
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
            RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, false, depth, new string('>', quotes) + " ", map.Take(paragraph));
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
                    RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, !list.IsOrdered, depth, task ? (taskChecked ? "1" : "0") : marker, map.Take(paragraph));
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

    private static void RenderCode(StringBuilder rtf, string code, int? gap) {
        rtf.Append("\\pard\\highlight2\\cb2\\f1\\fs22 ");
        AppendGap(rtf, gap);
        var lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            if (i > 0)
                rtf.Append("\\line ");
            AppendText(rtf, lines[i]);
        }

        rtf.Append("\\highlight0\\cb0\\f0\\fs24\\par");
    }

    private static void RenderKept(StringBuilder rtf, SourceMap map, Block block) {
        var text = Source(map.Text, block);
        if (text.Length == 0)
            return;
        rtf.Append("\\pard\\f0\\fs24 ");
        AppendGap(rtf, map.Take(block));
        rtf.Append("\\v keep:\\v0 ");
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            if (i > 0)
                rtf.Append("\\line ");
            AppendText(rtf, lines[i]);
        }

        rtf.Append("\\par");
    }

    private static void RenderParagraph(StringBuilder rtf, ContainerInline? inline, int halfPoints, FontStyleKind style, bool bullet, int depth, string? marker, int? gap) {
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
        AppendGap(rtf, gap);
        if (marker is "0" or "1")
            rtf.Append("\\v task:").Append(marker).Append("\\v0 ");
        else if (!string.IsNullOrEmpty(marker))
            AppendText(rtf, marker);
        if (inline is not null)
            RenderInline(rtf, inline, style == FontStyleKind.Bold, style == FontStyleKind.Italic);
        if (style == FontStyleKind.Bold)
            rtf.Append("\\b0");
        if (style == FontStyleKind.Italic)
            rtf.Append("\\i0");
        rtf.Append("\\par");
    }

    private static void RenderInline(StringBuilder rtf, ContainerInline inline, bool bold, bool italic) {
        for (var child = inline.FirstChild; child is not null; child = child.NextSibling)
            RenderOne(rtf, child, bold, italic);
    }

    private static void RenderOne(StringBuilder rtf, Inline inline, bool bold, bool italic) {
        switch (inline) {
            case LiteralInline literal:
                AppendLiteral(rtf, literal.Content.ToString());
                break;
            case LineBreakInline line:
                if (line.IsHard)
                    AppendHardBreak(rtf);
                else
                    rtf.Append("\\line ");
                break;
            case CodeInline code:
                rtf.Append("\\f1\\fs22\\highlight2\\cb2 ");
                AppendText(rtf, code.Content);
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
                RenderImage(rtf, InlineText(link), link.Url ?? "");
                break;
            case LinkInline link:
                RenderLink(rtf, link);
                break;
            case Markdig.Extensions.TaskLists.TaskList:
                break;
            case AutolinkInline autolink:
                RenderLink(rtf, autolink.Url, autolink.Url);
                break;
            case HtmlEntityInline entity:
                AppendLiteral(rtf, entity.Transcoded.ToString());
                break;
            case HtmlInline html when html.Tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase):
                AppendHardBreak(rtf);
                break;
            case ContainerInline container:
                RenderInline(rtf, container, bold, italic);
                break;
        }
    }

    private static void RenderLink(StringBuilder rtf, LinkInline link) {
        var text = new StringBuilder();
        CollectPlain(link, text);
        RenderLink(rtf, text.ToString(), link.Url ?? "");
    }

    private static void RenderLink(StringBuilder rtf, string text, string url) {
        if (text.Length == 0)
            text = url;
        AppendMarker(rtf, "lnk:", text, url);
        AppendLinkField(rtf, text, url);
    }

    private static void AppendLinkField(StringBuilder rtf, string text, string url) {
        rtf.Append("{\\field{\\*\\fldinst{HYPERLINK \"");
        AppendText(rtf, url);
        rtf.Append("\"}}{\\fldrslt{\\ul\\cf1 ");
        AppendText(rtf, text);
        rtf.Append("\\ul0\\cf0}}}");
    }

    private static void AppendMarker(StringBuilder rtf, string kind, string text, string url) {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text + "\n" + url));
        rtf.Append("\\v ").Append(kind).Append(payload).Append("\\v0 ");
    }

    private static void RenderImage(StringBuilder rtf, string alt, string url) {
        AppendMarker(rtf, "img:", alt, url);
        var picture = TryPicture(url);
        if (picture is not null)
            rtf.Append(picture);
        else
            AppendLinkField(rtf, string.IsNullOrEmpty(alt) ? url : alt, url);
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

    private static void AppendHardBreak(StringBuilder rtf) {
        rtf.Append(HardBreakMark);
        rtf.Append("\\line ");
    }

    private static void AppendGap(StringBuilder rtf, int? gap) {
        if (gap is int count)
            rtf.Append("\\v gap:").Append(count).Append("\\v0 ");
    }

    private static void AppendLiteral(StringBuilder rtf, string text) {
        AppendText(rtf, text);
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

    public static string HealBreakLines(string? markdown) {
        var lines = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var fence = "";
        var changed = false;
        for (var i = 0; i < lines.Length; i++) {
            var trimmed = lines[i].TrimStart();
            if (fence.Length > 0) {
                if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                    fence = "";
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)) {
                fence = trimmed[..3];
                continue;
            }

            if (lines[i] == "\\") {
                lines[i] = "  ";
                changed = true;
            }
        }

        return changed ? string.Join("\n", lines) : markdown ?? "";
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
            return markdown.Trim();
        }

        private sealed record Run(string Text, bool Bold, bool Italic, bool Code, string? Link, bool Hidden, int Size, bool Raw = false);
    }
}
