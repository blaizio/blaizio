namespace Blaizio.Cli.Core.Styling;

/// <summary>One icon set behind the /themes "Icons" knob and the preset code's icons digit.</summary>
/// <param name="Name">Kebab-case id: the <see cref="PresetCode"/> value and the <c>icons</c> entry in blaizio.json.</param>
/// <param name="Title">Display label.</param>
/// <param name="Package">The NuGet package that ships the set.</param>
/// <param name="Class">The generated static class consumers reference (<c>Tabler</c>, <c>Lucide</c>...).</param>
/// <param name="License">The icon data's licence (the package code is always MIT).</param>
/// <param name="Home">The set's website.</param>
public sealed record IconSetDefinition(string Name, string Title, string Package, string Class, string License, string Home);

/// <summary>
/// The icon sets, in canonical order. APPEND-ONLY: the index is the preset code's icons digit,
/// so reordering or removing an entry breaks every previously shared code. <c>tabler</c> is
/// first and the default - the styled components draw from it, so every project carries it;
/// a preset naming another set installs that set's package on top.
/// </summary>
public static class IconSetCatalog
{
    public static readonly IconSetDefinition[] All =
    [
        new("tabler", "Tabler Icons", "Blaizio.Icons.Tabler", "Tabler", "MIT", "https://tabler.io/icons"),
        new("lucide", "Lucide", "Blaizio.Icons.Lucide", "Lucide", "ISC", "https://lucide.dev"),
        new("phosphor", "Phosphor Icons", "Blaizio.Icons.Phosphor", "Phosphor", "MIT", "https://phosphoricons.com"),
        new("remix", "Remix Icon", "Blaizio.Icons.Remix", "Remix", "Apache-2.0", "https://remixicon.com"),
        new("hugeicons", "Hugeicons", "Blaizio.Icons.HugeIcons", "HugeIcons", "MIT", "https://hugeicons.com"),
    ];

    /// <summary>The default set: what a code without an icons segment means.</summary>
    public const string Default = "tabler";

    /// <summary>The set with this <see cref="IconSetDefinition.Name"/>, or null.</summary>
    public static IconSetDefinition? Find(string? name) =>
        name is null ? null : Array.Find(All, s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
