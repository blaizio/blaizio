using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Configuration;

/// <summary>
/// The <c>blaizio.json</c> written into a consumer project by <c>init</c> and read by every
/// later command. Records where components land, their namespace, theme and the registry URL.
/// </summary>
public sealed class BlaizioConfig
{
    /// <summary>The file name looked up at the project root.</summary>
    public const string FileName = "blaizio.json";

    /// <summary>JSON schema URL for editor completion.</summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://blaiz.io/schema.json";

    /// <summary>Root namespace applied to every copied component.</summary>
    [JsonPropertyName("namespace")]
    public required string Namespace { get; set; }

    /// <summary>Directory (relative to the project) copied components are written into.</summary>
    [JsonPropertyName("output")]
    public string Output { get; set; } = "Components/Ui";

    /// <summary>
    /// Custom Tailwind input file (project-relative) for bundler setups (rollup/vite/postcss own
    /// the compile). When set, the CLI never writes its own <c>Styles/app.css</c> — it keeps the
    /// managed <c>@import</c>s in THIS file in sync instead (adding what's missing, swapping stale
    /// skin/preset lines). Null = the CLI-managed <c>Styles/app.css</c> input.
    /// </summary>
    [JsonPropertyName("css")]
    public string? Css { get; set; }

    /// <summary>Active theme token set name.</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "default";

    /// <summary>Active color preset name (<c>"nova"</c> = the built-in default palette).</summary>
    [JsonPropertyName("preset")]
    public string Preset { get; set; } = "nova";

    /// <summary>Whether RTL support was wired up at init.</summary>
    [JsonPropertyName("rtl")]
    public bool Rtl { get; set; }

    /// <summary>Active heading font (a FontCatalog name); null = not customized. The recorded pair
    /// is what the fonts.css overlay regenerates from when one half changes (e.g. adding a
    /// <c>font-heading-*</c> item keeps the body font).</summary>
    [JsonPropertyName("heading")]
    public string? Heading { get; set; }

    /// <summary>Active body font (a FontCatalog name); null = not customized.</summary>
    [JsonPropertyName("font")]
    public string? Font { get; set; }

    /// <summary>Base registry URL (or local path) items are fetched from.</summary>
    [JsonPropertyName("registry")]
    public string Registry { get; set; } = "https://blaiz.io/r";

    /// <summary>Namespace aliases; <c>ui</c> mirrors <see cref="Namespace"/>, <c>base</c> is the headless layer.</summary>
    /// <remarks>Null-tolerant: a hand-edited <c>"aliases": null</c> must not crash later commands.</remarks>
    [JsonPropertyName("aliases")]
    public Dictionary<string, string> Aliases
    {
        get => field;
        set => field = value ?? [];
    } = new()
    {
        ["base"] = "Blaizio",
    };

    /// <summary>
    /// Named registries recorded by <c>registry add</c>, keyed by <c>@namespace</c> with the
    /// registry base URL (or local path) as the value.
    /// </summary>
    /// <remarks>Null-tolerant like <see cref="Aliases"/>.</remarks>
    [JsonPropertyName("registries")]
    public Dictionary<string, string> Registries
    {
        get => field;
        set => field = value ?? [];
    } = [];

    /// <summary>
    /// Items installed by <c>add</c>, keyed by registry name. The record of what's in the project —
    /// what <c>add --update</c> re-pulls with no arguments and what <c>add --diff</c> compares upstream.
    /// </summary>
    /// <remarks>Null-tolerant like <see cref="Aliases"/>.</remarks>
    [JsonPropertyName("installed")]
    public Dictionary<string, InstalledItem> Installed
    {
        get => field;
        set => field = value ?? [];
    } = [];

    /// <summary>
    /// NuGet package ids the CLI itself installed (init/add/upgrade), recorded at install time so
    /// <c>uninstall</c> can undo exactly them. Packages the project referenced before the CLI touched
    /// it are never recorded — undo-by-record, never by name pattern.
    /// </summary>
    /// <remarks>Null-tolerant like <see cref="Aliases"/>.</remarks>
    [JsonPropertyName("packages")]
    public List<string> Packages
    {
        get => field;
        set => field = value ?? [];
    } = [];
}

/// <summary>A single installed registry item recorded in <c>blaizio.json</c>.</summary>
public sealed class InstalledItem
{
    /// <summary>File paths written for the item, relative to the output directory (POSIX separators).</summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = [];
}
