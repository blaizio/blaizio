using System.Text.Json;
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

    /// <summary>
    /// Whether <c>init</c> created the tokens file itself (vs adopting one the project already
    /// had). <c>uninstall</c> deletes the file only when this is true — an adopted file is the
    /// user's; only the Blaizio lines inside it are ours to strip.
    /// </summary>
    [JsonPropertyName("cssCreated")]
    public bool CssCreated { get; set; }

    /// <summary>
    /// Whether <c>eject</c> copied the materialized contract into the tokens file. Once true,
    /// <c>update</c>/<c>doctor</c> stop expecting the <c>.blaizio/</c> materialization and the
    /// contract import.
    /// </summary>
    [JsonPropertyName("ejected")]
    public bool Ejected { get; set; }

    /// <summary>Active skin (style-*) name - the structural look components are copied with.</summary>
    [JsonPropertyName("style")]
    public string Style { get; set; } = "default";

    /// <summary>Legacy alias for <see cref="Style"/> - the field shipped as "theme" through the
    /// alphas, colliding with registry theme items (token sets). Read-only migration shim:
    /// accepted on load, never written.</summary>
    [JsonPropertyName("theme")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public string? LegacyTheme { get => null; set { if (value is not null) Style = value; } }

    /// <summary>Active color preset name (<c>"nova"</c> = the built-in default palette).</summary>
    [JsonPropertyName("preset")]
    public string Preset { get; set; } = "nova";

    /// <summary>Whether RTL support was wired up at init.</summary>
    [JsonPropertyName("rtl")]
    public bool Rtl { get; set; }

    /// <summary>Active heading font (a FontCatalog name); null = not customized. The recorded pair
    /// is what the fonts.css overlay regenerates from when one half changes (e.g. adding a
    /// <c>font-heading-*</c> item keeps the body font).</summary>
    [JsonPropertyName("headingFont")]
    public string? Heading { get; set; }

    /// <summary>Legacy alias for <see cref="Heading"/> ("heading" through the alphas). Read-only
    /// migration shim: accepted on load, never written.</summary>
    [JsonPropertyName("heading")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public string? LegacyHeading { get => null; set { if (value is not null) Heading = value; } }

    /// <summary>Active body font (a FontCatalog name); null = not customized.</summary>
    [JsonPropertyName("bodyFont")]
    public string? Font { get; set; }

    /// <summary>Legacy alias for <see cref="Font"/> ("font" through the alphas - ambiguous next
    /// to the heading half). Read-only migration shim: accepted on load, never written.</summary>
    [JsonPropertyName("font")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public string? LegacyFont { get => null; set { if (value is not null) Font = value; } }

    /// <summary>Chart palette overlay (a /create name: ocean, sunset, forest, mono, or a
    /// preset-named series like polaris); null = the
    /// theme's own palette. Baked into theme.css's <c>--chart-*</c> whenever the managed CSS is
    /// rewritten, so update/apply re-runs keep the selection.</summary>
    [JsonPropertyName("chart")]
    public string? Chart { get; set; }

    /// <summary>Radius scale overlay (none, sm, lg, xl); null = the theme's own radius. Baked into
    /// theme.css's <c>--radius</c> like <see cref="Chart"/>.</summary>
    [JsonPropertyName("radius")]
    public string? Radius { get; set; }

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
    /// Origins (scheme + host) the user approved at the <c>add</c> trust gate for direct-URL
    /// installs. Recorded on accept so the same host never re-prompts; the configured registry
    /// and everything under <see cref="Registries"/> are implicitly trusted.
    /// </summary>
    /// <remarks>Null-tolerant like <see cref="Aliases"/>.</remarks>
    [JsonPropertyName("trustedHosts")]
    public List<string> TrustedHosts
    {
        get => field;
        set => field = value ?? [];
    } = [];

    /// <summary>
    /// Items installed by <c>add</c>, keyed by registry name. The record of what's in the project —
    /// what <c>update</c> re-pulls with no arguments and what <c>add --diff</c> compares upstream.
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
    /// <summary>The files written for the item, each with the baseline hash of what was written.</summary>
    [JsonPropertyName("files")]
    public List<InstalledFile> Files { get; set; } = [];

    /// <summary>
    /// Registry items this one depended on at install time, recorded so <c>remove</c>'s dependency
    /// guard still works when the registry is unreachable. <see langword="null"/> means the record
    /// predates dependency tracking (unknown), distinct from an empty list (known to have none).
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<string>? Dependencies { get; set; }

    /// <summary>The recorded baseline for one path, or <see langword="null"/> when there is none.</summary>
    public string? HashFor(string path) => Files
        .FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal))?.Hash;
}

/// <summary>
/// One installed file: where it landed (relative to the output directory, POSIX separators) and
/// the content hash it had AS THE CLI WROTE IT. That hash is the baseline <c>update</c> compares
/// the working copy against, so it can tell a local edit apart from a new upstream version - see
/// <c>ContentHash</c>. <see langword="null"/> means no baseline (the file already existed when
/// <c>add</c> ran, or the record predates the ledger): unknown, not clean.
/// </summary>
[JsonConverter(typeof(InstalledFileConverter))]
public sealed record InstalledFile(string Path, string? Hash = null)
{
    /// <summary>A bare path with no baseline - lets call sites and tests pass plain strings.</summary>
    public static implicit operator InstalledFile(string path) => new(path);
}

/// <summary>
/// Reads an installed file as either an object (<c>{"path": "…", "hash": "…"}</c>) or a bare path
/// string, and always writes the object form. The string form is what every config written before
/// the hash ledger contains, so existing projects keep loading and heal on their next install.
/// </summary>
public sealed class InstalledFileConverter : JsonConverter<InstalledFile>
{
    /// <inheritdoc />
    public override InstalledFile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new InstalledFile(reader.GetString()!);

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected a file path or object, found {reader.TokenType}.");

        string? path = null;
        string? hash = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, "path", StringComparison.OrdinalIgnoreCase))
                path = reader.GetString();
            else if (string.Equals(name, "hash", StringComparison.OrdinalIgnoreCase))
                hash = reader.GetString();
            else
                reader.Skip();
        }

        return new InstalledFile(
            path ?? throw new JsonException("An installed file entry has no 'path'."), hash);
    }

    /// <inheritdoc />
    /// <remarks>Plainly indented, one property per line. Writing each entry compact on a single
    /// line would be shorter, but then editing one hash rewrites the whole array line - this way a
    /// changed file is a one-line diff.</remarks>
    public override void Write(Utf8JsonWriter writer, InstalledFile value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("path", value.Path);
        if (value.Hash is not null)
            writer.WriteString("hash", value.Hash);
        writer.WriteEndObject();
    }
}
