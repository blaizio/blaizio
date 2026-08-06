using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;
using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The community-directory fallback: a reference naming an <c>@namespace</c> the project has not
/// recorded is looked up in the reviewed directory behind the community page. A hit is OFFERED,
/// never used silently - an interactive run confirms, an unattended one proceeds with the warning
/// on record (the same posture as the direct-URL trust gate) - and acceptance records the mapping
/// in <c>blaizio.json</c>, exactly as <c>blaizio registry add</c> would have. From then on the
/// project resolves it locally; the CLI still never looks a namespace up remotely at install time.
/// </summary>
internal static class DirectoryFallback
{
    /// <summary>
    /// Record directory entries for the unrecorded <c>@namespace</c>s among
    /// <paramref name="references"/> (item references or bare namespaces). Returns true when
    /// anything was recorded - the caller's registry client map is stale then and must be rebuilt.
    /// Namespaces the directory does not list are left alone; the ordinary "unknown registry"
    /// error downstream stays the answer for them.
    /// </summary>
    public static async Task<bool> TryRecordAsync(
        GlobalSettings settings, string cwd, BlaizioConfig? config,
        IEnumerable<string> references, CancellationToken ct)
    {
        if (config is null)
            return false;

        var unknown = references
            .Select(Namespace)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            // The recorded map is consulted case-insensitively at resolve time; match that here or
            // a differently-cased reference would re-record a duplicate entry.
            .Where(ns => !config.Registries.Keys.Any(k => string.Equals(k, ns, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unknown.Count == 0)
            return false;

        var recorded = false;
        foreach (var ns in unknown)
        {
            if (await CommunityDirectory.FindAsync(CliServices.Http, ns, ct) is not { } entry)
                continue;

            settings.Warn(
                $"[yellow]{Markup.Escape(ns)} is not recorded in this project.[/] The community directory lists it: " +
                $"{Markup.Escape(entry.Url)}{(string.IsNullOrWhiteSpace(entry.Description) ? "" : $" - {Markup.Escape(entry.Description!)}")}");
            if (!settings.NonInteractive && AnsiConsole.Profile.Capabilities.Interactive
                && !AnsiConsole.Confirm($"Record {ns} and continue?", defaultValue: true))
                continue;

            config.Registries[ns] = entry.Url;
            settings.Line($"  [blue]registry[/] recorded [cyan]{Markup.Escape(ns)}[/] → {Markup.Escape(entry.Url)} (community directory)");
            recorded = true;
        }

        if (recorded)
            await ConfigStore.SaveAsync(cwd, config, ct);
        return recorded;
    }

    /// <summary>The <c>@namespace</c> a reference addresses: <c>@acme/tag</c>, or the bare
    /// <c>@acme</c> a search positional uses. Null for everything else.</summary>
    private static string? Namespace(string reference)
    {
        if (reference.Length < 2 || reference[0] != '@')
            return null;
        var slash = reference.IndexOf('/');
        return slash < 0 ? reference : slash >= 2 ? reference[..slash] : null;
    }
}
