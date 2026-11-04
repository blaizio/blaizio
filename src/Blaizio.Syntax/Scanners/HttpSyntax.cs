namespace Blaizio;

/// <summary>
/// HTTP request files (<c>.http</c> / <c>.rest</c>): the method as a keyword and the target as a
/// link, header names as keys, <c>{{variables}}</c> and <c>@name = value</c> definitions, <c>###</c>
/// request separators and <c>#</c> / <c>//</c> comments, and the body after the blank line -
/// JSON or XML tokenized as such, anything else plain.
/// </summary>
internal sealed class HttpSyntax : SyntaxLanguage
{
    public static readonly HttpSyntax Instance = new();

    internal enum Phase : byte
    {
        Headers,
        BodyStart,
        Body,
    }

    /// <summary>Where the request is; in the body, <paramref name="Language"/> and <paramref name="Inner"/> tokenize it.</summary>
    internal sealed record State(Phase Phase, SyntaxLanguage? Language, object? Inner);

    private static readonly WordSet Methods = new(true,
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT", "GRAPHQL");

    private static readonly object Headers = new State(Phase.Headers, null, null);

    private static readonly object BodyStart = new State(Phase.BodyStart, null, null);

    public override string Name => "http";

    public override IReadOnlyList<string> Aliases => ["rest", "https"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var st = state as State;
        var first = Scan.Spaces(s, start, end);

        if (Scan.At(s, first, end, "###"))
        {
            w.Add(start, end, SyntaxKind.Comment);
            return null;
        }
        if (st is null || st.Phase == Phase.Headers)
        {
            if (first < end && (s[first] == '#' || Scan.At(s, first, end, "//")))
            {
                w.Add(start, end, SyntaxKind.Comment);
                return st;
            }
        }

        switch (st?.Phase)
        {
            case null:
                if (first >= end) return null;
                if (s[first] == '@' && Scan.IndexOf(s, first, end, '=') is var eq and > 0)
                {
                    // @host = https://localhost:5001
                    var name = Scan.Word(s, first + 1, eq, "-.");
                    w.Add(first, name, SyntaxKind.Variable);
                    var value = Scan.Spaces(s, eq + 1, end);
                    w.Add(name, value, SyntaxKind.Text);
                    Variables(s, value, end, w, SyntaxKind.String);
                    return null;
                }
                RequestLine(s, first, end, w);
                return Headers;

            case Phase.Headers:
                if (first >= end) return BodyStart;
                if (s[first] is '?' or '&')
                {
                    // A query continued on its own line.
                    Variables(s, first, end, w, SyntaxKind.Link);
                    return st;
                }
                var colon = Scan.IndexOf(s, first, end, ':');
                if (colon > first)
                {
                    w.Add(first, colon, SyntaxKind.Property);
                    w.Add(colon, colon + 1, SyntaxKind.Text);
                    Variables(s, colon + 1, end, w, SyntaxKind.Text);
                    return st;
                }
                Variables(s, start, end, w, SyntaxKind.Text);
                return st;

            default:
                var language = st.Language;
                if (st.Phase == Phase.BodyStart)
                {
                    if (first >= end) return st;
                    language = s[first] switch
                    {
                        '{' or '[' => w.Find("json") ?? JsonSyntax.Instance,
                        '<' => w.Find("xml") ?? MarkupSyntax.Xml,
                        _ => null,
                    };
                }
                if (s.AsSpan(first, end - first).StartsWith("< "))
                {
                    // < ./body.json: the body comes from a file.
                    w.Add(start, end, SyntaxKind.Meta);
                    return new State(Phase.Body, language, st.Inner);
                }
                if (language is null)
                {
                    Variables(s, start, end, w, SyntaxKind.Text);
                    return new State(Phase.Body, null, null);
                }
                var inner = w.Embed(language, start, end, st.Phase == Phase.BodyStart ? null : st.Inner);
                return new State(Phase.Body, language, inner);
        }
    }

    // GET {{host}}/api/orders?page=1 HTTP/1.1
    private static void RequestLine(string s, int i, int end, SyntaxWriter w)
    {
        var methodEnd = Scan.Identifier(s, i, end);
        if (Methods.Contains(s, i, methodEnd))
        {
            w.Add(i, methodEnd, SyntaxKind.Keyword);
            i = methodEnd;
        }
        var target = Scan.Spaces(s, i, end);
        w.Add(i, target, SyntaxKind.Text);
        var version = Scan.IndexOf(s, target, end, " HTTP/");
        var targetEnd = version < 0 ? end : version;
        Variables(s, target, targetEnd, w, SyntaxKind.Link);
        if (version >= 0)
        {
            w.Add(version, version + 1, SyntaxKind.Text);
            w.Add(version + 1, end, SyntaxKind.Meta);
        }
    }

    // Text of one kind with {{variables}} (and {{$guid}}-style functions) picked out.
    private static void Variables(string s, int i, int end, SyntaxWriter w, SyntaxKind kind)
    {
        while (i < end)
        {
            var open = Scan.IndexOf(s, i, end, "{{");
            if (open < 0)
            {
                w.Add(i, end, kind);
                return;
            }
            w.Add(i, open, kind);
            var close = Scan.IndexOf(s, open + 2, end, "}}");
            var e = close < 0 ? end : close + 2;
            w.Add(open, e, SyntaxKind.Variable);
            i = e;
        }
    }
}
