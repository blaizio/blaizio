namespace Blaizio.Cli.Core.Operations;

/// <summary>
/// Tracks every file an <c>add</c> run is about to mutate so a failure can put the project back:
/// the first snapshot per path wins (original content, or recorded absence for a file the run
/// creates), and <see cref="RollbackFilesAsync"/> restores or deletes accordingly. NuGet ids the
/// run introduced are recorded separately so the caller can uninstall exactly them. Config is not
/// tracked here - it is only ever saved after every other mutation succeeded.
/// </summary>
internal sealed class AddTransaction
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // Absolute path -> original bytes, or null when the file did not exist before this run.
    private readonly Dictionary<string, byte[]?> _snapshots = new(PathComparer);
    private readonly List<string> _packages = [];

    /// <summary>NuGet ids this run introduced (pre-existing references are never recorded).</summary>
    public IReadOnlyList<string> IntroducedPackages => _packages;

    /// <summary>
    /// Snapshot <paramref name="absolutePath"/> before its first mutation. Later calls for the same
    /// path are no-ops, so the snapshot always holds the pre-run state.
    /// </summary>
    public void SnapshotFile(string absolutePath)
    {
        var full = Path.GetFullPath(absolutePath);
        if (_snapshots.ContainsKey(full))
            return;
        _snapshots[full] = File.Exists(full) ? File.ReadAllBytes(full) : null;
    }

    /// <summary>Record NuGet ids introduced by this run, for rollback.</summary>
    public void RecordPackages(IEnumerable<string> ids) => _packages.AddRange(ids);

    /// <summary>
    /// Restore every snapshotted file to its pre-run state: recreate originals (and their
    /// directories), delete files the run created. Best-effort per file - one unrestorable path
    /// must not stop the rest.
    /// </summary>
    public async Task<IReadOnlyList<string>> RollbackFilesAsync(CancellationToken ct = default)
    {
        var failures = new List<string>();
        foreach (var (path, original) in _snapshots)
        {
            try
            {
                if (original is null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, original, ct);
                }
            }
            catch (IOException)
            {
                failures.Add(path);
            }
            catch (UnauthorizedAccessException)
            {
                failures.Add(path);
            }
        }
        return failures;
    }
}
