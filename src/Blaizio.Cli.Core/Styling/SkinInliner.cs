using System.Text;
using System.Text.RegularExpressions;
using TailwindMerge;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The v3 registry inliner: turns the authoring model (components with semantic <c>bz-*</c> class
/// tokens + shared/skin sheets of <c>@apply</c> rules) into self-contained shipped source whose
/// class strings carry the skin's utilities directly.
///
/// The sheets keep serving the v1 cascade unchanged - instead of rewriting them, the parser
/// COMPILES their selectors (see docs/v3-audit.md, "Execution note"):
/// <list type="bullet">
/// <item>repeated-class specificity hacks (<c>.bz-x.bz-x</c>) normalize to the token;</item>
/// <item>subject suffixes (<c>[data-color='success']</c>, <c>:first-child</c>, <c>::after</c>,
/// a <c> th</c> descendant element) become variant prefixes on every utility;</item>
/// <item>parent-state selectors (<c>.bz-attachment[data-state='error'] .bz-attachment-title</c>)
/// become named-group variants on the child, and the parent token auto-gains
/// <c>group/&lt;name&gt;</c>;</item>
/// <item>a <c>[dir="rtl"]</c> ancestor becomes <c>rtl:</c>;</item>
/// <item>raw declarations become arbitrary-property utilities before prefixing.</item>
/// </list>
/// Selectors outside that grammar are skipped - contract-sheet territory (chart svg parts, toast
/// machinery), whose tokens stay unmapped and therefore pass substitution verbatim so the
/// contract keeps matching them.
/// </summary>
public sealed partial class SkinInliner
{
    private static readonly TwMerge Merger = new();

    private readonly IReadOnlyDictionary<string, string> _map;

    private SkinInliner(IReadOnlyDictionary<string, string> map) => _map = map;

    /// <summary>Tokens this inliner substitutes (sorted, for diagnostics and goldens).</summary>
    public IReadOnlyList<string> Tokens => [.. _map.Keys.Order(StringComparer.Ordinal)];

    /// <summary>The utilities a token resolves to, or null when unmapped (contract-owned).</summary>
    public string? Resolve(string token) => _map.GetValueOrDefault(token);

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
        Fold(map, ParseSheet(sharedCss), merge: false);
        Fold(map, ParseSheet(skinCss), merge: true);
        return new SkinInliner(map);
    }

    private static void Fold(
        Dictionary<string, string> map, IEnumerable<KeyValuePair<string, string>> entries, bool merge)
    {
        foreach (var (token, classes) in entries)
        {
            // Group markers repeat once per compiled parent-state rule - fold them silently.
            if (map.TryGetValue(token, out var existing)
                && classes.StartsWith("group/", StringComparison.Ordinal)
                && existing.Split(' ').Contains(classes))
                continue;

            map[token] = existing is not null
                // Within one sheet entries accumulate (base rule + compiled variant rules);
                // across sheets the skin's utilities override the baseline's via TwMerge.
                ? merge ? Merger.Merge($"{existing} {classes}") ?? classes : $"{existing} {classes}"
                : classes;
        }
    }

    /// <summary>
    /// Substitute every mapped <c>bz-*</c> token in <paramref name="source"/> (a shipped
    /// <c>.razor</c> or <c>.cs</c> file) with its utilities. Tokens without a map entry are left
    /// verbatim - they are contract-owned hooks (e.g. <c>bz-chart-*</c>), not misses.
    /// </summary>
    public string Inline(string source) =>
        TokenRegex().Replace(source, m => _map.TryGetValue(m.Value, out var classes) ? classes : m.Value);

    /// <summary>
    /// Parse a sheet into token → utilities entries, compiling selector shapes into variant
    /// prefixes. Handles the flat form and the skin form (rules nested one level under the
    /// <c>.style-&lt;skin&gt;</c> wrapper).
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

                if (BodyUtilities(body) is not { } utilities)
                    continue;

                foreach (var part in selector.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (CompileSelector(part) is not var (token, prefix, groupParent))
                        continue;
                    if (groupParent is not null)
                        yield return new(groupParent.Value.Token, groupParent.Value.GroupClass);
                    yield return new(token, Prefix(utilities, prefix));
                }
            }
            else
            {
                buf.Append(ch);
            }
        }
    }

    /// <summary>
    /// Compile one selector into (subject token, variant prefix, optional parent group marker);
    /// null when the selector is outside the grammar (contract territory).
    /// </summary>
    private static (string Token, string Prefix, (string Token, string GroupClass)? Group)?
        CompileSelector(string selector)
    {
        var prefix = new StringBuilder();
        (string Token, string GroupClass)? group = null;
        var s = selector.Trim();

        // Ancestor: [dir="rtl"]
        var rtl = RtlAncestorRegex().Match(s);
        if (rtl.Success)
        {
            prefix.Append("rtl:");
            s = s[rtl.Length..].TrimStart();
        }

        // Ancestor: .bz-parent[attr]... followed by a descendant subject.
        var parent = ParentRegex().Match(s);
        if (parent.Success && s[parent.Length..].TrimStart().StartsWith('.'))
        {
            var parentToken = parent.Groups["token"].Value;
            var groupName = parentToken["bz-".Length..];
            foreach (Capture attr in parent.Groups["attr"].Captures)
            {
                if (AttrToVariant(attr.Value) is not { } v)
                    return null;
                prefix.Append($"group-{v}/{groupName}:");
            }
            group = (parentToken, $"group/{groupName}");
            s = s[parent.Length..].TrimStart();
        }

        // Subject: .bz-token, optionally repeated (specificity hack), then a suffix chain.
        var subject = SubjectRegex().Match(s);
        if (!subject.Success)
            return null;
        var token = subject.Groups["token"].Value;
        s = s[subject.Length..];

        // Drop repeated .bz-token (hack) occurrences.
        while (s.StartsWith($".{token}", StringComparison.Ordinal))
            s = s[(token.Length + 1)..];

        // Suffixes: attributes, pseudo-classes, pseudo-elements, one descendant element.
        var seenAttrs = new List<string>();
        while (s.Length > 0)
        {
            var attr = AttrRegex().Match(s);
            if (attr.Success)
            {
                // A bare repeat of an attribute already matched with a value is the doubled
                // specificity hack ([data-color='x'][data-color]) - fold it away.
                var name = attr.Groups["name"].Value;
                var hackRepeat = !attr.Groups["value"].Success && seenAttrs.Contains(name);
                if (!hackRepeat)
                {
                    if (AttrToVariant(attr.Value) is not { } v)
                        return null;
                    prefix.Append($"{v}:");
                    seenAttrs.Add(name);
                }
                s = s[attr.Length..];
                continue;
            }

            var pseudo = PseudoRegex().Match(s);
            if (pseudo.Success)
            {
                var variant = pseudo.Value.TrimStart(':') switch
                {
                    "first-child" => "first",
                    "last-child" => "last",
                    _ => pseudo.Value.TrimStart(':'),
                };
                prefix.Append($"{variant}:");
                s = s[pseudo.Length..];
                continue;
            }

            var element = ElementRegex().Match(s);
            if (element.Success)
            {
                prefix.Append($"[&_{element.Value.Trim()}]:");
                s = s[element.Length..];
                continue;
            }

            return null; // unrecognized suffix - contract territory
        }

        return (token, prefix.ToString(), group);
    }

    /// <summary>Tailwind variant for an attribute selector, or null when inexpressible.</summary>
    private static string? AttrToVariant(string attr)
    {
        var m = AttrRegex().Match(attr);
        if (!m.Success || m.Length != attr.Length)
            return null;
        var name = m.Groups["name"].Value;
        if (!name.StartsWith("data-", StringComparison.Ordinal))
            return null;
        return m.Groups["value"].Success
            ? $"data-[{name["data-".Length..]}={m.Groups["value"].Value}]"
            : $"data-{name["data-".Length..]}";
    }

    /// <summary>
    /// The body as utilities: <c>@apply</c> statements pass through, raw declarations become
    /// arbitrary-property utilities (<c>[prop:value]</c>, spaces underscored). Null for bodies the
    /// grammar can't express (nested rules would already have been split by the tokenizer).
    /// </summary>
    private static string? BodyUtilities(string body)
    {
        var classes = new List<string>();
        foreach (var raw in body.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.StartsWith("@apply", StringComparison.Ordinal))
            {
                classes.Add(WhitespaceRegex().Replace(raw["@apply".Length..].Trim(), " "));
                continue;
            }
            var colon = raw.IndexOf(':');
            if (colon <= 0 || raw.StartsWith('@'))
                return null;
            var prop = raw[..colon].Trim();
            var value = WhitespaceRegex().Replace(raw[(colon + 1)..].Trim(), " ").Replace(' ', '_');
            if (value.Contains("!important", StringComparison.Ordinal))
                return null;
            classes.Add($"[{prop}:{value}]");
        }
        return classes.Count > 0 ? string.Join(' ', classes) : null;
    }

    private static string Prefix(string utilities, string prefix) =>
        prefix.Length == 0
            ? utilities
            : string.Join(' ', utilities.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(u => prefix + u));

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"^\.style-[a-z0-9-]+$")]
    private static partial Regex WrapperRegex();

    [GeneratedRegex(@"^\[dir=(""rtl""|'rtl')\]")]
    private static partial Regex RtlAncestorRegex();

    [GeneratedRegex(@"^\.(?<token>bz-[a-z0-9-]+)(?<attr>\[[^\]]+\])+")]
    private static partial Regex ParentRegex();

    [GeneratedRegex(@"^\.(?<token>bz-[a-z0-9-]+)")]
    private static partial Regex SubjectRegex();

    [GeneratedRegex(@"^\[(?<name>[a-z-]+)(=(""|')?(?<value>[^""'\]]+)(""|')?)?\]")]
    private static partial Regex AttrRegex();

    [GeneratedRegex(@"^::?(first-child|last-child|empty|focus-visible|focus-within|focus|hover|active|disabled|before|after)")]
    private static partial Regex PseudoRegex();

    [GeneratedRegex(@"^ +(th|td|svg|img|a|p)\b(?=$)")]
    private static partial Regex ElementRegex();

    // A token is a maximal bz- run: the character class includes '-', so bz-button can never
    // match inside bz-button-variant-default.
    [GeneratedRegex(@"\bbz-[a-z0-9-]+\b")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
