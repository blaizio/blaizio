namespace Blaizio.Docs.Services;

/// <summary>One icon set the docs browse: the package that ships it and how it is licensed.</summary>
/// <param name="Id">The browser's key and the JSON file prefix (<c>tabler</c>, <c>lucide</c>...).</param>
/// <param name="Class">The generated static class consumers reference (<c>Icons</c>, <c>Lucide</c>...).</param>
/// <param name="Label">The set's own name.</param>
/// <param name="Package">The NuGet package id.</param>
/// <param name="License">The icon data's licence (the package code is always MIT).</param>
/// <param name="Home">The set's website.</param>
public sealed record IconSet(string Id, string Class, string Label, string Package, string License, string Home);

/// <summary>
/// The five icon sets, in the order the browser lists them. What the generated code cannot say:
/// package ids, licences, websites. Everything else about a set (its families, their paint, grid
/// and stroke, every icon) comes from the build-generated <c>wwwroot/icons/*.json</c>
/// (the <c>BlaizioIconsJson</c> task), so this list only has to name the sets.
/// </summary>
public static class IconSets
{
    public static readonly IconSet[] All =
    [
        new("tabler", "Tabler", "Tabler Icons", "Blaizio.Icons.Tabler", "MIT", "https://tabler.io/icons"),
        new("lucide", "Lucide", "Lucide", "Blaizio.Icons.Lucide", "ISC", "https://lucide.dev"),
        new("phosphor", "Phosphor", "Phosphor Icons", "Blaizio.Icons.Phosphor", "MIT", "https://phosphoricons.com"),
        new("remix", "Remix", "Remix Icon", "Blaizio.Icons.Remix", "Apache-2.0", "https://remixicon.com"),
        new("hugeicons", "HugeIcons", "Hugeicons", "Blaizio.Icons.HugeIcons", "MIT", "https://hugeicons.com"),
    ];

    /// <summary>The set whose generated class is <paramref name="cls"/>, or null.</summary>
    public static IconSet? ByClass(string cls) => Array.Find(All, s => s.Class == cls);
}
