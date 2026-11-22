using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace rsaReader;

internal static class RichMarkdown {
    private const char HardBreak = '\uE000';

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static string ToRtf(string? markdown) {
        var source = markdown ?? "";
        var document = Markdown.Parse(source, Pipeline);
        var rtf = new StringBuilder();
        rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0\\deflang1033");
        rtf.Append("{\\fonttbl{\\f0\\fnil\\fcharset0 Segoe UI;}{\\f1\\fnil\\fcharset0 Consolas;}}");
        rtf.Append("{\\colortbl ;\\red11\\green87\\blue208;\\red243\\green243\\blue243;}");
        rtf.Append("\\viewkind4\\uc1");
        foreach (var block in document)
            RenderBlock(rtf, source, block, 0);
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

    public static string ImageRtf(string alt, string url) {
        var rtf = new StringBuilder();
        rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0{\\fonttbl{\\f0\\fnil\\fcharset0 Segoe UI;}}{\\colortbl ;\\red11\\green87\\blue208;}\\f0\\fs24 ");
        RenderImage(rtf, alt, url);
        rtf.Append('}');
        return rtf.ToString();
    }

    private static void RenderBlock(StringBuilder rtf, string source, Block block, int depth) {
        switch (block) {
            case HeadingBlock heading:
                RenderParagraph(rtf, heading.Inline, HeadingSize(heading.Level), FontStyleKind.Bold, false, 0, null);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, false, depth, null);
                break;
            case ListBlock list:
                RenderList(rtf, source, list, depth);
                break;
            case FencedCodeBlock or CodeBlock:
                RenderCode(rtf, block is LeafBlock leaf ? leaf.Lines.ToString() : "");
                break;
            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderQuote(rtf, source, child, depth + 1, 1);
                break;
            case ThematicBreakBlock:
                rtf.Append("\\pard\\f0\\fs24\\cf0 ---\\par");
                break;
            default:
                RenderKept(rtf, source, block);
                break;
        }
    }

    private static void RenderQuote(StringBuilder rtf, string source, Block block, int depth, int quotes) {
        if (block is QuoteBlock nested) {
            foreach (var child in nested)
                RenderQuote(rtf, source, child, depth + 1, quotes + 1);
            return;
        }

        if (block is ParagraphBlock paragraph) {
            RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, false, depth, new string('>', quotes) + " ");
            return;
        }

        RenderBlock(rtf, source, block, depth);
    }

    private static void RenderList(StringBuilder rtf, string source, ListBlock list, int depth) {
        var number = 1;
        if (list.IsOrdered && int.TryParse(list.OrderedStart, out var ordered))
            number = ordered;

        foreach (var item in list.OfType<ListItemBlock>()) {
            var task = TryTask(item, out var taskChecked);
            var marker = !list.IsOrdered ? null : number + ". ";
            var first = true;
            foreach (var child in item) {
                if (child is ParagraphBlock paragraph && first) {
                    RenderParagraph(rtf, paragraph.Inline, 24, FontStyleKind.None, !list.IsOrdered, depth, task ? (taskChecked ? "1" : "0") : marker);
                    first = false;
                }
                else if (child is ListBlock nested)
                    RenderList(rtf, source, nested, depth + 1);
                else
                    RenderBlock(rtf, source, child, depth);
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

    private static void RenderCode(StringBuilder rtf, string code) {
        rtf.Append("\\pard\\highlight2\\cb2\\f1\\fs22 ");
        var lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            if (i > 0)
                rtf.Append("\\line ");
            AppendText(rtf, lines[i]);
        }

        rtf.Append("\\highlight0\\cb0\\f0\\fs24\\par");
    }

    private static void RenderKept(StringBuilder rtf, string source, Block block) {
        var text = Source(source, block);
        if (text.Length == 0)
            return;
        rtf.Append("\\pard\\f0\\fs24\\v keep:\\v0 ");
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            if (i > 0)
                rtf.Append("\\line ");
            AppendText(rtf, lines[i]);
        }

        rtf.Append("\\par");
    }

    private static void RenderParagraph(StringBuilder rtf, ContainerInline? inline, int halfPoints, FontStyleKind style, bool bullet, int depth, string? marker) {
        var indent = 360 * Math.Max(depth, bullet ? 1 : 0);
        rtf.Append("\\pard");
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
                rtf.Append(line.IsHard ? "\\line " : " ");
                break;
            case CodeInline code:
                rtf.Append("\\f1\\fs22 ");
                AppendText(rtf, code.Content);
                rtf.Append("\\f0\\fs24 ");
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
            case ContainerInline container:
                RenderInline(rtf, container, bold, italic);
                break;
            default:
                AppendText(rtf, inline.ToString() ?? "");
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
        rtf.Append("{\\field{\\*\\fldinst{HYPERLINK \"");
        AppendText(rtf, url);
        rtf.Append("\"}}{\\fldrslt{\\ul\\cf1 ");
        AppendText(rtf, text);
        rtf.Append("\\ul0\\cf0}}}");
    }

    private static void RenderImage(StringBuilder rtf, string alt, string url) {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(alt + "\n" + url));
        rtf.Append("\\v img:").Append(payload).Append("\\v0 ");
        var picture = TryPicture(url);
        if (picture is not null)
            rtf.Append(picture);
        else
            RenderLink(rtf, string.IsNullOrEmpty(alt) ? url : alt, url);
    }

    private static string? TryPicture(string url) {
        var bytes = LoadBytes(url);
        if (bytes is null || bytes.Length == 0)
            return null;
        try {
            using var input = new MemoryStream(bytes);
            using var image = Image.FromStream(input);
            using var output = new MemoryStream();
            image.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            var png = output.ToArray();
            var width = Math.Min(image.Width, 520);
            var height = Math.Max(1, image.Height * width / Math.Max(1, image.Width));
            var twipsW = width * 1440 / 96;
            var twipsH = height * 1440 / 96;
            var hex = new StringBuilder(png.Length * 2 + 64);
            hex.Append("{\\pict\\pngblip\\picwgoal").Append(twipsW).Append("\\pichgoal").Append(twipsH).Append(' ');
            for (var i = 0; i < png.Length; i++) {
                hex.Append(png[i].ToString("x2"));
                if (i % 64 == 63)
                    hex.Append('\n');
            }

            hex.Append('}');
            return hex.ToString();
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) {
            return null;
        }
    }

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

    private static void AppendLiteral(StringBuilder rtf, string text) {
        var parts = text.Split(HardBreak);
        for (var i = 0; i < parts.Length; i++) {
            if (i > 0)
                rtf.Append("\\line ");
            AppendText(rtf, parts[i]);
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
        private int _font;
        private int _fs = 24;
        private string? _link;
        private string? _imageMarkdown;
        private readonly Dictionary<int, string> _fonts = new();
        private readonly StringBuilder _run = new();
        private readonly List<Run> _runs = new();
        private readonly StringBuilder _output = new();
        private bool _lastWasList;
        private bool _lastWasCode;

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

                    if (word is "colortbl" or "stylesheet" or "info" or "pntext" or "listtext" or "pict" or "object" or "generator" or "datastore") {
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
            var text = ReadGroupText();
            var marker = "HYPERLINK \"";
            var at = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
                return;
            at += marker.Length;
            var end = text.IndexOf('"', at);
            if (end > at)
                _link = text[at..end];
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
                else if (depth == 1)
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
                    break;
                case "line":
                    FlushRun();
                    _runs.Add(new Run("\n", _bold, _italic, IsCodeFont(_font), _link, false, _fs));
                    break;
                case "pard":
                    FlushRun();
                    _bullet = false;
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

            if (_imageMarkdown is not null) {
                EmitRaw(_imageMarkdown);
                _imageMarkdown = null;
            }

            _run.Append(c);
        }

        private void EmitRaw(string markdown) {
            FlushRun();
            _runs.Add(new Run(markdown, false, false, false, null, false, _fs, true));
        }

        private void FlushRun() {
            if (_hidden) {
                var hidden = _run.ToString();
                _run.Clear();
                if (hidden.StartsWith("img:", StringComparison.Ordinal))
                    _imageMarkdown = ImageMarkdown(hidden[4..]);
                else if (hidden == "keep:")
                    _keep = true;
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

        private static string ImageMarkdown(string payload) {
            try {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var split = decoded.Split('\n', 2);
                var alt = split[0];
                var url = split.Length > 1 ? split[1] : "";
                return "![" + alt + "](" + url + ")";
            }
            catch (FormatException) {
                return "";
            }
        }

        private bool IsCodeFont(int font) {
            return _fonts.TryGetValue(font, out var name)
                && name.Contains("Consolas", StringComparison.OrdinalIgnoreCase);
        }

        private void FlushParagraph() {
            FlushRun();
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
                    Separate(false);
                    _output.Append(kept);
                }

                _lastWasList = false;
                _lastWasCode = false;
                return;
            }

            var allCode = visible.Count > 0 && visible.TrueForAll(run => run.Code || run.Text.Trim().Length == 0);
            var text = RenderRuns(visible, allCode, out var size);
            if (text.Length == 0) {
                if (_lastWasCode)
                    _output.Append("\n```");
                else if (_output.Length > 0)
                    _output.Append("\n\n");
                _lastWasList = false;
                _lastWasCode = false;
                return;
            }

            if (allCode && !bullet && string.IsNullOrEmpty(task)) {
                if (!_lastWasCode) {
                    Separate(false);
                    _output.Append("```\n");
                }
                else
                    _output.Append('\n');
                _output.Append(text);
                _lastWasCode = true;
                _lastWasList = false;
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

            Separate(bullet);
            _output.Append(prefix).Append(text);
            _lastWasList = bullet;
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
                if (piece == "\n") {
                    text.Append(allCode ? "\n" : "  \n");
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

            return text.ToString().TrimStart();
        }

        private static string Escape(string text) {
            return text.Replace("\\", "\\\\").Replace("`", "\\`").Replace("*", "\\*").Replace("_", "\\_").Replace("[", "\\[");
        }

        private void Separate(bool list) {
            if (_output.Length == 0)
                return;
            if (_lastWasCode)
                return;
            _output.Append(_lastWasList && list ? "\n" : "\n\n");
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
