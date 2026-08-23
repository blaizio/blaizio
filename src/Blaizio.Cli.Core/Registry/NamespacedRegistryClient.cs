using Blaizio.Cli.Core.Writing;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// Routes <c>@namespace/item</c> references to the registry recorded for that namespace
/// (<c>blaizio.json</c> <c>registries</c>, written by <c>registry add</c>); everything else goes
/// to the default registry. Namespaced references work anywhere an item reference does — the
/// command line and <c>registryDependencies</c> alike.
/// </summary>
/// <remarks>
/// <see cref="DefaultNamespace"/> is reserved and never recorded: it names the CONSUMER's default
/// registry, so a third-party registry can depend on the components its project already installs
/// from there. It exists because a plain dependency name inside a namespaced item is claimed by
/// that item's own registry (see DependencyResolver) — which is right for a self-contained
/// registry and wrong for one whose components build on the base set.
/// </remarks>
public sealed class NamespacedRegistryClient(
    IRegistryClient fallback,
    IReadOnlyDictionary<string, IRegistryClient> named,
    Func<GitHubAddress, IRegistryClient>? repository = null) : IRegistryClient
{
    /// <summary>The reserved namespace meaning "the project's default registry".</summary>
    public const string DefaultNamespace = "@default";

    /// <summary>True for the reserved namespace, which is resolved rather than looked up.</summary>
    public static bool IsDefaultNamespace(string @namespace) =>
        string.Equals(@namespace, DefaultNamespace, StringComparison.OrdinalIgnoreCase);

    /// <summary>The root namespace of Blaizio's own packages, which nothing may nest under.</summary>
    public const string PackageRoot = "Blaizio";

    /// <summary>
    /// True when installs from this namespace would land in a <c>Blaizio</c> folder, and so in a
    /// <c>&lt;components&gt;.Blaizio</c> namespace. C# resolves a name from the innermost namespace
    /// outward, so every <c>Blaizio.Base</c> reference inside those files would bind to that nested
    /// segment instead of the package: the component installs and then fails to compile.
    /// </summary>
    public static bool ShadowsPackageRoot(string @namespace) =>
        string.Equals(ComponentWriter.FolderFor(@namespace), PackageRoot, StringComparison.Ordinal);

    /// <inheritdoc />
    public Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default)
        => fallback.GetIndexAsync(ct);

    /// <summary>Forwarded, not defaulted: the interface default would call THIS wrapper's
    /// GetIndexAsync and silently strip the search off the default registry.</summary>
    public Task<RegistryIndex> SearchAsync(RegistrySearch search, CancellationToken ct = default)
        => fallback.SearchAsync(search, ct);

    /// <summary>
    /// The client for one recorded <c>@namespace</c>, so a caller that wants that registry's
    /// CATALOGUE (rather than an item from it) still goes through the configured client - carrying
    /// its credentials - instead of rebuilding one from the bare URL.
    /// </summary>
    public IRegistryClient For(string @namespace) =>
        IsDefaultNamespace(@namespace) ? fallback
        : named.TryGetValue(@namespace, out var client) ? client
        : throw new RegistryException(
            $"Unknown registry '{@namespace}'. Record it first: blaizio registry add \"{@namespace}=<url>\"");

    /// <inheritdoc />
    public Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default)
    {
        // owner/repo/item resolves out of the repository itself. Checked before the fallback so a
        // three-segment reference is never mistaken for an item name on the default registry.
        if (repository is not null && GitHubAddress.TryParse(nameOrUrlOrPath, out var address))
            return FetchSourcedAsync(repository(address), address.Item, nameOrUrlOrPath, ct);

        if (!TrySplit(nameOrUrlOrPath, out var ns, out var name))
            return fallback.GetItemAsync(nameOrUrlOrPath, ct);

        // The reserved namespace resolves to the default registry and is NOT stamped: the item
        // came from there, so it lands in the ordinary output folder under the ordinary name -
        // exactly as it would had the project installed it itself.
        if (IsDefaultNamespace(ns))
            return fallback.GetItemAsync(name, ct);

        if (!named.TryGetValue(ns, out var client))
            throw new RegistryException(
                $"Unknown registry '{ns}'. Record it first: blaizio registry add \"{ns}=<url>\"");
        return FetchStampedAsync(client, ns, name, ct);
    }

    /// <summary>Fetch out of a repository and stamp the address as the item's source, the way a
    /// direct file or URL is stamped by the base client - so the install record can say where
    /// it came from.</summary>
    private static async Task<RegistryItem> FetchSourcedAsync(
        IRegistryClient client, string name, string reference, CancellationToken ct)
    {
        var item = await client.GetItemAsync(name, ct);
        item.SourceReference = reference;
        return item;
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
