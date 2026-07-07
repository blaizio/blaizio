using System.ComponentModel;
using Blaizio.Cli.Infrastructure;
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

/// <summary>Re-pulls components from the registry, overwriting local copies. Thin wrapper over <c>add --overwrite</c>.</summary>
public sealed class UpdateCommand : AsyncCommand<UpdateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings)
    {
        var components = settings.Components;
        if (components.Length == 0)
        {
            // No args: re-pull everything blaizio.json records as installed.
            var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, CliCancellation.Token);
            var config = services.RequireConfig();
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
        return await new AddCommand().ExecuteAsync(context, add);
    }
}
