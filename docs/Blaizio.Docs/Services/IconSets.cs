using Blaizio.Cli.Core.Styling;

namespace Blaizio.Docs.Services;

/// <summary>One icon set the docs browse: the package that ships it and how it is licensed.</summary>
/// <param name="Id">The browser's key and the JSON file prefix (<c>tabler</c>, <c>lucide</c>...).</param>
/// <param name="Class">The generated static class consumers reference (<c>Tabler</c>, <c>Lucide</c>...).</param>
/// <param name="Label">The set's own name.</param>
/// <param name="Package">The NuGet package id.</param>
/// <param name="License">The icon data's licence (the package code is always MIT).</param>
/// <param name="Home">The set's website.</param>
public sealed record IconSet(string Id, string Class, string Label, string Package, string License, string Home);

/// <summary>
/// The icon sets the docs browse, in the order the browser lists them - projected from the CLI
/// contract's <see cref="IconSetCatalog"/>, the one canonical list (it is also the preset code's
/// icons digit). Everything else about a set (its families, their paint, grid and stroke, every
/// icon) comes from the build-generated <c>wwwroot/icons/*.json</c> (the <c>BlaizioIconsJson</c>
/// task).
/// </summary>
public static class IconSets
{
    public static readonly IconSet[] All =
        [.. IconSetCatalog.All.Select(s => new IconSet(s.Name, s.Class, s.Title, s.Package, s.License, s.Home))];

    /// <summary>The set whose generated class is <paramref name="cls"/>, or null.</summary>
    public static IconSet? ByClass(string cls) => Array.Find(All, s => s.Class == cls);
}
