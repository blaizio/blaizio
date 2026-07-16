using System.Text;
using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// Line-oriented edits inside top-level CSS blocks of the v3 tokens file. The CLI never rewrites
/// the user-owned file wholesale after init — it patches declaration values inside a named block
/// (<c>:root</c>, <c>.dark</c>, <c>@layer base</c>) and appends what's missing. Block scoping is
/// what keeps a <c>--font-heading</c> patch in <c>:root</c> from clobbering the same name inside
/// the <c>@theme inline</c> utility map.
/// </summary>
public static partial class CssBlocks
{
    /// <summary>
    /// Locate the top-level block opened by <paramref name="selector"/> (matched against the full
    /// prelude before <c>{</c>, at brace depth zero). Returns the inner content span — the indices
    /// just after the opening brace and just before its matching close — or null when absent.
    /// </summary>
    public static (int Start, int End)? FindBlock(string css, string selector)
    {
        var depth = 0;
        var preludeStart = 0;
        for (var i = 0; i < css.Length; i++)
        {
            var ch = css[i];
            if (ch == '{')
            {
                if (depth == 0)
                {
                    var prelude = css[preludeStart..i].Trim();
                    if (string.Equals(prelude, selector, StringComparison.Ordinal))
                    {
                        var end = MatchingClose(css, i);
                        if (end >= 0)
                            return (i + 1, end);
                    }
                }
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    preludeStart = i + 1;
            }
        }
        return null;
    }

    /// <summary>Index of the <c>}</c> matching the <c>{</c> at <paramref name="open"/>, or -1.</summary>
    private static int MatchingClose(string css, int open)
    {
        var depth = 0;
        for (var i = open; i < css.Length; i++)
        {
            if (css[i] == '{')
                depth++;
            else if (css[i] == '}' && --depth == 0)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// The <c>name: value;</c> declarations directly inside the top-level
    /// <paramref name="selector"/> block (nested rules are skipped), in source order.
    /// </summary>
    public static IReadOnlyList<(string Name, string Value)> Declarations(string css, string selector)
    {
        if (FindBlock(css, selector) is not { } span)
            return [];

        var result = new List<(string, string)>();
        var body = StripComments(css[span.Start..span.End]);
        var depth = 0;
        var buf = new StringBuilder();
        foreach (var ch in body)
        {
            if (ch == '{')
            {
                depth++;
                buf.Clear();
            }
            else if (ch == '}')
            {
                depth--;
            }
            else if (ch == ';' && depth == 0)
            {
                var decl = buf.ToString().Trim();
                buf.Clear();
                var colon = decl.IndexOf(':');
                if (colon > 0)
                    result.Add((decl[..colon].Trim(), decl[(colon + 1)..].Trim()));
            }
            else
            {
                buf.Append(ch);
            }
        }
        return result;
    }

    /// <summary>
    /// Set <c>name: value;</c> inside the top-level <paramref name="selector"/> block: replace the
    /// declaration's value in place when the exact name is present (never a prefix match, so
    /// <c>--radius</c> leaves <c>--radius-sm</c> alone), append it before the block's close when
    /// missing. No-op (returns the input) when the block itself is absent.
    /// </summary>
    public static string SetDeclaration(string css, string selector, string name, string value)
    {
        if (FindBlock(css, selector) is not { } span)
            return css;

        var body = css[span.Start..span.End];
        var lines = body.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith($"{name}:", StringComparison.Ordinal))
                continue;
            var indent = lines[i][..^trimmed.Length];
            lines[i] = $"{indent}{name}: {value};";
            return css[..span.Start] + string.Join('\n', lines) + css[span.End..];
        }

        // Absent: append as the block's last line, matching the block's own indentation.
        var insert = $"{Indent(body)}{name}: {value};\n";
        var tail = body.TrimEnd(' ', '\t');
        return css[..span.Start] + tail + (tail.EndsWith('\n') ? "" : "\n") + insert + css[span.End..];
    }

    /// <summary>
    /// Ensure a one-line rule (e.g. <c>html { font-family: …; }</c>) inside the top-level
    /// <paramref name="selector"/> block, keyed by <paramref name="prelude"/> (the nested rule's
    /// selector): an existing single-line rule with that prelude is replaced, otherwise
    /// <paramref name="rule"/> is appended at the block's end. No-op when the block is absent.
    /// </summary>
    public static string SetNestedRule(string css, string selector, string prelude, string rule)
    {
        if (FindBlock(css, selector) is not { } span)
            return css;

        var body = css[span.Start..span.End];
        var lines = body.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith($"{prelude} {{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
                continue;
            var indent = lines[i][..^trimmed.Length];
            lines[i] = $"{indent}{rule}";
            return css[..span.Start] + string.Join('\n', lines) + css[span.End..];
        }

        var insert = $"{Indent(body)}{rule}\n";
        var tail = body.TrimEnd(' ', '\t');
        return css[..span.Start] + tail + (tail.EndsWith('\n') ? "" : "\n") + insert + css[span.End..];
    }

    /// <summary>
    /// Remove the single-line nested rule with <paramref name="prelude"/> from the top-level
    /// <paramref name="selector"/> block (the reverse of <see cref="SetNestedRule"/>).
    /// </summary>
    public static string RemoveNestedRule(string css, string selector, string prelude)
    {
        if (FindBlock(css, selector) is not { } span)
            return css;

        var body = css[span.Start..span.End];
        var lines = body.Split('\n').ToList();
        var removed = lines.RemoveAll(l =>
        {
            var trimmed = l.TrimStart();
            return trimmed.StartsWith($"{prelude} {{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal);
        });
        return removed == 0 ? css : css[..span.Start] + string.Join('\n', lines) + css[span.End..];
    }

    /// <summary>
    /// Remove a <c>name: …;</c> declaration line from the top-level <paramref name="selector"/>
    /// block (exact name, like <see cref="SetDeclaration"/>). No-op when absent.
    /// </summary>
    public static string RemoveDeclaration(string css, string selector, string name)
    {
        if (FindBlock(css, selector) is not { } span)
            return css;

        var lines = css[span.Start..span.End].Split('\n').ToList();
        var removed = lines.RemoveAll(l => l.TrimStart().StartsWith($"{name}:", StringComparison.Ordinal));
        return removed == 0 ? css : css[..span.Start] + string.Join('\n', lines) + css[span.End..];
    }

    /// <summary>
    /// Remove an entire top-level block (prelude + braces) whose prelude matches
    /// <paramref name="selector"/> exactly. Repeats until no match (e.g. a rule and its
    /// <c>:hover</c> sibling are separate calls, duplicates aren't). No-op when absent.
    /// </summary>
    public static string RemoveTopLevelBlock(string css, string selector)
    {
        while (FindBlock(css, selector) is { } span)
        {
            // Walk back from the opening brace over the prelude to the previous block's end
            // (or file start), so the prelude text and its leading blank line go too.
            var start = css.LastIndexOf('}', span.Start - 1) + 1;
            var end = span.End + 1; // include the closing brace
            css = css[..start].TrimEnd('\n', ' ', '\t') + "\n\n" + css[end..].TrimStart('\n');
        }
        return css;
    }

    /// <summary>
    /// Remove a multi-line nested block (prelude + braces) inside the top-level
    /// <paramref name="selector"/> block — e.g. the v1 <c>.bz-font-heading {{ … }}</c> rule
    /// inside <c>@layer base</c>. No-op when absent.
    /// </summary>
    public static string RemoveNestedBlock(string css, string selector, string prelude)
    {
        if (FindBlock(css, selector) is not { } span)
            return css;

        var body = css[span.Start..span.End];
        var idx = body.IndexOf($"{prelude} {{", StringComparison.Ordinal);
        if (idx < 0)
            return css;
        var open = body.IndexOf('{', idx);
        var close = MatchingClose(body, open);
        if (close < 0)
            return css;

        var lineStart = body.LastIndexOf('\n', idx) + 1;
        var lineEnd = body.IndexOf('\n', close);
        lineEnd = lineEnd < 0 ? body.Length : lineEnd + 1;
        return css[..span.Start] + body[..lineStart] + body[lineEnd..] + css[span.End..];
    }

    /// <summary>Strip <c>/* … */</c> comments.</summary>
    public static string StripComments(string css) => CommentRegex().Replace(css, "");

    /// <summary>The indentation of the block's first indented line (fallback: two spaces).</summary>
    private static string Indent(string body)
    {
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.Length > 0 && trimmed.Length < line.Length)
                return line[..^trimmed.Length];
        }
        return "  ";
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();
}
