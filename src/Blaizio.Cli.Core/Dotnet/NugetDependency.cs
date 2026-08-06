namespace Blaizio.Cli.Core.Dotnet;

/// <summary>
/// One NuGet dependency as a registry item declares it: a bare package id floats to the latest
/// version, <c>Id@Version</c> pins one. The <c>@</c> separator is safe - NuGet ids allow letters,
/// digits, <c>.</c>, <c>_</c> and <c>-</c> only.
/// </summary>
public sealed record NugetDependency(string Id, string? Version = null)
{
    /// <summary>Parse a <c>nugetDependencies</c> entry (<c>Id</c> or <c>Id@Version</c>).</summary>
    public static NugetDependency Parse(string reference)
    {
        var at = reference.LastIndexOf('@');
        if (at < 0)
            return new NugetDependency(reference);
        if (at == 0 || at == reference.Length - 1)
            throw new InvalidOperationException(
                $"Invalid NuGet dependency '{reference}': expected a package id, or id@version to pin one.");
        return new NugetDependency(reference[..at], reference[(at + 1)..]);
    }

    /// <summary>The wire form back: <c>Id</c> or <c>Id@Version</c>.</summary>
    public override string ToString() => Version is null ? Id : $"{Id}@{Version}";
}
