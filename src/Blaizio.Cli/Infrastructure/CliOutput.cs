using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// Output policy for every command: human-facing text is muted by <c>--silent</c> and suppressed in
/// <c>--json</c> mode (stdout must stay pure JSON); warnings/diagnostics go to stderr so they never
/// pollute a piped stdout stream.
/// </summary>
internal static class CliOutput
{
    /// <summary>Console bound to stderr for warnings and diagnostics.</summary>
    public static readonly IAnsiConsole Error = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Error),
    });

    /// <summary>Write human-facing markup to stdout unless <c>--silent</c> or <c>--json</c>.</summary>
    public static void Line(this GlobalSettings settings, string markup)
    {
        if (!settings.Silent && !settings.Json)
            AnsiConsole.MarkupLine(markup);
    }

    /// <summary>Write a diagnostic to stderr (survives <c>--json</c>, muted by <c>--silent</c>).</summary>
    public static void Warn(this GlobalSettings settings, string markup)
    {
        if (!settings.Silent)
            Error.MarkupLine(markup);
    }
}
