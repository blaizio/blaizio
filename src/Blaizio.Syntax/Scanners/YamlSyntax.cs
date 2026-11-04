namespace Blaizio;

/// <summary>
/// YAML (workflows, compose files, configs): keys, quoted and plain scalars as strings, numbers
/// and booleans, anchors and aliases, tags, comments, document markers, and block scalars
/// (<c>|</c> / <c>&gt;</c>), whose more-indented lines are one string. A workflow step's
/// <c>run:</c> value is a script, tokenized as bash. GitHub Actions expressions
/// (<c>${{ ... }}</c>) read as variables.
/// </summary>
internal sealed class YamlSyntax : SyntaxLanguage
{
    public static readonly YamlSyntax Instance = new();

    /// <summary>
    /// A block scalar under a key at <paramref name="Indent"/> (a script when <paramref name="Shell"/>
    /// holds its state), or a quoted scalar left open by <paramref name="Quote"/>.
    /// </summary>
    internal sealed record State(int Indent, char Quote, bool Script = false, object? Shell = null);

    private static readonly WordSet Constants = new(true, "true", "false", "null", "yes", "no", "on", "off", "~");

    public override string Name => "yaml";

    public override IReadOnlyList<string> Aliases => ["yml"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var i = start;
        var indent = Scan.Spaces(s, i, end) - i;
        if (state is State st)
        {
            if (st.Quote != '\0')
            {
                var close = QuoteEnd(s, i, end, st.Quote);
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.String);
                    return st;
                }
                w.Add(i, close, SyntaxKind.String);
                i = close;
                return Rest(s, i, end, w, flow: 0);
            }
            // A block scalar runs while lines are more indented than its key (blank lines included).
            if (i + indent >= end || indent > st.Indent)
            {
                if (!st.Script)
                {
                    w.Add(i, i + indent, SyntaxKind.Text);
                    w.Add(i + indent, end, SyntaxKind.String);
                    return st;
                }
                return st with { Shell = w.Embed(w.Find("bash") ?? ShellSyntax.Instance, i, end, st.Shell) };
            }
        }

        i += indent;
        w.Add(start, i, SyntaxKind.Text);
        if (i >= end) return null;

        if (s[i] == '#')
        {
            w.Add(i, end, SyntaxKind.Comment);
            return null;
        }
        if (indent == 0 && (Scan.At(s, i, end, "---") || Scan.At(s, i, end, "...")) && (i + 3 >= end || Scan.IsSpace(s[i + 3])))
        {
            w.Add(i, i + 3, SyntaxKind.Meta);
            return Rest(s, i + 3, end, w, flow: 0);
        }
        if (indent == 0 && s[i] == '%')
        {
            w.Add(i, end, SyntaxKind.Meta);
            return null;
        }

        // List markers: "- ", "- - ".
        while (i < end && s[i] == '-' && (i + 1 >= end || Scan.IsSpace(s[i + 1])))
        {
            var k = Scan.Spaces(s, i + 1, end);
            w.Add(i, k, SyntaxKind.Text);
            i = k;
        }

        var keyStart = i;
        var keyEnd = KeyEnd(s, i, end);
        if (keyEnd > 0)
        {
            w.Add(keyStart, keyEnd, SyntaxKind.Property);
            w.Add(keyEnd, keyEnd + 1, SyntaxKind.Text);
            i = keyEnd + 1;
            var v = Scan.Spaces(s, i, end);
            w.Add(i, v, SyntaxKind.Text);
            i = v;
            var script = keyEnd - keyStart == 3 && Scan.At(s, keyStart, end, "run");
            if (i < end && s[i] is '|' or '>')
            {
                var e = i + 1;
                while (e < end && (s[e] is '+' or '-' || char.IsAsciiDigit(s[e]))) e++;
                if (Scan.Spaces(s, e, end) == end || Scan.At(s, Scan.Spaces(s, e, end), end) == '#')
                {
                    w.Add(i, e, SyntaxKind.Keyword);
                    Rest(s, e, end, w, flow: 0);
                    return new State(keyStart - start, '\0', script);
                }
            }
            if (script && i < end && s[i] is not ('"' or '\'' or '{' or '['))
            {
                // run: dotnet test --no-build
                w.Embed(w.Find("bash") ?? ShellSyntax.Instance, i, end, null);
                return null;
            }
        }
        return Rest(s, i, end, w, flow: 0);
    }

    // The end of a key at i (the index of its ':'), or -1: a plain or quoted scalar followed by ':' and a space or the line end.
    private static int KeyEnd(string s, int i, int end)
    {
        if (i >= end) return -1;
        if (s[i] is '"' or '\'')
        {
            var close = QuoteEnd(s, i + 1, end, s[i]);
            return close > 0 && Scan.At(s, close, end) == ':' && (close + 1 >= end || Scan.IsSpace(s[close + 1])) ? close : -1;
        }
        if (s[i] is '{' or '[' or '&' or '*' or '!' or '|' or '>' or '#' or '%' or '@' or '`') return -1;
        for (var k = i; k < end; k++)
        {
            if (s[k] == ':' && (k + 1 >= end || Scan.IsSpace(s[k + 1]))) return k > i ? k : -1;
            if (s[k] == '#' && k > i && Scan.IsSpace(s[k - 1])) return -1;
        }
        return -1;
    }

    // The index just past the closing quote at or after i, or -1 when the scalar continues.
    private static int QuoteEnd(string s, int i, int end, char quote)
    {
        for (var k = i; k < end; k++)
        {
            if (quote == '"' && s[k] == '\\')
            {
                k++;
                continue;
            }
            if (s[k] != quote) continue;
            if (quote == '\'' && Scan.At(s, k + 1, end) == '\'')
            {
                k++;
                continue;
            }
            return k + 1;
        }
        return -1;
    }

    // A value and whatever follows it on the line: flow collections, comments, more values.
    private static State? Rest(string s, int i, int end, SyntaxWriter w, int flow)
    {
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
            if (c == '#' && (i == 0 || Scan.IsSpace(s[i - 1])))
            {
                w.Add(i, end, SyntaxKind.Comment);
                return null;
            }
            if (c is '"' or '\'')
            {
                var close = QuoteEnd(s, i + 1, end, c);
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.String);
                    return new State(0, c);
                }
                var key = Scan.At(s, close, end) == ':' && (flow > 0 || close + 1 >= end || Scan.IsSpace(s[close + 1]));
                if (c == '"') JsonSyntax.WriteQuoted(s, i, close, w, key ? SyntaxKind.Property : SyntaxKind.String);
                else w.Add(i, close, key ? SyntaxKind.Property : SyntaxKind.String);
                i = close;
                continue;
            }
            if (c is '{' or '[')
            {
                flow++;
                w.Add(i, i + 1, SyntaxKind.Text);
                i++;
                continue;
            }
            if (c is '}' or ']' or ',' or ':')
            {
                if (c is '}' or ']' && flow > 0) flow--;
                w.Add(i, i + 1, SyntaxKind.Text);
                i++;
                continue;
            }
            if (c is '&' or '*' && i + 1 < end && !Scan.IsSpace(s[i + 1]))
            {
                var e = ScalarEnd(s, i + 1, end, flow);
                w.Add(i, e, SyntaxKind.Variable);
                i = e;
                continue;
            }
            if (c == '!')
            {
                var e = i + 1;
                while (e < end && !Scan.IsSpace(s[e]) && s[e] is not (',' or ']' or '}')) e++;
                w.Add(i, e, SyntaxKind.Type);
                i = e;
                continue;
            }
            if (c == '$' && Scan.At(s, i + 1, end, "{{"))
            {
                var close = Scan.IndexOf(s, i + 3, end, "}}");
                var e = close < 0 ? end : close + 2;
                w.Add(i, e, SyntaxKind.Variable);
                i = e;
                continue;
            }

            // A plain scalar: up to a comment, or a flow delimiter inside a flow collection.
            var stop = ScalarEnd(s, i, end, flow);
            if (stop == i) stop = i + 1;
            var text = s.AsSpan(i, stop - i).TrimEnd();
            var scalarEnd = i + text.Length;
            if (flow > 0 && Scan.At(s, scalarEnd, end) == ':')
                w.Add(i, scalarEnd, SyntaxKind.Property);
            else if (Constants.Contains(text))
                w.Add(i, scalarEnd, SyntaxKind.Constant);
            else if (IsNumber(text))
                w.Add(i, scalarEnd, SyntaxKind.Number);
            else
                PlainScalar(s, i, scalarEnd, w);
            i = scalarEnd;
        }
        return null;
    }

    // A plain string, with any ${{ }} expressions inside it.
    private static void PlainScalar(string s, int i, int end, SyntaxWriter w)
    {
        while (i < end)
        {
            var open = Scan.IndexOf(s, i, end, "${{");
            if (open < 0)
            {
                w.Add(i, end, SyntaxKind.String);
                return;
            }
            w.Add(i, open, SyntaxKind.String);
            var close = Scan.IndexOf(s, open + 3, end, "}}");
            var e = close < 0 ? end : close + 2;
            w.Add(open, e, SyntaxKind.Variable);
            i = e;
        }
    }

    private static int ScalarEnd(string s, int i, int end, int flow)
    {
        for (var k = i; k < end; k++)
        {
            if (s[k] == '#' && k > i && Scan.IsSpace(s[k - 1])) return k;
            if (flow > 0 && s[k] is ',' or ']' or '}') return k;
            if (flow > 0 && s[k] == ':' && (k + 1 >= end || Scan.IsSpace(s[k + 1]) || s[k + 1] is ',' or ']' or '}')) return k;
        }
        return end;
    }

    private static bool IsNumber(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return false;
        var k = text[0] is '-' or '+' ? 1 : 0;
        if (k >= text.Length) return false;
        if (text[k..] is ".inf" or ".Inf" or ".nan" or ".NaN") return true;
        var digits = 0;
        for (; k < text.Length; k++)
        {
            var c = text[k];
            if (char.IsAsciiDigit(c)) digits++;
            else if (c is not ('.' or '_' or 'e' or 'E' or 'x' or 'o' or '+' or '-') && !char.IsAsciiHexDigit(c)) return false;
        }
        return digits > 0 && char.IsAsciiDigit(text[text[0] is '-' or '+' ? 1 : 0]);
    }
}
