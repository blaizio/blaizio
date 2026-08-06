using System.Text;
using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The managed CSS region an item's <c>css</c> blocks live in inside the tokens file. Each region
/// is fenced by item-keyed marker comments, so a re-install replaces exactly its own region and
/// <c>remove</c>/<c>uninstall</c> take exactly it back out - the rest of the user-owned file is
/// never touched. The registry's block preludes are written verbatim (<c>@keyframes ...</c>,
/// <c>@utility ...</c>, <c>@layer ...</c>, a plain selector); the CLI does not interpret them.
/// </summary>
public static partial class ItemCssRegions
{
    private static string Open(string item) => $"/* blaizio:css {item} */";
    private static string Close(string item) => $"/* blaizio:css:end {item} */";

    /// <summary>Render an item's blocks as its full fenced region.</summary>
    public static string Render(string item, IReadOnlyDictionary<string, string> blocks)
    {
        var sb = new StringBuilder();
        sb.Append(Open(item)).Append('\n');
        foreach (var (prelude, body) in blocks)
        {
            sb.Append(prelude.Trim()).Append(" {\n");
            foreach (var line in body.Trim().Split('\n'))
                sb.Append("  ").Append(line.TrimEnd('\r')).Append('\n');
            sb.Append("}\n");
        }
        sb.Append(Close(item));
        return sb.ToString();
    }

    /// <summary>
    /// Put the item's region into <paramref name="css"/>: replace an existing region in place,
    /// else append at the end of the file. Idempotent for identical blocks.
    /// </summary>
    public static string Apply(string css, string item, IReadOnlyDictionary<string, string> blocks)
    {
        var region = Render(item, blocks);
        if (FindRegion(css, item) is { } span)
            return css[..span.Start] + region + css[span.End..];

        var trimmed = css.TrimEnd('\n', ' ', '\t');
        return trimmed + "\n\n" + region + "\n";
    }

    /// <summary>Remove the item's region (markers included). No-op when absent.</summary>
    public static string Remove(string css, string item)
    {
        if (FindRegion(css, item) is not { } span)
            return css;
        var before = css[..span.Start].TrimEnd('\n', ' ', '\t');
        var after = css[span.End..].TrimStart('\n');
        return before.Length == 0 ? after : before + "\n" + (after.Length == 0 ? "" : "\n" + after);
    }

    /// <summary>Every item name that has a region in <paramref name="css"/>.</summary>
    public static IReadOnlyList<string> Items(string css) =>
        [.. OpenMarker().Matches(css).Select(m => m.Groups["item"].Value)];

    /// <summary>The span of the item's whole region (open marker through close marker), or null.</summary>
    private static (int Start, int End)? FindRegion(string css, string item)
    {
        var start = css.IndexOf(Open(item), StringComparison.Ordinal);
        if (start < 0)
            return null;
        var close = Close(item);
        var end = css.IndexOf(close, start, StringComparison.Ordinal);
        if (end >= 0)
            return (start, end + close.Length);

        // Corrupted fence (close marker gone): reclaim only the open-marker line. The old body
        // stays behind as plain user CSS - orphaned, but never eaten.
        var lineEnd = css.IndexOf('\n', start);
        return (start, lineEnd < 0 ? css.Length : lineEnd + 1);
    }

    [GeneratedRegex(@"/\* blaizio:css (?<item>[^*]+?) \*/")]
    private static partial Regex OpenMarker();
}
