using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;
using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// Renders help in the compact <c>Usage:/Options:/Commands:</c> layout (lowercase placeholders,
/// defaults inline in the descriptions, <c>-h</c> always last). Also renders the documented
/// <c>-ns</c> alias next to <c>--namespace</c> — the alias itself is rewritten in <c>Program</c>
/// because Spectre reserves two-character shorts.
/// </summary>
internal sealed class BlaizioHelpProvider : IHelpProvider
{
    private const string RootDescription = "Build your component library";

    /// <inheritdoc />
    public IEnumerable<IRenderable> Write(ICommandModel model, ICommandInfo? command)
    {
        var lines = new List<IRenderable>
        {
            new Text($"Usage: {Usage(model, command)}"),
            Text.NewLine,
        };

        var description = command?.Description ?? RootDescription;
        if (!string.IsNullOrWhiteSpace(description))
        {
            lines.Add(Text.NewLine);
            lines.Add(new Text(description));
            lines.Add(Text.NewLine);
        }

        var arguments = Arguments(command);
        if (arguments.Count > 0)
        {
            lines.Add(Text.NewLine);
            lines.Add(new Text("Arguments:"));
            lines.Add(Text.NewLine);
            lines.Add(Columns(arguments));
        }

        lines.Add(Text.NewLine);
        lines.Add(new Text("Options:"));
        lines.Add(Text.NewLine);
        lines.Add(Columns(Options(command)));

        var commands = Commands(command is null ? model.Commands : command.Commands, includeHelpRow: command is null);
        if (commands.Count > 0)
        {
            lines.Add(Text.NewLine);
            lines.Add(new Text("Commands:"));
            lines.Add(Text.NewLine);
            lines.Add(Columns(commands));
        }

        return lines;
    }

    /// <summary>The one-line usage: <c>blaizio add [options] [components...]</c>.</summary>
    private static string Usage(ICommandModel model, ICommandInfo? command)
    {
        var path = new List<string> { model.ApplicationName };
        for (var current = command; current is not null; current = current.Parent)
            path.Insert(1, current.Name);

        var usage = string.Join(' ', path) + " [options]";
        if (command is null || command.IsBranch)
            return usage + " [command]";

        foreach (var argument in command.Parameters.OfType<ICommandArgument>()
                     .Where(a => !a.IsHidden).OrderBy(a => a.Position))
            usage += " " + (argument.IsRequired ? $"<{ArgumentName(argument)}>" : $"[{ArgumentName(argument)}]");
        return usage;
    }

    /// <summary>Lowercased argument display name; repeatable args carry their <c>...</c> in the template.</summary>
    private static string ArgumentName(ICommandArgument argument) => argument.Value.ToLowerInvariant();

    private static List<(string Left, string Right)> Arguments(ICommandInfo? command)
    {
        var rows = new List<(string, string)>();
        if (command is null || command.IsBranch)
            return rows;

        foreach (var argument in command.Parameters.OfType<ICommandArgument>()
                     .Where(a => !a.IsHidden).OrderBy(a => a.Position))
            rows.Add((argument.Value.ToLowerInvariant().TrimEnd('.'), argument.Description ?? string.Empty));
        return rows;
    }

    private static List<(string Left, string Right)> Options(ICommandInfo? command)
    {
        var rows = new List<(string, string)>();

        if (command is null)
            rows.Add(("-v, --version", "Display the version number"));

        if (command is { IsBranch: false })
        {
            foreach (var option in command.Parameters.OfType<ICommandOption>().Where(o => !o.IsHidden))
                rows.Add((OptionColumn(option), OptionDescription(option)));
        }

        rows.Add(("-h, --help", "Display help for command"));
        return rows;
    }

    /// <summary>The invocation column: <c>-t, --template &lt;template&gt;</c> (value brackets follow optionality).</summary>
    private static string OptionColumn(ICommandOption option)
    {
        var longName = option.LongNames.FirstOrDefault();
        var names = new List<string>();
        names.AddRange(option.ShortNames.Select(s => $"-{s}"));
        // The -ns alias is parser-rewritten in Program (Spectre reserves 2-char shorts); document it here.
        if (longName == "namespace")
            names.Add("-ns");
        names.AddRange(option.LongNames.Select(l => $"--{l}"));

        var column = string.Join(", ", names);
        if (option.ValueName is { } value)
            column += option.ValueIsOptional ? $" [{value.ToLowerInvariant()}]" : $" <{value.ToLowerInvariant()}>";
        return column;
    }

    /// <summary>Descriptions carry their own "(default: …)" text; cwd's default is the live directory name.</summary>
    private static string OptionDescription(ICommandOption option)
    {
        var description = option.Description ?? string.Empty;
        if (option.LongNames.FirstOrDefault() == "cwd")
            description += $" (default: \"{Path.GetFileName(Directory.GetCurrentDirectory())}\")";
        return description;
    }

    /// <summary>Command aliases the help should advertise (registered in <c>CliApp</c>).</summary>
    private static readonly Dictionary<string, string> AdvertisedAliases = new()
    {
        ["new"] = "create",
        ["remove"] = "rm",
        ["uninstall"] = "un",
    };

    private static List<(string Left, string Right)> Commands(
        IReadOnlyList<ICommandInfo> commands, bool includeHelpRow)
    {
        var rows = new List<(string, string)>();
        foreach (var child in commands.Where(c => !c.IsHidden))
        {
            // Render advertised aliases next to the name ("uninstall, un"), matching the option
            // column's "-ns, --namespace" style. ICommandInfo exposes no alias metadata, so they
            // are listed here (like the -ns special case above).
            var left = AdvertisedAliases.TryGetValue(child.Name, out var alias)
                ? $"{child.Name}, {alias}"
                : child.Name;
            if (child.IsBranch)
            {
                left += " [command]";
            }
            else
            {
                left += " [options]";
                foreach (var argument in child.Parameters.OfType<ICommandArgument>()
                             .Where(a => !a.IsHidden).OrderBy(a => a.Position))
                    left += " " + (argument.IsRequired ? $"<{ArgumentName(argument)}>" : $"[{ArgumentName(argument)}]");
            }

            // `help [command]` is registered as a real command; strip its generated "[options]".
            if (child.Name == "help")
                left = "help [command]";

            rows.Add((left, child.Description ?? string.Empty));
        }

        return rows;
    }

    /// <summary>Two aligned columns with a two-space indent, plain text (descriptions may contain markup-y chars).</summary>
    private static Grid Columns(List<(string Left, string Right)> rows)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().Padding(2, 0, 2, 0));
        grid.AddColumn(new GridColumn().Padding(0, 0, 0, 0));
        foreach (var (left, right) in rows)
            grid.AddRow(new Text(left), new Text(right));
        return grid;
    }
}
