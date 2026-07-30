using System.ComponentModel;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// Options every command shares: working directory and output behavior. Commands that confirm
/// derive <see cref="ConfirmSettings"/> (adds <c>-y</c>); commands that read a registry derive
/// <see cref="RegistrySettings"/> (adds <c>--registry</c>); commands that do both derive
/// <see cref="ConfirmRegistrySettings"/>. A flag only exists where the command honors it -
/// an accepted-and-ignored option is a lie in the help screen.
/// </summary>
public class GlobalSettings : CommandSettings
{
    /// <summary>Working directory the command operates on. Defaults to the current directory.
    /// The live default is appended by <see cref="BlaizioHelpProvider"/>.</summary>
    [CommandOption("-c|--cwd <cwd>")]
    [Description("The working directory. Defaults to the current directory")]
    public string? Cwd { get; init; }

    /// <summary>Suppress all non-essential output.</summary>
    [CommandOption("-s|--silent")]
    [Description("Mute output (default: false)")]
    public bool Silent { get; init; }

    /// <summary>Emit machine-readable JSON instead of formatted console output.</summary>
    [CommandOption("--json")]
    [Description("Output as JSON, for scripts, IDE plugins, and MCP (default: false)")]
    public bool Json { get; init; }

    /// <summary>Skip interactive prompts and take defaults. The <c>-y</c> flag lives on the
    /// Confirm tiers; here it is programmatic only (command-to-command forwarding).</summary>
    public virtual bool Yes { get; init; }

    /// <summary>Registry base URL or local path, overriding blaizio.json. The <c>--registry</c>
    /// flag lives on the Registry tiers; here it is programmatic only.</summary>
    public virtual string? Registry { get; init; }

    /// <summary>Absolute working directory, resolving <see cref="Cwd"/> against the process directory.</summary>
    public string ResolvedCwd => Path.GetFullPath(Cwd ?? Directory.GetCurrentDirectory());

    /// <summary>True when the command should run without any interactive prompt.</summary>
    public bool NonInteractive => Yes || Json || Silent;
}

/// <summary>Settings for commands that prompt for confirmation: adds <c>-y|--yes</c>.</summary>
public class ConfirmSettings : GlobalSettings
{
    /// <inheritdoc />
    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt (default: false)")]
    public override bool Yes { get; init; }
}

/// <summary>Settings for commands that read a registry: adds <c>--registry</c>.</summary>
public class RegistrySettings : GlobalSettings
{
    /// <inheritdoc />
    [CommandOption("--registry <url>")]
    [Description("Registry base URL or local path (overrides blaizio.json)")]
    public override string? Registry { get; init; }
}

/// <summary>Settings for commands that both read a registry and confirm.</summary>
public class ConfirmRegistrySettings : RegistrySettings
{
    /// <inheritdoc />
    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt (default: false)")]
    public override bool Yes { get; init; }
}
