using System.ComponentModel;
using Blaizio.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>update</c>.</summary>
public sealed class UpdateSettings : GlobalSettings
{
    /// <summary>Components to re-pull from the registry.</summary>
    [CommandArgument(0, "<COMPONENTS>")]
    [Description("Components to re-pull, overwriting local copies.")]
    public string[] Components { get; init; } = [];
}

/// <summary>Re-pulls components from the registry, overwriting local copies. Thin wrapper over <c>add --overwrite</c>.</summary>
public sealed class UpdateCommand : AsyncCommand<UpdateSettings>
{
    /// <inheritdoc />
    public override Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings)
    {
        var add = new AddSettings
        {
            Cwd = settings.Cwd,
            Yes = settings.Yes,
            Silent = settings.Silent,
            Json = settings.Json,
            Components = settings.Components,
            Overwrite = true,
        };
        return new AddCommand().ExecuteAsync(context, add);
    }
}
