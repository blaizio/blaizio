using System.Text;
using System.Text.Json;
using Blaizio.Cli.Core.Configuration;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// Default <see cref="IRegistryClient"/>. Resolves items against a base registry that is either
/// an <c>http(s)</c> URL or a local directory, and also accepts a fully-qualified URL or file path
/// as the item reference (so <c>add ./my-item.json</c> and <c>add https://.../x.json</c> work).
/// With a <paramref name="style"/> (the project's recorded skin), plain names resolve to the
/// registry's per-skin inlined variant under <c>{base}/{style}/</c> — when the registry's index
/// says it ships that skin; otherwise items resolve at the base path (v1 raw sources).
/// </summary>
/// <remarks>
/// <paramref name="credentials"/> carries the headers and query parameters a private registry
/// needs. They are resolved once, on the first request rather than at construction, so an unset
/// environment variable is reported by the command that actually touches that registry instead of
/// breaking every command in a project that merely records it.
/// </remarks>
public sealed class RegistryClient(
    HttpClient http,
    string baseRegistry,
    string? style = null,
    Func<ResolvedRegistrySource>? credentials = null) : IRegistryClient
{
    private readonly bool _remote =
        baseRegistry.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        baseRegistry.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    // An address carrying {name} is a template: the registry decides where its items sit, and the
    // catalogue is the same template with the reserved name "index".
    private readonly bool _templated = baseRegistry.Contains(RegistryTemplate.Name, StringComparison.Ordinal);

    private RegistryIndex? _index;
    private ResolvedRegistrySource? _resolved;

    /// <summary>The credentials for this registry, resolved once per client.</summary>
    private ResolvedRegistrySource Credentials
    {
        get
        {
            if (_resolved is not null)
                return _resolved;
            if (credentials is null)
                return _resolved = ResolvedRegistrySource.None;
            try
            {
                return _resolved = credentials();
            }
            catch (InvalidOperationException ex)
            {
                throw new RegistryException(ex.Message, ex, RegistryFailure.Credentials);
            }
        }
    }

    /// <inheritdoc />
    public async Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default)
        => _index ??= await ReadAsync(
            _templated ? Template(RegistryTemplate.IndexName) : Combine("index.json"),
            CoreJson.Default.RegistryIndex, ct);

    /// <inheritdoc />
    public async Task<RegistryIndex> SearchAsync(RegistrySearch search, CancellationToken ct = default)
    {
        // A local directory cannot filter - the caller does. And a search with nothing in it IS
        // the catalogue, so it shares the cache instead of re-fetching.
        var empty = search is { Query: null or "", Limit: null, Offset: null }
            && search.Types is null or { Count: 0 };
        if (!_remote || empty)
            return await GetIndexAsync(ct);

        var location = _templated ? Template(RegistryTemplate.IndexName) : Combine("index.json");
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(search.Query))
            parameters["q"] = search.Query;
        if (search.Types is { Count: > 0 })
            parameters["type"] = string.Join(',', search.Types);
        if (search.Limit is { } limit)
            parameters["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (search.Offset is { } offset)
            parameters["offset"] = offset.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Deliberately NOT cached into _index: this is a filtered page, and a later plain
        // GetIndexAsync must still see the whole catalogue.
        return await ReadAsync(WithParams(location, parameters), CoreJson.Default.RegistryIndex, ct);
    }

    /// <inheritdoc />
    public async Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default)
    {
        // Registry files are kebab-case (input-text.json); callers name components in PascalCase
        // (InputText) on the command line. Normalize plain names so the round-trip matches on any
        // filesystem — case-insensitive ones mask single-word slips, but hyphens never resolve.
        // ToKebab is idempotent, so already-kebab dependency references pass through unchanged.
        if (IsQualified(nameOrUrlOrPath))
            return await ReadAsync(nameOrUrlOrPath, CoreJson.Default.RegistryItem, ct);

        var name = await ResolveNameAsync(nameOrUrlOrPath, ct);

        // A templated address places the name (and the skin) itself, so the layout conventions
        // below - a .json leaf, a per-skin sub-folder - are the template's business, not ours.
        if (_templated)
            return await ReadAsync(Template(name), CoreJson.Default.RegistryItem, ct);

        var subdir = style is not null && await ShipsStyleAsync(ct) ? style : null;
        return await ReadAsync(Combine($"{name}.json", subdir), CoreJson.Default.RegistryItem, ct);
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

    /// <summary>
    /// The address for one item under a templated registry: <c>{name}</c> becomes the item (the
    /// reserved <c>index</c> for the catalogue) and <c>{style}</c> the project's recorded skin.
    /// A template asking for a skin the project has not chosen is a configuration error, not a
    /// request worth sending.
    /// </summary>
    private string Template(string name)
    {
        if (name.AsSpan().ContainsAny('/', '\\') || name.Contains(".."))
            throw new RegistryException($"Invalid registry item name '{name}'.");

        var address = baseRegistry.Replace(
            RegistryTemplate.Name, _remote ? Uri.EscapeDataString(name) : name, StringComparison.Ordinal);

        if (!address.Contains(RegistryTemplate.Style, StringComparison.Ordinal))
            return address;

        if (style is null)
        {
            throw new RegistryException(
                $"The registry address '{baseRegistry}' wants a {RegistryTemplate.Style}, and this project has no style recorded. " +
                "Set one in blaizio.json, or record the registry with a fixed style in its URL.",
                null, RegistryFailure.Credentials);
        }

        return address.Replace(
            RegistryTemplate.Style, _remote ? Uri.EscapeDataString(style) : style, StringComparison.Ordinal);
    }

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
                return await ReadHttpAsync(location, typeInfo, ct);

            await using var stream = File.OpenRead(location);
            var local = await JsonSerializer.DeserializeAsync(stream, typeInfo, ct);
            return local ?? throw Malformed(location);
        }
        catch (HttpRequestException ex)
        {
            // Nothing answered: DNS, connection, TLS. Status-carrying failures never reach here -
            // ReadHttpAsync classifies them from the response itself.
            throw new RegistryException(
                $"Could not reach the registry at '{location}'.", ex, RegistryFailure.Unreachable);
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

    /// <summary>
    /// One GET, decorated with this registry's credentials. The request is built by hand rather
    /// than through GetFromJsonAsync so the headers can be attached, and so a failure can be
    /// classified from the RESPONSE - a 401 has to read differently from a dead host, and the
    /// registry's own explanation ("token expired, get a new one at ...") is worth passing on.
    /// </summary>
    private async Task<T> ReadHttpAsync<T>(
        string location,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
    {
        var credentials = Credentials;
        using var request = new HttpRequestMessage(HttpMethod.Get, WithParams(location, credentials.Params));
        foreach (var (name, value) in credentials.Headers)
            request.Headers.TryAddWithoutValidation(name, value);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw await FailureAsync(response, location, ct);

        var stream = await response.Content.ReadAsStreamAsync(ct);
        await using (stream.ConfigureAwait(false))
        {
            var result = await JsonSerializer.DeserializeAsync(stream, typeInfo, ct);
            return result ?? throw Malformed(location);
        }
    }

    /// <summary>The location with this registry's query parameters appended (credentials included).</summary>
    private static string WithParams(string location, IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
            return location;

        var url = new StringBuilder(location);
        url.Append(location.Contains('?') ? '&' : '?');
        var first = true;
        foreach (var (name, value) in parameters)
        {
            if (!first) url.Append('&');
            first = false;
            url.Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
        }
        return url.ToString();
    }

    /// <summary>
    /// Turn a non-success response into the failure it is. The location in the message is the
    /// UNDECORATED one: a credential passed as a query parameter must not end up in an error
    /// string that gets pasted into an issue.
    /// </summary>
    private static async Task<RegistryException> FailureAsync(
        HttpResponseMessage response, string location, CancellationToken ct)
    {
        var status = response.StatusCode;
        var detail = await ExplanationAsync(response, ct);
        var suffix = detail is null ? "" : $" The registry said: {detail}";

        return status switch
        {
            System.Net.HttpStatusCode.NotFound => new RegistryException(
                $"Registry file not found: '{location}'.", null, RegistryFailure.NotFound),
            System.Net.HttpStatusCode.Unauthorized => new RegistryException(
                $"Not authorized for '{location}'. Check the credentials recorded for this registry " +
                $"and that their environment variables are set in this shell.{suffix}",
                null, RegistryFailure.Unauthorized),
            System.Net.HttpStatusCode.Forbidden => new RegistryException(
                $"Access denied for '{location}'. The credentials were accepted but do not cover this item.{suffix}",
                null, RegistryFailure.Forbidden),
            System.Net.HttpStatusCode.TooManyRequests => new RegistryException(
                $"Rate limited by the registry at '{location}'. Wait and retry.{suffix}",
                null, RegistryFailure.RateLimited),
            _ => new RegistryException(
                $"The registry at '{location}' answered {(int)status} {status}.{suffix}",
                null, RegistryFailure.Unreachable),
        };
    }

    /// <summary>
    /// The registry's own message, when it sent one: a JSON body's <c>message</c> or <c>error</c>,
    /// else a short plain-text body. Bounded and single-lined - this is going into one console line,
    /// and a registry that answers with an HTML error page has nothing to say here.
    /// </summary>
    private static async Task<string?> ExplanationAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body) || body.Length > 4096)
                return null;

            var text = body.Trim();
            if (text.StartsWith('{'))
            {
                using var document = JsonDocument.Parse(text);
                foreach (var name in (ReadOnlySpan<string>)["message", "error", "detail"])
                {
                    if (document.RootElement.TryGetProperty(name, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && value.GetString() is { Length: > 0 } explanation)
                    {
                        return Flatten(explanation);
                    }
                }
                return null;
            }

            return text.StartsWith('<') ? null : Flatten(text);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException)
        {
            return null;
        }

        static string Flatten(string value)
        {
            var single = value.ReplaceLineEndings(" ").Trim();
            return single.Length <= 200 ? single : single[..200] + "...";
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

    /// <summary>401: the request carried no usable credential.</summary>
    Unauthorized,

    /// <summary>403: the credential was accepted but does not cover what was asked for.</summary>
    Forbidden,

    /// <summary>429: too many requests, and the registry asked for a pause.</summary>
    RateLimited,

    /// <summary>A credential could not be assembled locally - an unset environment variable.</summary>
    Credentials,
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
