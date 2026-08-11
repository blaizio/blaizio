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
        ("TailwindMerge.NET", TailwindMerge),
    ];
}
