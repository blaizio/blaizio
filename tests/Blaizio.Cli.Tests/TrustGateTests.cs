using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Configuration;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// The trust gate behind direct-URL installs: which origins count as foreign (prompt-worthy)
/// versus already chosen (the configured registry, everything under `registry add`).
/// </summary>
public class TrustGateTests
{
    private static BlaizioConfig Config(string registry = "https://blaiz.io/r") => new()
    {
        Namespace = "App.Ui",
        Registry = registry,
    };

    [Fact]
    public void Plain_names_namespaces_and_local_paths_are_never_foreign()
    {
        var hosts = AddCommand.ForeignHosts(
            ["button", "@acme/tag", "./items/tag.json", @"C:\registry\tag.json"], Config(), null);
        Assert.Empty(hosts);
    }

    [Fact]
    public void A_url_on_an_unknown_host_is_foreign()
    {
        var hosts = AddCommand.ForeignHosts(["https://evil.example/r/tag.json"], Config(), null);
        Assert.Equal(["https://evil.example"], hosts);
    }

    [Fact]
    public void The_default_registrys_own_host_is_trusted()
    {
        var hosts = AddCommand.ForeignHosts(["https://blaiz.io/r/button.json"], Config(), null);
        Assert.Empty(hosts);
    }

    [Fact]
    public void A_registry_override_is_trusted_instead_of_the_config_url()
    {
        var config = Config("https://blaiz.io/r");
        Assert.Empty(AddCommand.ForeignHosts(
            ["https://mirror.example/r/button.json"], config, "https://mirror.example/r"));
        Assert.Equal(["https://mirror.example"],
            AddCommand.ForeignHosts(["https://mirror.example/r/button.json"], config, null));
    }

    [Fact]
    public void Recorded_registries_are_trusted()
    {
        var config = Config();
        config.Registries["@acme"] = "https://acme.dev/r";

        Assert.Empty(AddCommand.ForeignHosts(["https://acme.dev/r/tag.json"], config, null));
    }

    [Fact]
    public void Foreign_origins_are_distinct_and_ordered()
    {
        var hosts = AddCommand.ForeignHosts(
            [
                "https://zeta.example/r/a.json",
                "https://alpha.example/r/b.json",
                "https://zeta.example/r/c.json",
            ], Config(), null);
        Assert.Equal(["https://alpha.example", "https://zeta.example"], hosts);
    }
}
