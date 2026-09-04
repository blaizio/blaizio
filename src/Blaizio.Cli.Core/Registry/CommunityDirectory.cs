using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>One community directory listing: a pointer to a registry someone published.</summary>
public sealed class DirectoryEntry
{
    /// <summary>The <c>@namespace</c> the registry is listed under.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The publisher's homepage.</summary>
    [JsonPropertyName("homepage")]
    public string? Homepage { get; init; }

    /// <summary>The built registry's base URL.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>One line about what the registry ships.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// The community registry directory: the reviewed listing behind the docs site's community page.
/// The CLI consults it as a FALLBACK when a reference names an <c>@namespace</c> the project has
/// not recorded - the entry is offered, and only an accepted (or unattended-with-warning) run
/// records it into <c>blaizio.json</c>. Recording stays the source of truth; the directory never
/// resolves silently.
/// </summary>
public static class CommunityDirectory
{
    /// <summary>Where the published directory lives.</summary>
    public const string DefaultUrl = "https://blaiz.io/community/registries.json";

    /// <summary>
    /// The directory location: <c>BLAIZIO_DIRECTORY</c> (a URL or a local file path - tests and
    /// air-gapped mirrors) or the published default.
    /// </summary>
    public static string Location =>
        Environment.GetEnvironmentVariable("BLAIZIO_DIRECTORY") is { Length: > 0 } overridden
            ? overridden
            : DefaultUrl;

    /// <summary>
    /// The directory's entry for <paramref name="ns"/> (e.g. <c>@acme</c>), or null - including on
    /// any failure. The lookup is a best-effort courtesy on an error path; an unreachable
    /// directory must never replace the real "unknown registry" message with a network one.
    /// </summary>
    public static async Task<DirectoryEntry?> FindAsync(HttpClient http, string ns, CancellationToken ct = default)
    {
        try
        {
            var location = Location;
            var isHttp = location.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            List<DirectoryEntry>? entries;
            if (isHttp)
            {
                // A courtesy lookup gets a short leash of its own, not the client's full timeout:
                // a host that accepts the connection and never answers (a parked domain, a
                // firewall that drops instead of refusing) must not hold up the real error.
                using var leash = CancellationTokenSource.CreateLinkedTokenSource(ct);
                leash.CancelAfter(LookupTimeout);
                await using var stream = await http.GetStreamAsync(location, leash.Token);
                entries = await JsonSerializer.DeserializeAsync(stream, CoreJson.Default.ListDirectoryEntry, leash.Token);
            }
            else
            {
                await using var stream = File.OpenRead(location);
                entries = await JsonSerializer.DeserializeAsync(stream, CoreJson.Default.ListDirectoryEntry, ct);
            }

            return entries?.FirstOrDefault(e =>
                string.Equals(e.Name, ns, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(e.Url));
        }
        // Only the CALLER's cancellation (Ctrl+C) propagates. HttpClient reports its own timeout
        // as a TaskCanceledException too, and letting that one out turned an unreachable
        // directory into "Cancelled." with exit 130 instead of the unknown-registry error.
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>How long the best-effort directory fetch may take before it is treated as absent.</summary>
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(5);
}
