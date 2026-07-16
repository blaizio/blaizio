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
/// <c>Styles/blaizio/</c> layout goes through the confirm-gated migration first.
/// </summary>
public sealed class UpdateCommand : AsyncCommand<UpdateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var config = services.RequireConfig();

        // v1 layout detected: migrate to v3 (confirm-gated; -y accepts) instead of half-updating.
        if (TailwindSetup.IsLegacyV1(settings.ResolvedCwd))
            return await MigrateAsync(context, settings, config, ct);

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

    /// <summary>
    /// The v1 → v3 migration: re-install every ledgered component from the recorded skin's
    /// inlined registry variants, compose the tokens file from the v1 managed sheets (the user's
    /// values, preset/fonts/pointer folded in), delete <c>Styles/blaizio/</c>, gitignore
    /// <c>.blaizio/</c> and strip the dead <c>style-*</c>/<c>preset-*</c> host classes.
    /// Destructive to local component edits, so it is confirm-gated (<c>-y</c> accepts). The
    /// component re-install runs FIRST: if the registry is unreachable, the project stays fully
    /// v1 instead of half-migrated.
    /// </summary>
    private static async Task<int> MigrateAsync(
        CommandContext context, UpdateSettings settings, Core.Configuration.BlaizioConfig config, CancellationToken ct)
    {
        var cwd = settings.ResolvedCwd;
        var components = config.Installed.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (!settings.NonInteractive && !AnsiConsole.Confirm(
                $"Migrate this project to the v3 CSS layout? [yellow]This re-installs {components.Length} component(s) " +
                $"(overwriting local edits — commit or stash first), rewrites the Tailwind input and deletes Styles/blaizio/.[/]"))
        {
            settings.Warn("[yellow]Migration cancelled. The project stays on the v1 layout.[/]");
            return 0;
        }

        // 1. Components first (network can fail; the CSS stays v1 until this succeeds). The
        //    registry client already resolves them to the recorded skin's inlined variants.
        if (components.Length > 0)
        {
            var exit = await new AddCommand().ExecuteAsync(context, new AddSettings
            {
                Cwd = settings.Cwd,
                Yes = settings.Yes,
                // A --json update must stay a single clean document: run the add leg silently.
                Silent = settings.Silent || settings.Json,
                Json = false,
                Registry = settings.Registry,
                Components = components,
                Overwrite = true,
            });
            if (exit != 0)
                return exit;
        }

        // 2. The CSS leg: compose + rewrite + delete, then record what happened.
        var migration = await new TailwindSetup(new EmbeddedCssAssets())
            .MigrateAsync(cwd, config.Output, config.Preset, config.Css, ct);
        config.CssCreated |= migration.InputWasCliOwned;
        await Core.Configuration.ConfigStore.SaveAsync(cwd, config, ct);

        // 3. The style-*/preset-* classes on <html> are dead in v3 - EnsureAsync strips them
        //    (and only ever adds wiring that is missing, so a customized host stays intact).
        var host = await new HostPageSetup().EnsureAsync(cwd, ct: ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(new System.Text.Json.Nodes.JsonObject
            {
                ["migrated"] = true,
                ["input"] = migration.InputPath,
                ["components"] = components.Length,
                ["removed"] = new System.Text.Json.Nodes.JsonArray(
                    [.. migration.Removed.Select(r => (System.Text.Json.Nodes.JsonNode)r)]),
            }.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        AnsiConsole.MarkupLine($"[green]Migrated to the v3 CSS layout.[/]");
        AnsiConsole.MarkupLine($"  [blue]css[/] rewrote {Markup.Escape(migration.InputPath)} (your token values carried over)");
        if (components.Length > 0)
            AnsiConsole.MarkupLine($"  [blue]components[/] re-installed {components.Length} from skin [cyan]{Markup.Escape(config.Theme)}[/]");
        foreach (var removed in migration.Removed)
            AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(removed)}");
        foreach (var change in host.Changes)
            AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        AnsiConsole.MarkupLine("[grey]The contract sheets now materialize into .blaizio/ on 'dotnet build' (gitignored).[/]");
        return 0;
    }
}
