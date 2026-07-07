using System.ComponentModel;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Infrastructure;

/// <summary>Options every command shares: working directory, prompt/output behavior.</summary>
public class GlobalSettings : CommandSettings
{
    /// <summary>Working directory the command operates on. Defaults to the current directory.</summary>
    [CommandOption("-c|--cwd <DIR>")]
    [Description("Working directory (defaults to the current directory).")]
    public string? Cwd { get; init; }

    /// <summary>Skip interactive prompts and take defaults (CI / scripting).</summary>
    [CommandOption("-y|--yes")]
    [Description("Skip prompts and accept defaults.")]
    public bool Yes { get; init; }

    /// <summary>Suppress all non-essential output.</summary>
    [CommandOption("-s|--silent")]
    [Description("Mute output.")]
    public bool Silent { get; init; }

    /// <summary>Emit machine-readable JSON instead of formatted console output.</summary>
    [CommandOption("--json")]
    [Description("Emit JSON (for scripts, IDE plugins and MCP).")]
    public bool Json { get; init; }

    /// <summary>Registry base URL or local path, overriding blaizio.json.</summary>
    [CommandOption("--registry <URL>")]
    [Description("Registry base URL or local path (overrides blaizio.json).")]
    public string? Registry { get; init; }

    /// <summary>Absolute working directory, resolving <see cref="Cwd"/> against the process directory.</summary>
    public string ResolvedCwd => Path.GetFullPath(Cwd ?? Directory.GetCurrentDirectory());

    /// <summary>True when the command should run without any interactive prompt.</summary>
    public bool NonInteractive => Yes || Json || Silent;
}
