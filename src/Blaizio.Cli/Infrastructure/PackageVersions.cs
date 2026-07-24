namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The NuGet versions this tool release installs and upgrades to. One place so <c>init</c>,
/// <c>upgrade</c> and the scaffolded csproj never drift apart.
/// </summary>
internal static class PackageVersions
{
    /// <summary>Version of the Blaizio.Base / Blaizio.Icons packages.</summary>
    public const string Blaizio = "0.1.0-alpha.7";

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
