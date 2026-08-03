using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Writing;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The guard in front of an overwrite: a file the user changed since install is never replaced
/// without a decision, while an untouched one updates silently. The baseline recorded at install
/// time (<c>installed[].hashes</c>) is what separates the two.
/// </summary>
public class LocalEditsTests
{
    private const string Destination = "Components/Ui/Button/Button.razor";
    private const string Recorded = "Button/Button.razor";

    private static FakeRegistryClient Registry(string content) => new FakeRegistryClient()
        .Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = content }],
        });

    private static (AddService Service, BlaizioConfig Config) Build(TempDir dir, FakeRegistryClient client)
    {
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var config = new BlaizioConfig { Namespace = "Acme.Ui", Output = "Components/Ui" };
        return (new AddService(client, ProjectContext.Discover(dir.Path), config, new DotnetCli(dir.Path)), config);
    }

    private static WriteAction ActionOf(AddResult result) =>
        result.Files.Single(f => f.Path == Recorded).Action;

    [Fact]
    public async Task Add_records_a_baseline_hash_for_every_file_it_writes()
    {
        using var dir = new TempDir();
        var (service, config) = Build(dir, Registry("<div>v1</div>"));

        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });

        var file = Assert.Single(config.Installed["button"].Files);
        Assert.Equal(Recorded, file.Path);
        Assert.Equal(ContentHash.Of("<div>v1</div>"), file.Hash);
    }

    [Fact]
    public async Task An_untouched_file_is_replaced_without_asking()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, config) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        var asked = false;
        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"],
            NoNuget = true,
            Overwrite = true,
            ResolveConflicts = (_, _) =>
            {
                asked = true;
                return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
            },
        });

        Assert.False(asked);
        Assert.Empty(result.Edited);
        Assert.Equal(WriteAction.Overwritten, ActionOf(result));
        Assert.Equal("<div>v2</div>", dir.Read(Destination));
        Assert.Equal(ContentHash.Of("<div>v2</div>"), config.Installed["button"].HashFor(Recorded));
    }

    [Fact]
    public async Task A_file_already_matching_upstream_is_left_alone()
    {
        using var dir = new TempDir();
        var (service, _) = Build(dir, Registry("<div>v1</div>"));
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        var written = File.GetLastWriteTimeUtc(dir.Combine(Destination));

        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"], NoNuget = true, Overwrite = true,
        });

        Assert.Equal(WriteAction.Unchanged, ActionOf(result));
        Assert.Equal(written, File.GetLastWriteTimeUtc(dir.Combine(Destination)));
    }

    [Fact]
    public async Task An_edited_file_survives_an_overwrite_when_no_one_can_be_asked()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, _) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>mine</div>");

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        // No resolver: the unattended path (-y, --json, a script). Local edits win.
        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"], NoNuget = true, Overwrite = true,
        });

        Assert.Equal("<div>mine</div>", dir.Read(Destination));
        Assert.Equal(WriteAction.Skipped, ActionOf(result));
        Assert.Equal(["button"], result.KeptLocal);
        var edit = Assert.Single(Assert.Single(result.Edited).Files);
        Assert.Equal(Recorded, edit.Path);
        Assert.Equal(LocalEditKind.Edited, edit.Kind);
    }

    [Fact]
    public async Task A_kept_file_keeps_its_old_baseline_so_it_stays_flagged()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, config) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>mine</div>");

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true, Overwrite = true });

        Assert.Equal(ContentHash.Of("<div>v1</div>"), config.Installed["button"].HashFor(Recorded));
        var again = await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true, Overwrite = true });
        Assert.Equal(["button"], again.KeptLocal);
    }

    [Fact]
    public async Task Force_replaces_an_edited_file()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, config) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>mine</div>");

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"], NoNuget = true, Overwrite = true, Force = true,
        });

        Assert.Equal("<div>v2</div>", dir.Read(Destination));
        Assert.Equal(WriteAction.Overwritten, ActionOf(result));
        Assert.Empty(result.KeptLocal);
        Assert.Equal(ContentHash.Of("<div>v2</div>"), config.Installed["button"].HashFor(Recorded));
    }

    [Fact]
    public async Task The_resolver_decides_which_items_are_replaced()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, _) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>mine</div>");

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        var offered = Array.Empty<string>();
        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"],
            NoNuget = true,
            Overwrite = true,
            ResolveConflicts = (items, _) =>
            {
                offered = [.. items.Select(i => i.Name)];
                return Task.FromResult<IReadOnlySet<string>>(offered.ToHashSet(StringComparer.OrdinalIgnoreCase));
            },
        });

        Assert.Equal(["button"], offered);
        Assert.Equal("<div>v2</div>", dir.Read(Destination));
        Assert.Empty(result.KeptLocal);
    }

    [Fact]
    public async Task A_file_with_no_recorded_baseline_counts_as_edited()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, config) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>mine</div>");
        // A record written before the hash ledger existed: bare paths, no baselines.
        config.Installed["button"].Files =
            [.. config.Installed["button"].Files.Select(f => new InstalledFile(f.Path))];

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"], NoNuget = true, Overwrite = true,
        });

        Assert.Equal("<div>mine</div>", dir.Read(Destination));
        Assert.Equal(LocalEditKind.Unknown, Assert.Single(Assert.Single(result.Edited).Files).Kind);
    }

    [Fact]
    public async Task An_edit_that_already_matches_upstream_is_not_a_conflict()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, _) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>v2</div>");

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"], NoNuget = true, Overwrite = true,
        });

        Assert.Empty(result.Edited);
        Assert.Equal(WriteAction.Unchanged, ActionOf(result));
    }

    [Fact]
    public async Task A_line_ending_difference_is_not_an_edit()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>\nv1\n</div>");
        var (service, _) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>\r\nv1\r\n</div>");

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>\nv2\n</div>" }],
        });
        var result = await service.RunAsync(new AddRequest
        {
            Components = ["button"], NoNuget = true, Overwrite = true,
        });

        Assert.Empty(result.Edited);
        Assert.Equal(WriteAction.Overwritten, ActionOf(result));
    }

    [Fact]
    public async Task A_config_written_before_the_ledger_loads_as_bare_paths()
    {
        using var dir = new TempDir();
        dir.Write(BlaizioConfig.FileName, """
            {
              "namespace": "Acme.Ui",
              "installed": { "button": { "files": ["Button/BzButton.razor"] } }
            }
            """);

        var config = await ConfigStore.RequireAsync(dir.Path);

        var file = Assert.Single(config.Installed["button"].Files);
        Assert.Equal("Button/BzButton.razor", file.Path);
        Assert.Null(file.Hash); // no baseline: unknown, so an overwrite asks

        // Saving migrates the entry to the object form, hash or not.
        await ConfigStore.SaveAsync(dir.Path, config);
        Assert.Contains("\"path\": \"Button/BzButton.razor\"", dir.Read(BlaizioConfig.FileName));
    }

    [Fact]
    public async Task A_plain_add_reports_the_edit_but_claims_no_decision()
    {
        using var dir = new TempDir();
        var registry = Registry("<div>v1</div>");
        var (service, _) = Build(dir, registry);
        await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });
        dir.Write(Destination, "<div>mine</div>");

        registry.Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "<div>v2</div>" }],
        });
        var result = await service.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });

        Assert.Equal(WriteAction.Skipped, ActionOf(result));
        Assert.Single(result.Edited);
        Assert.Empty(result.KeptLocal);
    }
}
