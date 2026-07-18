using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Shared pieces of the <c>preset</c> subcommands.</summary>
internal static class PresetSupport
{
    /// <summary>The docs /create page a code round-trips through.</summary>
    public static string CreateUrl(string code) => $"https://blaiz.io/create?preset={code.Trim()}";

    /// <summary>Decode or die with a friendly error (exit 1).</summary>
    public static bool TryDecode(string code, out PresetSelection selection)
    {
        if (PresetCode.TryDecode(code, out selection))
            return true;
        CliOutput.Error.MarkupLine(
            $"[red]Error:[/] '{Markup.Escape(code)}' is not a valid preset code. Codes come from the Create page at blaiz.io/create (e.g. [white]32r[/]).");
        return false;
    }

    /// <summary>The decoded selection as one JSON object (shared by decode and resolve).</summary>
    public static JsonObject ToJson(string code, PresetSelection s) => new()
    {
        ["code"] = code.Trim().ToLowerInvariant(),
        ["style"] = s.Style,
        ["preset"] = s.Preset,
        ["rtl"] = s.Rtl,
        ["chart"] = s.Chart,
        ["heading"] = s.Heading,
        ["font"] = s.Font,
        ["radius"] = s.Radius,
        ["url"] = CreateUrl(code),
    };

    /// <summary>Print the selection as aligned key/value lines.</summary>
    public static void Render(string code, PresetSelection s)
    {
        AnsiConsole.MarkupLine($"[grey]code[/]     [cyan]{Markup.Escape(code.Trim().ToLowerInvariant())}[/]");
        AnsiConsole.MarkupLine($"[grey]style[/]    {Markup.Escape(s.Style)}");
        AnsiConsole.MarkupLine($"[grey]preset[/]   {Markup.Escape(s.Preset)}");
        AnsiConsole.MarkupLine($"[grey]rtl[/]      {(s.Rtl ? "yes" : "no")}");
        AnsiConsole.MarkupLine($"[grey]chart[/]    {Markup.Escape(s.Chart)}");
        AnsiConsole.MarkupLine($"[grey]heading[/]  {Markup.Escape(s.Heading)}");
        AnsiConsole.MarkupLine($"[grey]font[/]     {Markup.Escape(s.Font)}");
        AnsiConsole.MarkupLine($"[grey]radius[/]   {Markup.Escape(s.Radius)}");
        AnsiConsole.MarkupLine($"[grey]url[/]      {Markup.Escape(CreateUrl(code))}");
    }
}

/// <summary>Settings for <c>preset decode</c>.</summary>
public sealed class PresetDecodeSettings : CommandSettings
{
    /// <summary>The compact code to expand.</summary>
    [CommandArgument(0, "<code>")]
    [Description("The preset code to decode")]
    public string Code { get; init; } = string.Empty;

    /// <summary>Emit machine-readable JSON.</summary>
    [CommandOption("--json")]
    [Description("Output as JSON, for scripts, IDE plugins, and MCP (default: false)")]
    public bool Json { get; init; }
}

/// <summary>Expands a /create preset code into its style/preset/overlay parts.</summary>
public sealed class PresetDecodeCommand : Command<PresetDecodeSettings>
{
    /// <inheritdoc />
    public override int Execute(CommandContext context, PresetDecodeSettings settings)
    {
        if (!PresetSupport.TryDecode(settings.Code, out var selection))
            return 1;

        if (settings.Json)
            Console.Out.WriteLine(PresetSupport.ToJson(settings.Code, selection).ToJsonString());
        else
            PresetSupport.Render(settings.Code, selection);
        return 0;
    }
}

/// <summary>Settings for <c>preset resolve</c>.</summary>
public sealed class PresetResolveSettings : CommandSettings
{
    /// <summary>Working directory holding the blaizio.json to resolve from.</summary>
    [CommandOption("-c|--cwd <cwd>")]
    [Description("The working directory. Defaults to the current directory")]
    public string? Cwd { get; init; }

    /// <summary>Emit machine-readable JSON.</summary>
    [CommandOption("--json")]
    [Description("Output as JSON, for scripts, IDE plugins, and MCP (default: false)")]
    public bool Json { get; init; }
}

/// <summary>
/// Turns the project's recorded styling (blaizio.json: theme, preset, rtl, plus the recorded
/// chart/fonts/radius overlays) back into a shareable /create code.
/// </summary>
public sealed class PresetResolveCommand : AsyncCommand<PresetResolveSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, PresetResolveSettings settings)
    {
        var cwd = Path.GetFullPath(settings.Cwd ?? Directory.GetCurrentDirectory());
        var config = await ConfigStore.LoadAsync(cwd, CliCancellation.Token);
        if (config is null)
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] No {BlaizioConfig.FileName} found in {Markup.Escape(cwd)}. Run [white]blaizio add[/] first.");
            return 1;
        }

        var selection = new PresetSelection(config.Theme, config.Preset, config.Rtl,
            Chart: config.Chart ?? "default",
            Heading: config.Heading ?? "default",
            Font: config.Font ?? "default",
            Radius: config.Radius ?? "default");
        var code = PresetCode.Encode(selection);

        if (settings.Json)
            Console.Out.WriteLine(PresetSupport.ToJson(code, selection).ToJsonString());
        else
            PresetSupport.Render(code, selection);
        return 0;
    }
}

/// <summary>Settings for <c>preset url</c> / <c>preset open</c>.</summary>
public sealed class PresetCodeSettings : CommandSettings
{
    /// <summary>The compact preset code.</summary>
    [CommandArgument(0, "<code>")]
    [Description("The preset code")]
    public string Code { get; init; } = string.Empty;
}

/// <summary>Prints the docs /create URL that carries a preset code.</summary>
public sealed class PresetUrlCommand : Command<PresetCodeSettings>
{
    /// <inheritdoc />
    public override int Execute(CommandContext context, PresetCodeSettings settings)
    {
        if (!PresetSupport.TryDecode(settings.Code, out _))
            return 1;
        Console.Out.WriteLine(PresetSupport.CreateUrl(settings.Code));
        return 0;
    }
}

/// <summary>Opens the docs /create page for a preset code in the default browser.</summary>
public sealed class PresetOpenCommand : Command<PresetCodeSettings>
{
    /// <inheritdoc />
    public override int Execute(CommandContext context, PresetCodeSettings settings)
    {
        if (!PresetSupport.TryDecode(settings.Code, out _))
            return 1;

        var url = PresetSupport.CreateUrl(settings.Code);
        try
        {
            // UseShellExecute routes through the OS default-browser association on Windows;
            // macOS/Linux need their opener commands instead.
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch (Exception ex)
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] Could not open the browser ({Markup.Escape(ex.Message)}). URL: {Markup.Escape(url)}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Opened[/] {Markup.Escape(url)}");
        return 0;
    }
}
