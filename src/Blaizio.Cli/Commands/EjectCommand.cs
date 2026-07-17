using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>
/// Copies the materialized contract into the tokens file and marks the project ejected: the
/// <c>.blaizio/</c> imports are replaced with the sheets' content, frozen at the current version
/// and the user's to edit, and no later command re-manages them. Base stays — eject removes no
/// dependency (behavior/JS still ship via the package); it exists to FREEZE and own the styling
/// plumbing. Irreversible, so it confirms first (<c>-y</c> skips).
/// </summary>
public sealed class EjectCommand : AsyncCommand<GlobalSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;

        var config = await ConfigStore.LoadAsync(cwd, ct);
        if (config is null)
        {
            CliOutput.Error.MarkupLine("[red]Error:[/] No blaizio.json found. Run [white]blaizio init[/] first.");
            return 1;
        }

        if (config.Ejected)
        {
            if (settings.Json)
                Console.Out.WriteLine("""{"ejected":false,"alreadyEjected":true}""");
            else
                // The command's only output on this path - default color, not grey.
                settings.Line("Already ejected: the tokens file owns the contract.");
            return 0;
        }

        if (!settings.NonInteractive && !AnsiConsole.Confirm(
                "Copy the Blaizio contract into your tokens file and own it from here on? " +
                "[yellow]This is irreversible: future styling-plumbing updates (Blaizio.Base bumps) " +
                "will no longer reach the ejected copy.[/]",
                defaultValue: false))
        {
            settings.Warn("[yellow]Eject cancelled.[/]");
            return 0;
        }

        var result = await new TailwindSetup(new EmbeddedCssAssets()).EjectAsync(cwd, config.Css, ct);

        config.Ejected = true;
        await ConfigStore.SaveAsync(cwd, config, ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["ejected"] = true,
                ["input"] = result.InputPath,
                ["source"] = result.Materialized ? "materialized" : "embedded",
            }.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        AnsiConsole.MarkupLine($"[green]Ejected[/] the contract into {Markup.Escape(result.InputPath)} - it's yours now.");
        AnsiConsole.MarkupLine(result.Materialized
            ? "  [blue]source[/] .blaizio/ (version-matching the installed Blaizio.Base)"
            : "  [blue]source[/] the CLI's embedded copy [grey](the project was never built - no .blaizio/ found)[/]");
        AnsiConsole.MarkupLine(
            "[grey].blaizio/ still materializes on every build (harmless, nothing imports it). To stop it, add " +
            "<BlaizioMaterializeContract>false</BlaizioMaterializeContract> to the csproj.[/]");
        return 0;
    }
}
