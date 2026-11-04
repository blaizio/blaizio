namespace Blaizio;

/// <summary>
/// Unified diffs and patches: added lines as inserted, removed lines as deleted, file headers
/// (<c>diff --git</c>, <c>index</c>, <c>---</c> / <c>+++</c>) as metadata and <c>@@ ... @@</c> hunk ranges
/// as functions. The hunk's line counts tell a removed line that starts with <c>--</c> from a header.
/// </summary>
internal sealed class DiffSyntax : SyntaxLanguage
{
    public static readonly DiffSyntax Instance = new();

    /// <summary>The old and new lines still to come in the current hunk.</summary>
    internal sealed record State(int Old, int New);

    private static readonly string[] Headers =
    [
        "diff ", "index ", "new file mode", "deleted file mode", "old mode", "new mode", "similarity index",
        "dissimilarity index", "rename from", "rename to", "copy from", "copy to", "Binary files", "Only in",
    ];

    public override string Name => "diff";

    public override IReadOnlyList<string> Aliases => ["patch", "udiff"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var st = state as State;
        var c = Scan.At(s, start, end);
        var inHunk = st is not null && (st.Old > 0 || st.New > 0);

        if (Scan.At(s, start, end, "@@"))
        {
            var close = Scan.IndexOf(s, start + 2, end, "@@");
            var e = close < 0 ? end : close + 2;
            w.Add(start, e, SyntaxKind.Function);
            w.Add(e, end, SyntaxKind.Text);
            return Hunk(s, start + 2, close < 0 ? end : close);
        }
        if (!inHunk)
        {
            if ((Scan.At(s, start, end, "--- ") || Scan.At(s, start, end, "+++ ") || Scan.At(s, start, end, "*** ")) || IsHeader(s, start, end))
            {
                w.Add(start, end, SyntaxKind.Meta);
                return st;
            }
        }

        switch (c)
        {
            case '+' or '>':
                w.Add(start, end, SyntaxKind.Inserted);
                return Count(st, old: 0, @new: 1);
            case '-' or '<':
                w.Add(start, end, SyntaxKind.Deleted);
                return Count(st, old: 1, @new: 0);
            case '\\':
                w.Add(start, end, SyntaxKind.Comment);
                return st;
            default:
                w.Add(start, end, SyntaxKind.Text);
                return Count(st, old: 1, @new: 1);
        }
    }

    private static bool IsHeader(string s, int start, int end)
    {
        foreach (var header in Headers)
            if (Scan.At(s, start, end, header)) return true;
        return false;
    }

    private static State? Count(State? st, int old, int @new)
    {
        if (st is null) return null;
        var next = new State(Math.Max(0, st.Old - old), Math.Max(0, st.New - @new));
        return next.Old == 0 && next.New == 0 ? null : next;
    }

    // "-12,5 +14,7" (a missing count is 1); a range that cannot be read keeps the hunk open.
    private static State Hunk(string s, int i, int end)
    {
        var old = Range(s, i, end, '-');
        var @new = Range(s, i, end, '+');
        return old < 0 || @new < 0 ? new State(int.MaxValue, int.MaxValue) : new State(old, @new);
    }

    private static int Range(string s, int i, int end, char sign)
    {
        var k = Scan.IndexOf(s, i, end, sign);
        if (k < 0) return -1;
        k++;
        while (k < end && char.IsAsciiDigit(s[k])) k++;
        if (Scan.At(s, k, end) != ',') return 1;
        var digits = ++k;
        while (k < end && char.IsAsciiDigit(s[k])) k++;
        return k > digits && int.TryParse(s.AsSpan(digits, k - digits), out var count) ? count : -1;
    }
}
