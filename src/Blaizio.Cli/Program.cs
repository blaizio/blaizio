using Blaizio.Cli;
using Blaizio.Cli.Commands;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

// Ctrl+C cancels in-flight registry fetches / child processes instead of leaving them orphaned.
CliCancellation.Install();

// A command typed as a flag (`blaizio -update`) would otherwise be parsed as options of the default
// `init` command and fail with an unrelated message. Catch it up front with an actionable error.
if (CliApp.DetectFlaggedCommand(args) is { } flagged)
{
    CliOutput.Error.MarkupLine(
        $"[red]Error:[/] '{Spectre.Console.Markup.Escape(args[0])}' is not an option. " +
        $"Did you mean the command [white]{flagged}[/]? Run: [white]blaizio {flagged}[/]");
    return 1;
}

// `-ns` is a two-character short option, which Spectre's parser reserves for long options only.
// Normalize it to the canonical `--namespace` before parsing so the documented alias still works.
var cliArgs = CliApp.NormalizeNamespaceAlias(args);

// No command + no flags => run `init` interactively (the solo TUI experience).
var app = new CommandApp<InitCommand>();
app.Configure(CliApp.Configure);

return await app.RunAsync(cliArgs);
