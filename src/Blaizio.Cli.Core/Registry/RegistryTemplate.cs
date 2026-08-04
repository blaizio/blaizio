namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// The placeholders a registry address may carry. A registry that serves its items at
/// <c>&lt;base&gt;/&lt;name&gt;.json</c> needs none of this - the base URL alone is enough. One that
/// serves them anywhere else records a template instead, and the CLI fills it in per request.
/// </summary>
public static class RegistryTemplate
{
    /// <summary>The item name. Its presence is what makes an address a template.</summary>
    public const string Name = "{name}";

    /// <summary>The project's recorded skin, for registries that publish one variant per style.</summary>
    public const string Style = "{style}";

    /// <summary>The item name a templated registry serves its catalogue under.</summary>
    public const string IndexName = "index";

    /// <summary>True when the address places items itself rather than following the default layout.</summary>
    public static bool IsTemplate(string address) => address.Contains(Name, StringComparison.Ordinal);

    /// <summary>
    /// The address with both placeholders filled by a stand-in, so a template can be checked for
    /// well-formedness (by <c>registry add</c>) without pretending to resolve anything.
    /// </summary>
    public static string Sample(string address) => address
        .Replace(Name, "sample", StringComparison.Ordinal)
        .Replace(Style, "sample", StringComparison.Ordinal);
}
