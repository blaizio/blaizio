using System.Text;
using System.Text.RegularExpressions;
using TailwindMerge;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The v3 registry inliner: turns the authoring model (components with semantic <c>bz-*</c> class
/// tokens + shared/skin sheets of <c>@apply</c> rules) into self-contained shipped source whose
/// class strings carry the skin's utilities directly.
///
/// Sheets contribute only rules whose selector is a single <c>.bz-*</c> class or a comma list of
/// them, with an <c>@apply</c>-only body (the audited SIMPLE/MULTI shapes - see docs/v3-audit.md);
/// everything else (contract plumbing, chart infrastructure) is ignored here and ships via the
/// contract sheet, whose selectors keep matching because tokens WITHOUT a map entry pass through
/// substitution verbatim.
/// </summary>
public sealed partial class SkinInliner
{
    private static readonly TwMerge Merger = new();

    private readonly IReadOnlyDictionary<string, string> _map;

    private SkinInliner(IReadOnlyDictionary<string, string> map) => _map = map;

    /// <summary>Tokens this inliner substitutes (sorted, for diagnostics and goldens).</summary>
    public IReadOnlyList<string> Tokens => [.. _map.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// Build the token map for one skin: the shared baseline merged under the skin's own list per
    /// token, with <see cref="TwMerge"/> resolving conflicts exactly like the runtime
    /// <c>Tw.Merge</c> call the components make (skin wins).
    /// </summary>
    /// <param name="sharedCss">Content of <c>shared.css</c> (unscoped rules).</param>
    /// <param name="skinCss">Content of <c>style-&lt;skin&gt;.css</c> (rules nested under the
    /// <c>.style-&lt;skin&gt;</c> wrapper).</param>
    public static SkinInliner Create(string sharedCss, string skinCss)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (token, classes) in ParseSheet(sharedCss))
            map[token] = classes;
        foreach (var (token, classes) in ParseSheet(skinCss))
            map[token] = map.TryGetValue(token, out var baseline)
                ? Merger.Merge($"{baseline} {classes}") ?? classes
                : classes;
        return new SkinInliner(map);
    }

    /// <summary>
    /// Substitute every mapped <c>bz-*</c> token in <paramref name="source"/> (a shipped
    /// <c>.razor</c> or <c>.cs</c> file) with its utilities. Tokens without a map entry are left
    /// verbatim - they are contract-owned hooks (e.g. <c>bz-chart-*</c>), not misses.
    /// </summary>
    public string Inline(string source) =>
        TokenRegex().Replace(source, m => _map.TryGetValue(m.Value, out var classes) ? classes : m.Value);

    /// <summary>
    /// Parse a sheet into token → space-joined <c>@apply</c> utilities. Handles the flat form
    /// (<c>.bz-x { @apply …; }</c>) and the skin form (the same rules nested one level under the
    /// <c>.style-&lt;skin&gt;</c> wrapper). Comma lists of simple <c>.bz-*</c> names assign the
    /// body to every listed token. Anything else - compound/descendant selectors, raw
    /// declarations, at-rules - is skipped: those rules belong to the contract sheet, and the
    /// audit keeps the authored sheets free of them.
    /// </summary>
    internal static IEnumerable<KeyValuePair<string, string>> ParseSheet(string css)
    {
        css = CommentRegex().Replace(css, "");

        var stack = new List<string>();
        var buf = new StringBuilder();
        foreach (var ch in css)
        {
            if (ch == '{')
            {
                stack.Add(buf.ToString().Trim());
                buf.Clear();
            }
            else if (ch == '}')
            {
                var body = buf.ToString().Trim();
                buf.Clear();
                if (stack.Count == 0)
                    continue;
                var selector = stack[^1];
                stack.RemoveAt(stack.Count - 1);

                if (body.Length == 0)
                    continue;
                // Only rules at the top level, or nested exactly one deep inside a skin wrapper.
                var depthOk = stack.Count == 0
                    || (stack.Count == 1 && WrapperRegex().IsMatch(stack[0]));
                if (!depthOk || selector.StartsWith('@') || stack.Any(s => s.StartsWith('@')))
                    continue;

                var parts = selector.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !Array.TrueForAll(parts, p => SimpleTokenRegex().IsMatch(p)))
                    continue;

                if (ExtractApply(body) is not { } classes)
                    continue;

                foreach (var part in parts)
                    yield return new(part[1..], classes); // strip the leading '.'
            }
            else
            {
                buf.Append(ch);
            }
        }
    }

    /// <summary>
    /// The body's utilities when it is <c>@apply</c>-only (possibly several <c>@apply</c>
    /// statements); null when any raw declaration is present.
    /// </summary>
    private static string? ExtractApply(string body)
    {
        var classes = new List<string>();
        foreach (var raw in body.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!raw.StartsWith("@apply", StringComparison.Ordinal))
                return null;
            classes.Add(WhitespaceRegex().Replace(raw["@apply".Length..].Trim(), " "));
        }
        return classes.Count > 0 ? string.Join(' ', classes) : null;
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"^\.bz-[a-z0-9-]+$")]
    private static partial Regex SimpleTokenRegex();

    [GeneratedRegex(@"^\.style-[a-z0-9-]+$")]
    private static partial Regex WrapperRegex();

    // A token is a maximal bz- run: the character class includes '-', so bz-button can never
    // match inside bz-button-variant-default.
    [GeneratedRegex(@"\bbz-[a-z0-9-]+\b")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
