using System.Text.RegularExpressions;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Rewriting;
using Blaizio.Cli.Core.Styling;

namespace Blaizio.Cli.Core.Dotnet;

/// <summary>
/// The one substitution the icon-set choice makes in a package list: the default set's package
/// (<c>Blaizio.Icons.Tabler</c>, which every registry item declares because the glyph file ships
/// pointed at Tabler) becomes the recorded set's package. Everything else passes through.
/// </summary>
public static partial class IconSetPackages
{
    /// <summary>
    /// Whether any of <paramref name="items"/> draws the default set directly - a
    /// <c>Tabler.Family.Icon</c> in code (comments stripped), outside the glyph file, which is
    /// retargeted anyway. A third-party item that names Tabler needs Tabler's package whatever
    /// set the project picked; the substitution must not take it away.
    /// </summary>
    public static bool DrawsDefaultSet(IEnumerable<RegistryItem> items) =>
        items.SelectMany(i => i.Files)
            .Where(f => f.Content is not null && !GlyphRewriter.IsGlyphFile(f.Path))
            .Any(f => DefaultSetToken().IsMatch(CommentToken().Replace(f.Content!, " ")));

    [GeneratedRegex(@"\bTabler\.\w+\.\w+")]
    private static partial Regex DefaultSetToken();

    // Razor, C#, HTML and line comments - the same strip the registry generator uses to keep
    // prose ("see Tabler.Outline.HandStop") from counting as a reference.
    [GeneratedRegex(@"@\*.*?\*@|/\*.*?\*/|<!--.*?-->|(?<!:)//[^\r\n]*", RegexOptions.Singleline)]
    private static partial Regex CommentToken();

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
    /// recorded set's, same version; unchanged when there is nothing to swap. With
    /// <paramref name="keepDefault"/> (something installed draws Tabler directly - see
    /// <see cref="DrawsDefaultSet"/>) the default set's package stays in the list beside the
    /// recorded set's.</summary>
    public static IReadOnlyList<NugetDependency> Substitute(IEnumerable<NugetDependency> packages, string? icons, bool keepDefault = false)
    {
        var replacement = Replacement(icons);
        if (replacement is null)
            return [.. packages];
        var list = packages.ToList();
        var swapped = list.Select(p => IsDefaultPackage(p.Id) ? p with { Id = replacement } : p).ToList();
        if (keepDefault && list.FirstOrDefault(p => IsDefaultPackage(p.Id)) is { } tabler
            && !swapped.Any(p => IsDefaultPackage(p.Id)))
            swapped.Add(tabler);
        return swapped;
    }

    /// <summary>The tuple-shaped twin of <see cref="Substitute(IEnumerable{NugetDependency}, string?, bool)"/>.</summary>
    public static (string Id, string? Version)[] Substitute(IEnumerable<(string Id, string? Version)> packages, string? icons)
    {
        var replacement = Replacement(icons);
        return replacement is null
            ? [.. packages]
            : [.. packages.Select(p => IsDefaultPackage(p.Id) ? (replacement, p.Version) : p)];
    }
}
