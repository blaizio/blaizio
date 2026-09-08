using Blaizio.Cli.Core.Dotnet;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The NuGet versions this tool release installs and upgrades to. One place so <c>init</c>,
/// <c>upgrade</c> and the scaffolded csproj never drift apart.
/// </summary>
internal static class PackageVersions
{
    /// <summary>
    /// Version of the Blaizio.Base / Blaizio.Icons packages. Generated from
    /// <c>BlaizioVersionBase</c> (Directory.Build.props) at build time, so the version this tool
    /// writes into a project is the version the repo packs - never a stale literal. Dogfood
    /// revisions are deliberately not carried here: they only ever exist in the local feed.
    /// </summary>
    public const string Blaizio = BuildVersions.Blaizio;

    /// <summary>Version of TailwindMerge.NET.</summary>
    public const string TailwindMerge = "1.4.0";

    /// <summary>The base package set every Blaizio project references.</summary>
    public static readonly (string Id, string? Version)[] BaseSet =
    [
        ("Blaizio.Base", Blaizio),
        ("Blaizio.Icons", Blaizio),
        ("Blaizio.Icons.Tabler", Blaizio),
        ("TailwindMerge.NET", TailwindMerge),
    ];

    /// <summary>
    /// The optional icon set packages, versioned with Blaizio.Icons. Never installed by init (a
    /// project picks the sets it wants), but an update moves the ones a csproj references in
    /// lockstep with the base set - Blaizio.Icons and a set at different versions is exactly the
    /// split the shared version exists to prevent.
    /// </summary>
    public static readonly (string Id, string? Version)[] IconSets =
    [
        ("Blaizio.Icons.Lucide", Blaizio),
        ("Blaizio.Icons.Phosphor", Blaizio),
        ("Blaizio.Icons.Remix", Blaizio),
        ("Blaizio.Icons.HugeIcons", Blaizio),
    ];

    /// <summary>Every package this tool versions: the base set plus the icon sets.</summary>
    public static readonly (string Id, string? Version)[] All = [.. BaseSet, .. IconSets];

    /// <summary>The base set for a project whose components draw from <paramref name="icons"/>:
    /// that set's package stands in for Tabler's (the glyph file is retargeted on install, so
    /// nothing the CLI copies needs Tabler). Null or <c>tabler</c> is the plain base set.</summary>
    public static (string Id, string? Version)[] BaseSetFor(string? icons) =>
        IconSetPackages.Substitute(BaseSet, icons);

    /// <summary>
    /// What an update pins for a CLI-managed project: the whole base set (an update may introduce
    /// a missing base package) plus whichever icon sets <paramref name="referenced"/> already
    /// names - an icon set is only ever moved, never introduced.
    /// </summary>
    public static (string Id, string? Version)[] ForUpdate(IReadOnlySet<string> referenced, string? icons = null)
    {
        var baseSet = BaseSetFor(icons);
        return [.. baseSet, .. IconSets.Where(p => referenced.Contains(p.Id) && !baseSet.Any(b => b.Id == p.Id))];
    }
}
