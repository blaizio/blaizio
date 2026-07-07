using Blaizio.Cli.Core.Registry;
using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>The one interactive component picker shared by <c>init</c> and <c>add</c>.</summary>
internal static class ComponentPrompts
{
    /// <summary>Checkbox picker over the registry catalogue (space to toggle, enter to confirm).</summary>
    public static async Task<string[]> PickAsync(IRegistryClient registry, string title, CancellationToken ct = default)
    {
        var index = await registry.GetIndexAsync(ct);
        if (index.Items.Count == 0)
            return [];

        var prompt = new MultiSelectionPrompt<string>()
            .Title(title)
            .NotRequired()
            .PageSize(15)
            .MoreChoicesText("[grey](move up/down to reveal more)[/]")
            .InstructionsText("[grey](space to toggle, enter to confirm)[/]")
            .AddChoices(index.Items.Select(i => i.Name));

        return [.. AnsiConsole.Prompt(prompt)];
    }
}
