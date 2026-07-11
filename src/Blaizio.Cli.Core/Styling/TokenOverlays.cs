namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The CSS custom-property values behind the /create "Chart" and "Radius" knobs (see
/// <see cref="PresetCode.Charts"/> / <see cref="PresetCode.Radii"/>). Keep in sync with docs
/// DocsThemes / Styles/tokens.css.
/// </summary>
public static class TokenOverlays
{
    /// <summary>
    /// The <c>--radius</c> value for a <see cref="PresetCode.Radii"/> value, or <c>null</c> for
    /// <c>"default"</c> (or any unknown value) — the theme's own radius, which needs no overlay.
    /// </summary>
    public static string? Radius(string name) => name switch
    {
        "none" => "0rem",
        "sm" => "0.45rem",
        "lg" => "1.05rem",
        "xl" => "1.4rem",
        _ => null,
    };

    /// <summary>
    /// The five <c>--chart-*</c> values for a <see cref="PresetCode.Charts"/> value, or <c>null</c>
    /// for <c>"default"</c> (or any unknown value) — the preset's own palette.
    /// </summary>
    public static IReadOnlyList<string>? Chart(string name) => name switch
    {
        "ocean" =>
        [
            "oklch(0.6 0.17 245)", "oklch(0.65 0.14 220)", "oklch(0.7 0.13 195)",
            "oklch(0.72 0.12 170)", "oklch(0.55 0.18 260)",
        ],
        "sunset" =>
        [
            "oklch(0.63 0.19 25)", "oklch(0.7 0.15 50)", "oklch(0.76 0.14 75)",
            "oklch(0.63 0.2 350)", "oklch(0.57 0.19 320)",
        ],
        "forest" =>
        [
            "oklch(0.6 0.14 155)", "oklch(0.68 0.15 130)", "oklch(0.55 0.12 175)",
            "oklch(0.75 0.15 110)", "oklch(0.68 0.12 195)",
        ],
        "mono" =>
        [
            "oklch(0.4 0.015 300)", "oklch(0.52 0.015 300)", "oklch(0.64 0.012 300)",
            "oklch(0.76 0.01 300)", "oklch(0.87 0.008 300)",
        ],
        _ => null,
    };
}
