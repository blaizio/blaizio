namespace Blaizio.Cli.Core.Dotnet;

/// <summary>Wraps the <c>dotnet</c> SDK commands the CLI needs (currently package installation).</summary>
public sealed class DotnetCli(string projectDir)
{
    /// <summary>
    /// Add a NuGet package to the project via <c>dotnet add package</c>. No-op-safe: the SDK is
    /// idempotent and simply updates the version if the package is already referenced.
    /// </summary>
    public Task<ProcessResult> AddPackageAsync(string packageId, CancellationToken ct = default)
        => ProcessRunner.RunAsync("dotnet", ["add", "package", packageId], projectDir, ct);

    /// <summary>Add several packages, stopping at the first failure and returning its result.</summary>
    public async Task<ProcessResult> AddPackagesAsync(
        IEnumerable<string> packageIds,
        CancellationToken ct = default)
    {
        ProcessResult last = new(0, string.Empty, string.Empty);
        foreach (var id in packageIds)
        {
            last = await AddPackageAsync(id, ct);
            if (!last.Success)
                return last;
        }
        return last;
    }
}
