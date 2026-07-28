using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Blaizio.Docs.Services;

/// <summary>
/// One community registry listed on /community. Contribution is a pull request adding an entry to
/// <c>wwwroot/community/registries.json</c>; each entry is everything a consumer needs to wire the
/// registry into a project (<c>blaizio registry add name=url</c>).
/// </summary>
public sealed class CommunityRegistry
{
    /// <summary>The registry's <c>@namespace</c>, e.g. <c>@acme</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Public site for the registry (docs, source, gallery).</summary>
    [JsonPropertyName("homepage")]
    public required string Homepage { get; init; }

    /// <summary>The registry base URL consumers record in <c>blaizio.json</c>.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>One-line description shown in the list.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>The command that records this registry in a project.</summary>
    [JsonIgnore]
    public string AddCommand => $"blaizio registry add {Name}={Url}";
}

/// <summary>
/// One community theme listed on /community: token overrides for <c>:root</c> (light) and
/// <c>.dark</c>, the same <c>cssVars</c> payload a <c>registry:theme</c> item carries.
/// Contribution is a pull request adding an entry to <c>wwwroot/community/themes.json</c>.
/// </summary>
public sealed class CommunityTheme
{
    /// <summary>Stable identifier (kebab-case), e.g. <c>crimson</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Display title, e.g. <c>Crimson</c>.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Who made it (a handle or name; shown on the card).</summary>
    [JsonPropertyName("author")]
    public required string Author { get; init; }

    /// <summary>One-line description shown on the card.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>Light values (patched into <c>:root</c>), keyed by token name.</summary>
    [JsonPropertyName("light")]
    public Dictionary<string, string> Light { get; init; } = [];

    /// <summary>Dark values (patched into <c>.dark</c>), keyed by token name.</summary>
    [JsonPropertyName("dark")]
    public Dictionary<string, string> Dark { get; init; } = [];

    /// <summary>A light-mode token's value, for the swatch strip; null when the theme doesn't set it.</summary>
    public string? Swatch(string token) =>
        Light.TryGetValue(token, out var v) ? v : Light.TryGetValue($"--{token}", out var p) ? p : null;

    /// <summary>
    /// The theme as CSS - the same text "Copy CSS" puts on the clipboard and "Apply" injects.
    /// Token names get their <c>--</c> prefix when the author omitted it, mirroring the CLI.
    /// </summary>
    public string ToCss()
    {
        var sb = new StringBuilder();
        AppendBlock(sb, ":root", Light);
        if (Dark.Count > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            AppendBlock(sb, ".dark", Dark);
        }
        return sb.ToString();

        static void AppendBlock(StringBuilder sb, string selector, Dictionary<string, string> vars)
        {
            if (vars.Count == 0)
                return;
            sb.Append(selector).AppendLine(" {");
            foreach (var (name, value) in vars)
                sb.Append("  ").Append(name.StartsWith("--", StringComparison.Ordinal) ? name : $"--{name}")
                  .Append(": ").Append(value).AppendLine(";");
            sb.AppendLine("}");
        }
    }
}

/// <summary>Serialization context for the /community data files (trim/AOT-safe, like CoreJson).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CommunityRegistry[]))]
[JsonSerializable(typeof(CommunityTheme[]))]
public sealed partial class CommunityJson : JsonSerializerContext;

/// <summary>
/// Read-only client for the /community data files under <c>wwwroot/community/</c>. Fetched once
/// and cached for the session; an unreachable file degrades to an empty list, never an error page.
/// </summary>
public interface ICommunitySource
{
    /// <summary>The listed community registries.</summary>
    Task<IReadOnlyList<CommunityRegistry>> GetRegistriesAsync();

    /// <summary>The listed community themes.</summary>
    Task<IReadOnlyList<CommunityTheme>> GetThemesAsync();
}

/// <inheritdoc cref="ICommunitySource" />
public sealed class CommunitySource(HttpClient http) : ICommunitySource
{
    private Task<IReadOnlyList<CommunityRegistry>>? _registries;
    private Task<IReadOnlyList<CommunityTheme>>? _themes;

    /// <inheritdoc />
    public Task<IReadOnlyList<CommunityRegistry>> GetRegistriesAsync() =>
        _registries ??= FetchAsync("community/registries.json", CommunityJson.Default.CommunityRegistryArray);

    /// <inheritdoc />
    public Task<IReadOnlyList<CommunityTheme>> GetThemesAsync() =>
        _themes ??= FetchAsync("community/themes.json", CommunityJson.Default.CommunityThemeArray);

    private async Task<IReadOnlyList<T>> FetchAsync<T>(
        string url, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T[]> type)
    {
        try
        {
            return await http.GetFromJsonAsync(url, type) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
