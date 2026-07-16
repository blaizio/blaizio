using System.ComponentModel;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for the update flow behind <c>add --update</c>.</summary>
public sealed class UpdateSettings : GlobalSettings
{
    /// <summary>Components to re-pull. Empty re-pulls everything recorded in blaizio.json.</summary>
    [CommandArgument(0, "[components...]")]
    [Description("Components to re-pull, overwriting local copies (default: all installed)")]
    public string[] Components { get; init; } = [];
}

/// <summary>
/// The engine behind <c>add --update</c> (not registered as a command of its own). Re-pulls
/// components from the registry — the recorded skin's inlined variants — overwriting local copies
/// (thin wrapper over <c>add --overwrite</c>). No styling leg exists in v3: the tokens file is
/// the user's, and the contract plumbing version-tracks the Blaizio.Base package (materialized
/// into <c>.blaizio/</c> at build), so there is nothing for the CLI to rewrite. A host page that
/// already loads <c>boot.js</c> counts as wired and is skipped. A project still on the v1
/// <c>Styles/blaizio/</c> layout gets the migration pointer.
/// </summary>
public sealed class UpdateCommand : AsyncCommand<UpdateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var config = services.RequireConfig();

        // v1 layout detected: the migration (compose the tokens file, re-install components from
        // the skin variants, delete Styles/blaizio/) is its own confirm-gated leg — coming with
        // the next step of the v3 build order. Until then, surface it instead of half-updating.
        if (TailwindSetup.IsLegacyV1(settings.ResolvedCwd))
            settings.Warn(
                "[yellow]This project uses the old Styles/blaizio/ CSS layout.[/] " +
                "The v1 → v3 migration lands in the next CLI release; components were still re-pulled from the current skin.");

        var components = settings.Components;
        if (components.Length == 0)
        {
            // No args: re-pull everything blaizio.json records as installed.
            components = [.. config.Installed.Keys.Order(StringComparer.OrdinalIgnoreCase)];

            if (components.Length == 0)
            {
                settings.Warn("[yellow]No installed components recorded in blaizio.json.[/] Run [white]blaizio add <component>[/] first.");
                if (settings.Json)
                    Console.Out.WriteLine("""{"items":[],"nugetPackages":[],"files":[],"namespace":"","importsUpdated":false,"dryRun":false}""");
                return 0;
            }
        }

        var add = new AddSettings
        {
            Cwd = settings.Cwd,
            Yes = settings.Yes,
            Silent = settings.Silent,
            Json = settings.Json,
            Registry = settings.Registry,
            Components = components,
            Overwrite = true,
        };
        var exit = await new AddCommand().ExecuteAsync(context, add);
        if (exit != 0)
            return exit;

        // No styling leg: the tokens file is the user's and the contract sheets version-track the
        // Blaizio.Base package. Only the imports inside a bundler-recorded input are kept in sync
        // (paths can go stale when the output dir moves); the default flow's own file is never
        // touched by an update.
        TailwindResult? tailwind = null;
        if (config.Css is not null)
        {
            tailwind = await new TailwindSetup(new EmbeddedCssAssets()).EnsureAsync(
                settings.ResolvedCwd, config.Output, new TailwindOptions(Rtl: config.Rtl),
                config.Preset, topUpUserInput: false, cssInput: config.Css,
                chart: config.Chart ?? "default", radius: config.Radius ?? "default", ct: ct);
        }

        // The host page: once it loads boot.js it's wired and the app's to evolve - repatching
        // would re-guess hrefs it may have customized (fingerprinted links). Only an unwired host
        // is (re)wired here.
        var hostSetup = new HostPageSetup();
        var host = hostSetup.IsWired(settings.ResolvedCwd)
            ? new HostPageResult()
            : await hostSetup.EnsureAsync(settings.ResolvedCwd, ct: ct);

        if (!settings.Json && !settings.Silent)
        {
            if (tailwind is not null)
                AnsiConsole.MarkupLine($"  [blue]css[/] synced imports in {Markup.Escape(tailwind.InputPath)}");
            foreach (var change in host.Changes)
                AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        }

        return 0;
    }
}
