namespace Blaizio;

/// <summary>
/// CSS, with nesting and the Tailwind at-rules (<c>@apply</c>, <c>@theme</c>, <c>@utility</c>,
/// <c>@custom-variant</c>, ...). Inside a block, a statement that opens a block before it ends is a
/// nested rule; one with a colon is a declaration. Selectors color element names as tags and
/// <c>.class</c> / <c>#id</c> like attributes; declarations color the property, numbers with their
/// unit, colors, strings, functions and <c>--custom-properties</c>.
/// </summary>
internal sealed class CssSyntax : SyntaxLanguage
{
    public static readonly CssSyntax Instance = new();

    /// <summary>
    /// <paramref name="Depth"/> is the block nesting; <paramref name="InValue"/> means a declaration
    /// value (or an at-rule's prelude) continues from the previous line.
    /// </summary>
    internal sealed record State(int Depth, bool InComment, bool InValue);

    public override string Name => "css";

    public override IReadOnlyList<string> Aliases => ["tailwindcss", "tailwind", "postcss", "pcss"];

    private static readonly WordSet MediaKeywords = new(true, "and", "or", "not", "only");

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var st = state as State;
        var depth = st?.Depth ?? 0;
        var inValue = st?.InValue ?? false;
        var i = start;

        if (st?.InComment == true)
        {
            var close = Scan.IndexOf(s, i, end, "*/");
            if (close < 0)
            {
                w.Add(i, end, SyntaxKind.Comment);
                return st;
            }
            w.Add(i, close + 2, SyntaxKind.Comment);
            i = close + 2;
        }

        // What the scanner is in: a selector (or at-rule prelude), a property name, or a value.
        var context = inValue ? Context.Value : StatementContext(s, i, end, depth);
        while (i < end)
        {
            var c = s[i];
            if (c == '/' && Scan.At(s, i + 1, end) == '*')
            {
                var close = Scan.IndexOf(s, i + 2, end, "*/");
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.Comment);
                    return new State(depth, true, context == Context.Value);
                }
                w.Add(i, close + 2, SyntaxKind.Comment);
                i = close + 2;
                continue;
            }
            if (Scan.IsSpace(c))
            {
                var e = Scan.Spaces(s, i, end);
                w.Add(i, e, SyntaxKind.Text);
                i = e;
                continue;
            }
            if (c is '"' or '\'')
            {
                i = Scan.QuotedWithEscapes(s, i, end, w, out _);
                continue;
            }
            if (c == '{')
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                depth++;
                i++;
                context = StatementContext(s, i, end, depth);
                continue;
            }
            if (c == '}')
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                if (depth > 0) depth--;
                i++;
                context = StatementContext(s, i, end, depth);
                continue;
            }
            if (c == ';')
            {
                w.Add(i, i + 1, SyntaxKind.Text);
                i++;
                context = StatementContext(s, i, end, depth);
                continue;
            }
            if (c == '@' && Scan.IsIdentStart(Scan.At(s, i + 1, end)))
            {
                var e = Scan.Word(s, i + 1, end, "-");
                w.Add(i, e, SyntaxKind.Keyword);
                // `@apply a b;` and `@import url(x);` read like a declaration value up to the ';'.
                context = Context.Prelude;
                i = e;
                continue;
            }

            switch (context)
            {
                case Context.Property:
                    i = Property(s, i, end, w, ref context);
                    break;
                case Context.Value:
                    i = Value(s, i, end, w);
                    break;
                case Context.Prelude:
                    i = Prelude(s, i, end, w);
                    break;
                default:
                    i = Selector(s, i, end, w);
                    break;
            }
        }

        var open = context is Context.Value or Context.Prelude && depth > 0 && !EndsStatement(s, start, end);
        return depth == 0 && !open ? null : new State(depth, false, open);
    }

    private enum Context : byte
    {
        Selector,
        Property,
        Value,
        Prelude,
    }

    // Inside a block: a statement that opens a block before it ends is a nested rule, one with a
    // colon is a declaration. At the top level everything starts as a selector.
    private static Context StatementContext(string s, int i, int end, int depth)
    {
        if (depth == 0) return Context.Selector;
        var colon = false;
        for (var k = i; k < end; k++)
        {
            switch (s[k])
            {
                case '{':
                    return Context.Selector;
                case ';' or '}':
                    return colon ? Context.Property : Context.Selector;
                case ':':
                    colon = true;
                    break;
                case '"' or '\'':
                    k = JsonSyntax.QuotedEnd(s, k, end) - 1;
                    break;
            }
        }
        return colon ? Context.Property : Context.Selector;
    }

    private static bool EndsStatement(string s, int start, int end)
    {
        for (var k = end - 1; k >= start; k--)
        {
            if (Scan.IsSpace(s[k])) continue;
            return s[k] is ';' or '{' or '}';
        }
        return false;
    }

    private static int Property(string s, int i, int end, SyntaxWriter w, ref Context context)
    {
        if (s[i] == ':')
        {
            w.Add(i, i + 1, SyntaxKind.Text);
            context = Context.Value;
            return i + 1;
        }
        if (s[i] == '-' && Scan.At(s, i + 1, end) == '-')
        {
            var e = Scan.Word(s, i + 2, end, "-");
            w.Add(i, e, SyntaxKind.Variable);
            return e;
        }
        if (Scan.IsIdentStart(s[i]) || s[i] == '-')
        {
            var e = Scan.Word(s, i + 1, end, "-");
            w.Add(i, e, SyntaxKind.Property);
            return e;
        }
        w.Add(i, i + 1, SyntaxKind.Text);
        return i + 1;
    }

    private static int Value(string s, int i, int end, SyntaxWriter w)
    {
        var c = s[i];
        if (c == '-' && Scan.At(s, i + 1, end) == '-')
        {
            var e = Scan.Word(s, i + 2, end, "-");
            w.Add(i, e, SyntaxKind.Variable);
            return e;
        }
        if (c == '#' && i + 1 < end && char.IsAsciiHexDigit(s[i + 1]))
        {
            var e = i + 1;
            while (e < end && char.IsAsciiHexDigit(s[e])) e++;
            w.Add(i, e, SyntaxKind.Number);
            return e;
        }
        if (Scan.IsNumberStart(s, i, end) || ((c is '-' or '+') && i + 1 < end && Scan.IsNumberStart(s, i + 1, end) && !Scan.IsIdentPart(Scan.At(s, i - 1, end))))
        {
            var e = i + (char.IsAsciiDigit(c) || c == '.' ? 0 : 1);
            e = Dimension(s, e, end);
            w.Add(i, e, SyntaxKind.Number);
            return e;
        }
        if (c == '!' && Scan.IsIdentStart(Scan.At(s, Scan.Spaces(s, i + 1, end), end)))
        {
            var k = Scan.Spaces(s, i + 1, end);
            var e = Scan.Identifier(s, k, end);
            w.Add(i, e, SyntaxKind.Keyword);
            return e;
        }
        if (Scan.IsIdentStart(c) || c == '-')
        {
            var e = Scan.Word(s, i + 1, end, "-");
            if (Scan.At(s, e, end) == '(')
            {
                w.Add(i, e, SyntaxKind.Function);
                // url(...) holds an unquoted address.
                if (e - i == 3 && Scan.AtIgnoreCase(s, i, end, "url") && Scan.At(s, Scan.Spaces(s, e + 1, end), end) is not ('"' or '\''))
                {
                    var close = Scan.IndexOf(s, e + 1, end, ')');
                    if (close > 0)
                    {
                        w.Add(e, e + 1, SyntaxKind.Text);
                        w.Add(e + 1, close, SyntaxKind.String);
                        return close;
                    }
                }
                return e;
            }
            w.Add(i, e, SyntaxKind.Text);
            return e;
        }
        w.Add(i, i + 1, SyntaxKind.Text);
        return i + 1;
    }

    // An at-rule's prelude: media features, keywords, strings, numbers, plain names.
    private static int Prelude(string s, int i, int end, SyntaxWriter w)
    {
        var c = s[i];
        if (Scan.IsIdentStart(c) || c == '-')
        {
            var e = Scan.Word(s, i + 1, end, "-");
            if (Scan.At(s, Scan.Spaces(s, e, end), end) == ':' && Scan.At(s, i - 1, end) == '(')
                w.Add(i, e, SyntaxKind.Property);
            else if (MediaKeywords.Contains(s, i, e))
                w.Add(i, e, SyntaxKind.Keyword);
            else if (Scan.At(s, e, end) == '(')
                w.Add(i, e, SyntaxKind.Function);
            else
                w.Add(i, e, SyntaxKind.Text);
            return e;
        }
        if (char.IsAsciiDigit(c))
        {
            var e = Dimension(s, i, end);
            w.Add(i, e, SyntaxKind.Number);
            return e;
        }
        w.Add(i, i + 1, SyntaxKind.Text);
        return i + 1;
    }

    private static int Selector(string s, int i, int end, SyntaxWriter w)
    {
        var c = s[i];
        switch (c)
        {
            case '.' or '#' when Scan.IsIdentStart(Scan.At(s, i + 1, end)) || Scan.At(s, i + 1, end) is '-' or '\\':
            {
                var e = SelectorName(s, i + 1, end);
                w.Add(i, e, SyntaxKind.Attribute);
                return e;
            }
            case ':':
            {
                var e = i + 1;
                if (Scan.At(s, e, end) == ':') e++;
                e = Scan.Word(s, e, end, "-");
                w.Add(i, e, SyntaxKind.Keyword);
                return e;
            }
            case '&':
                w.Add(i, i + 1, SyntaxKind.Keyword);
                return i + 1;
            case '[':
            {
                // [data-state='open']: the attribute name, then its value.
                w.Add(i, i + 1, SyntaxKind.Text);
                var k = Scan.Spaces(s, i + 1, end);
                var e = Scan.Word(s, k, end, "-:");
                w.Add(k, e, SyntaxKind.Attribute);
                return e;
            }
        }
        if (Scan.IsIdentStart(c) || (c == '-' && Scan.IsIdentStart(Scan.At(s, i + 1, end))))
        {
            var e = Scan.Word(s, i + 1, end, "-");
            w.Add(i, e, Scan.At(s, e, end) == '(' ? SyntaxKind.Function : SyntaxKind.Tag);
            return e;
        }
        if (char.IsAsciiDigit(c))
        {
            // Keyframe stops: 0%, 50%, 100%.
            var e = Dimension(s, i, end);
            w.Add(i, e, SyntaxKind.Number);
            return e;
        }
        w.Add(i, i + 1, SyntaxKind.Text);
        return i + 1;
    }

    // A class or id name, including Tailwind's escaped characters (.hover\:bg-x, .w-1\/2).
    private static int SelectorName(string s, int i, int end)
    {
        while (i < end)
        {
            if (s[i] == '\\' && i + 1 < end) i += 2;
            else if (Scan.IsIdentPart(s[i]) || s[i] == '-') i++;
            else break;
        }
        return i;
    }

    // A number with its unit or percent sign: 1.5rem, 50%, 200ms.
    private static int Dimension(string s, int i, int end)
    {
        while (i < end && (char.IsAsciiDigit(s[i]) || s[i] == '.')) i++;
        if (i < end && s[i] is 'e' or 'E' && Scan.At(s, i + 1, end) is '-' or '+' && char.IsAsciiDigit(Scan.At(s, i + 2, end))) i += 2;
        while (i < end && char.IsAsciiDigit(s[i])) i++;
        if (i < end && s[i] == '%') return i + 1;
        while (i < end && char.IsAsciiLetter(s[i])) i++;
        return i;
    }
}
