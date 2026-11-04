namespace Blaizio;

/// <summary>A keyword list looked up by span, so scanners test words without allocating them.</summary>
internal sealed class WordSet
{
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public WordSet(bool ignoreCase, params string[] words)
    {
        var set = new HashSet<string>(words, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        _lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public bool Contains(ReadOnlySpan<char> word) => _lookup.Contains(word);

    public bool Contains(string s, int start, int end) => end > start && _lookup.Contains(s.AsSpan(start, end - start));
}

/// <summary>Character and literal helpers shared by the scanners. Every index stays below the range end.</summary>
internal static class Scan
{
    public static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

    public static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    public static bool IsSpace(char c) => c is ' ' or '\t' or '\f' or '\v';

    public static char At(string s, int i, int end) => i >= 0 && i < end ? s[i] : '\0';

    /// <summary>The end of the identifier starting at <paramref name="i"/>.</summary>
    public static int Identifier(string s, int i, int end)
    {
        while (i < end && IsIdentPart(s[i])) i++;
        return i;
    }

    /// <summary>The end of a word made of identifier characters and the given extra characters.</summary>
    public static int Word(string s, int i, int end, string extra)
    {
        while (i < end && (IsIdentPart(s[i]) || extra.Contains(s[i]))) i++;
        return i;
    }

    /// <summary>The first index at or after <paramref name="i"/> that is not a space or tab.</summary>
    public static int Spaces(string s, int i, int end)
    {
        while (i < end && IsSpace(s[i])) i++;
        return i;
    }

    /// <summary>Whether only spaces and tabs come before <paramref name="i"/> since <paramref name="start"/>.</summary>
    public static bool OnlySpacesBefore(string s, int start, int i)
    {
        for (var k = start; k < i; k++)
            if (!IsSpace(s[k])) return false;
        return true;
    }

    /// <summary>Whether <paramref name="value"/> appears at <paramref name="i"/> (ordinal).</summary>
    public static bool At(string s, int i, int end, string value) =>
        i >= 0 && end - i >= value.Length && string.CompareOrdinal(s, i, value, 0, value.Length) == 0;

    /// <summary>Whether <paramref name="value"/> appears at <paramref name="i"/>, ignoring case.</summary>
    public static bool AtIgnoreCase(string s, int i, int end, string value) =>
        i >= 0 && end - i >= value.Length && string.Compare(s, i, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>The index of <paramref name="value"/> at or after <paramref name="i"/>, or -1.</summary>
    public static int IndexOf(string s, int i, int end, string value) =>
        i >= end ? -1 : s.IndexOf(value, i, end - i, StringComparison.Ordinal);

    /// <summary>The index of <paramref name="c"/> at or after <paramref name="i"/>, or -1.</summary>
    public static int IndexOf(string s, int i, int end, char c) => i >= end ? -1 : s.IndexOf(c, i, end - i);

    /// <summary>The index of the first of <paramref name="a"/> or <paramref name="b"/> at or after <paramref name="i"/>, or -1.</summary>
    public static int IndexOfAny(string s, int i, int end, char a, char b)
    {
        if (i >= end) return -1;
        var k = s.AsSpan(i, end - i).IndexOfAny(a, b);
        return k < 0 ? -1 : i + k;
    }

    /// <summary>
    /// The end of a C-family number starting at <paramref name="i"/> (a digit, or a dot before a
    /// digit): hex, binary and octal prefixes, digit separators, a fraction, an exponent and letter
    /// suffixes (<c>u</c>, <c>L</c>, <c>f</c>, <c>m</c>, <c>n</c>, ...).
    /// </summary>
    public static int Number(string s, int i, int end)
    {
        if (s[i] == '0' && i + 1 < end && s[i + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            i += 2;
            while (i < end && (char.IsAsciiHexDigit(s[i]) || s[i] == '_')) i++;
        }
        else
        {
            while (i < end && (char.IsAsciiDigit(s[i]) || s[i] == '_')) i++;
            if (i + 1 < end && s[i] == '.' && char.IsAsciiDigit(s[i + 1]))
            {
                i++;
                while (i < end && (char.IsAsciiDigit(s[i]) || s[i] == '_')) i++;
            }
            if (i < end && s[i] is 'e' or 'E')
            {
                var k = i + 1;
                if (k < end && s[k] is '+' or '-') k++;
                if (k < end && char.IsAsciiDigit(s[k]))
                {
                    i = k;
                    while (i < end && char.IsAsciiDigit(s[i])) i++;
                }
            }
        }
        while (i < end && char.IsAsciiLetter(s[i])) i++;
        return i;
    }

    /// <summary>Whether a number starts at <paramref name="i"/>: a digit, or a dot followed by a digit.</summary>
    public static bool IsNumberStart(string s, int i, int end) =>
        char.IsAsciiDigit(s[i]) || (s[i] == '.' && i + 1 < end && char.IsAsciiDigit(s[i + 1]));

    /// <summary>
    /// The end of a quoted string opening at <paramref name="i"/> with <c>s[i]</c> as the quote,
    /// writing backslash escapes as <see cref="SyntaxKind.Escape"/>. Stops at the closing quote or
    /// the range end; <paramref name="closed"/> says which.
    /// </summary>
    public static int QuotedWithEscapes(string s, int i, int end, SyntaxWriter w, out bool closed)
    {
        var quote = s[i];
        var runStart = i;
        i++;
        while (i < end)
        {
            var c = s[i];
            if (c == '\\' && i + 1 < end)
            {
                w.Add(runStart, i, SyntaxKind.String);
                var escEnd = EscapeEnd(s, i, end);
                w.Add(i, escEnd, SyntaxKind.Escape);
                i = runStart = escEnd;
                continue;
            }
            i++;
            if (c == quote)
            {
                w.Add(runStart, i, SyntaxKind.String);
                closed = true;
                return i;
            }
        }
        w.Add(runStart, end, SyntaxKind.String);
        closed = false;
        return end;
    }

    /// <summary>The end of a backslash escape at <paramref name="i"/>: <c>\n</c>, <c>\x41</c>, <c>é</c>, <c>\u{1F600}</c>, <c>\U0001F600</c>.</summary>
    public static int EscapeEnd(string s, int i, int end)
    {
        var k = i + 1;
        if (k >= end) return end;
        switch (s[k])
        {
            case 'x':
                k++;
                for (var n = 0; n < 4 && k < end && char.IsAsciiHexDigit(s[k]); n++) k++;
                return k;
            case 'u' when k + 1 < end && s[k + 1] == '{':
                var close = IndexOf(s, k + 2, end, '}');
                return close < 0 ? k + 1 : close + 1;
            case 'u':
                k++;
                for (var n = 0; n < 4 && k < end && char.IsAsciiHexDigit(s[k]); n++) k++;
                return k;
            case 'U':
                k++;
                for (var n = 0; n < 8 && k < end && char.IsAsciiHexDigit(s[k]); n++) k++;
                return k;
            default:
                return k + (char.IsHighSurrogate(s[k]) && k + 1 < end ? 2 : 1);
        }
    }

    /// <summary>Whether <paramref name="word"/> is all upper-case letters, digits and underscores, with at least two letters.</summary>
    public static bool IsConstantCase(ReadOnlySpan<char> word)
    {
        var letters = 0;
        foreach (var c in word)
        {
            if (char.IsLower(c)) return false;
            if (char.IsLetter(c)) letters++;
            else if (c != '_' && !char.IsDigit(c)) return false;
        }
        return letters >= 2;
    }
}
