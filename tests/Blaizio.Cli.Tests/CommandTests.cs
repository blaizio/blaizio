using Blaizio.Cli.Commands;
using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// End-to-end command tests hosting the real app surface via <see cref="CommandAppTester"/> against
/// a local registry fixture. Asserts exit codes and --json output shapes — the seams IDE plugins
/// and scripts rely on.
/// </summary>
public class CommandTests
{
    private static CommandAppTester App()
    {
        var tester = new CommandAppTester();
        tester.Configure(CliApp.Configure);
        return tester;
    }

    private static async Task<(int ExitCode, string Stdout)> RunAsync(params string[] args)
    {
        using var stdout = new StdoutCapture();
        var result = await App().RunAsync(args);
        return (result.ExitCode, stdout.Text);
    }

    // --- add ---

    [Fact]
    public async Task Add_resolves_transitive_deps_writes_files_and_records_installs()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "card", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("card", items);
        Assert.Contains("button", items); // transitive
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Card", "BzCard.razor")));

        var config = File.ReadAllText(dir.Combine("blaizio.json"));
        Assert.Contains("\"installed\"", config);
        Assert.Contains("\"card\"", config);
    }

    [Fact]
    public async Task Add_with_nothing_resolved_still_emits_json()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(0, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    // --- add --upgrade ---

    [Fact]
    public async Task Add_upgrade_without_csproj_skips_packages_but_repulls_components()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "button", "--json", "-c", dir.Path);
        File.AppendAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor"), "// drift\n");

        var (exit, stdout) = await RunAsync("add", "--upgrade", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("packagesBumped").GetBoolean()); // no csproj
        Assert.True(doc.RootElement.GetProperty("updated").GetProperty("items").GetArrayLength() > 0);

        var (diffExit, _) = await RunAsync("add", "--diff", "--json", "-c", dir.Path);
        Assert.Equal(0, diffExit); // drift healed
    }

    // --- deinit ---

    [Fact]
    public async Task Deinit_removes_config_css_and_tracked_components()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "card", "--json", "-c", dir.Path);
        // A user-authored file under the output dir must survive — removal is by record, not sweep.
        dir.Write(Path.Combine("Components", "Ui", "Mine.razor"), "<h1>mine</h1>\n");

        var (exit, stdout) = await RunAsync("deinit", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var removed = doc.RootElement.GetProperty("removed").EnumerateArray()
            .Select(e => e.GetString()).ToArray();
        Assert.Contains("blaizio.json", removed);
        Assert.Contains(removed, f => f!.StartsWith("Styles/blaizio/"));
        Assert.Contains(removed, f => f!.EndsWith("BzCard.razor"));

        Assert.False(File.Exists(dir.Combine("blaizio.json")));
        Assert.False(Directory.Exists(dir.Combine("Styles", "blaizio")));
        Assert.False(File.Exists(dir.Combine("Styles", "app.css"))); // managed input goes too
        // Tracked components go — card and its transitive button dependency.
        Assert.False(File.Exists(dir.Combine("Components", "Ui", "Card", "BzCard.razor")));
        Assert.False(File.Exists(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
        // The user's own file (and thus the output dir) survives; the @usings add wrote are gone.
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Mine.razor")));
        Assert.DoesNotContain("@using", File.ReadAllText(dir.Combine("_Imports.razor")));
    }

    [Fact]
    public async Task Deinit_dry_run_touches_nothing()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("deinit", "--dry-run", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("removed").GetArrayLength() > 0);
        Assert.True(File.Exists(dir.Combine("blaizio.json")));
        Assert.True(Directory.Exists(dir.Combine("Styles", "blaizio")));
    }

    [Fact]
    public async Task Deinit_on_an_untouched_project_is_a_clean_noop()
    {
        using var dir = new TempDir();
        var (exit, stdout) = await RunAsync("deinit", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(0, doc.RootElement.GetProperty("removed").GetArrayLength());
    }

    // --- exit codes ---

    [Fact]
    public async Task Add_without_init_fails_with_exit_1()
    {
        using var dir = new TempDir();
        var (exit, _) = await RunAsync("add", "button", "--json", "-c", dir.Path);
        Assert.Equal(1, exit);
    }

    [Fact]
    public async Task Missing_registry_maps_to_exit_2()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, _) = await RunAsync("search", "--json", "-c", dir.Path, "--registry", dir.Combine("nope"));
        Assert.Equal(2, exit);
    }

    // --- init ---

    [Fact]
    public async Task NonInteractive_init_is_config_only()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("init", "-y", "--json", "--tailwind", "none", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("template").ValueKind is System.Text.Json.JsonValueKind.Null);
        Assert.Empty(Directory.GetFiles(dir.Path, "*.csproj"));
    }

    [Fact]
    public async Task Init_library_template_scaffolds_a_razor_classlib()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync("init", "-t", "library", "-n", "My.Lib", "--json",
            "--tailwind", "none", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        var csproj = File.ReadAllText(dir.Combine("My.Lib.csproj"));
        Assert.Contains("Microsoft.NET.Sdk.Razor", csproj);
        Assert.Contains("Microsoft.AspNetCore.App", csproj);
        Assert.Contains("Blaizio.Base", csproj);

        var imports = File.ReadAllText(dir.Combine("_Imports.razor"));
        Assert.Contains("@using Microsoft.AspNetCore.Components.Web", imports);
    }

    [Fact]
    public async Task Init_hardens_a_preexisting_bare_class_library()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("Old.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");

        var (exit, stdout) = await RunAsync("init", "-y", "--json", "--tailwind", "none", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("csprojHardened").GetArrayLength() >= 3);
        var csproj = File.ReadAllText(dir.Combine("Old.csproj"));
        Assert.Contains("Microsoft.NET.Sdk.Razor", csproj);
        Assert.Contains("Microsoft.AspNetCore.App", csproj);
    }

    [Fact]
    public async Task Init_json_stdout_is_a_single_json_document()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("init", "--json", "--tailwind", "none", "--style", "EMBER", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout); // throws if not one clean document
        Assert.Equal("ember", doc.RootElement.GetProperty("css").GetProperty("skin").GetString());
    }

    // --- help surface ---

    [Fact]
    public async Task Bare_invocation_shows_the_root_help()
    {
        var result = await App().RunAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: blaizio [options] [command]", result.Output);
        Assert.Contains("Build your component library", result.Output);
    }

    [Fact]
    public async Task Root_help_lists_commands_and_puts_dash_h_last()
    {
        var result = await App().RunAsync("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("init [options] [components...]", result.Output);
        Assert.Contains("apply [options] [preset]", result.Output);
        Assert.Contains("search [options] [registries...]", result.Output);
        Assert.Contains("help [command]", result.Output);
        // Deprecated commands still run but stay out of the listing.
        Assert.DoesNotContain("diff [options]", result.Output);
        Assert.DoesNotContain("update [options]", result.Output);
        Assert.DoesNotContain("upgrade [options]", result.Output);
        // -v before -h; -h is the last option.
        var version = result.Output.IndexOf("-v, --version", StringComparison.Ordinal);
        var help = result.Output.IndexOf("-h, --help", StringComparison.Ordinal);
        Assert.True(version >= 0 && help > version);
    }

    [Fact]
    public async Task Command_help_renders_the_ns_alias_and_ends_with_dash_h()
    {
        var result = await App().RunAsync("add", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: blaizio add [options] [components...]", result.Output);
        Assert.Contains("-ns, --namespace <ns>", result.Output);
        Assert.DoesNotContain("also -ns", result.Output);
        var help = result.Output.IndexOf("-h, --help", StringComparison.Ordinal);
        Assert.True(help > result.Output.IndexOf("--no-nuget", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Help_command_prints_a_commands_help()
    {
        using var ansi = new AnsiCapture();
        var result = await App().RunAsync("help", "add");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: blaizio add", ansi.Text);
    }

    // --- search (merged list) ---

    [Fact]
    public async Task Search_json_with_query_filters_items()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("search", "--json", "-q", "card", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("card", items[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Search_accepts_a_positional_registry()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("search", registry, "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(2, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    // --- add --update / --diff (absorbed commands) ---

    [Fact]
    public async Task Add_update_heals_drift_and_add_diff_reports_it()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "button", "--json", "-c", dir.Path);

        File.AppendAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor"), "// drift\n");
        var (driftExit, driftOut) = await RunAsync("add", "--diff", "--json", "-c", dir.Path);
        Assert.Equal(1, driftExit);
        using (var doc = System.Text.Json.JsonDocument.Parse(driftOut))
            Assert.True(doc.RootElement.GetProperty("hasDrift").GetBoolean());

        var (updateExit, _) = await RunAsync("add", "--update", "--json", "-c", dir.Path);
        Assert.Equal(0, updateExit);

        var (afterExit, _) = await RunAsync("add", "--diff", "--json", "-c", dir.Path);
        Assert.Equal(0, afterExit);
    }

    // --- apply ---

    [Fact]
    public async Task Apply_restyles_and_records_the_preset()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("apply", "eclipse", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal("eclipse", doc.RootElement.GetProperty("preset").GetString());
        Assert.Contains("\"eclipse\"", File.ReadAllText(dir.Combine("blaizio.json")));
    }

    // --- docs ---

    [Fact]
    public async Task Docs_json_reports_metadata_url_and_parameters()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("docs", "button", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var item = doc.RootElement[0];
        Assert.Equal("button", item.GetProperty("name").GetString());
        Assert.Equal("https://blaiz.io/docs/components/button", item.GetProperty("url").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Array, item.GetProperty("parameters").ValueKind);
    }

    [Fact]
    public void Docs_parameter_parser_reads_parameters_defaults_and_required()
    {
        var item = new Blaizio.Cli.Core.Registry.RegistryItem
        {
            Name = "button",
            Files =
            [
                new Blaizio.Cli.Core.Registry.RegistryFile
                {
                    Path = "Ui/Button/BzButton.razor",
                    Content =
                        """
                        @code {
                            [Parameter]
                            public ButtonVariant Variant { get; set; } = ButtonVariant.Default;

                            [Parameter, EditorRequired]
                            public RenderFragment? ChildContent { get; set; }

                            [Parameter]
                            [EditorRequired]
                            public EventCallback<MouseEventArgs> OnClick { get; set; }

                            [CascadingParameter]
                            public BzTheme? Theme { get; set; }
                        }
                        """,
                },
            ],
        };

        var parameters = DocsCommand.ExtractParameters(item);

        Assert.Equal(3, parameters.Count); // CascadingParameter excluded
        Assert.Equal("Variant", parameters[0].Name);
        Assert.Equal("ButtonVariant.Default", parameters[0].Default);
        Assert.Equal("ChildContent", parameters[1].Name);
        Assert.Equal("OnClick", parameters[2].Name);
        Assert.True(parameters[2].Required);
        Assert.Equal("EventCallback<MouseEventArgs>", parameters[2].Type);
    }

    // --- preset ---

    [Fact]
    public async Task Preset_decode_expands_a_code()
    {
        var (exit, stdout) = await RunAsync("preset", "decode", "32r", "--json");

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal("forge", doc.RootElement.GetProperty("style").GetString());
        Assert.Equal("quasar", doc.RootElement.GetProperty("preset").GetString());
        Assert.True(doc.RootElement.GetProperty("rtl").GetBoolean());
    }

    [Fact]
    public async Task Preset_decode_rejects_garbage()
    {
        var (exit, _) = await RunAsync("preset", "decode", "zzzzzzzzz", "--json");
        Assert.Equal(1, exit);
    }

    [Fact]
    public async Task Preset_resolve_round_trips_the_project_styling()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--style", "spark", "-p", "eclipse",
            "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("preset", "resolve", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal("spark", doc.RootElement.GetProperty("style").GetString());
        Assert.Equal("eclipse", doc.RootElement.GetProperty("preset").GetString());
        Assert.Equal("18", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Preset_url_prints_the_create_link()
    {
        var (exit, stdout) = await RunAsync("preset", "url", "32r");

        Assert.Equal(0, exit);
        Assert.Contains("https://blaiz.io/create?preset=32r", stdout);
    }

    // --- registry ---

    [Fact]
    public async Task Registry_add_records_namespace_url_pairs()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var result = await App().RunAsync("registry", "add", "@acme=https://acme.dev/r", "-c", dir.Path);

        Assert.Equal(0, result.ExitCode);
        var config = File.ReadAllText(dir.Combine("blaizio.json"));
        Assert.Contains("\"@acme\"", config);
        Assert.Contains("https://acme.dev/r", config);
    }

    [Fact]
    public async Task Registry_add_rejects_a_bare_namespace()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var result = await App().RunAsync("registry", "add", "@acme", "-c", dir.Path);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Registry_validate_accepts_a_good_manifest_and_flags_a_broken_one()
    {
        using var dir = new TempDir();
        dir.Write("Ui/Button/BzButton.razor", "<button>x</button>\n");
        dir.Write("registry.json",
            """
            {
              "name": "test",
              "items": [
                { "name": "button", "type": "registry:ui",
                  "files": [{ "path": "Ui/Button/BzButton.razor", "type": "registry:ui" }] }
              ]
            }
            """);

        var ok = await App().RunAsync("registry", "validate", "-c", dir.Path);
        Assert.Equal(0, ok.ExitCode);

        dir.Write("registry.json",
            """
            {
              "name": "test",
              "items": [
                { "name": "button", "type": "registry:ui",
                  "files": [{ "path": "Ui/Button/Missing.razor", "type": "registry:ui" }],
                  "registryDependencies": ["ghost"] }
              ]
            }
            """);

        using var ansi = new AnsiCapture();
        var bad = await App().RunAsync("registry", "validate", "-c", dir.Path);
        Assert.Equal(1, bad.ExitCode);
        Assert.Contains("Missing.razor", ansi.Text);
        Assert.Contains("ghost", ansi.Text);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("diff")]
    [InlineData("update")]
    [InlineData("upgrade")]
    public async Task Removed_commands_are_unknown(string command)
    {
        var result = await App().RunAsync(command);
        Assert.NotEqual(0, result.ExitCode);
    }

    // --- command-typed-as-flag guard (blaizio -init) ---

    [Theory]
    [InlineData("-init", "init")]
    [InlineData("--search", "search")]
    [InlineData("-add", "add")]
    [InlineData("--TAILWIND", "tailwind")] // case-insensitive
    public void Flagged_command_is_detected(string arg, string expected) =>
        Assert.Equal(expected, CliApp.DetectFlaggedCommand([arg, "extra"]));

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    [InlineData("-y")]
    [InlineData("--namespace")]
    [InlineData("search")] // the correct form is a command, not a flag
    [InlineData("button")] // a plain component argument
    public void Real_flags_and_commands_pass_through(string arg) =>
        Assert.Null(CliApp.DetectFlaggedCommand([arg]));

    [Fact]
    public void Flagged_command_only_guards_the_command_slot() =>
        // `add -search` is add's problem (a bad option), not a mistyped command.
        Assert.Null(CliApp.DetectFlaggedCommand(["add", "-search"]));

    [Fact]
    public void Empty_args_are_safe() =>
        Assert.Null(CliApp.DetectFlaggedCommand([]));
}
