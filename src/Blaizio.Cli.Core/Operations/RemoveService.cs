using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core.Operations;

/// <summary>Options controlling a single <c>remove</c> run.</summary>
public sealed class RemoveRequest
{
    /// <summary>Item names to remove. Resolved against the installed record, case- and separator-insensitively.</summary>
    public required IReadOnlyList<string> Components { get; init; }

    /// <summary>Report what would go without touching anything.</summary>
    public bool DryRun { get; init; }

    /// <summary>Remove even when another installed component depends on the item.</summary>
    public bool Force { get; init; }
}

/// <summary>The outcome of a <c>remove</c> run.</summary>
public sealed class RemoveResult
{
    /// <summary>Item names actually removed from the record.</summary>
    public required IReadOnlyList<string> Items { get; init; }

    /// <summary>Files deleted (project-relative, POSIX).</summary>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>Names passed that are not installed (nothing was done for them).</summary>
    public required IReadOnlyList<string> NotInstalled { get; init; }

    /// <summary>
    /// Blocking dependents, keyed by the item that could not be removed: the installed components
    /// that still declare it as a dependency. Empty once <see cref="RemoveRequest.Force"/> is set.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Blocked { get; init; }

    /// <summary>
    /// Components left installed that nothing depends on anymore and that were pulled in only as
    /// dependencies of what just went. Reported, never removed - a hint, not an action.
    /// </summary>
    public required IReadOnlyList<string> Orphaned { get; init; }

    /// <summary>
    /// Recorded NuGet packages no remaining component declares. Reported, never uninstalled: the
    /// project's own code may use them, and the wiring (boot.js, the icon set) needs the core ones.
    /// </summary>
    public required IReadOnlyList<string> UnusedPackages { get; init; }

    /// <summary>True when this was a preview: nothing was actually touched.</summary>
    public required bool DryRun { get; init; }

    /// <summary>True when nothing matched and nothing was blocked - there was nothing to do.</summary>
    public bool NothingToDo => Items.Count == 0 && Blocked.Count == 0;
}

/// <summary>
/// Removes individual components the way <see cref="UninstallService"/> removes everything: strictly
/// by record. Each item's files are the ones <c>add</c> wrote for it (<c>blaizio.json</c>
/// <c>installed</c>), so files the user authored under the output directory are never swept up, and
/// a file two items share survives while either is installed.
/// <para>
/// Removal refuses to break the project: an item another installed component depends on is blocked
/// (overridable with <see cref="RemoveRequest.Force"/>), and NuGet packages are never uninstalled -
/// they are only reported once nothing declares them. The wiring, the tokens file and the config
/// itself stay; tearing all of that down is <c>uninstall</c>'s job.
/// </para>
/// </summary>
public sealed class RemoveService(IRegistryClient registry)
{
    /// <summary>Run the removal for <paramref name="projectDir"/>.</summary>
    public async Task<RemoveResult> RunAsync(
        string projectDir,
        RemoveRequest request,
        CancellationToken ct = default)
    {
        var config = await ConfigStore.RequireAsync(projectDir, ct);

        // Resolve the names the user typed against what is actually installed, forgiving case and
        // separators the same way the registry lookup does (`inputnumber` -> `input-number`).
        var targets = new List<string>();
        var notInstalled = new List<string>();
        foreach (var name in request.Components)
        {
            var match = Match(config.Installed.Keys, name);
            if (match is null)
                notInstalled.Add(name);
            else if (!targets.Contains(match, StringComparer.Ordinal))
                targets.Add(match);
        }

        // Dependency guard, from the registry index: removing something a survivor needs would
        // leave the project uncompilable. An unreachable registry means no index to check - the
        // guard degrades to "no known dependents" rather than blocking the whole command.
        var index = await TryIndexAsync(ct);
        var dependencies = index.ToDictionary(
            item => item.Name,
            item => (IReadOnlyList<string>)item.RegistryDependencies,
            StringComparer.OrdinalIgnoreCase);

        var blocked = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (!request.Force)
        {
            foreach (var target in targets)
            {
                // Survivors only: an item also being removed in this run is not a reason to stop.
                var dependents = config.Installed.Keys
                    .Where(installed => !targets.Contains(installed, StringComparer.Ordinal))
                    .Where(installed => dependencies.TryGetValue(installed, out var deps)
                        && deps.Contains(target, StringComparer.OrdinalIgnoreCase))
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                if (dependents.Length > 0)
                    blocked[target] = dependents;
            }

            targets.RemoveAll(blocked.ContainsKey);
        }

        // The files to delete: an item's recorded files minus any a surviving item also ships, so
        // two components sharing a file don't take it away from each other.
        var survivorFiles = config.Installed
            .Where(entry => !targets.Contains(entry.Key, StringComparer.Ordinal))
            .SelectMany(entry => entry.Value.Files)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removed = new List<string>();
        foreach (var target in targets)
        {
            foreach (var file in config.Installed[target].Files)
            {
                if (survivorFiles.Contains(file))
                    continue;
                var relative = Path.Combine(config.Output, file);
                var abs = Path.Combine(projectDir, relative);
                if (!File.Exists(abs))
                    continue;
                if (!request.DryRun)
                    File.Delete(abs);
                removed.Add(ToPosix(relative));
            }
        }

        // What the removal leaves behind, reported for the user to act on: components that were
        // only ever pulled in as dependencies of what just went, and recorded packages nothing
        // declares anymore. Neither is touched - guessing wrong here breaks a build.
        var remaining = config.Installed.Keys
            .Where(name => !targets.Contains(name, StringComparer.Ordinal))
            .ToArray();
        var stillNeeded = remaining
            .SelectMany(name => dependencies.TryGetValue(name, out var deps) ? deps : [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wasDependency = targets
            .SelectMany(name => dependencies.TryGetValue(name, out var deps) ? deps : [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphaned = remaining
            .Where(name => wasDependency.Contains(name) && !stillNeeded.Contains(name))
            .Where(name => !request.Components.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var packagesInUse = remaining
            .Select(name => index.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Where(item => item is not null)
            .SelectMany(item => item!.NugetDependencies)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unusedPackages = index.Count == 0
            ? []
            : config.Packages.Where(id => !packagesInUse.Contains(id)).Order(StringComparer.Ordinal).ToArray();

        if (!request.DryRun && targets.Count > 0)
        {
            foreach (var target in targets)
                config.Installed.Remove(target);
            await ConfigStore.SaveAsync(projectDir, config, ct);
            PruneEmptyDirectories(Path.Combine(projectDir, config.Output));
        }

        return new RemoveResult
        {
            Items = targets,
            Removed = removed,
            NotInstalled = notInstalled,
            Blocked = blocked,
            Orphaned = orphaned,
            UnusedPackages = unusedPackages,
            DryRun = request.DryRun,
        };
    }

    /// <summary>The installed key matching <paramref name="name"/>, ignoring case and separators.</summary>
    private static string? Match(IEnumerable<string> installed, string name)
    {
        var wanted = Strip(name);
        return installed.FirstOrDefault(key => string.Equals(Strip(key), wanted, StringComparison.OrdinalIgnoreCase));
    }

    // '@' joins the separators so `acme/button` finds the recorded `@acme/button`.
    private static string Strip(string value) => value.Replace("-", "").Replace("_", "").Replace("@", "");

    /// <summary>The registry index, or an empty list when the registry cannot be reached.</summary>
    private async Task<IReadOnlyList<RegistryItem>> TryIndexAsync(CancellationToken ct)
    {
        try
        {
            return (await registry.GetIndexAsync(ct)).Items;
        }
        catch (RegistryException)
        {
            return [];
        }
    }

    /// <summary>Delete directories the removals emptied, deepest first; the output dir itself stays.</summary>
    private static void PruneEmptyDirectories(string outputAbs)
    {
        if (!Directory.Exists(outputAbs))
            return;
        foreach (var dir in Directory.EnumerateDirectories(outputAbs, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
