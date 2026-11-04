namespace Blaizio;

/// <summary>
/// Bash and POSIX shells: the command word of each command (after a line start, a pipe,
/// <c>&amp;&amp;</c>, <c>;</c>, <c>then</c>, <c>do</c>, ...) reads as a function, flags as attributes,
/// <c>$VARS</c> and <c>NAME=</c> assignments as variables, quotes as strings (with variables and
/// escapes inside double quotes), <c>#</c> comments at a word start, heredoc bodies as strings, and
/// a trailing backslash continues the command on the next line.
/// </summary>
internal sealed class ShellSyntax : SyntaxLanguage
{
    public static readonly ShellSyntax Instance = new();

    internal enum Mode : byte
    {
        Continuation,
        Quoted,
        Heredoc,
    }

    /// <summary>A continued command, an open quote, or a heredoc waiting for <paramref name="Terminator"/>.</summary>
    internal sealed record State(Mode Mode, char Quote, string? Terminator, bool StripTabs);

    // A command continued after a backslash: in its arguments, or where the next command starts
    // (`a && \`). The StripTabs slot carries which.
    private static readonly object Continued = new State(Mode.Continuation, '\0', null, false);

    private static readonly object ContinuedCommand = new State(Mode.Continuation, '\0', null, true);

    private static readonly WordSet Keywords = new(false,
        "if", "then", "else", "elif", "fi", "case", "esac", "for", "while", "until", "do", "done",
        "in", "function", "select", "time", "coproc", "[[", "]]", "!");

    // After these a new command starts.
    private static readonly WordSet CommandIntroducers = new(false,
        "then", "else", "elif", "do", "if", "while", "until", "time", "!", "sudo", "exec", "command", "nohup", "env", "xargs");

    public override string Name => "bash";

    public override IReadOnlyList<string> Aliases => ["sh", "shell", "zsh", "shellscript", "ksh", "fish"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var i = start;
        var st = state as State;
        var commandStart = st?.Mode != Mode.Continuation || st.StripTabs;
        string? heredoc = null;
        var stripTabs = false;

        switch (st?.Mode)
        {
            case Mode.Heredoc:
            {
                var text = s.AsSpan(start, end - start);
                if (st.StripTabs) text = text.TrimStart('\t');
                if (text.SequenceEqual(st.Terminator))
                {
                    w.Add(start, end, SyntaxKind.Meta);
                    return null;
                }
                w.Add(start, end, SyntaxKind.String);
                return st;
            }
            case Mode.Quoted:
            {
                var close = QuoteBody(s, i, end, st.Quote, w);
                if (close < 0) return st;
                i = close;
                commandStart = false;
                break;
            }
        }

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
            if (c == '#' && (i == start || Scan.IsSpace(s[i - 1]) || s[i - 1] is ';' or '(' or '|' or '&'))
            {
                w.Add(i, end, i == start && Scan.At(s, i + 1, end) == '!' ? SyntaxKind.Meta : SyntaxKind.Comment);
                break;
            }
            if (c == '\\' && i + 1 >= end)
            {
                // A trailing backslash continues the command on the next line.
                w.Add(i, end, SyntaxKind.Escape);
                if (heredoc is not null) return new State(Mode.Heredoc, '\0', heredoc, stripTabs);
                return commandStart ? ContinuedCommand : Continued;
            }
            if (c == '\\')
            {
                w.Add(i, i + 2, SyntaxKind.Escape);
                i += 2;
                continue;
            }
            if (c is '\'' or '"')
            {
                w.Add(i, i + 1, SyntaxKind.String);
                var close = QuoteBody(s, i + 1, end, c, w);
                if (close < 0) return new State(Mode.Quoted, c, null, false);
                i = close;
                commandStart = false;
                continue;
            }
            if (c == '$')
            {
                if (Scan.At(s, i + 1, end) == '\'')
                {
                    // $'...': ANSI-C quoting, escapes and all.
                    w.Add(i, i + 1, SyntaxKind.String);
                    i = Scan.QuotedWithEscapes(s, i + 1, end, w, out _);
                    commandStart = false;
                    continue;
                }
                var v = VariableEnd(s, i, end);
                if (v > i + 1)
                {
                    w.Add(i, v, SyntaxKind.Variable);
                    i = v;
                    commandStart = Scan.At(s, i - 1, end) == '(';
                    continue;
                }
            }
            if (c == '`')
            {
                w.Add(i, i + 1, SyntaxKind.Escape);
                i++;
                commandStart = true;
                continue;
            }
            if (c is '>' or '<' && Scan.At(s, i + 1, end) == '&')
            {
                // 2>&1, >&2: a redirection, not a background job.
                w.Add(i, i + 2, SyntaxKind.Text);
                i += 2;
                continue;
            }
            if (c is '|' or ';' or '&' or '(' or '{' or ')' or '}')
            {
                var e = i + 1;
                while (e < end && s[e] == c && c is '|' or '&' or ';') e++;
                w.Add(i, e, SyntaxKind.Text);
                i = e;
                commandStart = c is not (')' or '}');
                continue;
            }
            if (c == '<' && Scan.At(s, i + 1, end) == '<' && Scan.At(s, i + 2, end) != '<')
            {
                var e = HeredocStart(s, i, end, out heredoc, out stripTabs);
                if (heredoc is not null)
                {
                    w.Add(i, e, SyntaxKind.Meta);
                    i = e;
                    continue;
                }
            }
            if (c == '-' && commandStart is false && i + 1 < end && (s[i + 1] == '-' || char.IsLetterOrDigit(s[i + 1])) && (i == start || Scan.IsSpace(s[i - 1])))
            {
                // -x, --flag, --flag=value
                var e = i + 1;
                while (e < end && (Scan.IsIdentPart(s[e]) || s[e] is '-' or '.')) e++;
                w.Add(i, e, SyntaxKind.Attribute);
                i = e;
                continue;
            }

            var wordEnd = WordEnd(s, i, end);
            if (wordEnd == i)
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                i++;
                continue;
            }
            var word = s.AsSpan(i, wordEnd - i);
            if (commandStart)
            {
                var eq = word.IndexOf('=');
                if (eq > 0 && IsName(word[..eq]))
                {
                    // NAME=value before (or instead of) a command; a command can still follow.
                    w.Add(i, i + eq, SyntaxKind.Variable);
                    w.Add(i + eq, i + eq + 1, SyntaxKind.Text);
                    w.Add(i + eq + 1, wordEnd, SyntaxKind.String);
                    i = wordEnd;
                    if (i < end && s[i] is '"' or '\'')
                    {
                        w.Add(i, i + 1, SyntaxKind.String);
                        var close = QuoteBody(s, i + 1, end, s[i], w);
                        if (close < 0) return new State(Mode.Quoted, s[i], null, false);
                        i = close;
                    }
                    continue;
                }
                if (Keywords.Contains(word))
                {
                    w.Add(i, wordEnd, SyntaxKind.Keyword);
                    commandStart = CommandIntroducers.Contains(word) || word is "{";
                }
                else
                {
                    w.Add(i, wordEnd, SyntaxKind.Function);
                    commandStart = CommandIntroducers.Contains(word);
                }
                i = wordEnd;
                if (word is "export" or "local" or "readonly" or "declare" or "unset") commandStart = true;
                continue;
            }
            if (word is "in" or "do" or "then" or "esac" or "done" or "fi" or "]]")
            {
                w.Add(i, wordEnd, SyntaxKind.Keyword);
                commandStart = word is "do" or "then";
            }
            else if (IsNumber(word))
                w.Add(i, wordEnd, SyntaxKind.Number);
            else
                w.Add(i, wordEnd, SyntaxKind.Text);
            i = wordEnd;
        }

        return heredoc is null ? null : new State(Mode.Heredoc, '\0', heredoc, stripTabs);
    }

    // The body of a quoted string from i (the opening quote already written), up to and including
    // its closing quote. Returns the index past it, or -1 when the string continues on the next line.
    private static int QuoteBody(string s, int i, int end, char quote, SyntaxWriter w)
    {
        var run = i;
        while (i < end)
        {
            var c = s[i];
            if (c == quote)
            {
                w.Add(run, i + 1, SyntaxKind.String);
                return i + 1;
            }
            if (quote == '"' && c == '\\' && i + 1 < end)
            {
                w.Add(run, i, SyntaxKind.String);
                w.Add(i, i + 2, SyntaxKind.Escape);
                i = run = i + 2;
                continue;
            }
            if (quote == '"' && c == '$')
            {
                var v = VariableEnd(s, i, end);
                if (v > i + 1)
                {
                    w.Add(run, i, SyntaxKind.String);
                    w.Add(i, v, SyntaxKind.Variable);
                    i = run = v;
                    continue;
                }
            }
            i++;
        }
        w.Add(run, end, SyntaxKind.String);
        return -1;
    }

    // $NAME, ${...}, $(...) opening, $1, $@, $?, $$, $#, $!, $-, $*
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
        if (c == '(') return Scan.At(s, k + 1, end) == '(' ? k + 2 : k + 1;
        if (Scan.IsIdentStart(c)) return Scan.Identifier(s, k, end);
        if (char.IsAsciiDigit(c) || c is '@' or '?' or '$' or '#' or '!' or '-' or '*') return k + 1;
        return k;
    }

    // <<EOF, <<-EOF, <<'EOF', <<"EOF"
    private static int HeredocStart(string s, int i, int end, out string? terminator, out bool stripTabs)
    {
        var k = i + 2;
        stripTabs = Scan.At(s, k, end) == '-';
        if (stripTabs) k++;
        k = Scan.Spaces(s, k, end);
        var quote = Scan.At(s, k, end) is '\'' or '"' ? s[k] : '\0';
        if (quote != '\0') k++;
        var nameEnd = Scan.Word(s, k, end, "-.");
        if (nameEnd == k)
        {
            terminator = null;
            return i;
        }
        terminator = s[k..nameEnd];
        if (quote != '\0' && Scan.At(s, nameEnd, end) == quote) nameEnd++;
        return nameEnd;
    }

    private static int WordEnd(string s, int i, int end)
    {
        var k = i;
        while (k < end && !Scan.IsSpace(s[k]) && s[k] is not ('|' or ';' or '&' or '(' or ')' or '<' or '>' or '"' or '\'' or '`' or '$'))
            k++;
        // [[ and ]] are words; a lone { or } is punctuation.
        return k;
    }

    private static bool IsName(ReadOnlySpan<char> word)
    {
        if (word.IsEmpty || !Scan.IsIdentStart(word[0])) return false;
        foreach (var c in word)
            if (!Scan.IsIdentPart(c)) return false;
        return true;
    }

    private static bool IsNumber(ReadOnlySpan<char> word)
    {
        foreach (var c in word)
            if (!char.IsAsciiDigit(c)) return false;
        return !word.IsEmpty;
    }
}
