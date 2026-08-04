using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Registry;
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
        var hosts = TrustPolicy.ForeignHosts(
            ["button", "@acme/tag", "./items/tag.json", @"C:\registry\tag.json"], Config(), null);
        Assert.Empty(hosts);
    }

    [Fact]
    public void A_url_on_an_unknown_host_is_foreign()
    {
        var hosts = TrustPolicy.ForeignHosts(["https://evil.example/r/tag.json"], Config(), null);
        Assert.Equal(["https://evil.example"], hosts);
    }

    [Fact]
    public void The_default_registrys_own_host_is_trusted()
    {
        var hosts = TrustPolicy.ForeignHosts(["https://blaiz.io/r/button.json"], Config(), null);
        Assert.Empty(hosts);
    }

    [Fact]
    public void A_registry_override_is_trusted_instead_of_the_config_url()
    {
        var config = Config("https://blaiz.io/r");
        Assert.Empty(TrustPolicy.ForeignHosts(
            ["https://mirror.example/r/button.json"], config, "https://mirror.example/r"));
        Assert.Equal(["https://mirror.example"],
            TrustPolicy.ForeignHosts(["https://mirror.example/r/button.json"], config, null));
    }

    [Fact]
    public void Recorded_registries_are_trusted()
    {
        var config = Config();
        config.Registries["@acme"] = "https://acme.dev/r";

        Assert.Empty(TrustPolicy.ForeignHosts(["https://acme.dev/r/tag.json"], config, null));
    }

    [Fact]
    public void A_repository_address_is_foreign_until_that_repository_is_trusted()
    {
        var config = Config();

        Assert.Equal(["https://github.com/acme/toolkit"],
            TrustPolicy.ForeignHosts(["acme/toolkit/tag"], config, null));

        config.TrustedHosts.Add("https://github.com/acme/toolkit");
        Assert.Empty(TrustPolicy.ForeignHosts(["acme/toolkit/tag#v1.0.0"], config, null));

        // Per repository, not per host: the next project on github.com still asks.
        Assert.Equal(["https://github.com/evil/repo"],
            TrustPolicy.ForeignHosts(["evil/repo/tag"], config, null));
    }

    [Fact]
    public void Foreign_origins_are_distinct_and_ordered()
    {
        var hosts = TrustPolicy.ForeignHosts(
            [
                "https://zeta.example/r/a.json",
                "https://alpha.example/r/b.json",
                "https://zeta.example/r/c.json",
            ], Config(), null);
        Assert.Equal(["https://alpha.example", "https://zeta.example"], hosts);
    }
}
