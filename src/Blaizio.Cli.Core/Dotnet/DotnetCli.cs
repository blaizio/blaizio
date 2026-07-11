using System.Xml.Linq;

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
    /// <paramref name="progress"/> receives one message per package as it installs. Packages the
    /// csproj already satisfies are skipped without spawning the SDK: a pinned package when the
    /// reference carries that exact version, an unpinned one when any reference exists (bumping is
    /// <c>upgrade</c>'s job, and it pins).</summary>
    public async Task<ProcessResult> AddPackagesAsync(
        IEnumerable<(string Id, string? Version)> packages,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var list = packages as IReadOnlyList<(string Id, string? Version)> ?? [.. packages];
        var existing = ExistingReferences();
        ProcessResult last = new(0, string.Empty, string.Empty);
        for (var i = 0; i < list.Count; i++)
        {
            var (id, version) = list[i];
            if (existing.TryGetValue(id, out var referenced)
                && (version is null || string.Equals(referenced, version, StringComparison.OrdinalIgnoreCase)))
            {
                progress?.Report($"Skipping {id} (already referenced) ({i + 1}/{list.Count})...");
                continue;
            }
            progress?.Report($"Installing {id}{(version is null ? "" : $" {version}")} ({i + 1}/{list.Count})...");
            last = await AddPackageAsync(id, version, ct);
            if (!last.Success)
                return last;
        }
        return last;
    }

    /// <summary>
    /// The csproj's existing <c>PackageReference</c>s: id → pinned version (null for a versionless
    /// reference, e.g. central package management). Empty when no csproj exists or it fails to
    /// parse — every package then goes through the SDK as before.
    /// </summary>
    private Dictionary<string, string?> ExistingReferences()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (csproj is null)
            return result;

        try
        {
            var doc = XDocument.Load(csproj);
            foreach (var reference in doc.Descendants("PackageReference"))
            {
                var id = reference.Attribute("Include")?.Value?.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;
                var version = reference.Attribute("Version")?.Value?.Trim()
                    ?? reference.Element("Version")?.Value?.Trim();
                result[id] = version;
            }
        }
        catch (System.Xml.XmlException)
        {
            // Malformed csproj: fall back to installing everything rather than failing hard.
            result.Clear();
        }
        return result;
    }
}
