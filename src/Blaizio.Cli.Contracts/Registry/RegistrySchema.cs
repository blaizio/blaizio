namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// Where the published JSON Schemas live. A <c>$schema</c> key pointing at one of these is what
/// makes an editor complete and validate a registry file as it is typed; the CLI writes the key
/// into everything it generates and ignores it on the way back in.
/// </summary>
public static class RegistrySchema
{
    /// <summary>Schema for a manifest (<c>registry.json</c>) and for the built <c>index.json</c>.</summary>
    public const string Registry = "https://blaiz.io/schema/registry.json";

    /// <summary>Schema for one built item document.</summary>
    public const string Item = "https://blaiz.io/schema/registry-item.json";
}
