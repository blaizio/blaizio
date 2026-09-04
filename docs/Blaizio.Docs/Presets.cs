using System.Collections.Concurrent;

namespace Blaizio.Docs;

/// <summary>
/// One color preset: its class-name suffix (<c>preset-{Name}</c> on <c>&lt;html&gt;</c>), display
/// label, and the two swatch colors the Themes rail paints its chip with (raw CSS color strings -
/// the light primary and the dark background, so the chip previews both modes at a glance).
/// <see cref="PairedHeading"/>/<see cref="PairedFont"/> (FontCatalog names) and
/// <see cref="PairedChart"/> (a DocsThemes.ChartPalettes name) are the theme's designed
/// companions: The Themes page applies all three when the theme is picked, into every knob the user
/// hasn't locked - so a theme is a complete look, and a lock pins any knob against it.
/// </summary>
public sealed record PresetEntry(
    string Name, string Label, string SwatchPrimary, string SwatchDark,
    string PairedHeading = "default", string PairedFont = "default", string PairedChart = "default")
{
    /// <summary>
    /// A five-step single-hue ramp derived from <see cref="SwatchPrimary"/> - the Themes Color row
    /// paints it as a dot strip, matching the Chart Color row's palette preview. Purely for display;
    /// the real tokens live in the preset CSS.
    /// </summary>
    public string[] Swatches => _swatches ??= BuildRamp(SwatchPrimary);

    private string[]? _swatches;

    // oklch(L C H) → five swatches sweeping lightness at the same hue (chroma eased toward the
    // ends so the lightest/darkest don't read as neon). Falls back to the primary alone if the
    // string isn't the expected shape.
    private static string[] BuildRamp(string primary)
    {
        var open = primary.IndexOf('(');
        var close = primary.IndexOf(')');
        if (open < 0 || close < open) return [primary];
        var parts = primary[(open + 1)..close].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3
            || !double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var c)
            || !double.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var h))
            return [primary];

        (double L, double CScale)[] steps = [(0.78, 0.7), (0.66, 0.95), (0.55, 1.0), (0.43, 0.9), (0.3, 0.6)];
        return [.. steps.Select(s => string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"oklch({s.L:0.##} {c * s.CScale:0.###} {h:0.#})"))];
    }
}

/// <summary>
/// The color preset registry behind the Themes page: the chip grid and the Get Code CSS tab.
/// The order here is DISPLAY order (the Themes dropdown reads it top to bottom) and is free to
/// change: preset codes encode the index in <see cref="Blaizio.Cli.Core.Styling.PresetCode.Presets"/>, which is append-only.
/// "nova" is the built-in default (no preset class; its values are the tokens-file defaults). Keep in sync with
/// src/Blaizio.Ui/Styles/preset-*.css - the css files are the source of truth, embedded into this
/// assembly by the csproj so <see cref="GetCss"/> can serve their text verbatim.
/// </summary>
public static class Presets
{
    public static readonly PresetEntry[] All =
    [
        new("nova", "Nova", "oklch(0.55 0.22 304)", "oklch(0.176 0.017 302)"),
        // Fonts deliberately default, like Nova's - Vesper restyles color only.
        new("vesper", "Vesper", "oklch(0.52 0.225 283)", "oklch(0.142 0.028 285)",
            PairedChart: "vesper"),
        new("nebula", "Nebula", "oklch(0.52 0.2 275)", "oklch(0.176 0.017 273)",
            PairedHeading: "manrope", PairedFont: "inter"),
        new("quasar", "Quasar", "oklch(0.52 0.19 245)", "oklch(0.176 0.017 243)",
            PairedHeading: "ibm-plex-sans", PairedFont: "public-sans"),
        new("comet", "Comet", "oklch(0.5 0.11 195)", "oklch(0.176 0.015 215)",
            PairedHeading: "figtree", PairedFont: "nunito-sans"),
        new("zenith", "Zenith", "oklch(0.5 0.13 155)", "oklch(0.176 0.014 170)",
            PairedHeading: "raleway", PairedFont: "figtree"),
        new("solstice", "Solstice", "oklch(0.73 0.12 75)", "oklch(0.176 0.014 70)",
            PairedHeading: "roboto-slab", PairedFont: "roboto"),
        new("meteor", "Meteor", "oklch(0.52 0.18 20)", "oklch(0.176 0.012 22)",
            PairedHeading: "montserrat", PairedFont: "dm-sans"),
        new("pulsar", "Pulsar", "oklch(0.54 0.2 350)", "oklch(0.176 0.013 348)",
            PairedHeading: "outfit", PairedFont: "instrument-sans"),
        new("eclipse", "Eclipse", "oklch(0.21 0.01 285)", "oklch(0.175 0.005 285)",
            PairedHeading: "geist", PairedFont: "geist"),
        new("polaris", "Polaris", "oklch(0.45 0.11 240)", "oklch(0.185 0.025 245)",
            PairedHeading: "space-grotesk", PairedFont: "geist", PairedChart: "polaris"),
        new("umbra", "Umbra", "oklch(0.24 0.01 60)", "oklch(0.205 0.006 60)",
            PairedHeading: "playfair-display", PairedFont: "source-sans-3", PairedChart: "umbra"),
        new("corona", "Corona", "oklch(0.43 0.08 80)", "oklch(0.168 0.012 60)",
            PairedHeading: "instrument-serif", PairedFont: "dm-sans", PairedChart: "corona"),
        new("magnetar", "Magnetar", "oklch(0.55 0.22 345)", "oklch(0.165 0.035 300)",
            PairedHeading: "oxanium", PairedFont: "outfit", PairedChart: "magnetar"),
        new("aurora", "Aurora", "oklch(0.42 0.12 145)", "oklch(0.145 0.02 145)",
            PairedHeading: "jetbrains-mono", PairedFont: "jetbrains-mono", PairedChart: "aurora"),
        new("equinox", "Equinox", "oklch(0.46 0.09 140)", "oklch(0.21 0.025 140)",
            PairedHeading: "lora", PairedFont: "nunito-sans", PairedChart: "equinox"),
    ];

    /// <summary>Find a preset by name; null when unknown.</summary>
    public static PresetEntry? Find(string? name) =>
        Array.Find(All, p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private static readonly ConcurrentDictionary<string, string> Cache = new();

    /// <summary>
    /// The preset's raw CSS text (the scoped token sheet a consumer ships; for "nova" the full
    /// theme.css the CLI writes). Embedded by the csproj from src/Blaizio.Ui/Styles.
    /// </summary>
    public static string GetCss(string name) => Cache.GetOrAdd(name, static n =>
    {
        var resource = n is "nova"
            ? "Blaizio.Docs.Presets.theme.css"
            : $"Blaizio.Docs.Presets.preset-{n}.css";
        using var stream = typeof(Presets).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"No embedded css for preset '{n}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');
    });
}
