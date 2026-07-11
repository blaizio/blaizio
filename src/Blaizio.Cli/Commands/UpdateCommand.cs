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
/// components from the registry, overwriting local copies (thin wrapper over <c>add --overwrite</c>),
/// then re-ensures the non-component pieces - but only where the project hasn't taken ownership:
/// a custom <c>Styles/app.css</c> (its own Tailwind pipeline, no managed assets) is never touched,
/// and a host page that already loads <c>boot.js</c> counts as wired and is skipped. Adopting an
/// unwired project is <c>init</c>'s job; the update flow only repairs what <c>init</c> put there.
/// </summary>
public sealed class UpdateCommand : AsyncCommand<UpdateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var config = services.RequireConfig();

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

        // Refresh the managed styling. Bundler mode (blaizio.json `css`) syncs the managed imports
        // inside the recorded input - keeping the skin/preset lines current is the whole point.
        // Otherwise never adopt what the app owns: a custom Styles/app.css (its own Tailwind
        // pipeline) is left entirely alone, and an existing (unmanaged) input is never appended to -
        // only init tops up a user file.
        TailwindResult? tailwind = null;
        if (config.Css is not null || !TailwindSetup.HasCustomInput(settings.ResolvedCwd))
        {
            // The pointer flag isn't recorded in config - preserve whatever options.css state exists.
            var pointer = File.Exists(Path.Combine(settings.ResolvedCwd, "Styles", "blaizio", "options.css"));
            tailwind = await new TailwindSetup(new EmbeddedCssAssets()).EnsureAsync(
                settings.ResolvedCwd, config.Output, config.Theme, new TailwindOptions(pointer, config.Rtl),
                config.Preset, topUpUserInput: false, cssInput: config.Css, ct: ct);
        }

        // Same for the host page: once it loads boot.js it's wired and the app's to evolve -
        // repatching would re-guess hrefs/classes it may have customized (fingerprinted links,
        // its own skin switching). Only an unwired host is (re)wired here.
        var hostSetup = new HostPageSetup();
        var host = hostSetup.IsWired(settings.ResolvedCwd)
            ? new HostPageResult()
            : await hostSetup.EnsureAsync(settings.ResolvedCwd, config.Theme, preset: config.Preset, ct: ct);

        if (!settings.Json && !settings.Silent)
        {
            AnsiConsole.MarkupLine(tailwind is not null
                ? $"  [blue]css[/] refreshed {Markup.Escape(tailwind.InputPath)} (skin [cyan]{Markup.Escape(config.Theme)}[/])"
                : "  [blue]css[/] [grey]custom Styles/app.css - left untouched[/]");
            foreach (var change in host.Changes)
                AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        }

        return 0;
    }
}
