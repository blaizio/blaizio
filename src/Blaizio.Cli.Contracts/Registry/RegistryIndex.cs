using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// The registry catalogue served at <c>{registry}/index.json</c>: a lightweight list of
/// every available item for <c>list</c>/<c>search</c>, without the file payloads.
/// </summary>
public sealed class RegistryIndex
{
    /// <summary>
    /// The published schema this document follows (<see cref="RegistrySchema.Registry"/>). Written
    /// by <c>generate</c> and <c>build</c> so editors can complete and validate the file; read back
    /// as an ordinary field and never acted on.
    /// </summary>
    [JsonPropertyName("$schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>Registry display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Every item, minus <see cref="RegistryItem.Files"/> content.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<RegistryItem> Items { get => field ?? []; init; } = [];

    /// <summary>
    /// Other manifests folded into this one, as file paths relative to the manifest that lists
    /// them. A source-side field only: <c>build</c> flattens the includes away, so a served
    /// <c>index.json</c> never carries one.
    /// </summary>
    [JsonPropertyName("include")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>
    /// Skins this registry ships per-skin inlined item variants for (under <c>{base}/{skin}/</c>).
    /// Null for registries without style variants - items resolve at the base path only.
    /// </summary>
    [JsonPropertyName("styles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Styles { get; init; }

    /// <summary>
    /// Present when a DYNAMIC registry answered a search server-side: <see cref="Items"/> is the
    /// requested page of an already-filtered set, and the client must not filter it again. A
    /// static registry never writes one, which is exactly how the client tells the two apart.
    /// </summary>
    [JsonPropertyName("pagination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RegistryPagination? Pagination { get; init; }
}

/// <summary>How much of a server-filtered result this response carries.</summary>
public sealed class RegistryPagination
{
    /// <summary>How many items matched in total, across every page.</summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>How many matched items precede this page.</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    /// <summary>The page size the server applied.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    /// <summary>True when more matches exist past this page.</summary>
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }
}
