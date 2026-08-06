using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;
using ModelContextProtocol.Server;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The MCP tools <c>blaizio mcp</c> serves: the same registry operations the CLI exposes, riding
/// the exact <c>--json</c> payloads (<see cref="CliJson"/>) so agents and scripts read one shape.
/// Every tool loads <see cref="CliServices"/> fresh per call - config edits between calls are
/// picked up, matching the CLI's process-per-run semantics. Nothing here may write to the console:
/// in stdio mode stdout belongs to the protocol.
/// </summary>
internal static class McpToolset
{
    /// <summary>Guidance the client shows its model alongside the tool list.</summary>
    public const string ServerInstructions =
        "Blaizio installs Blazor components as source into the current project. " +
        "Typical flow: search_items to find components, get_docs for the parameter API, " +
        "get_item to inspect full sources, then add_items to install (dependencies resolve " +
        "automatically). project_info reports whether the project is wired up yet.";

    /// <summary>
    /// Build the tool collection for a working directory. <paramref name="registryOverride"/> is
    /// the server-level <c>--registry</c>; individual calls may still name their own source.
    /// </summary>
    public static McpServerPrimitiveCollection<McpServerTool> Build(string cwd, string? registryOverride) => new()
    {
        McpServerTool.Create(
            async Task<string> (
                [Description("Terms matched against name, title and description. Comma-separated for alternatives")] string? query = null,
                [Description("Item types to keep: ui, lib, theme, font. Comma-separated for multiple")] string? type = null,
                [Description("Registry category tags to keep. Comma-separated for multiple")] string? category = null,
                [Description("Maximum items to return (default 100)")] int limit = 100,
                [Description("Matched items to skip, for paging (default 0)")] int offset = 0,
                [Description("Registry to search: an @namespace recorded in blaizio.json, a URL or a local path. Omit for the project's configured registry")] string? registry = null,
                CancellationToken ct = default) =>
                await GuardAsync(() => SearchAsync(cwd, registryOverride, registry, query, type, category, limit, offset, ct)),
            new McpServerToolCreateOptions
            {
                Name = "search_items",
                Title = "Search registry items",
                Description = "Search a Blaizio registry for installable items (components, themes, fonts). "
                    + "Returns the matching page plus pagination, as the registry index shape.",
                ReadOnly = true,
            }),

        McpServerTool.Create(
            async Task<string> (
                [Description("Item name, @namespace/item reference, URL or local path")] string name,
                CancellationToken ct = default) =>
                await GuardAsync(async () =>
                {
                    var services = await CliServices.LoadAsync(cwd, registryOverride, ct);
                    var item = await services.Registry.GetItemAsync(name, ct);
                    return JsonSerializer.Serialize(item, CliJson.Default.RegistryItem);
                }),
            new McpServerToolCreateOptions
            {
                Name = "get_item",
                Title = "Get item sources",
                Description = "Fetch one registry item with its full file contents, dependencies and metadata - "
                    + "the read-only way to inspect exactly what add_items would write.",
                ReadOnly = true,
            }),

        McpServerTool.Create(
            async Task<string> (
                [Description("Component name, @namespace/item reference, URL or local path")] string name,
                CancellationToken ct = default) =>
                await GuardAsync(async () =>
                {
                    var services = await CliServices.LoadAsync(cwd, registryOverride, ct);
                    var item = await services.Registry.GetItemAsync(name, ct);
                    return DocsCommand.ToJson(item).ToJsonString();
                }),
            new McpServerToolCreateOptions
            {
                Name = "get_docs",
                Title = "Get component docs",
                Description = "A component's docs page URL, dependencies and parameter API reference "
                    + "(every [Parameter], parsed from the registry sources).",
                ReadOnly = true,
            }),

        McpServerTool.Create(
            async Task<string> (
                [Description("Item names or @namespace/item references to install. name@version pins an item to a version the registry stamps")] string[] items,
                [Description("Replace files that already exist. Files edited since install are kept regardless (default false)")] bool overwrite = false,
                [Description("Resolve and report only; write nothing and install nothing (default false)")] bool dryRun = false,
                CancellationToken ct = default) =>
                await GuardAsync(() => AddAsync(cwd, registryOverride, items, overwrite, dryRun, ct)),
            new McpServerToolCreateOptions
            {
                Name = "add_items",
                Title = "Add components",
                Description = "Install registry items (and their dependencies) into the project as source, "
                    + "including NuGet packages. Requires an initialized project (see project_info); "
                    + "files the user edited since install are always kept.",
            }),

        McpServerTool.Create(
            async Task<string> (CancellationToken ct = default) =>
                await GuardAsync(async () =>
                {
                    var services = await CliServices.LoadAsync(cwd, ct: ct);
                    return InfoCommand.BuildPayload(services).ToJsonString();
                }),
            new McpServerToolCreateOptions
            {
                Name = "project_info",
                Title = "Project info",
                Description = "The discovered project, whether Blaizio is initialized, and the blaizio.json "
                    + "configuration (namespace, output directory, style, registry).",
                ReadOnly = true,
            }),
    };

    /// <summary>
    /// Run a tool body, turning any failure into an <c>{"error": ...}</c> document instead of an
    /// exception: the SDK reports unhandled tool exceptions generically, and the agent needs the
    /// actual message ("registry unreachable", "unknown item") to react.
    /// </summary>
    private static async Task<string> GuardAsync(Func<Task<string>> body)
    {
        try
        {
            return await body();
        }
        catch (OperationCanceledException)
        {
            throw; // cancellation is the host shutting down, not a result
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = ex.Message }.ToJsonString();
        }
    }

    /// <summary>
    /// The <c>search --json</c> behavior for a single source: delegate the page to the registry;
    /// a static registry (no <c>pagination</c> in the answer) gets filtered and paged locally.
    /// </summary>
    private static async Task<string> SearchAsync(
        string cwd, string? registryOverride, string? registry,
        string? query, string? type, string? category, int limit, int offset, CancellationToken ct)
    {
        limit = Math.Max(0, limit);
        offset = Math.Max(0, offset);
        var types = SearchCommand.NormalizeTypes(type);
        var categories = SearchCommand.SplitList(category);

        // A tool-level source outranks the server-level --registry, which outranks blaizio.json.
        // An @namespace routes through ITS OWN client (that's where private-registry credentials
        // live), exactly like `search @ns` does.
        var baseServices = await CliServices.LoadAsync(cwd, registryOverride, ct);
        IRegistryClient client;
        if (registry is ['@', ..])
        {
            client = baseServices.Registry is NamespacedRegistryClient namespaced
                ? namespaced.For(registry)
                : throw new RegistryException(
                    $"Unknown registry '{registry}'. Record it first: blaizio registry add {registry}=<url>");
        }
        else
        {
            client = registry is null || registry == registryOverride
                ? baseServices.Registry
                : (await CliServices.LoadAsync(cwd, registry, ct)).Registry;
        }

        var index = await client.SearchAsync(new RegistrySearch(query, types, limit, offset, categories), ct);

        RegistryItem[] page;
        RegistryPagination pagination;
        if (index.Pagination is { } serverPage)
        {
            // The registry filtered; its items are final (see SearchCommand).
            page = [.. index.Items];
            pagination = serverPage;
        }
        else
        {
            var matched = SearchCommand.FilterItems(index.Items, query, types, categories).ToArray();
            page = [.. matched.Skip(offset).Take(limit)];
            pagination = new RegistryPagination
            {
                Total = matched.Length,
                Offset = offset,
                Limit = limit,
                HasMore = matched.Length > offset + page.Length,
            };
        }

        var payload = new RegistryIndex { Name = index.Name, Items = page, Pagination = pagination };
        return JsonSerializer.Serialize(payload, CliJson.Default.RegistryIndex);
    }

    /// <summary>
    /// The unattended <c>add</c>: no prompts, so local edits are always kept and nothing can be
    /// forced. Refuses two things a terminal run would have asked about - an uninitialized project
    /// (the wiring decisions are the user's) and an unrecorded host (the trust gate).
    /// </summary>
    private static async Task<string> AddAsync(
        string cwd, string? registryOverride, string[] items, bool overwrite, bool dryRun, CancellationToken ct)
    {
        if (items.Length == 0)
            throw new InvalidOperationException("No items given. Pass the item names to install.");

        var services = await CliServices.LoadAsync(cwd, registryOverride, ct);
        if (services.Config is null)
            throw new InvalidOperationException(
                "This project has no blaizio.json. Run `blaizio add <components>` in a terminal once - "
                + "it wires the project up (packages, Tailwind, configuration) before installing.");
        var config = services.Config;

        // The CLI's trust gate confirms interactively, once per host; here nobody can be asked, so
        // an unrecorded host is a refusal with the way to record it - never a silent trust.
        var foreignHosts = TrustPolicy.ForeignHosts(items, config, registryOverride);
        if (foreignHosts.Count > 0 && !dryRun)
            throw new InvalidOperationException(
                $"Unrecorded host(s): {string.Join(", ", foreignHosts)}. Registry items are source code "
                + "that compiles into the app. Record the registry first (blaizio registry add @ns=<url>) "
                + "or install from a terminal (blaizio add <url>), where the trust prompt can be answered.");

        var service = new AddService(services.Registry, services.Project, config, services.Dotnet);
        var result = await service.RunAsync(new AddRequest
        {
            Components = items,
            Overwrite = overwrite,
            DryRun = dryRun,
        }, ct: ct);
        return JsonSerializer.Serialize(result, CliJson.Default.AddResult);
    }
}
