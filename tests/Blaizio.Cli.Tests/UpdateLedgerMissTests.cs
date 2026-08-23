using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Configuration;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// The message `update` puts over a ledger entry it could not re-pull. The raw failure is a bare
/// path ("Registry file not found: '…/r/ember/editor.json'") - which names neither the item nor
/// the reason, and whose skin sub-folder reads like a skin bug rather than what it is: an item
/// that did not come from the default registry. Which fix applies depends on whether the record
/// says where it came from.
/// </summary>
public class UpdateLedgerMissTests
{
    private static BlaizioConfig Config(params string[] installed)
    {
        var config = new BlaizioConfig { Namespace = "App.Components.Ui", Registry = "https://blaiz.io/r" };
        foreach (var name in installed) config.Installed[name] = new InstalledItem();
        return config;
    }

    private const string Failure = @"Registry file not found: 'D:\repos\Blaizio\docs\Blaizio.Docs\wwwroot\r\ember\editor.json'.";

    [Fact]
    public void Names_the_item_and_covers_every_way_it_could_have_arrived()
    {
        var message = UpdateCommand.LedgerMiss("editor", Failure, Config("editor", "toolbar"));

        Assert.Contains("'editor' is recorded in blaizio.json", message, StringComparison.Ordinal);
        Assert.Contains("https://blaiz.io/r", message, StringComparison.Ordinal);
        // A file or URL install is the common case this message used to be blind to.
        Assert.Contains("blaizio add <path-or-url>", message, StringComparison.Ordinal);
        Assert.Contains("blaizio add @ns/editor", message, StringComparison.Ordinal);
        // Remove is not "forget the record": it deletes what the item installed, and says so.
        Assert.Contains("blaizio remove editor (this deletes the files it installed)", message, StringComparison.Ordinal);
        // The original stays readable - the path is still the fastest way to see what was requested.
        Assert.Contains("editor.json", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Strips_a_pin_off_the_reference_before_naming_the_item()
    {
        var message = UpdateCommand.LedgerMiss("editor@1.2.0", Failure, Config("editor"));

        Assert.Contains("'editor' is recorded in blaizio.json", message, StringComparison.Ordinal);
        Assert.Contains("blaizio remove editor", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_with_a_source_points_back_at_that_source()
    {
        var config = Config();
        config.Installed["editor"] = new InstalledItem { Source = "./registry/editor.json" };

        var message = UpdateCommand.LedgerMiss("./registry/editor.json", "Registry file not found: './registry/editor.json'.", config);

        Assert.Contains("'editor' was installed from ./registry/editor.json", message, StringComparison.Ordinal);
        Assert.Contains("blaizio add <path-or-url>", message, StringComparison.Ordinal);
        Assert.Contains("blaizio remove editor", message, StringComparison.Ordinal);
        // Not a namespace problem, so no namespace advice.
        Assert.DoesNotContain("registry add", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Falls_back_to_the_reference_itself_when_it_is_not_in_the_ledger()
    {
        var message = UpdateCommand.LedgerMiss("tag", "Registry file not found: 'https://blaiz.io/r/wisp/tag.json'.", Config("editor"));

        Assert.Contains("'tag' is recorded in blaizio.json", message, StringComparison.Ordinal);
        Assert.Contains("blaizio add @ns/tag", message, StringComparison.Ordinal);
    }
}
