namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// Routes <c>@namespace/item</c> references to the registry recorded for that namespace
/// (<c>blaizio.json</c> <c>registries</c>, written by <c>registry add</c>); everything else goes
/// to the default registry. Namespaced references work anywhere an item reference does — the
/// command line and <c>registryDependencies</c> alike.
/// </summary>
public sealed class NamespacedRegistryClient(
    IRegistryClient fallback,
    IReadOnlyDictionary<string, IRegistryClient> named) : IRegistryClient
{
    /// <inheritdoc />
    public Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default)
        => fallback.GetIndexAsync(ct);

    /// <inheritdoc />
    public Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default)
    {
        if (!TrySplit(nameOrUrlOrPath, out var ns, out var name))
            return fallback.GetItemAsync(nameOrUrlOrPath, ct);

        if (!named.TryGetValue(ns, out var client))
            throw new RegistryException(
                $"Unknown registry '{ns}'. Record it first: blaizio registry add {ns}=<url>");
        return FetchStampedAsync(client, ns, name, ct);
    }

    /// <summary>Fetch from the named registry and stamp the namespace the item came through.</summary>
    private static async Task<RegistryItem> FetchStampedAsync(
        IRegistryClient client, string ns, string name, CancellationToken ct)
    {
        var item = await client.GetItemAsync(name, ct);
        item.SourceNamespace = ns;
        return item;
    }

    /// <summary>Split <c>@namespace/item</c>; false for anything else (URLs, paths, plain names).</summary>
    internal static bool TrySplit(string reference, out string ns, out string name)
    {
        ns = name = string.Empty;
        if (reference.Length < 4 || reference[0] != '@')
            return false;

        var slash = reference.IndexOf('/');
        if (slash < 2 || slash == reference.Length - 1)
            return false;

        ns = reference[..slash];
        name = reference[(slash + 1)..];
        return true;
    }
}
