using System.Text;
using System.Text.RegularExpressions;
using Blaizio.Docs.Shared;

namespace Blaizio.Docs.Services;

/// <summary>
/// The code an RTL demo shows. <c>RtlDemo</c> is this site's Arabic / Hebrew switch, not a Blaizio
/// component, so the code panel leaves it out: the wrapper's tags go (what they held moves out one
/// level), and every <c>t["arabic", "hebrew"]</c> becomes the text of the language on show. A reader
/// sees, and copies, the demo as they would write it in that language; the preview still renders
/// through RtlDemo.
/// </summary>
/// <remarks>
/// Inputs are the docs' own RTL examples - trusted, short, and written to one pattern: the wrapper's
/// tags alone on their lines, and any copy of the context kept for code outside the template in an
/// <c>RtlLang</c> field filled by <c>@{ _t = t; }</c>. A lookup this can't read stays as written, so
/// a miss shows in the panel instead of quietly changing the code.
/// </remarks>
public static partial class RtlDemoSource
{
    /// <summary>
    /// <paramref name="source"/> as it reads with <paramref name="lang"/> on show, or
    /// <paramref name="source"/> itself when it has no RtlDemo.
    /// </summary>
    public static string ForLanguage(string source, RtlLang lang)
    {
        var open = OpenTag().Match(source);
        if (!open.Success) return source;

        // The names that hold the language: the template context (t unless Context renames it), and
        // the fields that keep a copy of it for code outside the template (_t).
        var context = ContextAttribute().Match(open.Groups["attributes"].Value) is { Success: true } named
            ? named.Groups["name"].Value
            : "context";
        var fields = LangField().Matches(source).Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
        var names = new HashSet<string>(fields, StringComparer.Ordinal) { context };

        var code = ResolveLookups(Unwrap(source, context, fields), names, lang);
        return BlankRun().Replace(code, "\n\n").Trim('\n');
    }

    // Drops the wrapper's tags and the plumbing that only exists for it (the RtlLang field, and the
    // @{ _t = t; } that fills it), and moves what the wrapper held out by one level.
    private static string Unwrap(string source, string context, HashSet<string> fields)
    {
        var kept = new List<string>();
        var inside = false;
        var wrapperIndent = 0;
        int? dedent = null;
        var dropBlank = false;

        foreach (var line in source.Split('\n'))
        {
            var blank = string.IsNullOrWhiteSpace(line);
            if (dropBlank && blank)
            {
                dropBlank = false;
                continue;
            }
            dropBlank = false;

            if (OpenTag().IsMatch(line))
            {
                inside = true;
                wrapperIndent = Indent(line);
                dedent = null;
                continue;
            }
            if (inside && CloseTag().IsMatch(line))
            {
                inside = false;
                continue;
            }
            if (IsPlumbing(line, context, fields))
            {
                // A field that opened its block would leave the blank line after it hanging under the brace.
                dropBlank = kept.Count > 0 && kept[^1].TrimEnd().EndsWith('{');
                continue;
            }

            if (inside && !blank)
            {
                dedent ??= Math.Max(0, Indent(line) - wrapperIndent);
                kept.Add(line[Math.Min(dedent.Value, Indent(line))..]);
            }
            else
            {
                kept.Add(inside ? "" : line);
            }
        }

        return string.Join('\n', kept);
    }

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    private static bool IsPlumbing(string line, string context, HashSet<string> fields) =>
        LangField().IsMatch(line)
        || KeepContext().Match(line) is { Success: true } keep
            && fields.Contains(keep.Groups["field"].Value)
            && keep.Groups["context"].Value == context;

    // Replaces each lookup on a language name with the argument for the language on show. In C#
    // (t[...] inside an expression or a code block) the argument goes in as written; in markup
    // (@t[...]) it becomes what a reader would type there instead.
    private static string ResolveLookups(string code, HashSet<string> names, RtlLang lang)
    {
        var shown = new StringBuilder(code.Length);
        var copied = 0;
        foreach (Match lookup in Lookup().Matches(code))
        {
            if (lookup.Index < copied || !names.Contains(lookup.Groups["name"].Value)) continue;
            if (InRazorComment(code, lookup.Index)) continue;
            if (!TryReadArguments(code, lookup.Index + lookup.Length, out var arabic, out var hebrew, out var end)) continue;

            var argument = lang.IsHebrew ? hebrew : arabic;
            var after = end < code.Length ? code[end] : '\n';
            if (!lookup.Groups["at"].Success)
            {
                shown.Append(code, copied, lookup.Index - copied).Append(argument);
                copied = end;
                continue;
            }

            // An attribute value quoted with ' only because the lookup inside it had " in it goes
            // back to the usual ", once what replaces the lookup has none.
            var markup = AsMarkup(argument, after);
            var requoted = lookup.Index >= 2 && code[lookup.Index - 1] == '\'' && code[lookup.Index - 2] == '='
                && after == '\'' && !markup.Contains('"');
            shown.Append(code, copied, lookup.Index - copied - (requoted ? 1 : 0))
                 .Append(requoted ? $"\"{markup}\"" : markup);
            copied = requoted ? end + 1 : end;
        }

        return shown.Append(code, copied, code.Length - copied).ToString();
    }

    private static bool InRazorComment(string code, int index)
    {
        var open = code.LastIndexOf("@*", index, StringComparison.Ordinal);
        if (open < 0) return false;
        var close = code.IndexOf("*@", open + 2, StringComparison.Ordinal);
        return close < 0 || close > index;
    }

    // Reads "first, second]" from start: two C# expressions split by a top-level comma. String and
    // char literals are skipped whole, so the commas and brackets inside them don't count.
    private static bool TryReadArguments(string code, int start, out string first, out string second, out int end)
    {
        first = second = "";
        end = start;
        string? firstArgument = null;
        var argumentStart = start;
        var depth = 0;

        for (var i = start; i < code.Length; i++)
        {
            var c = code[i];
            if (c is '"' or '\'' || c is '$' or '@' && i + 1 < code.Length && code[i + 1] == '"')
            {
                var close = SkipLiteral(code, i);
                if (close < 0) return false;
                i = close;
                continue;
            }

            switch (c)
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or '}':
                    depth--;
                    break;
                case ']' when depth > 0:
                    depth--;
                    break;
                case ']':
                    if (firstArgument is null) return false;
                    first = firstArgument;
                    second = code[argumentStart..i].Trim();
                    end = i + 1;
                    return second.Length > 0;
                case ',' when depth == 0:
                    // A third argument means this isn't a language pair.
                    if (firstArgument is not null) return false;
                    firstArgument = code[argumentStart..i].Trim();
                    argumentStart = i + 1;
                    break;
            }
        }

        return false;
    }

    // The index of the quote that closes the literal opening at i ("...", @"...", $"...", @$"..."
    // or a char), or -1. Raw strings aren't read: the examples don't use them in lookups.
    private static int SkipLiteral(string code, int i)
    {
        var verbatim = false;
        var interpolated = false;
        for (; code[i] is '$' or '@'; i++)
        {
            if (code[i] == '@') verbatim = true;
            else interpolated = true;
        }

        var quote = code[i];
        if (code.AsSpan(i).StartsWith("\"\"\"")) return -1;

        var j = i + 1;
        while (j < code.Length)
        {
            var c = code[j];
            if (c == '\\' && !verbatim)
            {
                j += 2;
                continue;
            }
            if (c == quote)
            {
                if (verbatim && j + 1 < code.Length && code[j + 1] == quote)
                {
                    j += 2;
                    continue;
                }
                return j;
            }
            if (interpolated && c == '{')
            {
                if (j + 1 < code.Length && code[j + 1] == '{')
                {
                    j += 2;
                    continue;
                }
                j = SkipHole(code, j);
                if (j < 0) return -1;
                continue;
            }
            if (c == '\n' && !verbatim) return -1;
            j++;
        }

        return -1;
    }

    // The index just past the interpolation hole opening at i, or -1.
    private static int SkipHole(string code, int i)
    {
        var depth = 0;
        while (i < code.Length)
        {
            var c = code[i];
            if (c is '"' or '\'')
            {
                var close = SkipLiteral(code, i);
                if (close < 0) return -1;
                i = close + 1;
                continue;
            }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i + 1;
            i++;
        }

        return -1;
    }

    // What a reader writes in markup in place of @t[...]: a plain string becomes its text, an
    // interpolated one text with @holes, a member chain an implicit expression, anything else an
    // explicit one. after is the character that follows in the source, which an implicit
    // expression must not run on into.
    private static string AsMarkup(string argument, char after)
    {
        if (PlainString().Match(argument) is { Success: true } plain && IsSafeText(plain.Groups["text"].Value))
            return plain.Groups["text"].Value.Replace("@", "@@");

        if (TryTextWithHoles(argument, after, out var text)) return text;

        if (MemberChain().IsMatch(argument) && !RunsOn(after)) return "@" + argument;

        return "@(" + argument + ")";
    }

    // $"Card {card}" as markup: Card @card. Only when every hole is a name or member chain that no
    // letter sits against: the one after would read as part of it, the one before as an email.
    private static bool TryTextWithHoles(string argument, char after, out string markup)
    {
        markup = "";
        var match = Interpolated().Match(argument);
        if (!match.Success) return false;

        var body = match.Groups["body"].Value;
        var built = new StringBuilder();
        var i = 0;
        while (true)
        {
            var open = body.IndexOf('{', i);
            var text = open < 0 ? body[i..] : body[i..open];
            if (!IsSafeText(text)) return false;
            built.Append(text.Replace("@", "@@"));
            if (open < 0) break;

            var close = body.IndexOf('}', open);
            var hole = body[(open + 1)..close];
            var before = open > 0 ? body[open - 1] : ' ';
            var next = close + 1 < body.Length ? body[close + 1] : after;
            if (!MemberChain().IsMatch(hole) || char.IsLetterOrDigit(before) || RunsOn(next)) return false;

            built.Append('@').Append(hole);
            i = close + 1;
        }

        markup = built.ToString();
        return true;
    }

    // Text that reads the same written straight into markup or an attribute value: no tags,
    // entities, quotes or braces.
    private static bool IsSafeText(string text) => text.IndexOfAny(UnsafeInText) < 0;

    private static readonly char[] UnsafeInText = ['<', '>', '&', '"', '\'', '{', '}'];

    // An implicit expression (@name) keeps reading through name characters, members, calls and indexers.
    private static bool RunsOn(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '(' or '[';

    // <RtlDemo Context="t"> or <RtlDemo Context="t" ShowLanguage="false">, alone on its line.
    [GeneratedRegex(@"^[ \t]*<RtlDemo\b(?<attributes>[^>\n]*)>[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex OpenTag();

    [GeneratedRegex(@"\bContext=""(?<name>\w+)""")]
    private static partial Regex ContextAttribute();

    [GeneratedRegex(@"^[ \t]*</RtlDemo>[ \t]*$")]
    private static partial Regex CloseTag();

    // private RtlLang _t;
    [GeneratedRegex(@"^[ \t]*(?:private\s+)?RtlLang\s+(?<name>\w+)\s*;[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex LangField();

    // @{ _t = t; }
    [GeneratedRegex(@"^[ \t]*@\{\s*(?<field>\w+)\s*=\s*(?<context>\w+)\s*;\s*\}[ \t]*$")]
    private static partial Regex KeepContext();

    // A name and its '[' that don't continue another name or member access; an '@' in front puts it in markup.
    [GeneratedRegex(@"(?<![\w.@])(?<at>@)?(?<name>[A-Za-z_]\w*)\[")]
    private static partial Regex Lookup();

    [GeneratedRegex("""^"(?<text>[^"\\]*)"$""")]
    private static partial Regex PlainString();

    [GeneratedRegex("""^\$"(?<body>[^"\\{}]*(?:\{[^"\\{}]+\}[^"\\{}]*)*)"$""")]
    private static partial Regex Interpolated();

    [GeneratedRegex(@"^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*$")]
    private static partial Regex MemberChain();

    // Two or more blank lines in a row, left where removed lines sat between blank ones.
    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+")]
    private static partial Regex BlankRun();
}
