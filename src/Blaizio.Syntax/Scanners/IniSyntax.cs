namespace Blaizio;

/// <summary>
/// INI-style config (<c>.editorconfig</c>, <c>.gitconfig</c>, <c>.ini</c>) and TOML: <c>[sections]</c>
/// and <c>[[tables]]</c> as types, keys, and values - strings (TOML's multi-line ones carry across
/// lines), numbers, booleans, dates and arrays. INI comments start a line with <c>;</c> or <c>#</c>;
/// TOML comments start anywhere with <c>#</c>.
/// </summary>
internal sealed class IniSyntax : SyntaxLanguage
{
    public static readonly IniSyntax Ini = new(toml: false);

    public static readonly IniSyntax Toml = new(toml: true);

    private readonly bool _toml;

    private IniSyntax(bool toml) => _toml = toml;

    /// <summary>An open TOML multi-line string, closed by <paramref name="Delimiter"/>.</summary>
    internal sealed record State(string Delimiter);

    private static readonly WordSet Booleans = new(true, "true", "false", "yes", "no", "on", "off");

    public override string Name => _toml ? "toml" : "ini";

    public override IReadOnlyList<string> Aliases => _toml ? [] : ["editorconfig", "gitconfig", "cfg", "conf", "properties", "dosini", "desktop"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var i = start;
        if (state is State open)
        {
            var close = Scan.IndexOf(s, i, end, open.Delimiter);
            if (close < 0)
            {
                w.Add(i, end, SyntaxKind.String);
                return open;
            }
            w.Add(i, close + 3, SyntaxKind.String);
            return Values(s, close + 3, end, w);
        }

        i = Scan.Spaces(s, i, end);
        w.Add(start, i, SyntaxKind.Text);
        if (i >= end) return null;
        var c = s[i];
        if (c == '#' || (!_toml && c == ';'))
        {
            w.Add(i, end, SyntaxKind.Comment);
            return null;
        }
        if (c == '[')
        {
            var close = Scan.IndexOf(s, i + 1, end, ']');
            var e = close < 0 ? end : close + 1;
            if (Scan.At(s, e, end) == ']') e++;
            w.Add(i, e, SyntaxKind.Type);
            return _toml ? Values(s, e, end, w) : Comment(s, e, end, w);
        }

        var eq = KeyEnd(s, i, end);
        if (eq < 0) return _toml ? Values(s, i, end, w) : Plain(s, i, end, w);

        var keyEnd = eq;
        while (keyEnd > i && Scan.IsSpace(s[keyEnd - 1])) keyEnd--;
        Key(s, i, keyEnd, w);
        w.Add(keyEnd, eq + 1, SyntaxKind.Text);
        return _toml ? Values(s, eq + 1, end, w) : Value(s, eq + 1, end, w);
    }

    // The index of the '=' (or INI ':') after the key at i, or -1.
    private int KeyEnd(string s, int i, int end)
    {
        for (var k = i; k < end; k++)
        {
            var c = s[k];
            if (c == '=' || (!_toml && c == ':')) return k;
            if (c is '"' or '\'') k = JsonSyntax.QuotedEnd(s, k, end) - 1;
            else if (_toml && !(Scan.IsIdentPart(c) || c is '-' or '.' or ' ' or '\t')) return -1;
        }
        return -1;
    }

    private static void Key(string s, int i, int end, SyntaxWriter w)
    {
        for (var k = i; k < end;)
        {
            if (s[k] is '"' or '\'')
            {
                var e = Math.Min(JsonSyntax.QuotedEnd(s, k, end), end);
                w.Add(k, e, SyntaxKind.Property);
                k = e;
            }
            else if (s[k] == '.')
            {
                w.Add(k, k + 1, SyntaxKind.Text);
                k++;
            }
            else
            {
                var e = k;
                while (e < end && s[e] is not ('.' or '"' or '\'')) e++;
                w.Add(k, e, SyntaxKind.Property);
                k = e;
            }
        }
    }

    // An INI value: a quoted string, a number or boolean, or plain text read as a string.
    private static object? Value(string s, int i, int end, SyntaxWriter w)
    {
        var k = Scan.Spaces(s, i, end);
        w.Add(i, k, SyntaxKind.Text);
        var text = s.AsSpan(k, end - k).TrimEnd();
        var e = k + text.Length;
        if (text.IsEmpty) return null;
        if (Booleans.Contains(text)) w.Add(k, e, SyntaxKind.Constant);
        else if (IsNumber(text)) w.Add(k, e, SyntaxKind.Number);
        else w.Add(k, e, SyntaxKind.String);
        w.Add(e, end, SyntaxKind.Text);
        return null;
    }

    private static object? Plain(string s, int i, int end, SyntaxWriter w)
    {
        w.Add(i, end, SyntaxKind.Text);
        return null;
    }

    private static object? Comment(string s, int i, int end, SyntaxWriter w)
    {
        var k = Scan.Spaces(s, i, end);
        w.Add(i, k, SyntaxKind.Text);
        if (k < end) w.Add(k, end, s[k] is '#' or ';' ? SyntaxKind.Comment : SyntaxKind.Text);
        return null;
    }

    // TOML values: strings (basic, literal, multi-line), numbers, dates, booleans, arrays and inline tables.
    private static object? Values(string s, int i, int end, SyntaxWriter w)
    {
        while (i < end)
        {
            var c = s[i];
            if (Scan.IsSpace(c) || c is ',' or '[' or ']' or '{' or '}')
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                i++;
                continue;
            }
            if (c == '#')
            {
                w.Add(i, end, SyntaxKind.Comment);
                return null;
            }
            if (Scan.At(s, i, end, "\"\"\"") || Scan.At(s, i, end, "'''"))
            {
                var delimiter = s.Substring(i, 3);
                var close = Scan.IndexOf(s, i + 3, end, delimiter);
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.String);
                    return new State(delimiter);
                }
                w.Add(i, close + 3, SyntaxKind.String);
                i = close + 3;
                continue;
            }
            if (c == '"')
            {
                i = Scan.QuotedWithEscapes(s, i, end, w, out _);
                continue;
            }
            if (c == '\'')
            {
                var close = Scan.IndexOf(s, i + 1, end, '\'');
                var e = close < 0 ? end : close + 1;
                w.Add(i, e, SyntaxKind.String);
                i = e;
                continue;
            }
            if (Scan.IsIdentStart(c) || char.IsAsciiDigit(c) || c is '+' or '-')
            {
                var e = i + 1;
                while (e < end && (Scan.IsIdentPart(s[e]) || s[e] is '-' or '+' or '.' or ':')) e++;
                var word = s.AsSpan(i, e - i);
                // A key inside an inline table: { name = "x" }
                if (Scan.At(s, Scan.Spaces(s, e, end), end) == '=')
                    w.Add(i, e, SyntaxKind.Property);
                else if (word is "true" or "false")
                    w.Add(i, e, SyntaxKind.Constant);
                else if (char.IsAsciiDigit(word[0]) || word.Length > 1 && char.IsAsciiDigit(word[1]) || word is "inf" or "+inf" or "-inf" or "nan" or "+nan" or "-nan")
                    w.Add(i, e, SyntaxKind.Number);
                else
                    w.Add(i, e, SyntaxKind.Text);
                i = e;
                continue;
            }
            w.Add(i, i + 1, SyntaxKind.Text);
            i++;
        }
        return null;
    }

    private static bool IsNumber(ReadOnlySpan<char> text)
    {
        var k = text[0] is '-' or '+' ? 1 : 0;
        if (k >= text.Length || !char.IsAsciiDigit(text[k])) return false;
        for (; k < text.Length; k++)
            if (!(char.IsAsciiDigit(text[k]) || text[k] is '.' or '_')) return false;
        return true;
    }
}
