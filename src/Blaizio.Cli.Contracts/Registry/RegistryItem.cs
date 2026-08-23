using System.Text.Json;
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

    /// <summary>
    /// The version this document describes, when the registry versions its items. Purely the
    /// registry's own scheme (the CLI never orders versions) - it is recorded at install time and
    /// echoed back on a pinned request (<c>add button@1.2.0</c>) so the client can verify the
    /// registry actually served what was asked. Null = an unversioned registry.
    /// </summary>
    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }

    /// <summary>One-line summary for gallery/search UIs.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Who published the item, e.g. <c>Jane Doe &lt;jane@acme.dev&gt;</c>. Display only.</summary>
    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; init; }

    /// <summary>Free-form browse/filter tags (e.g. <c>forms</c>, <c>charts</c>). Matched by
    /// <c>search --category</c>, case-insensitively.</summary>
    [JsonPropertyName("categories")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Categories { get; init; }

    /// <summary>
    /// A note shown to the consumer when the item installs and by <c>docs</c>/<c>view</c> -
    /// setup steps, a documentation URL, a caveat. Plain text, kept short.
    /// </summary>
    [JsonPropertyName("docs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Docs { get; init; }

    /// <summary>
    /// Arbitrary registry-defined metadata, carried through untouched. The CLI never reads it -
    /// it exists for registry tooling and UIs that want their own fields without schema fights.
    /// </summary>
    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, JsonElement>? Meta { get; init; }

    /// <summary>NuGet package ids this item needs (installed via <c>dotnet add package</c>).
    /// <c>Id@Version</c> pins one.</summary>
    [JsonPropertyName("nugetDependencies")]
    public IReadOnlyList<string> NugetDependencies { get => field ?? []; init; } = [];

    /// <summary>
    /// The lowest <c>Blaizio.Base</c> version this item's sources work against - the release that
    /// introduced whatever Base capability (typically a JS module) the item calls into. <c>add</c>
    /// fails fast with the upgrade path when the project's pinned reference is older, instead of
    /// installing sources whose interop 404s at runtime. Null (the default) skips the check, as
    /// does a project that floats or has no Base reference yet.
    /// </summary>
    [JsonPropertyName("minBase")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MinBase { get; init; }

    /// <summary>
    /// NuGet packages needed only at development/build time (analyzers, source generators, build
    /// tooling). Installed like <see cref="NugetDependencies"/>, then marked
    /// <c>PrivateAssets="all"</c> in the csproj so they never flow to the app's own consumers.
    /// </summary>
    [JsonPropertyName("devDependencies")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? DevDependencies { get; init; }

    /// <summary>Other registry item names this item depends on; resolved transitively.</summary>
    [JsonPropertyName("registryDependencies")]
    public IReadOnlyList<string> RegistryDependencies { get => field ?? []; init; } = [];

    /// <summary>Source files carried by this item.</summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<RegistryFile> Files { get => field ?? []; init; } = [];

    /// <summary>Theme token overrides contributed by this item, if any.</summary>
    [JsonPropertyName("cssVars")]
    public CssVarsSpec? CssVars { get; init; }

    /// <summary>
    /// CSS blocks the item ships into the consumer's tokens file: block prelude to block body
    /// (<c>"@keyframes spin"</c> to <c>"from { ... } to { ... }"</c>). Written inside a managed,
    /// item-keyed region so <c>remove</c>/<c>uninstall</c> take exactly it back out. Tokens
    /// (<c>:root</c>/<c>.dark</c> values) do NOT belong here - that is <see cref="CssVars"/>;
    /// the token contract itself stays canonical.
    /// </summary>
    [JsonPropertyName("css")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Css { get; init; }

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
    /// The version the caller pinned in the reference (<c>button@1.2.0</c>), or null for a
    /// floating request. Stamped by the client at fetch time like <see cref="SourceNamespace"/>,
    /// never serialized - it becomes the <c>pin</c> in the install record, which is what
    /// <c>update</c> and <c>diff</c> re-request instead of whatever is current.
    /// </summary>
    [JsonIgnore]
    public string? RequestedVersion { get; set; }

    /// <summary>
    /// The reference this item was fetched by when that was not a name on a registry: a file
    /// path, a URL, or an <c>owner/repo/item</c> address, exactly as given. Stamped by the client
    /// at fetch time like <see cref="SourceNamespace"/>, never serialized - it becomes the
    /// <c>source</c> of the install record, which is what lets <c>update</c> re-pull the item from
    /// where it came instead of assuming a plain name means the default registry.
    /// </summary>
    [JsonIgnore]
    public string? SourceReference { get; set; }

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

