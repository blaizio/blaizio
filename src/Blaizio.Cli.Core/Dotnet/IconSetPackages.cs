using Blaizio.Cli.Core.Styling;

namespace Blaizio.Cli.Core.Dotnet;

/// <summary>
/// The one substitution the icon-set choice makes in a package list: the default set's package
/// (<c>Blaizio.Icons.Tabler</c>, which every registry item declares because the glyph file ships
/// pointed at Tabler) becomes the recorded set's package. Everything else passes through.
/// </summary>
public static class IconSetPackages
{
    /// <summary>The package of the default set - the id the substitution replaces.</summary>
    public static string DefaultPackage => IconSetCatalog.Find(IconSetCatalog.Default)!.Package;

    /// <summary>The package <paramref name="icons"/> maps to, or null when it is the default set,
    /// unset, or unknown - the cases where the list is left alone.</summary>
    public static string? Replacement(string? icons) =>
        icons is not null
        && !string.Equals(icons, IconSetCatalog.Default, StringComparison.OrdinalIgnoreCase)
        && IconSetCatalog.Find(icons) is { } set
            ? set.Package
            : null;

    /// <summary>Whether <paramref name="id"/> is the default set's package.</summary>
    public static bool IsDefaultPackage(string id) =>
        string.Equals(id, DefaultPackage, StringComparison.OrdinalIgnoreCase);

    /// <summary><paramref name="packages"/> with the default set's package swapped for the
    /// recorded set's, same version; unchanged when there is nothing to swap.</summary>
    public static IReadOnlyList<NugetDependency> Substitute(IEnumerable<NugetDependency> packages, string? icons)
    {
        var replacement = Replacement(icons);
        return replacement is null
            ? [.. packages]
            : [.. packages.Select(p => IsDefaultPackage(p.Id) ? p with { Id = replacement } : p)];
    }

    /// <summary>The tuple-shaped twin of <see cref="Substitute(IEnumerable{NugetDependency}, string?)"/>.</summary>
    public static (string Id, string? Version)[] Substitute(IEnumerable<(string Id, string? Version)> packages, string? icons)
    {
        var replacement = Replacement(icons);
        return replacement is null
            ? [.. packages]
            : [.. packages.Select(p => IsDefaultPackage(p.Id) ? (replacement, p.Version) : p)];
    }
}
