using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// The message `update` puts over a ledger entry its registry cannot serve. The raw failure is a
/// bare path ("Registry file not found: '…/r/ember/editor.json'") - which names neither the item
/// nor the reason, and whose skin sub-folder reads like a skin bug rather than what it is: a
/// component installed from another registry but recorded under a plain name.
/// </summary>
public class UpdateLedgerMissTests
{
    private static BlaizioConfig Config(params string[] installed)
    {
        var config = new BlaizioConfig { Namespace = "App.Components.Ui", Registry = "https://blaiz.io/r" };
        foreach (var name in installed) config.Installed[name] = new InstalledItem();
        return config;
    }

    private static RegistryException NotFound(string location) =>
        new($"Registry file not found: '{location}'.", null, RegistryFailure.NotFound);

    [Fact]
    public void Names_the_item_and_points_at_the_namespaced_reinstall()
    {
        var message = UpdateCommand.LedgerMiss(
            NotFound(@"D:\repos\Blaizio\docs\Blaizio.Docs\wwwroot\r\ember\editor.json"), Config("editor", "toolbar"));

        Assert.Contains("'editor' is recorded in blaizio.json", message, StringComparison.Ordinal);
        Assert.Contains("https://blaiz.io/r", message, StringComparison.Ordinal);
        Assert.Contains("blaizio add @ns/editor", message, StringComparison.Ordinal);
        Assert.Contains("blaizio remove editor", message, StringComparison.Ordinal);
        // The original stays readable - the path is still the fastest way to see what was requested.
        Assert.Contains("editor.json", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Falls_back_to_a_generic_subject_when_the_item_is_not_in_the_ledger()
    {
        // A dependency of a ledger entry fails the same way. It is NOT in `installed`, so the
        // message must not claim it is recorded - but the fix still has to name it.
        var message = UpdateCommand.LedgerMiss(NotFound("https://blaiz.io/r/wisp/tag.json"), Config("editor"));

        Assert.Contains("a component recorded in blaizio.json", message, StringComparison.Ordinal);
        Assert.DoesNotContain("'tag' is recorded", message, StringComparison.Ordinal);
        Assert.Contains("blaizio add @ns/tag", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Survives_a_failure_message_without_a_readable_path()
    {
        var message = UpdateCommand.LedgerMiss(
            new RegistryException("something went wrong", null, RegistryFailure.NotFound), Config("editor"));

        Assert.Contains("a component recorded in blaizio.json", message, StringComparison.Ordinal);
        Assert.Contains("blaizio add @ns/<component>", message, StringComparison.Ordinal);
    }
}
