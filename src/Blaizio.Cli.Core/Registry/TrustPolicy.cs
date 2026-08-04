using Blaizio.Cli.Core.Configuration;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// The direct-source trust policy: which origins a component reference may be fetched from
/// without a fresh confirmation. Policy, not presentation - the CLI decides how to ask.
/// </summary>
public static class TrustPolicy
{
    /// <summary>
    /// Origins of item references that are neither the effective default registry nor any registry
    /// recorded under <c>registry add</c>. A direct URL counts as its own origin; an
    /// <c>owner/repo/item</c> address counts as that repository, so approving one repository does
    /// not approve every repository on the host. Names, <c>@namespaces</c> and local paths never
    /// count - they resolve against sources the project already chose.
    /// </summary>
    public static IReadOnlyList<string> ForeignHosts(
        IReadOnlyList<string> components, BlaizioConfig config, string? registryOverride)
    {
        var trusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Trust(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                return;

            // The origin, and the origin WITH its path: a URL registry is trusted per host, while a
            // repository is trusted as "https://github.com/owner/repo" and nothing wider.
            trusted.Add(uri.GetLeftPart(UriPartial.Authority));
            trusted.Add(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
        }
        Trust(registryOverride ?? config.Registry);
        foreach (var recorded in config.Registries.Values)
            Trust(recorded.Url);
        foreach (var host in config.TrustedHosts)
            Trust(host);

        return [.. components
            .Select(Origin)
            .OfType<string>()
            .Where(origin => !trusted.Contains(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

        static string? Origin(string reference)
        {
            if (Uri.TryCreate(reference, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                return uri.GetLeftPart(UriPartial.Authority);

            // Per repository, not per host: github.com is not a trust boundary, and approving one
            // project there must not silently approve the next one.
            return GitHubAddress.TryParse(reference, out var address)
                ? $"https://github.com/{address.Repository}"
                : null;
        }
    }
}
