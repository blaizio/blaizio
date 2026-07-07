using System.ComponentModel;
using Blaizio.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>search</c>: a required positional query, reusing list paging.</summary>
public sealed class SearchSettings : ListSettings
{
    /// <summary>The search term.</summary>
    [CommandArgument(0, "<QUERY>")]
    [Description("Term to search for.")]
    public string Term { get; init; } = string.Empty;
}

/// <summary>Searches registry items by term. A thin positional-argument front for <see cref="ListCommand"/>.</summary>
public sealed class SearchCommand : AsyncCommand<SearchSettings>
{
    /// <inheritdoc />
    public override Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        var list = new ListSettings
        {
            Cwd = settings.Cwd,
            Yes = settings.Yes,
            Silent = settings.Silent,
            Json = settings.Json,
            Registry = settings.Registry,
            Query = settings.Term,
            Limit = settings.Limit,
            Offset = settings.Offset,
        };
        return new ListCommand().ExecuteAsync(context, list);
    }
}
