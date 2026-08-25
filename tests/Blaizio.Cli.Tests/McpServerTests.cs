using System.IO.Pipelines;
using System.Text.Json;
using Blaizio.Cli.Commands;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// Hosts the exact server <c>blaizio mcp</c> runs (same options, same tool collection) over
/// in-process stream transports, and talks to it with the real MCP client - the full protocol
/// round-trip minus the stdio plumbing.
/// </summary>
[Collection("console")]
public class McpServerTests
{
    /// <summary>A connected client/server pair over pipes; disposing tears both down.</summary>
    private sealed class Session : IAsyncDisposable
    {
        public required McpClient Client { get; init; }
        public required McpServer Server { get; init; }
        public required Task Run { get; init; }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Server.DisposeAsync();
            await Run; // surface any server-side crash
        }
    }

    private static async Task<Session> ConnectAsync(string cwd, string? registry)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var server = McpServer.Create(
            new StreamServerTransport(
                clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream(), "blaizio"),
            McpCommand.BuildOptions(cwd, registry));
        var run = server.RunAsync(CancellationToken.None);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                serverInput: clientToServer.Writer.AsStream(),
                serverOutput: serverToClient.Reader.AsStream()),
            cancellationToken: CancellationToken.None);
        return new Session { Client = client, Server = server, Run = run };
    }

    /// <summary>Call a tool and parse the single text block it returns as JSON.</summary>
    private static async Task<JsonDocument> CallAsync(
        McpClient client, string tool, IReadOnlyDictionary<string, object?>? args = null)
    {
        var result = await client.CallToolAsync(tool, args,
            cancellationToken: CancellationToken.None);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        return JsonDocument.Parse(text);
    }

    [Fact]
    public async Task Server_lists_the_five_tools()
    {
        using var dir = new TempDir();
        await using var session = await ConnectAsync(dir.Path, LocalRegistry.Create(dir));

        var tools = await session.Client.ListToolsAsync(
            cancellationToken: CancellationToken.None);

        var names = tools.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["add_items", "get_docs", "get_item", "project_info", "search_items"], names);
    }

    [Fact]
    public async Task Search_filters_and_pages_a_static_registry()
    {
        using var dir = new TempDir();
        await using var session = await ConnectAsync(dir.Path, LocalRegistry.Create(dir));

        using var doc = await CallAsync(session.Client, "search_items",
            new Dictionary<string, object?> { ["query"] = "button" });

        var items = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToArray();
        Assert.Equal(["button"], items);
        Assert.Equal(1, doc.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Get_item_returns_full_file_contents()
    {
        using var dir = new TempDir();
        await using var session = await ConnectAsync(dir.Path, LocalRegistry.Create(dir));

        using var doc = await CallAsync(session.Client, "get_item",
            new Dictionary<string, object?> { ["name"] = "card" });

        Assert.Equal("card", doc.RootElement.GetProperty("name").GetString());
        var file = doc.RootElement.GetProperty("files").EnumerateArray().Single();
        Assert.Contains("card", file.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Get_docs_serves_the_docs_payload()
    {
        using var dir = new TempDir();
        await using var session = await ConnectAsync(dir.Path, LocalRegistry.Create(dir));

        using var doc = await CallAsync(session.Client, "get_docs",
            new Dictionary<string, object?> { ["name"] = "button" });

        Assert.Equal("button", doc.RootElement.GetProperty("name").GetString());
        Assert.Contains("/docs/components/button", doc.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Add_on_an_uninitialized_project_refuses_with_the_wiring_hint()
    {
        using var dir = new TempDir();
        await using var session = await ConnectAsync(dir.Path, LocalRegistry.Create(dir));

        using var doc = await CallAsync(session.Client, "add_items",
            new Dictionary<string, object?> { ["items"] = new[] { "button" } });

        Assert.Contains("blaizio add", doc.RootElement.GetProperty("error").GetString());
        Assert.False(File.Exists(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
    }

    [Fact]
    public async Task Add_installs_into_an_initialized_project()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        // Wire the project the supported way first (no csproj: package installs are skipped).
        var tester = new Spectre.Console.Cli.Testing.CommandAppTester();
        tester.Configure(CliApp.Configure);
        using (new StdoutCapture())
            await tester.RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        await using var session = await ConnectAsync(dir.Path, registry);
        using var doc = await CallAsync(session.Client, "add_items",
            new Dictionary<string, object?> { ["items"] = new[] { "card" } });

        var items = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetString()).ToArray();
        Assert.Contains("card", items);
        Assert.Contains("button", items); // transitive
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Card", "BzCard.razor")));
    }

    [Fact]
    public async Task Project_info_reports_initialization_state()
    {
        using var dir = new TempDir();
        await using var session = await ConnectAsync(dir.Path, LocalRegistry.Create(dir));

        using var doc = await CallAsync(session.Client, "project_info");

        Assert.False(doc.RootElement.GetProperty("initialized").GetBoolean());
    }

    [Fact]
    public async Task Registry_errors_come_back_as_error_documents_not_crashes()
    {
        using var dir = new TempDir();
        await using var session = await ConnectAsync(dir.Path, dir.Combine("missing-registry"));

        using var doc = await CallAsync(session.Client, "search_items");

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
