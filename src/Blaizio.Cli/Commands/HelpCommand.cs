using System.ComponentModel;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>help</c>.</summary>
public sealed class HelpSettings : CommandSettings
{
    /// <summary>Command path to describe (e.g. <c>add</c> or <c>tailwind setup</c>); empty shows the root help.</summary>
    [CommandArgument(0, "[command]")]
    [Description("The command to show help for")]
    public string[] Command { get; init; } = [];
}

/// <summary>
/// <c>blaizio help [command]</c> — the command-shaped twin of <c>-h</c>. Re-runs the same app
/// surface with <c>--help</c> appended so both paths render through the one help provider.
/// </summary>
public sealed class HelpCommand : AsyncCommand<HelpSettings>
{
    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, HelpSettings settings, CancellationToken cancellationToken)
    {
        var app = new CommandApp();
        app.Configure(CliApp.Configure);
        return app.RunAsync([.. settings.Command, "--help"]);
    }
}
