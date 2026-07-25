using System.Net.Http.Json;
using System.Text.Json;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// Default <see cref="IRegistryClient"/>. Resolves items against a base registry that is either
/// an <c>http(s)</c> URL or a local directory, and also accepts a fully-qualified URL or file path
/// as the item reference (so <c>add ./my-item.json</c> and <c>add https://.../x.json</c> work).
/// With a <paramref name="style"/> (the project's recorded skin), plain names resolve to the
/// registry's per-skin inlined variant under <c>{base}/{style}/</c> — when the registry's index
/// says it ships that skin; otherwise items resolve at the base path (v1 raw sources).
/// </summary>
public sealed class RegistryClient(HttpClient http, string baseRegistry, string? style = null) : IRegistryClient
{
    private readonly bool _remote =
        baseRegistry.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        baseRegistry.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private RegistryIndex? _index;

    /// <inheritdoc />
    public async Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default)
        => _index ??= await ReadAsync(Combine("index.json"), CoreJson.Default.RegistryIndex, ct);

    /// <inheritdoc />
    public async Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default)
    {
        // Registry files are kebab-case (input-text.json); callers name components in PascalCase
        // (InputText) on the command line. Normalize plain names so the round-trip matches on any
        // filesystem — case-insensitive ones mask single-word slips, but hyphens never resolve.
        // ToKebab is idempotent, so already-kebab dependency references pass through unchanged.
        if (IsQualified(nameOrUrlOrPath))
            return await ReadAsync(nameOrUrlOrPath, CoreJson.Default.RegistryItem, ct);

        var leaf = $"{await ResolveNameAsync(nameOrUrlOrPath, ct)}.json";
        var subdir = style is not null && await ShipsStyleAsync(ct) ? style : null;
        return await ReadAsync(Combine(leaf, subdir), CoreJson.Default.RegistryItem, ct);
    }

    /// <summary>
    /// Resolve a plain component name to its registry item name, forgiving case and separators:
    /// <c>inputnumber</c>, <c>INPUTNUMBER</c> and <c>Input-Number</c> all land on
    /// <c>input-number</c>. ToKebab alone only helps PascalCase — an all-lowercase multiword name
    /// has no capitals to split on — so the index is consulted for a separator-insensitive match.
    /// A registry without an index (v1 raw sources, third-party) keeps the literal kebab path.
    /// </summary>
    private async Task<string> ResolveNameAsync(string name, CancellationToken ct)
    {
        var kebab = Generation.RegistryGenerator.ToKebab(name);

        RegistryIndex index;
        try { index = await GetIndexAsync(ct); }
        catch (RegistryException) { return kebab; }

        var wanted = Strip(kebab);
        foreach (var item in index.Items)
        {
            if (string.Equals(Strip(item.Name), wanted, StringComparison.OrdinalIgnoreCase))
                return item.Name;
        }

        return kebab;

        static string Strip(string value) => value.Replace("-", "").Replace("_", "");
    }

    /// <summary>Whether the registry's index lists the configured skin under <c>styles</c> —
    /// the gate for the per-skin variant path (third-party registries may ship none).</summary>
    /// <remarks>
    /// A registry with no index at all ships no skin variants by definition, so a missing
    /// index.json answers "no" rather than failing the lookup: v1 (raw sources) and third-party
    /// registries have none, and their items resolve at the base path. An unreachable registry
    /// still throws — that is a real failure, not an absent file.
    /// </remarks>
    private async Task<bool> ShipsStyleAsync(CancellationToken ct)
    {
        try
        {
            return (await GetIndexAsync(ct)).Styles?.Contains(style!, StringComparer.OrdinalIgnoreCase) == true;
        }
        catch (RegistryException ex) when (ex.Reason is RegistryFailure.NotFound)
        {
            return false;
        }
    }

    private static bool IsQualified(string reference) =>
        reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        reference.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        Path.IsPathRooted(reference);

    private string Combine(string leaf, string? subdir = null)
    {
        // Item names come from user input and registry dependency lists (and the subdir from the
        // recorded skin) — never let one smuggle path segments into the URL or escape a local
        // registry directory.
        foreach (var segment in (ReadOnlySpan<string?>)[leaf, subdir])
        {
            if (segment is not null && (segment.AsSpan().ContainsAny('/', '\\') || segment.Contains("..")))
                throw new RegistryException($"Invalid registry item name '{segment}'.");
        }

        return (_remote, subdir) switch
        {
            (true, null) => $"{baseRegistry.TrimEnd('/')}/{Uri.EscapeDataString(leaf)}",
            (true, _) => $"{baseRegistry.TrimEnd('/')}/{Uri.EscapeDataString(subdir!)}/{Uri.EscapeDataString(leaf)}",
            (false, null) => Path.Combine(baseRegistry, leaf),
            (false, _) => Path.Combine(baseRegistry, subdir!, leaf),
        };
    }

    private async Task<T> ReadAsync<T>(
        string location,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
    {
        var isHttp =
            location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isHttp)
            {
                var result = await http.GetFromJsonAsync(location, typeInfo, ct);
                return result ?? throw Malformed(location);
            }

            await using var stream = File.OpenRead(location);
            var local = await JsonSerializer.DeserializeAsync(stream, typeInfo, ct);
            return local ?? throw Malformed(location);
        }
        catch (HttpRequestException ex)
        {
            // A 404 means the registry answered - that file simply is not there, which is normal
            // for an index on a v1/third-party registry. Anything else (DNS, refused, TLS) means
            // nothing is listening, and every later read would fail the same way.
            var reason = ex.StatusCode == System.Net.HttpStatusCode.NotFound
                ? RegistryFailure.NotFound
                : RegistryFailure.Unreachable;
            var message = reason == RegistryFailure.NotFound
                ? $"Registry file not found: '{location}'."
                : $"Could not reach the registry at '{location}'.";
            throw new RegistryException(message, ex, reason);
        }
        catch (FileNotFoundException ex)
        {
            throw new RegistryException($"Registry file not found: '{location}'.", ex, RegistryFailure.NotFound);
        }
        catch (DirectoryNotFoundException ex)
        {
            // A local registry path that does not exist is the same class of problem as an
            // unreachable host: nothing in this registry will ever resolve.
            throw new RegistryException($"Registry directory not found: '{location}'.", ex, RegistryFailure.Unreachable);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout (not a user cancel).
            throw new RegistryException($"Timed out fetching '{location}'.", ex, RegistryFailure.Unreachable);
        }
        catch (JsonException ex)
        {
            throw new RegistryException($"Registry response at '{location}' was not valid JSON.", ex, RegistryFailure.Malformed);
        }
    }

    private static RegistryException Malformed(string location)
        => new($"Registry response at '{location}' was empty.");
}

/// <summary>Why a registry read failed - what the preflight check keys off.</summary>
public enum RegistryFailure
{
    /// <summary>Unclassified (a caller-constructed message, e.g. an unknown <c>@namespace</c>).</summary>
    Unknown,

    /// <summary>Nothing answered: DNS, connection, TLS or a timeout. Every later read fails too.</summary>
    Unreachable,

    /// <summary>The registry answered but has no such file. A missing <c>index.json</c> is normal
    /// for a v1 (raw sources) or third-party registry - items still resolve at the base path.</summary>
    NotFound,

    /// <summary>The registry answered with something that is not the expected JSON.</summary>
    Malformed,
}

/// <summary>Raised when the registry cannot be reached or returns unusable data.</summary>
public sealed class RegistryException(
    string message,
    Exception? inner = null,
    RegistryFailure reason = RegistryFailure.Unknown) : Exception(message, inner)
{
    /// <summary>What went wrong, for callers that treat "no index" differently from "no registry".</summary>
    public RegistryFailure Reason { get; } = reason;
}
