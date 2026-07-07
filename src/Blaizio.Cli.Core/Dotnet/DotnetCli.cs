namespace Blaizio.Cli.Core.Dotnet;

/// <summary>Wraps the <c>dotnet</c> SDK commands the CLI needs (currently package installation).</summary>
public sealed class DotnetCli(string projectDir)
{
    /// <summary>
    /// Add a NuGet package to the project via <c>dotnet add package</c>. Pins <paramref name="version"/>
    /// when given; otherwise resolves the latest version including prereleases (the Blaizio packages
    /// ship prerelease). No-op-safe: the SDK is idempotent and simply updates the version if the
    /// package is already referenced.
    /// </summary>
    public Task<ProcessResult> AddPackageAsync(string packageId, string? version = null, CancellationToken ct = default)
    {
        string[] args = version is null
            ? ["add", "package", packageId, "--prerelease"]
            : ["add", "package", packageId, "--version", version];
        return ProcessRunner.RunAsync("dotnet", args, projectDir, ct);
    }

    /// <summary>Add several packages, stopping at the first failure and returning its result.</summary>
    public Task<ProcessResult> AddPackagesAsync(
        IEnumerable<string> packageIds,
        CancellationToken ct = default)
        => AddPackagesAsync(packageIds.Select(id => (id, (string?)null)), ct);

    /// <summary>Add several packages with pinned versions, stopping at the first failure.</summary>
    public async Task<ProcessResult> AddPackagesAsync(
        IEnumerable<(string Id, string? Version)> packages,
        CancellationToken ct = default)
    {
        ProcessResult last = new(0, string.Empty, string.Empty);
        foreach (var (id, version) in packages)
        {
            last = await AddPackageAsync(id, version, ct);
            if (!last.Success)
                return last;
        }
        return last;
    }
}
