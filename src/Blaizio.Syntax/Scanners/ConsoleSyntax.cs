namespace Blaizio;

/// <summary>
/// A console session: prompt lines (<c>$ </c>, <c>% </c>, <c>❯ </c>, <c>user@host:~$ </c>,
/// <c>PS&gt; </c>, <c>PS C:\src&gt; </c>, <c>C:\src&gt; </c>) show the prompt as metadata and the command
/// highlighted as bash, or as PowerShell after a Windows prompt; every other line is the command's
/// output, plain. A command left open (a quote, a trailing backslash or backtick) continues on the
/// next line, with or without a <c>&gt; </c> continuation prompt. <c>#</c> lines are comments.
/// </summary>
internal sealed class ConsoleSyntax : SyntaxLanguage
{
    public static readonly ConsoleSyntax Instance = new();

    /// <summary>A command that continues on the next line, in <paramref name="Shell"/>.</summary>
    internal sealed record State(SyntaxLanguage Shell, object Inner);

    public override string Name => "console";

    public override IReadOnlyList<string> Aliases => ["shell-session", "shellsession", "sh-session", "terminal-session"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        if (state is State open)
        {
            var from = start;
            if (Scan.At(s, start, end, "> ")) from = start + 2;
            else if (Scan.At(s, start, end, ">> ")) from = start + 3;
            w.Add(start, from, SyntaxKind.Meta);
            return Command(open.Shell, from, end, open.Inner, w);
        }

        var promptEnd = PromptEnd(s, start, end, out var powerShell);
        if (promptEnd > 0)
        {
            w.Add(start, promptEnd, SyntaxKind.Meta);
            // By name, so a command gets the shell language the app replaced bash or powershell with.
            var shell = powerShell
                ? w.Find("powershell") ?? PowerShellSyntax.Instance
                : w.Find("bash") ?? ShellSyntax.Instance;
            return Command(shell, promptEnd, end, null, w);
        }

        var first = Scan.Spaces(s, start, end);
        w.Add(start, end, Scan.At(s, first, end) == '#' ? SyntaxKind.Comment : SyntaxKind.Text);
        return null;
    }

    private static State? Command(SyntaxLanguage shell, int from, int end, object? inner, SyntaxWriter w)
    {
        var next = w.Embed(shell, from, end, inner);
        return next is null ? null : new State(shell, next);
    }

    // The end of the prompt at the start of the line (including the space after it), or -1.
    private static int PromptEnd(string s, int start, int end, out bool powerShell)
    {
        powerShell = false;
        var c = Scan.At(s, start, end);

        // $ cmd, % cmd, ❯ cmd, ➜ cmd
        if (c is '$' or '%' or '❯' or '➜' or '›')
        {
            var k = start + 1;
            if (k == end) return k;
            if (Scan.IsSpace(s[k])) return Scan.Spaces(s, k, end);
            return -1;
        }

        // PS> cmd, PS C:\src> cmd
        if (Scan.At(s, start, end, "PS") && (Scan.At(s, start + 2, end) == '>' || Scan.At(s, start + 2, end) == ' '))
        {
            var gt = Scan.IndexOf(s, start + 2, end, '>');
            if (gt > 0 && (gt + 1 == end || Scan.IsSpace(s[gt + 1])))
            {
                powerShell = true;
                return Scan.Spaces(s, gt + 1, end);
            }
        }

        // C:\src> cmd
        if (char.IsAsciiLetter(c) && Scan.At(s, start + 1, end) == ':' && Scan.At(s, start + 2, end) == '\\')
        {
            var gt = Scan.IndexOf(s, start + 3, end, '>');
            if (gt > 0 && (gt + 1 == end || Scan.IsSpace(s[gt + 1])) && Scan.IndexOf(s, start, gt, ' ') < 0)
            {
                powerShell = true;
                return Scan.Spaces(s, gt + 1, end);
            }
        }

        // user@host:~/src$ cmd, [user@host src]$ cmd, user@host:~# cmd
        var at = Scan.IndexOf(s, start, Math.Min(end, start + 64), '@');
        if (at > start)
        {
            for (var k = at + 1; k < end && k < start + 128; k++)
            {
                if (s[k] is '$' or '#' && (k + 1 == end || Scan.IsSpace(s[k + 1])))
                    return Scan.Spaces(s, k + 1, end);
                if (Scan.IsSpace(s[k]) && s[start] != '[') break;
            }
        }
        return -1;
    }
}
