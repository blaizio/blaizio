using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli;

/// <summary>
/// The command surface, factored out of <c>Program</c> so tests can host the exact same app
/// inside a <c>CommandAppTester</c>.
/// </summary>
internal static class CliApp
{
    /// <summary>
    /// Every top-level command/branch name. Used both to build the app and to catch a command typed
    /// as a flag (<c>blaizio -update</c>), which would otherwise be parsed as options of the default
    /// <see cref="InitCommand"/> and fail with a baffling message.
    /// </summary>
    // "help" stays out: `--help`/`-h` must reach Spectre as the built-in help flag, not be
    // "corrected" to the help command.
    private static readonly string[] CommandNames =
    [
        "init", "apply", "add", "docs", "search", "view", "uninstall", "deinit", "eject", "info",
        "contrast", "generate", "build", "tailwind", "preset", "registry",
    ];

    /// <summary>Register every command, branch and the exception handler.</summary>
    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("blaizio");
        config.SetApplicationVersion(ToolInfo.Version);
        config.SetHelpProvider(new BlaizioHelpProvider());

        // Runtime exceptions (registry down, no config, install failed) render as a clean one-liner;
        // set BLAIZIO_DEBUG=1 for the full trace. Parse/usage errors keep Spectre's own nice rendering.
        config.SetExceptionHandler((ex, _) =>
        {
            // Ctrl+C: quiet exit with the conventional 130.
            if (ex is OperationCanceledException)
            {
                CliOutput.Error.MarkupLine("[yellow]Cancelled.[/]");
                return 130;
            }

            // Errors go to stderr so a piped --json stdout stream stays clean.
            var label = ex is RegistryException ? "Registry error" : "Error";
            CliOutput.Error.MarkupLine($"[red]{label}:[/] {Spectre.Console.Markup.Escape(ex.Message)}");
            // The default registry is not deployed yet - a raw "could not reach" there is a trap.
            if (ex is RegistryException && ex.Message.Contains("blaiz.io", StringComparison.OrdinalIgnoreCase))
                CliOutput.Error.MarkupLine(
                    "[grey]The public blaiz.io registry is not live yet. Point the project at your registry: " +
                    "edit \"registry\" in blaizio.json, or run [white]blaizio init --registry <url|path>[/].[/]");
            if (Environment.GetEnvironmentVariable("BLAIZIO_DEBUG") == "1")
                CliOutput.Error.WriteException(ex);
            return ex is RegistryException ? 2 : 1;
        });

        // Registration order is display order: the help provider lists commands as declared here.
        config.AddCommand<InitCommand>("init")
            .WithDescription("Initialize a project: write blaizio.json, install packages, add components");
        config.AddCommand<ApplyCommand>("apply")
            .WithDescription("Apply a preset to an existing project");
        config.AddCommand<AddCommand>("add")
            .WithDescription("Add components (and their dependencies) into the project");
        config.AddCommand<DocsCommand>("docs")
            .WithDescription("Get docs, api references and usage examples for components");
        config.AddCommand<ViewCommand>("view")
            .WithDescription("Print a component's metadata and files without writing");
        config.AddCommand<SearchCommand>("search")
            .WithDescription("Search items from registries");
        config.AddCommand<UninstallCommand>("uninstall")
            .WithAlias("deinit") // the pre-rename name; scripts keep working
            .WithDescription("Undo init and add: remove the tracked components, packages and configuration");
        config.AddCommand<EjectCommand>("eject")
            .WithDescription("Copy the contract sheets into your tokens file and own the styling plumbing");
        config.AddCommand<InfoCommand>("info")
            .WithDescription("Show project and configuration details");
        config.AddCommand<ContrastCommand>("contrast")
            .WithDescription("Audit the tokens file's colors for WCAG AA contrast (light + dark)");
        config.AddCommand<GenerateCommand>("generate")
            .WithDescription("Scan the Blaizio.Ui source tree into a registry.json manifest (maintainers)");
        config.AddCommand<BuildCommand>("build")
            .WithDescription("Build components for a blaizio registry");
        config.AddBranch("tailwind", tw =>
        {
            tw.SetDescription("Inspect or wire the Tailwind compile pipeline");
            tw.AddCommand<TailwindDetectCommand>("detect")
                .WithDescription("Report which Tailwind pipelines are present and which is recommended");
            tw.AddCommand<TailwindSetupCommand>("setup")
                .WithDescription("Wire a Tailwind pipeline (standalone, node, …) into the project");
            tw.AddCommand<TailwindFetchCommand>("fetch")
                .WithDescription("Fetch the standalone Tailwind binary");
        });
        config.AddBranch("preset", preset =>
        {
            preset.SetDescription("Manage presets");
            preset.AddCommand<PresetDecodeCommand>("decode")
                .WithDescription("Decode a preset code");
            preset.AddCommand<PresetResolveCommand>("resolve")
                .WithDescription("Resolve a preset from your project");
            preset.AddCommand<PresetUrlCommand>("url")
                .WithDescription("Get the create URL for a preset code");
            preset.AddCommand<PresetOpenCommand>("open")
                .WithDescription("Open a preset code in the browser");
        });
        config.AddBranch("registry", registry =>
        {
            registry.SetDescription("Manage registries");
            registry.AddCommand<RegistryAddCommand>("add")
                .WithDescription("Add registries to your project");
            registry.AddCommand<RegistryValidateCommand>("validate")
                .WithDescription("Validate a blaizio registry");
        });
        config.AddCommand<HelpCommand>("help")
            .WithDescription("Display help for command");

#if DEBUG
        config.ValidateExamples();
#endif
    }

    /// <summary>
    /// A command name typed as a flag at the command slot (<c>-update</c>, <c>--add</c>), or <c>null</c>
    /// when the first argument is a real command/option. Without this, such a token is parsed as options
    /// of the default <see cref="InitCommand"/> and dies on an unrelated message (e.g. a missing
    /// <c>--template</c> value). The command name (dashes stripped) is returned so the caller can suggest
    /// the correct form. <c>--help</c>/<c>--version</c>/real init flags don't match a command, so they
    /// pass through untouched.
    /// </summary>
    public static string? DetectFlaggedCommand(string[] input)
    {
        // Only the first token matters - it's the command slot. Later dashed tokens are that
        // command's own options and belong to Spectre.
        var first = input.FirstOrDefault();
        if (first is null || first.Length == 0 || first[0] != '-')
            return null;

        var name = first.TrimStart('-');
        // Return the canonical name (not the raw input) so the suggestion is always correctly cased.
        return CommandNames.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Normalize the documented <c>-ns</c> alias (Spectre reserves two-char shorts).</summary>
    public static string[] NormalizeNamespaceAlias(string[] input)
    {
        var copy = (string[])input.Clone();
        for (var i = 0; i < copy.Length; i++)
        {
            if (copy[i] == "--")
                break; // everything past the terminator is a literal argument
            if (copy[i] == "-ns")
                copy[i] = "--namespace";
        }
        return copy;
    }
}
