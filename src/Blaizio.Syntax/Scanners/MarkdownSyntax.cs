namespace Blaizio;

/// <summary>
/// Markdown: headings, emphasis and strong text, inline code, links and images, list and task
/// markers, quotes, rules, table rows, inline HTML tags, YAML front matter, and fenced code blocks
/// whose body is tokenized as the fence's language when the document knows it: built in, or one
/// the app added.
/// </summary>
internal sealed class MarkdownSyntax : SyntaxLanguage
{
    public static readonly MarkdownSyntax Instance = new();

    internal enum Mode : byte
    {
        Body,
        FrontMatter,
        Fence,
    }

    /// <summary>
    /// Past the first line, always a state: front matter is only possible on line one. Inside a
    /// fence, <paramref name="Fence"/> is its marker and <paramref name="Language"/> its language.
    /// </summary>
    internal sealed record State(Mode Mode, string? Fence, SyntaxLanguage? Language, object? Inner);

    private static readonly State Body = new(Mode.Body, null, null, null);

    public override string Name => "markdown";

    public override IReadOnlyList<string> Aliases => ["md", "mdx", "mkd"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var st = state as State;
        var trimmed = s.AsSpan(start, end - start).TrimEnd();

        if (st is null && trimmed is "---")
        {
            w.Add(start, end, SyntaxKind.Meta);
            return new State(Mode.FrontMatter, null, w.Find("yaml") ?? YamlSyntax.Instance, null);
        }
        if (st?.Mode == Mode.FrontMatter)
        {
            if (trimmed is "---" or "...")
            {
                w.Add(start, end, SyntaxKind.Meta);
                return Body;
            }
            return st with { Inner = w.Embed(st.Language ?? YamlSyntax.Instance, start, end, st.Inner) };
        }

        var indent = Scan.Spaces(s, start, end);
        if (st?.Mode == Mode.Fence)
        {
            // The closing fence: the same character, at least as long, nothing after it.
            var t = s.AsSpan(indent, end - indent).TrimEnd();
            if (indent - start < 4 && t.Length >= st.Fence!.Length && t.Trim(st.Fence[0]).IsEmpty)
            {
                w.Add(start, end, SyntaxKind.Meta);
                return Body;
            }
            if (st.Language is null)
            {
                w.Add(start, end, SyntaxKind.String);
                return st;
            }
            return st with { Inner = w.Embed(st.Language, start, end, st.Inner) };
        }

        w.Add(start, indent, SyntaxKind.Text);
        var i = indent;
        if (i >= end) return Body;
        var c = s[i];

        // ``` or ~~~ opening a fence, with the language after it.
        if (indent - start < 4 && c is '`' or '~')
        {
            var k = i;
            while (k < end && s[k] == c) k++;
            if (k - i >= 3 && (c == '~' || Scan.IndexOf(s, k, end, '`') < 0))
            {
                w.Add(i, k, SyntaxKind.Meta);
                var info = Scan.Spaces(s, k, end);
                var infoEnd = info;
                while (infoEnd < end && !Scan.IsSpace(s[infoEnd]) && s[infoEnd] != '{') infoEnd++;
                w.Add(k, info, SyntaxKind.Text);
                w.Add(info, infoEnd, SyntaxKind.Type);
                w.Add(infoEnd, end, SyntaxKind.Meta);
                var language = infoEnd > info ? w.Find(s[info..infoEnd]) : null;
                return new State(Mode.Fence, s[i..k], language, null);
            }
        }

        if (c == '#')
        {
            var k = i;
            while (k < end && s[k] == '#') k++;
            if (k - i <= 6 && (k == end || Scan.IsSpace(s[k])))
            {
                w.Add(i, end, SyntaxKind.Heading);
                return Body;
            }
        }
        if (IsRule(trimmed))
        {
            w.Add(i, end, SyntaxKind.Meta);
            return Body;
        }
        if (c == '>')
        {
            w.Add(i, end, SyntaxKind.Comment);
            return Body;
        }
        if (c == '|' && IsTableDivider(s, i, end))
        {
            w.Add(i, end, SyntaxKind.Meta);
            return Body;
        }

        // List markers: -, *, +, 1., 1) and a task box.
        var marker = ListMarkerEnd(s, i, end);
        if (marker > 0)
        {
            w.Add(i, marker, SyntaxKind.Keyword);
            i = marker;
            if (Scan.At(s, i, end) == '[' && Scan.At(s, i + 2, end) == ']' && Scan.At(s, i + 1, end) is ' ' or 'x' or 'X')
            {
                w.Add(i, i + 3, SyntaxKind.Keyword);
                i += 3;
            }
        }

        // [id]: https://example.com "Title"
        if (i < end && s[i] == '[' && Scan.IndexOf(s, i, end, "]:") is var defEnd and > 0 && Scan.IndexOf(s, i + 1, defEnd, '[') < 0)
        {
            w.Add(i, defEnd + 1, SyntaxKind.Link);
            w.Add(defEnd + 1, end, SyntaxKind.Meta);
            return Body;
        }

        Inline(s, i, end, w);
        return Body;
    }

    // Emphasis, strong, code spans, links, autolinks, inline tags and escapes within a line.
    private static void Inline(string s, int i, int end, SyntaxWriter w)
    {
        var run = i;
        while (i < end)
        {
            var c = s[i];
            int e;
            switch (c)
            {
                case '\\' when i + 1 < end && char.IsAsciiLetterOrDigit(s[i + 1]) is false && !Scan.IsSpace(s[i + 1]):
                    w.Add(run, i, SyntaxKind.Text);
                    w.Add(i, i + 2, SyntaxKind.Escape);
                    i = run = i + 2;
                    continue;
                case '`':
                {
                    var ticks = i;
                    while (ticks < end && s[ticks] == '`') ticks++;
                    var close = Scan.IndexOf(s, ticks, end, s[i..ticks]);
                    if (close < 0) break;
                    w.Add(run, i, SyntaxKind.Text);
                    e = close + (ticks - i);
                    w.Add(i, e, SyntaxKind.String);
                    i = run = e;
                    continue;
                }
                case '*' or '_':
                {
                    if (c == '_' && i > 0 && Scan.IsIdentPart(s[i - 1])) break;
                    var strong = Scan.At(s, i + 1, end) == c;
                    var open = strong ? i + 2 : i + 1;
                    if (open >= end || Scan.IsSpace(s[open])) break;
                    var delimiter = strong ? new string(c, 2) : c.ToString();
                    var close = Scan.IndexOf(s, open + 1, end, delimiter);
                    while (close > 0 && (Scan.IsSpace(s[close - 1]) || (!strong && Scan.At(s, close + 1, end) == c)))
                        close = Scan.IndexOf(s, close + delimiter.Length + (strong ? 0 : 1), end, delimiter);
                    if (close < 0 || (c == '_' && Scan.IsIdentPart(Scan.At(s, close + delimiter.Length, end)))) break;
                    w.Add(run, i, SyntaxKind.Text);
                    e = close + delimiter.Length;
                    w.Add(i, e, strong ? SyntaxKind.Strong : SyntaxKind.Emphasis);
                    i = run = e;
                    continue;
                }
                case '!' when Scan.At(s, i + 1, end) == '[':
                case '[':
                {
                    var open = c == '!' ? i + 1 : i;
                    var close = Scan.IndexOf(s, open + 1, end, ']');
                    if (close < 0) break;
                    var after = Scan.At(s, close + 1, end);
                    if (after is not ('(' or '[')) break;
                    var targetClose = Scan.IndexOf(s, close + 2, end, after == '(' ? ')' : ']');
                    if (targetClose < 0) break;
                    w.Add(run, i, SyntaxKind.Text);
                    w.Add(i, close + 1, SyntaxKind.Link);
                    w.Add(close + 1, targetClose + 1, SyntaxKind.Meta);
                    i = run = targetClose + 1;
                    continue;
                }
                case '<':
                {
                    var close = Scan.IndexOf(s, i + 1, end, '>');
                    if (close < 0) break;
                    var inside = s.AsSpan(i + 1, close - i - 1);
                    if (inside.Contains("://", StringComparison.Ordinal) || (inside.Contains('@') && !inside.Contains(' ')))
                    {
                        w.Add(run, i, SyntaxKind.Text);
                        w.Add(i, close + 1, SyntaxKind.Link);
                        i = run = close + 1;
                        continue;
                    }
                    var nameStart = Scan.At(s, i + 1, end) == '/' ? i + 2 : i + 1;
                    if (nameStart < end && char.IsAsciiLetter(s[nameStart]))
                    {
                        w.Add(run, i, SyntaxKind.Text);
                        var nameEnd = MarkupSyntax.NameEnd(s, nameStart, close);
                        w.Add(i, nameStart, SyntaxKind.Text);
                        w.Add(nameStart, nameEnd, SyntaxKind.Tag);
                        w.Add(nameEnd, close + 1, SyntaxKind.Text);
                        i = run = close + 1;
                        continue;
                    }
                    break;
                }
                case 'h' when Scan.At(s, i, end, "https://") || Scan.At(s, i, end, "http://"):
                {
                    if (i > 0 && Scan.IsIdentPart(s[i - 1])) break;
                    e = i;
                    while (e < end && !Scan.IsSpace(s[e]) && s[e] is not ('<' or '>' or ')' or '"')) e++;
                    while (e > i && s[e - 1] is '.' or ',' or ';' or ':' or '!' or '?') e--;
                    w.Add(run, i, SyntaxKind.Text);
                    w.Add(i, e, SyntaxKind.Link);
                    i = run = e;
                    continue;
                }
            }
            i++;
        }
        w.Add(run, end, SyntaxKind.Text);
    }

    // ---, ***, ___ (three or more, spaces allowed between).
    private static bool IsRule(ReadOnlySpan<char> line)
    {
        var t = line.Trim();
        if (t.Length < 3 || t[0] is not ('-' or '*' or '_')) return false;
        var count = 0;
        foreach (var c in t)
        {
            if (c == t[0]) count++;
            else if (c != ' ') return false;
        }
        return count >= 3;
    }

    private static bool IsTableDivider(string s, int i, int end)
    {
        var dashes = false;
        for (var k = i; k < end; k++)
        {
            var c = s[k];
            if (c == '-') dashes = true;
            else if (c is not ('|' or ':' or ' ' or '\t')) return false;
        }
        return dashes;
    }

    // The end of a list marker and the space after it at i, or -1.
    private static int ListMarkerEnd(string s, int i, int end)
    {
        if (s[i] is '-' or '*' or '+')
            return i + 1 < end && Scan.IsSpace(s[i + 1]) ? i + 2 : -1;
        var k = i;
        while (k < end && k - i < 9 && char.IsAsciiDigit(s[k])) k++;
        if (k == i || k >= end || s[k] is not ('.' or ')')) return -1;
        return k + 1 < end && Scan.IsSpace(s[k + 1]) ? k + 2 : -1;
    }
}
