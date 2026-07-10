using System.ComponentModel;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>update</c>.</summary>
public sealed class UpdateSettings : GlobalSettings
{
    /// <summary>Components to re-pull. Empty re-pulls everything recorded in blaizio.json.</summary>
    [CommandArgument(0, "[COMPONENTS]")]
    [Description("Components to re-pull, overwriting local copies (default: all installed).")]
    public string[] Components { get; init; } = [];
}

/// <summary>
/// Re-pulls components from the registry, overwriting local copies (thin wrapper over
/// <c>add --overwrite</c>), then re-ensures the non-component pieces the same way <c>init</c> does:
/// the managed CSS assets / Tailwind input and the host-page wiring (skin class, stylesheet link,
/// boot.js). Both are idempotent, so anything missing is added and anything current is untouched.
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

        // Top up the non-component pieces so an update also repairs a partially-wired project.
        // The pointer flag isn't recorded in config - preserve whatever options.css state exists.
        var pointer = File.Exists(Path.Combine(settings.ResolvedCwd, "Styles", "blaizio", "options.css"));
        var tailwind = await new TailwindSetup(new EmbeddedCssAssets())
            .EnsureAsync(settings.ResolvedCwd, config.Output, config.Theme, new TailwindOptions(pointer, config.Rtl), config.Preset, ct);
        var host = await new HostPageSetup().EnsureAsync(settings.ResolvedCwd, config.Theme, config.Rtl, preset: config.Preset, ct: ct);

        if (!settings.Json && !settings.Silent)
        {
            AnsiConsole.MarkupLine($"  [blue]css[/] refreshed {Markup.Escape(tailwind.InputPath)} (skin [cyan]{Markup.Escape(config.Theme)}[/])");
            foreach (var change in host.Changes)
                AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        }

        return 0;
    }
}
