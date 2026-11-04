namespace Blaizio;

/// <summary>
/// HTML and XML: tags, attributes, quoted values, comments, entities, doctypes and processing
/// instructions, CDATA. In HTML a script element's body is JavaScript (JSON for a JSON or import
/// map type) and a style element's body is CSS, tokenized by those languages up to the closing tag.
/// A start tag or a quoted value can span lines.
/// </summary>
internal sealed class MarkupSyntax : SyntaxLanguage
{
    public static readonly MarkupSyntax Html = new(html: true);

    public static readonly MarkupSyntax Xml = new(html: false);

    private readonly bool _html;

    private MarkupSyntax(bool html) => _html = html;

    public override string Name => _html ? "html" : "xml";

    public override IReadOnlyList<string> Aliases => _html
        ? ["htm", "xhtml", "vue", "svelte"]
        : ["csproj", "fsproj", "vbproj", "props", "targets", "xaml", "axaml", "svg", "resx", "config", "nuspec", "plist", "xsd", "xslt", "wsdl", "rss", "atom"];

    internal enum Mode : byte
    {
        Comment,
        Tag,
        Value,
        Cdata,
        Embedded,
    }

    /// <summary>
    /// What is open at the end of a line: <paramref name="Tag"/> is the element whose start tag (or
    /// body, for an embedded language) is open; <paramref name="Embedded"/> and <paramref name="Inner"/>
    /// are the language and state of a script or style body.
    /// </summary>
    internal sealed record State(Mode Mode, string? Tag, char Quote, SyntaxLanguage? Embedded, object? Inner);

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var st = state as State;
        var i = start;
        var mode = st?.Mode;
        var tag = st?.Tag;
        var embedded = st?.Embedded;
        var inner = st?.Inner;

        while (i < end)
        {
            switch (mode)
            {
                case Mode.Comment:
                {
                    var close = Scan.IndexOf(s, i, end, "-->");
                    if (close < 0)
                    {
                        w.Add(i, end, SyntaxKind.Comment);
                        return new State(Mode.Comment, null, '\0', null, null);
                    }
                    w.Add(i, close + 3, SyntaxKind.Comment);
                    i = close + 3;
                    mode = null;
                    continue;
                }
                case Mode.Cdata:
                {
                    var close = Scan.IndexOf(s, i, end, "]]>");
                    if (close < 0)
                    {
                        w.Add(i, end, SyntaxKind.String);
                        return new State(Mode.Cdata, null, '\0', null, null);
                    }
                    w.Add(i, close + 3, SyntaxKind.String);
                    i = close + 3;
                    mode = null;
                    continue;
                }
                case Mode.Value:
                {
                    var close = Scan.IndexOf(s, i, end, st!.Quote);
                    if (close < 0)
                    {
                        w.Add(i, end, SyntaxKind.String);
                        return st;
                    }
                    w.Add(i, close + 1, SyntaxKind.String);
                    i = close + 1;
                    mode = Mode.Tag;
                    continue;
                }
                case Mode.Tag:
                {
                    var result = TagBody(s, i, end, w, tag!, out i, out var quote, ref embedded);
                    if (result is TagEnd.Open)
                        return new State(Mode.Tag, tag, '\0', embedded, null);
                    if (result is TagEnd.Quote)
                        return new State(Mode.Value, tag, quote, embedded, null);
                    mode = result is TagEnd.Embedded ? Mode.Embedded : null;
                    inner = null;
                    continue;
                }
                case Mode.Embedded:
                {
                    var close = IndexOfClosingTag(s, i, end, tag!);
                    var stop = close < 0 ? end : close;
                    inner = embedded is null ? null : w.Embed(embedded, i, stop, inner);
                    if (embedded is null) w.Add(i, stop, SyntaxKind.Text);
                    if (close < 0) return new State(Mode.Embedded, tag, '\0', embedded, inner);
                    i = close;
                    mode = null;
                    embedded = null;
                    inner = null;
                    continue;
                }
            }

            // Text content.
            var c = s[i];
            if (c == '<')
            {
                if (Scan.At(s, i, end, "<!--"))
                {
                    w.Add(i, i + 4, SyntaxKind.Comment);
                    i += 4;
                    mode = Mode.Comment;
                    continue;
                }
                if (Scan.At(s, i, end, "<![CDATA["))
                {
                    w.Add(i, i + 9, SyntaxKind.String);
                    i += 9;
                    mode = Mode.Cdata;
                    continue;
                }
                if (Scan.At(s, i + 1, end) is '!' or '?')
                {
                    // <!DOCTYPE html>, <?xml version="1.0"?>
                    var close = Scan.IndexOf(s, i, end, '>');
                    var e = close < 0 ? end : close + 1;
                    w.Add(i, e, SyntaxKind.Meta);
                    i = e;
                    continue;
                }
                var closing = Scan.At(s, i + 1, end) == '/';
                var nameStart = closing ? i + 2 : i + 1;
                if (nameStart < end && IsNameStart(s[nameStart]))
                {
                    var nameEnd = NameEnd(s, nameStart, end);
                    w.Add(i, nameStart, SyntaxKind.Text);
                    w.Add(nameStart, nameEnd, SyntaxKind.Tag);
                    i = nameEnd;
                    if (closing)
                    {
                        var close = Scan.IndexOf(s, i, end, '>');
                        var e = close < 0 ? end : close + 1;
                        w.Add(i, e, SyntaxKind.Text);
                        i = e;
                        continue;
                    }
                    tag = s[nameStart..nameEnd];
                    embedded = EmbeddedLanguage(tag, typeJson: false, w);
                    mode = Mode.Tag;
                    continue;
                }
            }
            if (c == '&')
            {
                var e = EntityEnd(s, i, end);
                if (e > 0)
                {
                    w.Add(i, e, SyntaxKind.Escape);
                    i = e;
                    continue;
                }
            }
            var next = Scan.IndexOfAny(s, i + 1, end, '<', '&');
            var stopAt = next < 0 ? end : next;
            w.Add(i, stopAt, SyntaxKind.Text);
            i = stopAt;
        }

        return mode switch
        {
            Mode.Tag => new State(Mode.Tag, tag, '\0', embedded, null),
            Mode.Comment or Mode.Cdata => new State(mode.Value, null, '\0', null, null),
            Mode.Embedded => new State(Mode.Embedded, tag, '\0', embedded, inner),
            _ => null,
        };
    }

    private enum TagEnd : byte
    {
        Open,
        Quote,
        Closed,
        Embedded,
    }

    // Attributes up to the end of a start tag. Returns how the tag ended on this line.
    private TagEnd TagBody(string s, int i, int end, SyntaxWriter w, string tag, out int pos, out char quote, ref SyntaxLanguage? embedded)
    {
        string? lastAttribute = null;
        quote = '\0';
        while (i < end)
        {
            var c = s[i];
            if (Scan.IsSpace(c))
            {
                var e = Scan.Spaces(s, i, end);
                w.Add(i, e, SyntaxKind.Text);
                i = e;
                continue;
            }
            if (c == '/' && Scan.At(s, i + 1, end) == '>')
            {
                w.Add(i, i + 2, SyntaxKind.Text);
                pos = i + 2;
                embedded = null;
                return TagEnd.Closed;
            }
            if (c == '>')
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                pos = i + 1;
                return embedded is not null || IsRawTextElement(tag) ? TagEnd.Embedded : TagEnd.Closed;
            }
            if (c == '=')
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                var k = Scan.Spaces(s, i + 1, end);
                w.Add(i + 1, k, SyntaxKind.Text);
                if (k < end && s[k] is '"' or '\'')
                {
                    var close = Scan.IndexOf(s, k + 1, end, s[k]);
                    if (close < 0)
                    {
                        w.Add(k, end, SyntaxKind.String);
                        pos = end;
                        quote = s[k];
                        return TagEnd.Quote;
                    }
                    w.Add(k, close + 1, SyntaxKind.String);
                    if (lastAttribute is "type" && tag.Equals("script", StringComparison.OrdinalIgnoreCase))
                        embedded = EmbeddedLanguage(tag, typeJson: s.AsSpan(k, close - k).Contains("json", StringComparison.OrdinalIgnoreCase), w);
                    i = close + 1;
                    continue;
                }
                var v = k;
                while (v < end && !Scan.IsSpace(s[v]) && s[v] != '>') v++;
                w.Add(k, v, SyntaxKind.String);
                i = v;
                continue;
            }
            var nameEnd = i;
            while (nameEnd < end && !Scan.IsSpace(s[nameEnd]) && s[nameEnd] is not ('=' or '>' or '"' or '\'') && !(s[nameEnd] == '/' && Scan.At(s, nameEnd + 1, end) == '>'))
                nameEnd++;
            if (nameEnd == i) nameEnd = i + 1;
            w.Add(i, nameEnd, SyntaxKind.Attribute);
            lastAttribute = s[i..nameEnd].ToLowerInvariant();
            i = nameEnd;
        }
        pos = end;
        return TagEnd.Open;
    }

    // Looked up by name, so a script or style body gets the language the app replaced it with.
    private SyntaxLanguage? EmbeddedLanguage(string tag, bool typeJson, SyntaxWriter w)
    {
        if (!_html) return null;
        if (tag.Equals("script", StringComparison.OrdinalIgnoreCase))
            return typeJson ? w.Find("json") ?? JsonSyntax.Instance : w.Find("javascript") ?? ScriptSyntax.JavaScript;
        if (tag.Equals("style", StringComparison.OrdinalIgnoreCase)) return w.Find("css") ?? CssSyntax.Instance;
        return null;
    }

    // Elements whose body is raw text up to their closing tag (no markup inside).
    private bool IsRawTextElement(string tag) =>
        _html && (tag.Equals("script", StringComparison.OrdinalIgnoreCase) || tag.Equals("style", StringComparison.OrdinalIgnoreCase));

    /// <summary>The index of <c>&lt;/tag</c> (any case) at or after <paramref name="i"/>, or -1.</summary>
    internal static int IndexOfClosingTag(string s, int i, int end, string tag)
    {
        for (var k = Scan.IndexOf(s, i, end, "</"); k >= 0; k = Scan.IndexOf(s, k + 2, end, "</"))
        {
            if (Scan.AtIgnoreCase(s, k + 2, end, tag) && !IsNamePart(Scan.At(s, k + 2 + tag.Length, end))) return k;
        }
        return -1;
    }

    internal static bool IsNameStart(char c) => char.IsLetter(c) || c is '_' or ':';

    internal static bool IsNamePart(char c) => char.IsLetterOrDigit(c) || c is '_' or ':' or '-' or '.';

    internal static int NameEnd(string s, int i, int end)
    {
        while (i < end && IsNamePart(s[i])) i++;
        return i;
    }

    /// <summary>The end of an entity (<c>&amp;amp;</c>, <c>&amp;#169;</c>, <c>&amp;#x1F600;</c>) at <paramref name="i"/>, or -1.</summary>
    internal static int EntityEnd(string s, int i, int end)
    {
        var k = i + 1;
        if (Scan.At(s, k, end) == '#')
        {
            k++;
            var hex = Scan.At(s, k, end) is 'x' or 'X';
            if (hex) k++;
            var digits = k;
            while (k < end && (hex ? char.IsAsciiHexDigit(s[k]) : char.IsAsciiDigit(s[k]))) k++;
            return k > digits && Scan.At(s, k, end) == ';' ? k + 1 : -1;
        }
        var name = k;
        while (k < end && char.IsAsciiLetterOrDigit(s[k])) k++;
        return k > name && Scan.At(s, k, end) == ';' ? k + 1 : -1;
    }
}
