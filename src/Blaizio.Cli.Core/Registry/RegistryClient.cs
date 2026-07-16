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

        var leaf = $"{Generation.RegistryGenerator.ToKebab(nameOrUrlOrPath)}.json";
        var subdir = style is not null && await ShipsStyleAsync(ct) ? style : null;
        return await ReadAsync(Combine(leaf, subdir), CoreJson.Default.RegistryItem, ct);
    }

    /// <summary>Whether the registry's index lists the configured skin under <c>styles</c> —
    /// the gate for the per-skin variant path (third-party registries may ship none).</summary>
    private async Task<bool> ShipsStyleAsync(CancellationToken ct)
        => (await GetIndexAsync(ct)).Styles?.Contains(style!, StringComparer.OrdinalIgnoreCase) == true;

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
            throw new RegistryException($"Could not reach the registry at '{location}'.", ex);
        }
        catch (FileNotFoundException ex)
        {
            throw new RegistryException($"Registry file not found: '{location}'.", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new RegistryException($"Registry directory not found: '{location}'.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout (not a user cancel).
            throw new RegistryException($"Timed out fetching '{location}'.", ex);
        }
        catch (JsonException ex)
        {
            throw new RegistryException($"Registry response at '{location}' was not valid JSON.", ex);
        }
    }

    private static RegistryException Malformed(string location)
        => new($"Registry response at '{location}' was empty.");
}

/// <summary>Raised when the registry cannot be reached or returns unusable data.</summary>
public sealed class RegistryException(string message, Exception? inner = null)
    : Exception(message, inner);
