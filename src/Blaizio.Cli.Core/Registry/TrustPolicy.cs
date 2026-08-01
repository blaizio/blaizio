using Blaizio.Cli.Core.Configuration;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// The direct-URL trust policy: which origins a component reference may be fetched from without
/// a fresh confirmation. Policy, not presentation - the CLI decides how to ask.
/// </summary>
public static class TrustPolicy
{
    /// <summary>
    /// Origins of direct-URL item references that are neither the effective default registry nor
    /// any registry recorded under <c>registry add</c>. Non-URL references (names, @namespaces,
    /// local paths) never count - they resolve against sources the project already chose.
    /// </summary>
    public static IReadOnlyList<string> ForeignHosts(
        IReadOnlyList<string> components, BlaizioConfig config, string? registryOverride)
    {
        var trusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Trust(string? url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                trusted.Add(uri.GetLeftPart(UriPartial.Authority));
        }
        Trust(registryOverride ?? config.Registry);
        foreach (var recorded in config.Registries.Values)
            Trust(recorded);
        foreach (var host in config.TrustedHosts)
            Trust(host);

        return [.. components
            .Select(reference => Uri.TryCreate(reference, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https" ? uri.GetLeftPart(UriPartial.Authority) : null)
            .OfType<string>()
            .Where(origin => !trusted.Contains(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }
}
