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
    public const string TailwindMerge = "1.5.0";

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

    /// <summary>
    /// The syntax highlighter and its extra languages, versioned with Blaizio.Base. Never installed
    /// by init (the code-block registry item brings Blaizio.Syntax; Blaizio.Syntax.Languages is the
    /// app's own choice), but moved by an update in lockstep with the base set when referenced:
    /// Blaizio.Syntax needs the Blaizio.Base of its own version.
    /// </summary>
    public static readonly (string Id, string? Version)[] Syntax =
    [
        ("Blaizio.Syntax", Blaizio),
        ("Blaizio.Syntax.Languages", Blaizio),
    ];

    /// <summary>The packages an update moves only when the csproj already references them: the icon sets and the syntax packages.</summary>
    public static readonly (string Id, string? Version)[] Optional = [.. IconSets, .. Syntax];

    /// <summary>Every package this tool versions: the base set plus the optional ones.</summary>
    public static readonly (string Id, string? Version)[] All = [.. BaseSet, .. Optional];

    /// <summary>The base set for a project whose components draw from <paramref name="icons"/>:
    /// that set's package stands in for Tabler's (the glyph file is retargeted on install, so
    /// nothing the CLI copies needs Tabler). Null or <c>tabler</c> is the plain base set.</summary>
    public static (string Id, string? Version)[] BaseSetFor(string? icons) =>
        IconSetPackages.Substitute(BaseSet, icons);

    /// <summary>
    /// What an update pins for a CLI-managed project: the whole base set (an update may introduce
    /// a missing base package) plus whichever optional packages <paramref name="referenced"/>
    /// already names - an icon set or a syntax package is only ever moved, never introduced.
    /// </summary>
    public static (string Id, string? Version)[] ForUpdate(IReadOnlySet<string> referenced, string? icons = null)
    {
        var baseSet = BaseSetFor(icons);
        return [.. baseSet, .. Optional.Where(p => referenced.Contains(p.Id) && !baseSet.Any(b => b.Id == p.Id))];
    }
}
