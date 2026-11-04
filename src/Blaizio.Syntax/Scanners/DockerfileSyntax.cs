namespace Blaizio;

/// <summary>
/// Dockerfiles (and Containerfiles): instructions as keywords; <c>RUN</c> / <c>CMD</c> /
/// <c>ENTRYPOINT</c> / <c>SHELL</c> tokenized as bash (or as a JSON array in exec form);
/// <c>ENV</c> and <c>ARG</c> names as variables; flags such as <c>--from=build</c>; build stage names
/// after <c>AS</c>; <c>$VARIABLES</c>; and a trailing backslash continuing an instruction.
/// </summary>
internal sealed class DockerfileSyntax : SyntaxLanguage
{
    public static readonly DockerfileSyntax Instance = new();

    /// <summary>An instruction continuing on the next line; <paramref name="Inner"/> is its shell state for RUN.</summary>
    internal sealed record State(string Instruction, object? Inner);

    private static readonly WordSet Instructions = new(true,
        "FROM", "RUN", "CMD", "LABEL", "MAINTAINER", "EXPOSE", "ENV", "ADD", "COPY", "ENTRYPOINT",
        "VOLUME", "USER", "WORKDIR", "ARG", "ONBUILD", "STOPSIGNAL", "HEALTHCHECK", "SHELL");

    private static readonly WordSet ShellInstructions = new(true, "RUN", "CMD", "ENTRYPOINT", "SHELL");

    public override string Name => "dockerfile";

    public override IReadOnlyList<string> Aliases => ["docker", "containerfile"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var first = Scan.Spaces(s, start, end);
        w.Add(start, first, SyntaxKind.Text);
        if (state is State open)
            return Arguments(open.Instruction, s, first, end, open.Inner, w);
        if (first >= end) return null;
        if (s[first] == '#')
        {
            w.Add(first, end, SyntaxKind.Comment);
            return null;
        }

        var wordEnd = Scan.Identifier(s, first, end);
        if (!Instructions.Contains(s, first, wordEnd))
        {
            Words(s, first, end, w);
            return null;
        }
        w.Add(first, wordEnd, SyntaxKind.Keyword);
        var instruction = s[first..wordEnd].ToUpperInvariant();
        if (instruction == "ONBUILD")
        {
            // ONBUILD RUN ...: the trigger is an instruction of its own.
            return Tokenize(s, wordEnd, end, null, w);
        }
        if (instruction == "HEALTHCHECK")
        {
            // HEALTHCHECK --interval=30s CMD curl -f http://localhost/ || exit 1, or NONE
            var k = Flags(s, wordEnd, end, w);
            var e = Scan.Identifier(s, k, end);
            if (e - k == 3 && Scan.AtIgnoreCase(s, k, end, "CMD"))
            {
                w.Add(k, e, SyntaxKind.Keyword);
                return Arguments("CMD", s, e, end, null, w);
            }
            if (e - k == 4 && Scan.AtIgnoreCase(s, k, end, "NONE")) w.Add(k, e, SyntaxKind.Keyword);
            Words(s, e, end, w);
            return null;
        }
        return Arguments(instruction, s, wordEnd, end, null, w);
    }

    private static State? Arguments(string instruction, string s, int i, int end, object? inner, SyntaxWriter w)
    {
        var continued = ContinuesOnNextLine(s, i, end);
        if (ShellInstructions.Contains(instruction))
        {
            var k = Scan.Spaces(s, i, end);
            if (inner is null && Scan.At(s, k, end) == '[')
            {
                // Exec form: ["dotnet", "Shop.Api.dll"]
                w.Add(i, k, SyntaxKind.Text);
                w.Embed(w.Find("json") ?? JsonSyntax.Instance, k, end, null);
                return continued ? new State(instruction, null) : null;
            }
            k = Flags(s, i, end, w);
            var next = w.Embed(w.Find("bash") ?? ShellSyntax.Instance, k, end, inner);
            return next is null && !continued ? null : new State(instruction, next);
        }

        switch (instruction)
        {
            case "FROM":
            {
                var k = Flags(s, i, end, w);
                var image = WordEndAt(s, k, end);
                Variables(s, k, image, w, SyntaxKind.String);
                k = image;
                var asStart = Scan.Spaces(s, k, end);
                w.Add(k, asStart, SyntaxKind.Text);
                var asEnd = Scan.Identifier(s, asStart, end);
                if (asEnd - asStart == 2 && Scan.AtIgnoreCase(s, asStart, end, "as"))
                {
                    w.Add(asStart, asEnd, SyntaxKind.Keyword);
                    var name = Scan.Spaces(s, asEnd, end);
                    w.Add(asEnd, name, SyntaxKind.Text);
                    var nameEnd = Scan.Word(s, name, end, "-.");
                    w.Add(name, nameEnd, SyntaxKind.Type);
                    k = nameEnd;
                }
                else k = asStart;
                Words(s, k, end, w);
                break;
            }
            case "ENV" or "ARG" or "LABEL":
                Pairs(s, i, end, w, instruction == "LABEL" ? SyntaxKind.Property : SyntaxKind.Variable);
                break;
            case "EXPOSE":
                for (var k = i; k < end;)
                {
                    if (char.IsAsciiDigit(s[k]))
                    {
                        var e = k;
                        while (e < end && char.IsAsciiDigit(s[e])) e++;
                        w.Add(k, e, SyntaxKind.Number);
                        k = e;
                    }
                    else
                    {
                        w.Add(k, k + 1, SyntaxKind.Text);
                        k++;
                    }
                }
                break;
            default:
                Words(s, Flags(s, i, end, w), end, w);
                break;
        }
        return continued ? new State(instruction, null) : null;
    }

    // Leading --flag=value options; returns where the rest starts.
    private static int Flags(string s, int i, int end, SyntaxWriter w)
    {
        while (true)
        {
            var k = Scan.Spaces(s, i, end);
            w.Add(i, k, SyntaxKind.Text);
            if (!Scan.At(s, k, end, "--")) return k;
            var e = Scan.Word(s, k + 2, end, "-");
            w.Add(k, e, SyntaxKind.Attribute);
            if (Scan.At(s, e, end) == '=')
            {
                w.Add(e, e + 1, SyntaxKind.Text);
                var v = WordEndAt(s, e + 1, end);
                Variables(s, e + 1, v, w, SyntaxKind.String);
                e = v;
            }
            i = e;
        }
    }

    // KEY=value pairs (or the legacy `ENV KEY value`).
    private static void Pairs(string s, int i, int end, SyntaxWriter w, SyntaxKind keyKind)
    {
        var k = Scan.Spaces(s, i, end);
        w.Add(i, k, SyntaxKind.Text);
        while (k < end)
        {
            if (s[k] == '\\' && Scan.Spaces(s, k + 1, end) == end)
            {
                w.Add(k, end, SyntaxKind.Escape);
                return;
            }
            var keyEnd = Scan.Word(s, k, end, "-.");
            if (keyEnd == k)
            {
                Words(s, k, end, w);
                return;
            }
            w.Add(k, keyEnd, keyKind);
            k = keyEnd;
            if (Scan.At(s, k, end) == '=')
            {
                w.Add(k, k + 1, SyntaxKind.Text);
                var v = s.Length > k + 1 && Scan.At(s, k + 1, end) is '"' or '\''
                    ? JsonSyntax.QuotedEnd(s, k + 1, end)
                    : WordEndAt(s, k + 1, end);
                Variables(s, k + 1, v, w, SyntaxKind.String);
                k = v;
            }
            else
            {
                // ENV NAME the rest is the value
                Variables(s, k, end, w, SyntaxKind.String);
                return;
            }
            var next = Scan.Spaces(s, k, end);
            w.Add(k, next, SyntaxKind.Text);
            k = next;
        }
    }

    // Plain arguments with $VARIABLES picked out.
    private static void Words(string s, int i, int end, SyntaxWriter w)
    {
        if (i < end && s[end - 1] == '\\' && i < end - 1)
        {
            Variables(s, i, end - 1, w, SyntaxKind.Text);
            w.Add(end - 1, end, SyntaxKind.Escape);
            return;
        }
        Variables(s, i, end, w, SyntaxKind.Text);
    }

    private static void Variables(string s, int i, int end, SyntaxWriter w, SyntaxKind kind)
    {
        while (i < end)
        {
            var dollar = Scan.IndexOf(s, i, end, '$');
            if (dollar < 0)
            {
                w.Add(i, end, kind);
                return;
            }
            w.Add(i, dollar, kind);
            var e = dollar + 1;
            if (Scan.At(s, e, end) == '{')
            {
                var close = Scan.IndexOf(s, e, end, '}');
                e = close < 0 ? end : close + 1;
            }
            else e = Scan.Identifier(s, e, end);
            w.Add(dollar, e, e > dollar + 1 ? SyntaxKind.Variable : kind);
            i = Math.Max(e, dollar + 1);
        }
    }

    private static int WordEndAt(string s, int i, int end)
    {
        while (i < end && !Scan.IsSpace(s[i])) i++;
        return i;
    }

    private static bool ContinuesOnNextLine(string s, int i, int end)
    {
        for (var k = end - 1; k >= i; k--)
        {
            if (Scan.IsSpace(s[k])) continue;
            return s[k] == '\\';
        }
        return false;
    }
}
