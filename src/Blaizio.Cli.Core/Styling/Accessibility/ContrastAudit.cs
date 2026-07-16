namespace Blaizio.Cli.Core.Styling.Accessibility;

/// <summary>One contrast measurement of a token pair in one mode.</summary>
/// <param name="Mode">"light" or "dark".</param>
/// <param name="Label">Human-readable pair, e.g. "primary-foreground on primary".</param>
/// <param name="Foreground">Foreground token name.</param>
/// <param name="Background">Background token name (or a derived-fill description).</param>
/// <param name="Ratio">Measured WCAG contrast ratio.</param>
/// <param name="Required">The AA minimum for this pair's role (4.5 text, 3.0 non-text).</param>
/// <param name="Advisory">A known design tension reported as a warning, never a failure:
/// <c>primary</c> plays two conflicting roles — link TEXT on the page background and a FILL
/// under its label — and one value cannot satisfy both in every palette. Text-first palettes
/// (nova: bright dark-mode primary) trade the raw fill; fill-first palettes (solstice: bright
/// amber with a dark label) trade the link text. The raw dark fill pairs and the
/// primary-as-text pairs are therefore advisory; the fill the default button actually paints
/// (derived, deepened 15%) stays a hard requirement.</param>
public sealed record ContrastFinding(
    string Mode, string Label, string Foreground, string Background, double Ratio, double Required,
    bool Advisory = false)
{
    /// <summary>Whether the pair clears its AA minimum.</summary>
    public bool Pass => Ratio >= Required;
}

/// <summary>The audit outcome: measurements plus the token values that couldn't be parsed.</summary>
public sealed record ContrastReport(
    IReadOnlyList<ContrastFinding> Findings,
    IReadOnlyList<string> Unparsed)
{
    /// <summary>True when every non-advisory pair clears its minimum (advisories never fail a run).</summary>
    public bool AllPass => Findings.All(f => f.Pass || f.Advisory);
}

/// <summary>
/// WCAG AA contrast audit over a tokens file's <c>:root</c>/<c>.dark</c> values — what
/// <c>blaizio contrast</c> runs after the user re-themes. Checks the pairs the components
/// actually paint: every foreground-on-surface token pair, the tokens used as TEXT directly on
/// the page background (muted-foreground, primary links, the status colors), the focus ring as
/// non-text UI (3:1), and the button's dark-mode derived fill
/// (<c>color-mix(in oklab, primary 85%, black)</c>) under <c>primary-foreground</c>.
/// Pairs whose tokens are missing are skipped (not every project defines the sidebar set);
/// values that aren't parseable literals are reported as unchecked instead of guessed at.
/// </summary>
public static class ContrastAudit
{
    private const double Text = 4.5;
    private const double NonText = 3.0;

    /// <summary>The percentage of the button's dark-fill mix (tracks shared.css).</summary>
    public const double DarkFillMix = 0.85;

    /// <summary>Foreground-on-surface pairs: (fg token, bg token).</summary>
    private static readonly (string Fg, string Bg)[] SurfacePairs =
    [
        ("foreground", "background"),
        ("card-foreground", "card"),
        ("popover-foreground", "popover"),
        ("primary-foreground", "primary"),
        ("secondary-foreground", "secondary"),
        ("muted-foreground", "muted"),
        ("accent-foreground", "accent"),
        ("destructive-foreground", "destructive"),
        ("success-foreground", "success"),
        ("warning-foreground", "warning"),
        ("info-foreground", "info"),
        ("sidebar-foreground", "sidebar"),
        ("sidebar-primary-foreground", "sidebar-primary"),
        ("sidebar-accent-foreground", "sidebar-accent"),
    ];

    /// <summary>Tokens components render as TEXT directly on the page background.</summary>
    private static readonly string[] TextOnBackground =
        ["muted-foreground", "primary", "destructive", "success", "warning", "info"];

    /// <summary>Run the audit over the tokens file's content.</summary>
    /// <param name="css">The tokens file text (<c>:root</c> + <c>.dark</c> blocks).</param>
    public static ContrastReport Run(string css)
    {
        var root = TokenMap(css, ":root");
        var dark = new Dictionary<string, string>(root, StringComparer.Ordinal);
        foreach (var (name, value) in TokenMap(css, ".dark"))
            dark[name] = value; // .dark inherits :root and overrides

        var findings = new List<ContrastFinding>();
        var unparsed = new List<string>();

        foreach (var (mode, map) in new[] { ("light", root), ("dark", dark) })
        {
            foreach (var (fg, bg) in SurfacePairs)
            {
                // Dark primary is tuned for link legibility on the page background; the raw fill
                // under its label is a known trade (the button derives a deeper fill instead).
                var advisory = mode == "dark" && bg is "primary" or "sidebar-primary";
                Measure(map, mode, fg, bg, Text, findings, unparsed, advisory: advisory);
            }

            foreach (var fg in TextOnBackground)
                Measure(map, mode, fg, "background", Text, findings, unparsed, advisory: fg == "primary");

            // The visible focus indicator: WCAG 1.4.11 non-text contrast.
            Measure(map, mode, "ring", "background", NonText, findings, unparsed, label: "ring on background (focus)");
        }

        // The default button's dark fill is derived, not a token: primary deepened 15% toward
        // black in oklab (see shared.css bz-button-variant-default). Check what actually paints.
        if (Resolve(dark, "primary") is { } primaryValue && Resolve(dark, "primary-foreground") is { } fgValue)
        {
            if (CssColor.Parse(primaryValue) is { } primary && CssColor.Parse(fgValue) is { } foreground)
            {
                var fill = primary.MixBlackOklab(DarkFillMix);
                findings.Add(new ContrastFinding(
                    "dark", "primary-foreground on button fill (derived)", "primary-foreground",
                    "color-mix(primary 85%, black)", CssColor.Contrast(foreground, fill), Text));
            }
        }

        return new ContrastReport(findings, [.. unparsed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    private static void Measure(
        Dictionary<string, string> map, string mode, string fg, string bg, double required,
        List<ContrastFinding> findings, List<string> unparsed, string? label = null, bool advisory = false)
    {
        var fgValue = Resolve(map, fg);
        var bgValue = Resolve(map, bg);
        if (fgValue is null || bgValue is null)
            return; // pair not defined - nothing to check

        var fgColor = CssColor.Parse(fgValue);
        var bgColor = CssColor.Parse(bgValue);
        if (fgColor is null)
            unparsed.Add($"--{fg}: {fgValue}");
        if (bgColor is null)
            unparsed.Add($"--{bg}: {bgValue}");
        if (fgColor is null || bgColor is null)
            return;

        findings.Add(new ContrastFinding(
            mode, label ?? $"{fg} on {bg}", fg, bg, CssColor.Contrast(fgColor.Value, bgColor.Value), required, advisory));
    }

    /// <summary>A token's value with <c>var(--x)</c> indirection followed (a few hops, no cycles).</summary>
    private static string? Resolve(Dictionary<string, string> map, string name)
    {
        if (!map.TryGetValue(name, out var value))
            return null;
        for (var hops = 0; hops < 5; hops++)
        {
            var trimmed = value.Trim();
            if (!trimmed.StartsWith("var(--", StringComparison.Ordinal) || !trimmed.EndsWith(")", StringComparison.Ordinal))
                return trimmed;
            var inner = trimmed[6..^1];
            var comma = inner.IndexOf(',');
            var target = (comma >= 0 ? inner[..comma] : inner).Trim();
            if (!map.TryGetValue(target, out var next))
                return comma >= 0 ? inner[(comma + 1)..].Trim() : null; // fallback value or dead end
            value = next;
        }
        return null;
    }

    private static Dictionary<string, string> TokenMap(string css, string selector)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in CssBlocks.Declarations(css, selector))
        {
            if (name.StartsWith("--", StringComparison.Ordinal))
                map[name[2..]] = value;
        }
        return map;
    }
}
