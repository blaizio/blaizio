using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// The prompt front door: every interactive question answers to single keys, and Escape cancels
/// the whole command (surfacing as <see cref="OperationCanceledException"/>, which the app's
/// exception handler renders as "Cancelled." / 130).
/// </summary>
[Collection("console")]
public class PromptTests : IDisposable
{
    private readonly IAnsiConsole _original = AnsiConsole.Console;
    private readonly TestConsole _console = new();

    public PromptTests()
    {
        _console.Interactive();
        AnsiConsole.Console = _console;
    }

    public void Dispose() => AnsiConsole.Console = _original;

    [Theory]
    [InlineData('y', true)]
    [InlineData('n', false)]
    public void Confirm_takes_a_single_key(char key, bool expected)
    {
        _console.Input.PushCharacter(key);
        Assert.Equal(expected, CliPrompts.Confirm("Continue?", defaultValue: !expected));
    }

    [Fact]
    public void Confirm_enter_takes_the_default()
    {
        _console.Input.PushKey(ConsoleKey.Enter);
        Assert.False(CliPrompts.Confirm("Continue?", defaultValue: false));
    }

    [Fact]
    public void Confirm_escape_cancels_the_command()
    {
        _console.Input.PushKey(ConsoleKey.Escape);
        Assert.ThrowsAny<OperationCanceledException>(() => CliPrompts.Confirm("Continue?"));
    }

    [Fact]
    public void Text_enter_on_an_empty_line_takes_the_default()
    {
        _console.Input.PushKey(ConsoleKey.Enter);
        Assert.Equal("App.Components.Ui", CliPrompts.Text("Root namespace?", "App.Components.Ui"));
    }

    [Fact]
    public void Text_typed_input_wins_and_backspace_edits()
    {
        _console.Input.PushText("Uix");
        _console.Input.PushKey(ConsoleKey.Backspace);
        _console.Input.PushKey(ConsoleKey.Enter);
        Assert.Equal("Ui", CliPrompts.Text("Output directory?", "Components/Ui"));
    }

    [Fact]
    public void Text_escape_cancels_the_command()
    {
        _console.Input.PushText("half-typed");
        _console.Input.PushKey(ConsoleKey.Escape);
        Assert.ThrowsAny<OperationCanceledException>(() => CliPrompts.Text("Root namespace?", "App"));
    }

    [Fact]
    public void Selection_escape_cancels_the_command()
    {
        _console.Input.PushKey(ConsoleKey.Escape);
        Assert.ThrowsAny<OperationCanceledException>(() => CliPrompts.Selection(
            new SelectionPrompt<string>().Title("Skin?").AddChoices("one", "two")));
    }

    [Fact]
    public void Selection_enter_picks_the_highlighted_choice()
    {
        _console.Input.PushKey(ConsoleKey.Enter);
        Assert.Equal("one", CliPrompts.Selection(
            new SelectionPrompt<string>().Title("Skin?").AddChoices("one", "two")));
    }

    [Fact]
    public void MultiSelect_escape_cancels_the_command()
    {
        _console.Input.PushKey(ConsoleKey.Escape);
        Assert.ThrowsAny<OperationCanceledException>(() =>
            ComponentPrompts.MultiSelect("Which ones?", ["a", "b"], preselectAll: true));
    }

    [Fact]
    public void MultiSelect_enter_returns_the_selection()
    {
        _console.Input.PushKey(ConsoleKey.Spacebar); // deselect the first
        _console.Input.PushKey(ConsoleKey.Enter);
        Assert.Equal(["b"], ComponentPrompts.MultiSelect("Which ones?", ["a", "b"], preselectAll: true));
    }
}
