namespace Blaizio;

/// <summary>
/// JSON, with the relaxations people paste: comments (JSONC), single quotes and bare keys (JSON5).
/// A string or bare word followed by <c>:</c> is a key.
/// </summary>
internal sealed class JsonSyntax : SyntaxLanguage
{
    public static readonly JsonSyntax Instance = new();

    // The one multi-line construct: an open /* */ comment.
    private static readonly object InComment = "json:comment";

    private static readonly WordSet Literals = new(false, "true", "false", "null", "NaN", "Infinity");

    public override string Name => "json";

    public override IReadOnlyList<string> Aliases => ["jsonc", "json5", "jsonl", "webmanifest"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var i = start;
        if (state is not null)
        {
            var close = Scan.IndexOf(s, i, end, "*/");
            if (close < 0)
            {
                w.Add(i, end, SyntaxKind.Comment);
                return InComment;
            }
            w.Add(i, close + 2, SyntaxKind.Comment);
            i = close + 2;
        }

        while (i < end)
        {
            var c = s[i];
            if (c is '"' or '\'')
            {
                var close = QuotedEnd(s, i, end);
                WriteQuoted(s, i, close, w, IsKey(s, close, end) ? SyntaxKind.Property : SyntaxKind.String);
                i = close;
            }
            else if (c == '/' && Scan.At(s, i + 1, end) == '/')
            {
                w.Add(i, end, SyntaxKind.Comment);
                return null;
            }
            else if (c == '/' && Scan.At(s, i + 1, end) == '*')
            {
                var close = Scan.IndexOf(s, i + 2, end, "*/");
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.Comment);
                    return InComment;
                }
                w.Add(i, close + 2, SyntaxKind.Comment);
                i = close + 2;
            }
            else if (c == '-' && i + 1 < end && Scan.IsNumberStart(s, i + 1, end))
            {
                var e = Scan.Number(s, i + 1, end);
                w.Add(i, e, SyntaxKind.Number);
                i = e;
            }
            else if (Scan.IsNumberStart(s, i, end))
            {
                var e = Scan.Number(s, i, end);
                w.Add(i, e, SyntaxKind.Number);
                i = e;
            }
            else if (Scan.IsIdentStart(c) || c == '$')
            {
                var e = Scan.Word(s, i + 1, end, "$-");
                var kind = IsKey(s, e, end) ? SyntaxKind.Property
                    : Literals.Contains(s, i, e) ? SyntaxKind.Constant
                    : SyntaxKind.Text;
                w.Add(i, e, kind);
                i = e;
            }
            else
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                i++;
            }
        }
        return null;
    }

    // A key: the next non-space character is a colon.
    private static bool IsKey(string s, int i, int end) => Scan.At(s, Scan.Spaces(s, i, end), end) == ':';

    /// <summary>The end of the quoted string at <paramref name="i"/> (past its closing quote, or the range end).</summary>
    internal static int QuotedEnd(string s, int i, int end)
    {
        var quote = s[i];
        for (var k = i + 1; k < end; k++)
        {
            if (s[k] == '\\') k++;
            else if (s[k] == quote) return k + 1;
        }
        return end;
    }

    /// <summary>Writes <c>s[start..stop]</c>, a quoted string, as <paramref name="kind"/> with its backslash escapes.</summary>
    internal static void WriteQuoted(string s, int start, int stop, SyntaxWriter w, SyntaxKind kind)
    {
        var run = start;
        for (var k = start + 1; k < stop; k++)
        {
            if (s[k] != '\\' || k + 1 >= stop) continue;
            w.Add(run, k, kind);
            var e = Math.Min(Scan.EscapeEnd(s, k, stop), stop);
            w.Add(k, e, SyntaxKind.Escape);
            run = e;
            k = e - 1;
        }
        w.Add(run, stop, kind);
    }
}
