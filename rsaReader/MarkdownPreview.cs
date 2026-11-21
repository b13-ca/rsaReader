using System.Text;
using Markdig;

namespace rsaReader;

internal static class MarkdownPreview {
    public const string PreviewUrl = "http://rsa.local/preview";
    private const char HardBreak = '\uE000';

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static string ToDocument(string markdown) {
        var body = Markdown.ToHtml(MarkHardBreaks(markdown), Pipeline).Replace(HardBreak.ToString(), "<br>");
        return """
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="color-scheme" content="light">
            <style>
            html {
                background: #ffffff;
                color-scheme: light;
            }
            body {
                font-family: "Segoe UI", sans-serif;
                font-size: 16px;
                line-height: 1.5;
                color: #1a1a1a;
                background: #ffffff;
                margin: 0;
                padding: 0 24px 40px;
            }
            body > :first-child { margin-top: 0; }
            h1, h2, h3, h4 { line-height: 1.25; margin: 1.1em 0 0.4em; }
            p, ul, ol, pre, blockquote, table { margin: 0.6em 0; }
            a { color: #0b57d0; }
            code, pre { font-family: Consolas, monospace; }
            code { background: #f3f3f3; padding: 0.1em 0.35em; border-radius: 3px; }
            pre { background: #f3f3f3; padding: 12px 14px; overflow: auto; }
            pre code { background: transparent; padding: 0; }
            blockquote {
                margin-left: 0;
                padding-left: 14px;
                border-left: 3px solid #ccc;
                color: #444;
            }
            table { border-collapse: collapse; }
            th, td { border: 1px solid #ccc; padding: 6px 10px; }
            img { max-width: 100%; }
            </style>
            </head>
            <body>
            """ + body + """
            </body>
            </html>
            """;
    }

    private static string MarkHardBreaks(string markdown) {
        var fence = false;
        var sb = new StringBuilder(markdown.Length);
        var start = 0;
        for (var i = 0; i <= markdown.Length; i++) {
            var atEnd = i == markdown.Length;
            if (!atEnd && markdown[i] != '\n' && markdown[i] != '\r')
                continue;

            var end = i;
            if (end > start && markdown[end - 1] == '\r')
                end--;

            var line = markdown.AsSpan(start, end - start);
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                fence = !fence;

            var hard = !atEnd && !fence && !IsIndentedCode(line) && !IsAtxHeading(line) && EndsWithHardBreak(line);
            if (hard) {
                var cut = end;
                while (cut > start && markdown[cut - 1] == ' ')
                    cut--;
                sb.Append(markdown, start, cut - start);
                sb.Append(HardBreak);
            }
            else
                sb.Append(markdown, start, end - start);

            if (!atEnd) {
                sb.Append('\n');
                if (markdown[i] == '\r' && i + 1 < markdown.Length && markdown[i + 1] == '\n')
                    i++;
            }

            start = i + 1;
        }

        return sb.ToString();
    }

    private static bool EndsWithHardBreak(ReadOnlySpan<char> line) {
        return line.Length >= 2 && line[^1] == ' ' && line[^2] == ' ';
    }

    private static bool IsIndentedCode(ReadOnlySpan<char> line) {
        if (line.Length == 0)
            return false;
        if (line[0] == '\t')
            return true;

        var spaces = 0;
        foreach (var c in line) {
            if (c != ' ')
                break;
            spaces++;
        }

        return spaces >= 4;
    }

    private static bool IsAtxHeading(ReadOnlySpan<char> line) {
        var i = 0;
        while (i < line.Length && i < 3 && line[i] == ' ')
            i++;

        var hashes = 0;
        while (i < line.Length && line[i] == '#') {
            hashes++;
            i++;
        }

        return hashes is >= 1 and <= 6 && i < line.Length && line[i] == ' ';
    }
}
