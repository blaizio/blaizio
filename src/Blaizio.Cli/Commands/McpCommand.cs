using System.ComponentModel;
using Blaizio.Cli.Infrastructure;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>
/// Settings for <c>mcp</c>. Deliberately NOT <see cref="GlobalSettings"/>: <c>--json</c> and
/// <c>--silent</c> shape console output, and a stdio server has none - an accepted-and-ignored
/// option is a lie in the help screen.
/// </summary>
public sealed class McpSettings : CommandSettings
{
    /// <summary>Project directory the tools operate on. Defaults to the current directory.</summary>
    [CommandOption("-c|--cwd <cwd>")]
    [Description("The working directory. Defaults to the current directory")]
    public string? Cwd { get; init; }

    /// <summary>Registry base URL or local path, overriding blaizio.json for every tool call.</summary>
    [CommandOption("--registry <url>")]
    [Description("Registry base URL or local path (overrides blaizio.json)")]
    public string? Registry { get; init; }

    /// <summary>Absolute working directory.</summary>
    public string ResolvedCwd => Path.GetFullPath(Cwd ?? Directory.GetCurrentDirectory());
}

/// <summary>
/// Runs a Model Context Protocol server over stdio, exposing the registry to AI agents: search,
/// item sources, the docs/API payload, installs and project info - the same operations the CLI's
/// <c>--json</c> mode serves, spoken as MCP tools. Runs until the client disconnects.
/// </summary>
public sealed class McpCommand : AsyncCommand<McpSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, McpSettings settings)
    {
        // stdout is the protocol channel from here on: nothing else in this process may write to
        // it. Tool implementations (McpToolset) uphold that by never touching the console.
        await using var transport = new StdioServerTransport("blaizio");
        await using var server = McpServer.Create(transport, BuildOptions(settings.ResolvedCwd, settings.Registry));
        await server.RunAsync(CliCancellation.Token);
        return 0;
    }

    /// <summary>Server identity + the tool collection. Internal so tests can host the exact same
    /// server over stream transports instead of stdio.</summary>
    internal static McpServerOptions BuildOptions(string cwd, string? registryOverride) => new()
    {
        ServerInfo = new Implementation { Name = "blaizio", Title = "Blaizio", Version = ToolInfo.Version },
        ToolCollection = McpToolset.Build(cwd, registryOverride),
        ServerInstructions = McpToolset.ServerInstructions,
    };
}
