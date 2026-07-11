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

    /// <summary>Add several packages, stopping at the first failure and returning its result.
    /// <paramref name="progress"/> receives one message per package as it installs.</summary>
    public Task<ProcessResult> AddPackagesAsync(
        IEnumerable<string> packageIds,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => AddPackagesAsync(packageIds.Select(id => (id, (string?)null)), progress, ct);

    /// <summary>Remove a NuGet package reference via <c>dotnet remove package</c>.</summary>
    public Task<ProcessResult> RemovePackageAsync(string packageId, CancellationToken ct = default)
        => ProcessRunner.RunAsync("dotnet", ["remove", "package", packageId], projectDir, ct);

    /// <summary>Add several packages with pinned versions, stopping at the first failure.
    /// <paramref name="progress"/> receives one message per package as it installs.</summary>
    public async Task<ProcessResult> AddPackagesAsync(
        IEnumerable<(string Id, string? Version)> packages,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var list = packages as IReadOnlyList<(string Id, string? Version)> ?? [.. packages];
        ProcessResult last = new(0, string.Empty, string.Empty);
        for (var i = 0; i < list.Count; i++)
        {
            var (id, version) = list[i];
            progress?.Report($"Installing {id}{(version is null ? "" : $" {version}")} ({i + 1}/{list.Count})...");
            last = await AddPackageAsync(id, version, ct);
            if (!last.Success)
                return last;
        }
        return last;
    }
}
