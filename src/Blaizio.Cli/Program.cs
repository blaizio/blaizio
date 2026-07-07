using Blaizio.Cli;
using Blaizio.Cli.Commands;
using Blaizio.Cli.Infrastructure;
using Spectre.Console.Cli;

// Ctrl+C cancels in-flight registry fetches / child processes instead of leaving them orphaned.
CliCancellation.Install();

// `-ns` is a two-character short option, which Spectre's parser reserves for long options only.
// Normalize it to the canonical `--namespace` before parsing so the documented alias still works.
var cliArgs = CliApp.NormalizeNamespaceAlias(args);

// No command + no flags => run `init` interactively (the solo TUI experience).
var app = new CommandApp<InitCommand>();
app.Configure(CliApp.Configure);

return await app.RunAsync(cliArgs);
