using Blaizio.Cli.Core.Styling;

namespace Blaizio.Docs;

/// <summary>One chart palette: its class-name suffix, label and the five series colors (for the Themes swatches).</summary>
public sealed record ChartPaletteEntry(string Name, string Label, string[] Colors);

/// <summary>One font option: its class-name suffix, label and the CSS stack (for the "Aa" previews).</summary>
public sealed record FontEntry(string Value, string Label, string Stack);

/// <summary>
/// The Themes knob registries beyond the color presets (see <see cref="Presets"/>): the eight
/// built-in styles (skins), the chart palettes, the font stacks and the radius scales. Shared by
/// the header's ThemeToggle and the Themes rail so the lists can't drift. Every list is
/// APPEND-ONLY: preset codes encode option indices (see PresetCode), so reordering or removing
/// entries breaks previously shared codes.
/// </summary>
public static class DocsThemes
{
    public static readonly (string Value, string Label)[] Styles =
    [
        ("ember", "Ember"),
        ("spark", "Spark"),
        ("glow", "Glow"),
        ("forge", "Forge"),
        ("flint", "Flint"),
        ("aura", "Aura"),
        ("ash", "Ash"),
        ("wisp", "Wisp"),
    ];

    /// <summary>Chart palettes - the chart-* overlay classes in Styles/tokens.css ("default" = the Nova palette, no class).</summary>
    public static readonly ChartPaletteEntry[] ChartPalettes =
    [
        new("default", "Nova", ["oklch(0.63 0.23 304)", "oklch(0.62 0.19 275)", "oklch(0.65 0.2 350)", "oklch(0.65 0.15 245)", "oklch(0.7 0.13 195)"]),
        new("ocean", "Ocean", ["oklch(0.6 0.17 245)", "oklch(0.65 0.14 220)", "oklch(0.7 0.13 195)", "oklch(0.72 0.12 170)", "oklch(0.55 0.18 260)"]),
        new("sunset", "Sunset", ["oklch(0.63 0.19 25)", "oklch(0.7 0.15 50)", "oklch(0.76 0.14 75)", "oklch(0.63 0.2 350)", "oklch(0.57 0.19 320)"]),
        new("forest", "Forest", ["oklch(0.6 0.14 155)", "oklch(0.68 0.15 130)", "oklch(0.55 0.12 175)", "oklch(0.75 0.15 110)", "oklch(0.68 0.12 195)"]),
        new("mono", "Mono", ["oklch(0.4 0.015 300)", "oklch(0.52 0.015 300)", "oklch(0.64 0.012 300)", "oklch(0.76 0.01 300)", "oklch(0.87 0.008 300)"]),
        new("polaris", "Polaris", ["oklch(0.6 0.13 240)", "oklch(0.72 0.1 200)", "oklch(0.55 0.08 270)", "oklch(0.78 0.1 180)", "oklch(0.45 0.1 250)"]),
        new("umbra", "Umbra", ["oklch(0.5 0.19 28)", "oklch(0.3 0.01 60)", "oklch(0.55 0.012 60)", "oklch(0.72 0.008 80)", "oklch(0.55 0.08 250)"]),
        new("corona", "Corona", ["oklch(0.75 0.11 88)", "oklch(0.5 0.08 80)", "oklch(0.35 0.03 60)", "oklch(0.45 0.12 20)", "oklch(0.65 0.06 60)"]),
        new("magnetar", "Magnetar", ["oklch(0.67 0.23 345)", "oklch(0.78 0.13 195)", "oklch(0.62 0.2 300)", "oklch(0.8 0.14 85)", "oklch(0.6 0.18 260)"]),
        new("aurora", "Aurora", ["oklch(0.72 0.19 148)", "oklch(0.55 0.13 145)", "oklch(0.8 0.15 85)", "oklch(0.65 0.12 195)", "oklch(0.42 0.08 145)"]),
        new("equinox", "Equinox", ["oklch(0.55 0.12 140)", "oklch(0.62 0.14 45)", "oklch(0.72 0.13 90)", "oklch(0.52 0.09 200)", "oklch(0.68 0.1 110)"]),
    ];

    /// <summary>Font options - shared by the Heading and Font knobs (heading-* / font-* overlay
    /// classes). Derived from <see cref="FontCatalog"/>, the canonical CLI-shared list; keep the
    /// overlay classes in Styles/create-overlays.css in sync with it.</summary>
    public static readonly FontEntry[] Fonts =
        [.. FontCatalog.All.Select(f => new FontEntry(f.Name, f.Title, f.Stack))];

    /// <summary>Find a font by value; the default when unknown.</summary>
    public static FontEntry FindFont(string? value) =>
        Array.Find(Fonts, f => string.Equals(f.Value, value, StringComparison.OrdinalIgnoreCase)) ?? Fonts[0];

    /// <summary>Radius scales - the radius-* overlay classes ("default" = 0.75rem).</summary>
    public static readonly (string Value, string Label)[] Radii =
    [
        ("default", "Default"),
        ("none", "None"),
        ("sm", "Small"),
        ("lg", "Large"),
        ("xl", "Extra large"),
    ];

    /// <summary>Find a chart palette by name; the default when unknown.</summary>
    public static ChartPaletteEntry FindChart(string? name) =>
        Array.Find(ChartPalettes, p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) ?? ChartPalettes[0];

    /// <summary>The display label for a (Value, Label) list entry; the raw value when unknown.</summary>
    public static string Label((string Value, string Label)[] list, string value)
    {
        var i = Array.FindIndex(list, t => t.Value == value);
        return i >= 0 ? list[i].Label : value;
    }
}
