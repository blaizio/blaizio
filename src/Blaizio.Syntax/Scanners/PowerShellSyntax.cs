namespace Blaizio;

/// <summary>
/// PowerShell: cmdlets (<c>Verb-Noun</c>) and the command word of each pipeline element read as
/// functions, <c>-Parameters</c> as attributes and <c>-eq</c> / <c>-match</c> / ... as keywords,
/// <c>$variables</c> (with <c>$true</c> / <c>$false</c> / <c>$null</c> as constants), <c>[Types]</c>,
/// strings with their backtick escapes and expansions, here-strings, <c>#</c> and <c>&lt;# #&gt;</c>
/// comments; a trailing backtick continues the line.
/// </summary>
internal sealed class PowerShellSyntax : SyntaxLanguage
{
    public static readonly PowerShellSyntax Instance = new();

    internal enum Mode : byte
    {
        Continuation,
        BlockComment,
        Quoted,
        HereString,
    }

    internal sealed record State(Mode Mode, char Quote);

    private static readonly object Continued = new State(Mode.Continuation, '\0');

    private static readonly WordSet Keywords = new(true,
        "begin", "break", "catch", "class", "continue", "data", "define", "do", "dynamicparam", "else",
        "elseif", "end", "enum", "exit", "filter", "finally", "for", "foreach", "from", "function",
        "hidden", "if", "in", "param", "process", "return", "static", "switch", "throw", "trap", "try",
        "until", "using", "var", "while", "workflow", "parallel", "sequence", "inlinescript", "configuration");

    private static readonly WordSet Operators = new(true,
        "eq", "ne", "gt", "ge", "lt", "le", "like", "notlike", "match", "notmatch", "contains",
        "notcontains", "in", "notin", "replace", "split", "join", "and", "or", "not", "xor", "is",
        "isnot", "as", "f", "band", "bor", "bxor", "bnot", "shl", "shr",
        "ceq", "cne", "cgt", "cge", "clt", "cle", "clike", "cnotlike", "cmatch", "cnotmatch", "creplace", "csplit",
        "ieq", "ine", "igt", "ige", "ilt", "ile", "ilike", "inotlike", "imatch", "inotmatch", "ireplace", "isplit");

    private static readonly WordSet Constants = new(true, "true", "false", "null");

    public override string Name => "powershell";

    public override IReadOnlyList<string> Aliases => ["ps1", "ps", "pwsh", "psm1", "psd1", "posh"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var i = start;
        var st = state as State;
        var commandStart = st?.Mode != Mode.Continuation;
        var functionName = false;
        var typeEnd = -1;

        switch (st?.Mode)
        {
            case Mode.BlockComment:
            {
                var close = Scan.IndexOf(s, i, end, "#>");
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.Comment);
                    return st;
                }
                w.Add(i, close + 2, SyntaxKind.Comment);
                i = close + 2;
                break;
            }
            case Mode.HereString:
            {
                // The terminator ("@ or '@) opens its own line.
                if (Scan.At(s, i, end) == st.Quote && Scan.At(s, i + 1, end) == '@')
                {
                    w.Add(i, i + 2, SyntaxKind.String);
                    i += 2;
                    commandStart = false;
                    break;
                }
                if (st.Quote == '"') Expandable(s, i, end, w, quote: '\0');
                else w.Add(i, end, SyntaxKind.String);
                return st;
            }
            case Mode.Quoted:
            {
                var close = st.Quote == '"' ? Expandable(s, i, end, w, quote: '"') : Literal(s, i, end, w);
                if (close < 0) return st;
                i = close;
                commandStart = false;
                break;
            }
        }

        while (i < end)
        {
            var c = s[i];
            var next = Scan.At(s, i + 1, end);
            if (Scan.IsSpace(c))
            {
                var e = Scan.Spaces(s, i, end);
                w.Add(i, e, SyntaxKind.Text);
                i = e;
                continue;
            }
            if (c == '<' && next == '#')
            {
                var close = Scan.IndexOf(s, i + 2, end, "#>");
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.Comment);
                    return new State(Mode.BlockComment, '\0');
                }
                w.Add(i, close + 2, SyntaxKind.Comment);
                i = close + 2;
                continue;
            }
            if (c == '#')
            {
                w.Add(i, end, SyntaxKind.Comment);
                break;
            }
            if (c == '`')
            {
                if (i + 1 >= end)
                {
                    w.Add(i, end, SyntaxKind.Escape);
                    return Continued;
                }
                w.Add(i, i + 2, SyntaxKind.Escape);
                i += 2;
                continue;
            }
            if (c == '@' && next is '"' or '\'' && Scan.Spaces(s, i + 2, end) == end)
            {
                w.Add(i, end, SyntaxKind.String);
                return new State(Mode.HereString, next);
            }
            if (c == '"')
            {
                w.Add(i, i + 1, SyntaxKind.String);
                var close = Expandable(s, i + 1, end, w, quote: '"');
                if (close < 0) return new State(Mode.Quoted, '"');
                i = close;
                commandStart = false;
                continue;
            }
            if (c == '\'')
            {
                w.Add(i, i + 1, SyntaxKind.String);
                var close = Literal(s, i + 1, end, w);
                if (close < 0) return new State(Mode.Quoted, '\'');
                i = close;
                commandStart = false;
                continue;
            }
            if (c == '$')
            {
                var e = VariableEnd(s, i, end);
                if (e > i + 1)
                {
                    var name = s.AsSpan(i + 1, e - i - 1);
                    w.Add(i, e, Constants.Contains(name) ? SyntaxKind.Constant : SyntaxKind.Variable);
                    i = e;
                    commandStart = false;
                    continue;
                }
            }
            if (c == '@' && Scan.IsIdentStart(next))
            {
                // @params: splatting.
                var e = Scan.Identifier(s, i + 1, end);
                w.Add(i, e, SyntaxKind.Variable);
                i = e;
                continue;
            }
            // [Parameter(Mandatory)][string[]]: a type literal can follow another one.
            if (c == '[' && TypeEnd(s, i, end) is var close2 and > 0 && (!AfterValue(s, start, i) || i == typeEnd))
            {
                // [string], [System.IO.Path]::Combine, [Parameter(Mandatory)]: names outside the
                // attribute's argument list are types.
                w.Add(i, i + 1, SyntaxKind.Text);
                var k = i + 1;
                var args = 0;
                while (k < close2)
                {
                    if (Scan.IsIdentStart(s[k]))
                    {
                        var e = Scan.Word(s, k, close2, ".");
                        w.Add(k, e, args > 0 ? SyntaxKind.Text : SyntaxKind.Type);
                        k = e;
                        continue;
                    }
                    if (s[k] == '(') args++;
                    else if (s[k] == ')' && args > 0) args--;
                    else if (s[k] is '"' or '\'')
                    {
                        var q = Math.Min(JsonSyntax.QuotedEnd(s, k, close2), close2);
                        w.Add(k, q, SyntaxKind.String);
                        k = q;
                        continue;
                    }
                    w.Add(k, k + 1, SyntaxKind.Text);
                    k++;
                }
                i = typeEnd = close2;
                continue;
            }
            if (c == '-' && (Scan.IsIdentStart(next) || (next == '-' && Scan.IsIdentStart(Scan.At(s, i + 2, end))))
                && (i == start || !Scan.IsIdentPart(s[i - 1])))
            {
                // -Name, -eq, and a native command's --long-flag.
                var e = next == '-' ? Scan.Word(s, i + 2, end, "-") : Scan.Word(s, i + 1, end, "");
                w.Add(i, e, next != '-' && Operators.Contains(s, i + 1, e) ? SyntaxKind.Keyword : SyntaxKind.Attribute);
                i = e;
                commandStart = false;
                continue;
            }
            if (Scan.IsNumberStart(s, i, end) && (i == start || !Scan.IsIdentPart(s[i - 1])))
            {
                var e = Scan.Number(s, i, end);
                w.Add(i, e, SyntaxKind.Number);
                i = e;
                commandStart = false;
                continue;
            }
            if (Scan.IsIdentStart(c))
            {
                var e = Scan.Word(s, i, end, "-");
                var word = s.AsSpan(i, e - i);
                var member = i > start && s[i - 1] == '.' || Scan.At(s, i - 1, end) == ':' && Scan.At(s, i - 2, end) == ':';
                SyntaxKind kind;
                if (member)
                    kind = Scan.At(s, e, end) == '(' ? SyntaxKind.Function : SyntaxKind.Text;
                else if (Keywords.Contains(word))
                    kind = SyntaxKind.Keyword;
                else if (commandStart || functionName || IsCmdlet(word))
                    kind = SyntaxKind.Function;
                else
                    kind = Scan.At(s, e, end) == '(' ? SyntaxKind.Function : SyntaxKind.Text;
                w.Add(i, e, kind);
                i = e;
                commandStart = false;
                functionName = kind == SyntaxKind.Keyword && (word.Equals("function", StringComparison.OrdinalIgnoreCase) || word.Equals("filter", StringComparison.OrdinalIgnoreCase));
                continue;
            }

            w.Add(i, i + 1, SyntaxKind.Text);
            commandStart = c is '|' or ';' or '{' or '(' or '=';
            functionName = false;
            i++;
        }
        return null;
    }

    // An expandable ("...") string body from i: `escapes, $variables and $(subexpressions).
    // Returns the index past the closing quote, or -1 when the string continues on the next line.
    private static int Expandable(string s, int i, int end, SyntaxWriter w, char quote)
    {
        var run = i;
        while (i < end)
        {
            var c = s[i];
            if (c == '`' && i + 1 < end)
            {
                w.Add(run, i, SyntaxKind.String);
                w.Add(i, i + 2, SyntaxKind.Escape);
                i = run = i + 2;
                continue;
            }
            if (quote != '\0' && c == quote)
            {
                if (Scan.At(s, i + 1, end) == quote)
                {
                    w.Add(run, i, SyntaxKind.String);
                    w.Add(i, i + 2, SyntaxKind.Escape);
                    i = run = i + 2;
                    continue;
                }
                w.Add(run, i + 1, SyntaxKind.String);
                return i + 1;
            }
            if (c == '$')
            {
                var e = VariableEnd(s, i, end);
                if (e > i + 1)
                {
                    w.Add(run, i, SyntaxKind.String);
                    w.Add(i, e, SyntaxKind.Variable);
                    i = run = e;
                    continue;
                }
            }
            i++;
        }
        w.Add(run, end, SyntaxKind.String);
        return -1;
    }

    private static int Literal(string s, int i, int end, SyntaxWriter w)
    {
        for (var k = i; k < end; k++)
        {
            if (s[k] != '\'') continue;
            if (Scan.At(s, k + 1, end) == '\'')
            {
                k++;
                continue;
            }
            w.Add(i, k + 1, SyntaxKind.String);
            return k + 1;
        }
        w.Add(i, end, SyntaxKind.String);
        return -1;
    }

    // $name, $env:PATH, $script:count, ${any name}, $_, $$, $?, $^, $( opening a subexpression
    private static int VariableEnd(string s, int i, int end)
    {
        var k = i + 1;
        if (k >= end) return k;
        var c = s[k];
        if (c == '{')
        {
            var close = Scan.IndexOf(s, k + 1, end, '}');
            return close < 0 ? end : close + 1;
        }
        if (c == '(') return k + 1;
        if (c is '_' or '$' or '?' or '^' && !Scan.IsIdentPart(Scan.At(s, k + 1, end))) return k + 1;
        if (!Scan.IsIdentStart(c)) return k;
        k = Scan.Identifier(s, k, end);
        // A scope or drive qualifier: $env:PATH, $script:count, $global:x
        if (Scan.At(s, k, end) == ':' && Scan.IsIdentStart(Scan.At(s, k + 1, end)))
            k = Scan.Identifier(s, k + 1, end);
        return k;
    }

    // The index of the ']' closing a type literal at i ([string], [System.IO.Path], [int[]], [Parameter(Mandatory)]), or -1.
    private static int TypeEnd(string s, int i, int end)
    {
        var k = i + 1;
        if (!Scan.IsIdentStart(Scan.At(s, k, end))) return -1;
        var depth = 1;
        for (; k < end; k++)
        {
            var c = s[k];
            if (c == '[') depth++;
            else if (c == ']' && --depth == 0) return k + 1;
            else if (c is '"' or '\'') k = JsonSyntax.QuotedEnd(s, k, end) - 1;
        }
        return -1;
    }

    // Whether s[i] follows a value ($list[0], $(x)[1], f()[2]): then '[' indexes instead of naming a type.
    private static bool AfterValue(string s, int start, int i) =>
        i > start && (Scan.IsIdentPart(s[i - 1]) || s[i - 1] is ')' or ']');

    private static bool IsCmdlet(ReadOnlySpan<char> word)
    {
        var dash = word.IndexOf('-');
        return dash > 0 && dash < word.Length - 1 && char.IsUpper(word[0]) && char.IsUpper(word[dash + 1]);
    }
}
