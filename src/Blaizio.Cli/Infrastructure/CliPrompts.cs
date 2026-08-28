using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The interactive prompt front door. Spectre's synchronous prompts own the keyboard and never
/// look at a token, so the first Ctrl+C during a question was swallowed by their key loop and only
/// the second (hard-exit) press got out. Every prompt here observes
/// <see cref="CliCancellation.Token"/> and treats Escape as the same answer - stop the command -
/// surfacing both as <see cref="OperationCanceledException"/>, which the app's exception handler
/// renders as the quiet "Cancelled." exit (130).
/// </summary>
internal static class CliPrompts
{
    /// <summary>A single-key yes/no: <c>y</c>/<c>n</c> decide, Enter takes the default, Escape
    /// cancels the command. Input running dry (a piped stdin) cancels too - a confirm is standing
    /// in front of something destructive, and silence must not mean yes.</summary>
    public static bool Confirm(string prompt, bool defaultValue = true)
    {
        var console = AnsiConsole.Console;
        console.Markup($"{prompt} [blue][[y/n]][/] [green]({(defaultValue ? "y" : "n")})[/]: ");
        while (true)
        {
            switch (ReadKey(console))
            {
                case { KeyChar: 'y' or 'Y' }:
                    console.MarkupLine("[green]y[/]");
                    return true;
                case { KeyChar: 'n' or 'N' }:
                    console.MarkupLine("[green]n[/]");
                    return false;
                case { Key: ConsoleKey.Enter }:
                    console.MarkupLine($"[green]{(defaultValue ? "y" : "n")}[/]");
                    return defaultValue;
                case { Key: ConsoleKey.Escape } or null:
                    console.WriteLine();
                    throw new OperationCanceledException();
            }
        }
    }

    /// <summary>A line of text with a default: Enter on an empty line takes the default, Backspace
    /// edits, Escape cancels the command. Input running dry takes the default - unlike a confirm,
    /// there is nothing destructive behind a name to type.</summary>
    public static string Text(string prompt, string defaultValue)
    {
        var console = AnsiConsole.Console;
        console.Markup($"{prompt} [green]({Markup.Escape(defaultValue)})[/]: ");
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            switch (ReadKey(console))
            {
                case null or { Key: ConsoleKey.Enter }:
                    console.WriteLine();
                    return buffer.Length == 0 ? defaultValue : buffer.ToString();
                case { Key: ConsoleKey.Escape }:
                    console.WriteLine();
                    throw new OperationCanceledException();
                case { Key: ConsoleKey.Backspace }:
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                        console.Write("\b \b");
                    }
                    break;
                case { KeyChar: var c } when !char.IsControl(c):
                    buffer.Append(c);
                    console.Write(c.ToString());
                    break;
            }
        }
    }

    /// <summary>A Spectre selection list with Escape wired to cancel (<c>CancelResult</c>) and the
    /// key loop observing the token (<c>ShowAsync</c>).</summary>
    public static T Selection<T>(SelectionPrompt<T> prompt) where T : notnull
    {
        prompt.CancelResult = () => throw new OperationCanceledException();
        return prompt.ShowAsync(AnsiConsole.Console, CliCancellation.Token).GetAwaiter().GetResult();
    }

    /// <summary>One key, token-observed. Ctrl+C lands here as the cancellation exception, with the
    /// prompt line closed off first so "Cancelled." starts on its own line.</summary>
    internal static ConsoleKeyInfo? ReadKey(IAnsiConsole console)
    {
        try
        {
            return console.Input.ReadKeyAsync(intercept: true, CliCancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            console.WriteLine();
            throw;
        }
    }
}
