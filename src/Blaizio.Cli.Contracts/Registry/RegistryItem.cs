using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// A single installable unit in the registry: a component, lib file, theme or template,
/// together with everything needed to add it (files, NuGet packages, sibling items).
/// </summary>
public sealed class RegistryItem
{
    /// <summary>
    /// The published schema this document follows (<see cref="RegistrySchema.Item"/>), written into
    /// each built item. Items inside a manifest leave it null - the manifest carries its own.
    /// </summary>
    [JsonPropertyName("$schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>Unique registry name, e.g. <c>button</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>What this item installs.</summary>
    [JsonPropertyName("type")]
    public ItemType Type { get; init; } = ItemType.Ui;

    /// <summary>Human title for gallery/search UIs.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>One-line summary for gallery/search UIs.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>NuGet package ids this item needs (installed via <c>dotnet add package</c>).</summary>
    [JsonPropertyName("nugetDependencies")]
    public IReadOnlyList<string> NugetDependencies { get => field ?? []; init; } = [];

    /// <summary>Other registry item names this item depends on; resolved transitively.</summary>
    [JsonPropertyName("registryDependencies")]
    public IReadOnlyList<string> RegistryDependencies { get => field ?? []; init; } = [];

    /// <summary>Source files carried by this item.</summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<RegistryFile> Files { get => field ?? []; init; } = [];

    /// <summary>Theme token overrides contributed by this item, if any.</summary>
    [JsonPropertyName("cssVars")]
    public CssVarsSpec? CssVars { get; init; }

    /// <summary>The font this item applies, for <see cref="ItemType.Font"/> items.</summary>
    [JsonPropertyName("font")]
    public FontSpec? Font { get; init; }

    /// <summary>
    /// The <c>@namespace</c> this item was fetched through (e.g. <c>"@acme"</c>), or null for the
    /// default registry. Stamped by the client at fetch time, never serialized - it drives where
    /// the files land (a per-registry subfolder) and how the install is recorded.
    /// </summary>
    [JsonIgnore]
    public string? SourceNamespace { get; set; }

    /// <summary>
    /// The name the item is tracked under: <c>@ns/name</c> when namespaced, the plain
    /// <see cref="Name"/> otherwise. Two registries can both ship a <c>button</c> without
    /// their records or dependency graphs colliding.
    /// </summary>
    [JsonIgnore]
    public string QualifiedName => SourceNamespace is null ? Name : $"{SourceNamespace}/{Name}";
}

/// <summary>
/// What a <see cref="ItemType.Font"/> item applies: a FontCatalog font, targeting either the
/// document body or the <c>--font-heading</c> variable. Installing one patches the selection
/// into the tokens file and wires the Google Fonts host link.
/// </summary>
public sealed class FontSpec
{
    /// <summary>The FontCatalog font name, e.g. <c>inter</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>True to set the heading face (<c>--font-heading</c>); false for the body font.</summary>
    [JsonPropertyName("heading")]
    public bool Heading { get; init; }
}

/// <summary>
/// Theme token overrides an item contributes, split by mode: <c>light</c> patches the tokens
/// file's <c>:root</c> block, <c>dark</c> its <c>.dark</c> block. Names may be written with or
/// without their <c>--</c> prefix (<c>"primary"</c> and <c>"--primary"</c> are the same token).
/// The payload of a <see cref="ItemType.Theme"/> item; other item types may carry one too.
/// </summary>
public sealed class CssVarsSpec
{
    /// <summary>Values patched into <c>:root</c> (the light mode block).</summary>
    [JsonPropertyName("light")]
    public IReadOnlyDictionary<string, string> Light { get => field ?? Empty; init; } = Empty;

    /// <summary>Values patched into <c>.dark</c>.</summary>
    [JsonPropertyName("dark")]
    public IReadOnlyDictionary<string, string> Dark { get => field ?? Empty; init; } = Empty;

    private static readonly Dictionary<string, string> Empty = [];

    /// <summary>True when the spec carries no values at all.</summary>
    [JsonIgnore]
    public bool IsEmpty => Light.Count == 0 && Dark.Count == 0;
}

