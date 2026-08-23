using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core.Resolution;

/// <summary>
/// Walks <see cref="RegistryItem.RegistryDependencies"/> transitively from a set of requested
/// items and returns them in install order (a dependency always precedes anything that needs it).
/// Cycles and diamonds are handled: each item is emitted exactly once, after its dependencies.
/// </summary>
/// <remarks>
/// <paramref name="pins"/> maps installed item names to their recorded version pins. A DEPENDENCY
/// that lands on a pinned name is fetched at that pin, so pulling item B never silently floats
/// its pinned dependency A. Requested references are exempt: what the user names on the command
/// line says exactly what it means - <c>add button</c> floats (and unpins), <c>add button@1.2.0</c>
/// pins.
/// </remarks>
public sealed class DependencyResolver(
    IRegistryClient client,
    IReadOnlyDictionary<string, string>? pins = null)
{
    /// <summary>
    /// Resolve the full graph for the requested item names/URLs/paths. With
    /// <paramref name="skipMissing"/>, a requested reference that is not found - or whose own
    /// dependencies are not - is left out and reported in <see cref="ResolvedGraph.Skipped"/>
    /// instead of failing the whole resolution; everything else still resolves.
    /// </summary>
    public async Task<ResolvedGraph> ResolveAsync(
        IReadOnlyList<string> requested,
        CancellationToken ct = default,
        bool skipMissing = false)
    {
        var ordered = new List<RegistryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fetched = new Dictionary<string, RegistryItem>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<SkippedItem>();

        foreach (var reference in requested)
        {
            try
            {
                await VisitAsync(reference, ordered, seen, fetched, isRequested: true, origin: null, ct);
            }
            catch (RegistryException ex) when (skipMissing && ex.Reason is RegistryFailure.NotFound)
            {
                // Not found anywhere it could be: the run goes on without it, and the caller says
                // so. An unreachable registry is not this - that fails, as it should.
                skipped.Add(new SkippedItem(reference, ex.Message));
            }
        }

        var nuget = ReconcileNuget(ordered, i => i.NugetDependencies);
        var dev = ReconcileNuget(ordered, i => i.DevDependencies ?? []);

        // A package some item needs at runtime is a runtime package, whatever another item says -
        // the dev list keeps only what NO item ships as a regular dependency. Disagreeing pins
        // across the two lists are still a conflict, not a silent pick.
        var runtimeIds = nuget.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var dep in dev)
        {
            if (runtimeIds.TryGetValue(dep.Id, out var runtime)
                && dep.Version is not null && runtime.Version is not null
                && !string.Equals(dep.Version, runtime.Version, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Conflicting NuGet pins for '{dep.Id}': {runtime.Version} as a dependency, " +
                    $"{dep.Version} as a devDependency. The registry items disagree - report this upstream.");
            }
        }
        dev.RemoveAll(d => runtimeIds.ContainsKey(d.Id));

        return new ResolvedGraph
        {
            Items = ordered,
            NugetPackages = nuget,
            DevNugetPackages = dev,
            Requested = [.. requested],
            Skipped = skipped,
        };
    }

    /// <summary>
    /// The distinct NuGet dependencies across the graph, one entry per package id. A pinned
    /// declaration (<c>Id@Version</c>) beats a floating one for the same id; two items pinning
    /// DIFFERENT versions is a registry authoring error worth failing loudly on - installing
    /// them in sequence would silently leave whichever ran last.
    /// </summary>
    private static List<Dotnet.NugetDependency> ReconcileNuget(
        List<RegistryItem> ordered, Func<RegistryItem, IReadOnlyList<string>> select)
    {
        var byId = new Dictionary<string, (Dotnet.NugetDependency Dep, string From)>(StringComparer.OrdinalIgnoreCase);
        var ids = new List<string>(); // first-seen order; the dictionary's is not contractual
        foreach (var item in ordered)
        {
            foreach (var raw in select(item) ?? [])
            {
                var dep = Dotnet.NugetDependency.Parse(raw);
                if (!byId.TryGetValue(dep.Id, out var existing))
                {
                    byId[dep.Id] = (dep, item.QualifiedName);
                    ids.Add(dep.Id);
                    continue;
                }
                if (dep.Version is null || string.Equals(existing.Dep.Version, dep.Version, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (existing.Dep.Version is null)
                {
                    byId[dep.Id] = (dep, item.QualifiedName);
                    continue;
                }
                throw new InvalidOperationException(
                    $"Conflicting NuGet pins for '{dep.Id}': '{existing.From}' wants {existing.Dep.Version}, " +
                    $"'{item.QualifiedName}' wants {dep.Version}. The registry items disagree - report this upstream.");
            }
        }
        return [.. ids.Select(id => byId[id].Dep)];
    }

    /// <summary>
    /// What a reference was before this resolver rewrote it: the plain dependency name, the
    /// namespace it was claimed by, and the item that declared it. Carried only so a failure to
    /// fetch can say WHY it looked where it did - a plain name inside a namespaced item is the one
    /// rewrite a registry author never wrote and cannot see in the address that failed.
    /// </summary>
    private readonly record struct DependencyOrigin(string Name, string Namespace, string Parent);

    private async Task VisitAsync(
        string reference,
        List<RegistryItem> ordered,
        HashSet<string> seen,
        Dictionary<string, RegistryItem> fetched,
        bool isRequested,
        DependencyOrigin? origin,
        CancellationToken ct)
    {
        // A dependency reference landing on a pinned installed name is re-pointed at its pin, so
        // the files fetched and the record written stay at the version the user chose.
        if (!isRequested
            && pins is { Count: > 0 }
            && !ItemReference.TrySplitVersion(reference, out _, out _)
            && pins.TryGetValue(reference, out var pin))
        {
            reference = $"{reference}@{pin}";
        }

        if (!fetched.TryGetValue(reference, out var item))
            fetched[reference] = item = await FetchAsync(reference, origin, ct);

        // Reserve the name before recursing so a cycle back to this item terminates, and a
        // diamond (two dependents share one dep) emits the dep once. Keyed by the qualified
        // name so @acme/button and the default registry's button stay distinct items.
        if (!seen.Add(item.QualifiedName))
            return;

        // A namespaced item's plain-name dependencies live in its own registry: under @acme/tag,
        // a dep "chip" means "@acme/chip". Already-qualified deps (@other/x, URLs, paths) pass
        // through - including @default/x, the escape hatch a registry uses to depend on the
        // components its consumers install from THEIR registry rather than shipping copies.
        var ns = NamespacedRegistryClient.TrySplit(reference, out var itemNs, out _) ? itemNs : null;

        foreach (var dep in item.RegistryDependencies ?? [])
        {
            var claimed = ns is not null && IsPlainName(dep);
            await VisitAsync(
                claimed ? $"{ns}/{dep}" : dep,
                ordered, seen, fetched, isRequested: false,
                claimed ? new DependencyOrigin(dep, ns!, item.QualifiedName) : null,
                ct);
        }

        ordered.Add(item);
    }

    /// <summary>
    /// Fetch one reference, and when a rewritten one is missing, say what the address alone cannot:
    /// the plain name it came from, the item that declared it, and the spelling that reaches the
    /// consumer's own registry instead. Without this the author sees their own registry's URL and a
    /// filename they never wrote, with nothing joining the two.
    /// </summary>
    private async Task<RegistryItem> FetchAsync(
        string reference, DependencyOrigin? origin, CancellationToken ct)
    {
        try
        {
            return await client.GetItemAsync(reference, ct);
        }
        catch (RegistryException ex) when (ex.Reason is RegistryFailure.NotFound && origin is { } from)
        {
            throw new RegistryException(
                $"{ex.Message} '{from.Name}' is a dependency of '{from.Parent}', so it resolved " +
                $"inside {from.Namespace}: a plain dependency name belongs to its own item's registry. " +
                $"If you meant the component the consumer already installs, name it " +
                $"'{NamespacedRegistryClient.DefaultNamespace}/{from.Name}'.",
                ex, RegistryFailure.NotFound);
        }
    }

    /// <summary>A bare registry item name — no namespace, URL, or path qualification.</summary>
    private static bool IsPlainName(string reference) =>
        !reference.StartsWith('@')
        && !reference.Contains('/') && !reference.Contains('\\')
        && !reference.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}
