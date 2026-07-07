using System.ComponentModel;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>diff</c>.</summary>
public sealed class DiffSettings : GlobalSettings
{
    /// <summary>Component to diff; empty diffs all installed components.</summary>
    [CommandArgument(0, "[COMPONENT]")]
    [Description("Component to diff against the registry (default: all).")]
    public string? Component { get; init; }
}

/// <summary>Shows local vs upstream differences. Not implemented yet.</summary>
public sealed class DiffCommand : AsyncCommand<DiffSettings>
{
    /// <inheritdoc />
    public override Task<int> ExecuteAsync(CommandContext context, DiffSettings settings)
    {
        AnsiConsole.MarkupLine("[yellow]'diff' is not implemented yet.[/]");
        return Task.FromResult(0);
    }
}

/// <summary>Settings for <c>migrate</c>.</summary>
public sealed class MigrateSettings : GlobalSettings
{
    /// <summary>Migration name (e.g. rtl, icons).</summary>
    [CommandArgument(0, "<MIGRATION>")]
    [Description("Migration to run (rtl, icons).")]
    public string Migration { get; init; } = string.Empty;

    /// <summary>Optional path or glob to scope the migration.</summary>
    [CommandArgument(1, "[PATH]")]
    [Description("Optional path or glob to scope the migration.")]
    public string? Path { get; init; }
}

/// <summary>Runs a codemod migration. Not implemented yet.</summary>
public sealed class MigrateCommand : AsyncCommand<MigrateSettings>
{
    /// <inheritdoc />
    public override Task<int> ExecuteAsync(CommandContext context, MigrateSettings settings)
    {
        AnsiConsole.MarkupLine($"[yellow]'migrate {Markup.Escape(settings.Migration)}' is not implemented yet.[/]");
        return Task.FromResult(0);
    }
}
