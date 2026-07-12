using System.Collections.Concurrent;

namespace Blaizio.Docs;

/// <summary>
/// One color preset: its class-name suffix (<c>preset-{Name}</c> on <c>&lt;html&gt;</c>), display
/// label, and the two swatch colors the /create rail paints its chip with (raw CSS color strings -
/// the light primary and the dark background, so the chip previews both modes at a glance).
/// </summary>
public sealed record PresetEntry(string Name, string Label, string SwatchPrimary, string SwatchDark)
{
    /// <summary>
    /// A five-step single-hue ramp derived from <see cref="SwatchPrimary"/> - the /create Color row
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
/// The color preset registry behind the /create page: the chip grid and the Get Code CSS tab.
/// "nova" is the built-in default (no preset class; tokens come from theme.css). Keep in sync with
/// src/Blaizio.Ui/Styles/preset-*.css - the css files are the source of truth, embedded into this
/// assembly by the csproj so <see cref="GetCss"/> can serve their text verbatim.
/// </summary>
public static class Presets
{
    public static readonly PresetEntry[] All =
    [
        new("nova", "Nova", "oklch(0.55 0.22 304)", "oklch(0.176 0.017 302)"),
        new("nebula", "Nebula", "oklch(0.52 0.2 275)", "oklch(0.176 0.017 273)"),
        new("quasar", "Quasar", "oklch(0.52 0.19 245)", "oklch(0.176 0.017 243)"),
        new("comet", "Comet", "oklch(0.5 0.11 195)", "oklch(0.176 0.015 215)"),
        new("zenith", "Zenith", "oklch(0.5 0.13 155)", "oklch(0.176 0.014 170)"),
        new("solstice", "Solstice", "oklch(0.73 0.12 75)", "oklch(0.176 0.014 70)"),
        new("meteor", "Meteor", "oklch(0.52 0.18 20)", "oklch(0.176 0.012 22)"),
        new("pulsar", "Pulsar", "oklch(0.54 0.2 350)", "oklch(0.176 0.013 348)"),
        new("eclipse", "Eclipse", "oklch(0.21 0.01 285)", "oklch(0.175 0.005 285)"),
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
