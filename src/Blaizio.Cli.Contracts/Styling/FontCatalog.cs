namespace Blaizio.Cli.Core.Styling;

/// <summary>Broad shape of a font option, for grouping and fallback stacks.</summary>
public enum FontKind
{
    /// <summary>A system stack: no webfont, nothing to load.</summary>
    System,

    /// <summary>A Google-served sans-serif webfont.</summary>
    Sans,

    /// <summary>A Google-served serif webfont.</summary>
    Serif,

    /// <summary>A Google-served monospace webfont.</summary>
    Mono,
}

/// <summary>
/// One font option behind the /create "Heading" and "Font" knobs.
/// </summary>
/// <param name="Name">Kebab-case id - the <c>font-*</c>/<c>heading-*</c> overlay class suffix and
/// the <see cref="PresetCode"/> digit value.</param>
/// <param name="Title">Display label (for webfonts, also the exact Google Fonts family name).</param>
/// <param name="Kind">Grouping/fallback shape; <see cref="FontKind.System"/> = no webfont.</param>
/// <param name="Stack">The full CSS <c>font-family</c> stack.</param>
/// <param name="Weights">The css2 <c>wght</c> tuple list requested for a webfont
/// (<c>;</c>-separated); null for system stacks.</param>
/// <param name="Offered">Whether pickers offer this option. Retired entries stay in the table so
/// previously shared preset codes keep decoding, but no new selection can produce them.</param>
public sealed record FontDefinition(string Name, string Title, FontKind Kind, string Stack, string? Weights = null, bool Offered = true)
{
    /// <summary>True when the font must be fetched from Google Fonts (a host <c>&lt;link&gt;</c> / css2 import).</summary>
    public bool IsWebFont => Kind is not FontKind.System;

    /// <summary>The css2 <c>family=</c> query fragment for this webfont, or null for system stacks.</summary>
    public string? FamilyQuery =>
        IsWebFont ? $"family={Title.Replace(' ', '+')}:wght@{Weights}" : null;
}

/// <summary>
/// The canonical font options - the single source behind <see cref="PresetCode.Fonts"/>, the CLI
/// font overlay (<c>TailwindSetup.EnsureFontsAsync</c> in Blaizio.Cli.Core) and the docs /create
/// knobs (both reference this assembly). The list is APPEND-ONLY: preset codes encode option
/// indices, so reordering or removing entries breaks every previously shared code. The first five
/// are the original system stacks; everything after is a Google Fonts webfont.
/// </summary>
public static class FontCatalog
{
    private const string SansFallback = "ui-sans-serif, system-ui, sans-serif";
    private const string SerifFallback = "Georgia, Cambria, \"Times New Roman\", serif";
    private const string MonoFallback = "ui-monospace, \"Cascadia Code\", Consolas, monospace";

    /// <summary>The weights requested for most webfonts: the range Tailwind's font-normal..bold utilities use.</summary>
    private const string DefaultWeights = "400;500;600;700";

    /// <summary>Every option, in canonical (append-only) order. Index 0 is the built-in default.</summary>
    public static readonly FontDefinition[] All =
    [
        // Index 0: the "don't customize" option - no overlay is written, the app's own font wins.
        new("default", "Default", FontKind.System, SansFallback),
        // The original four system stacks, RETIRED (a website renders on the visitor's machine, so
        // pinning "whatever this OS has" was never a real font choice). Kept un-offered so v2 codes
        // that encode these indices keep decoding to their original look.
        new("humanist", "Humanist", FontKind.System, "\"Segoe UI\", \"Helvetica Neue\", Helvetica, Arial, sans-serif", Offered: false),
        new("classic", "Classic Serif", FontKind.System, "Georgia, Cambria, \"Times New Roman\", serif", Offered: false),
        new("code", "Monospace", FontKind.System, "ui-monospace, \"Cascadia Code\", Consolas, \"SF Mono\", monospace", Offered: false),
        new("soft", "Rounded", FontKind.System, "ui-rounded, \"SF Pro Rounded\", \"Segoe UI\", system-ui, sans-serif", Offered: false),
        // Google sans faces.
        Web("geist", "Geist", FontKind.Sans),
        Web("inter", "Inter", FontKind.Sans),
        Web("noto-sans", "Noto Sans", FontKind.Sans),
        Web("nunito-sans", "Nunito Sans", FontKind.Sans),
        Web("figtree", "Figtree", FontKind.Sans),
        Web("roboto", "Roboto", FontKind.Sans),
        Web("raleway", "Raleway", FontKind.Sans),
        Web("dm-sans", "DM Sans", FontKind.Sans),
        Web("public-sans", "Public Sans", FontKind.Sans),
        Web("outfit", "Outfit", FontKind.Sans),
        Web("oxanium", "Oxanium", FontKind.Sans),
        Web("manrope", "Manrope", FontKind.Sans),
        Web("space-grotesk", "Space Grotesk", FontKind.Sans),
        Web("montserrat", "Montserrat", FontKind.Sans),
        Web("ibm-plex-sans", "IBM Plex Sans", FontKind.Sans),
        Web("source-sans-3", "Source Sans 3", FontKind.Sans),
        Web("instrument-sans", "Instrument Sans", FontKind.Sans),
        // Google mono faces.
        Web("jetbrains-mono", "JetBrains Mono", FontKind.Mono),
        Web("geist-mono", "Geist Mono", FontKind.Mono),
        // Google serif faces.
        Web("noto-serif", "Noto Serif", FontKind.Serif),
        Web("roboto-slab", "Roboto Slab", FontKind.Serif),
        Web("merriweather", "Merriweather", FontKind.Serif),
        Web("lora", "Lora", FontKind.Serif),
        Web("playfair-display", "Playfair Display", FontKind.Serif),
        Web("eb-garamond", "EB Garamond", FontKind.Serif),
        // Instrument Serif ships a single 400 weight - requesting more errors the css2 endpoint.
        Web("instrument-serif", "Instrument Serif", FontKind.Serif, "400"),
    ];

    /// <summary>Find an option by name (case-insensitive); null when unknown.</summary>
    public static FontDefinition? Find(string? name) =>
        Array.Find(All, f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The CSS font-family stack for a font name, or null for <c>"default"</c> (or any unknown
    /// value) - the built-in stack, which needs no overlay.
    /// </summary>
    public static string? Stack(string? name) =>
        Find(name) is { Name: not "default" } f ? f.Stack : null;

    /// <summary>
    /// The Google Fonts css2 stylesheet URL loading the given selections' webfonts (deduplicated,
    /// one combined request), or null when neither needs one.
    /// </summary>
    public static string? CssUrl(params string?[] names)
    {
        var families = names
            .Select(Find)
            .Where(f => f is { IsWebFont: true })
            .Select(f => f!.FamilyQuery!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return families.Length == 0
            ? null
            : $"https://fonts.googleapis.com/css2?{string.Join("&", families)}&display=swap";
    }

    private static FontDefinition Web(string name, string title, FontKind kind, string weights = DefaultWeights) =>
        new(name, title, kind, $"\"{title}\", {Fallback(kind)}", weights);

    private static string Fallback(FontKind kind) => kind switch
    {
        FontKind.Serif => SerifFallback,
        FontKind.Mono => MonoFallback,
        _ => SansFallback,
    };
}
