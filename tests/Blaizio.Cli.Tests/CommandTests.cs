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
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

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
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(0, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    // --- add --preset ---

    [Fact]
    public async Task Add_with_preset_applies_it_before_adding_components()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "button", "-p", "eclipse", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        // A --json add stays a single AddResult document - the apply leg runs silently.
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Contains("button", doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("\"eclipse\"", File.ReadAllText(dir.Combine("blaizio.json")));
        // v3: the preset's values patch straight into the tokens file - no preset sheet.
        Assert.False(File.Exists(dir.Combine("Styles", "blaizio", "preset-eclipse.css")));
        Assert.True(File.Exists(dir.Combine("Styles", "app.css")));
    }

    [Fact]
    public async Task Add_with_preset_alone_is_a_complete_operation()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, _) = await RunAsync("add", "-p", "eclipse", "-y", "-s", "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.Contains("\"eclipse\"", File.ReadAllText(dir.Combine("blaizio.json")));
    }

    // --- update: packages + components in lockstep ---

    [Fact]
    public async Task Update_without_csproj_skips_packages_but_repulls_components()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "button", "--json", "-c", dir.Path);
        File.AppendAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor"), "// drift\n");

        var (exit, stdout) = await RunAsync("update", "--force", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("packagesBumped").GetBoolean()); // no csproj
        Assert.True(doc.RootElement.GetProperty("updated").GetProperty("items").GetArrayLength() > 0);

        var (diffExit, _) = await RunAsync("add", "--diff", "--json", "-c", dir.Path);
        Assert.Equal(0, diffExit); // drift healed
    }

    [Fact]
    public async Task Add_update_flag_is_gone_and_parse_fails_loudly()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        // The absorbed-flag era is over: strict parsing rejects the old spellings outright.
        var (updateExit, _) = await RunAsync("add", "--update", "-c", dir.Path);
        var (upgradeExit, _) = await RunAsync("add", "--upgrade", "-c", dir.Path);

        Assert.NotEqual(0, updateExit);
        Assert.NotEqual(0, upgradeExit);
    }

    // --- update: v1 -> v3 migration ---

    [Fact]
    public async Task Update_migrates_a_v1_project_to_the_v3_layout()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "button", "--json", "-c", dir.Path);

        // Retrofit the project to the v1 shape: managed sheets + a marker input importing them.
        dir.Write(Path.Combine("Styles", "blaizio", "theme.css"),
            ":root {\n  --radius: 0.75rem;\n  --primary: oklch(0.42 0.19 275);\n}\n\n.dark {\n  --primary: oklch(0.6 0.17 275);\n  --primary-button: oklch(0.53 0.18 275);\n}\n");
        dir.Write(Path.Combine("Styles", "blaizio", "style-ember.css"), "/* skin */\n");
        dir.Write(Path.Combine("Styles", "app.css"),
            "/* blaizio:managed */\n@import \"tailwindcss\" source(none);\n@import \"./blaizio/theme.css\";\n@import \"./blaizio/style-ember.css\" layer(components);\n");
        File.AppendAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor"), "// local edit\n");

        var (exit, stdout) = await RunAsync("update", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("migrated").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("removed").GetArrayLength() >= 2);

        // The managed dir is gone, the input is v3, the user's token values carried over.
        Assert.False(Directory.Exists(dir.Combine("Styles", "blaizio")));
        var css = File.ReadAllText(dir.Combine("Styles", "app.css"));
        Assert.Contains("@import \"../.blaizio/blaizio.css\";", css);
        Assert.DoesNotContain("./blaizio/", css);
        Assert.Contains("--primary: oklch(0.42 0.19 275);", css);
        Assert.DoesNotContain("--primary-button", css);
        Assert.Contains(".blaizio/", File.ReadAllText(dir.Combine(".gitignore")));
        // The ledgered component was re-pulled, overwriting the local edit.
        Assert.DoesNotContain("// local edit", File.ReadAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
        // Recorded as CLI-owned so uninstall may delete the regenerated file.
        Assert.Contains("\"cssCreated\": true", File.ReadAllText(dir.Combine("blaizio.json")));

        // A second update is a plain v3 update - no migration document, no legacy left.
        var (again, stdout2) = await RunAsync("update", "-y", "--json", "-c", dir.Path);
        Assert.Equal(0, again);
        Assert.DoesNotContain("\"migrated\"", stdout2);
    }

    // --- uninstall ---

    [Fact]
    public async Task Uninstall_removes_config_css_and_tracked_components()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "card", "--json", "-c", dir.Path);
        // A user-authored file under the output dir must survive — removal is by record, not sweep.
        dir.Write(Path.Combine("Components", "Ui", "Mine.razor"), "<h1>mine</h1>\n");

        var (exit, stdout) = await RunAsync("uninstall", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var removed = doc.RootElement.GetProperty("removed").EnumerateArray()
            .Select(e => e.GetString()).ToArray();
        Assert.Contains("blaizio.json", removed);
        Assert.Contains("Styles/app.css", removed); // init created it (cssCreated)
        Assert.Contains(removed, f => f!.EndsWith("BzCard.razor"));

        Assert.False(File.Exists(dir.Combine("blaizio.json")));
        Assert.False(File.Exists(dir.Combine("Styles", "app.css")));
        // Tracked components go — card and its transitive button dependency.
        Assert.False(File.Exists(dir.Combine("Components", "Ui", "Card", "BzCard.razor")));
        Assert.False(File.Exists(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
        // The user's own file (and thus the output dir) survives; the @usings add wrote are gone.
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Mine.razor")));
        Assert.DoesNotContain("@using", File.ReadAllText(dir.Combine("_Imports.razor")));
    }

    [Fact]
    public async Task Uninstall_dry_run_touches_nothing()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("uninstall", "--dry-run", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("removed").GetArrayLength() > 0);
        Assert.True(File.Exists(dir.Combine("blaizio.json")));
        Assert.True(File.Exists(dir.Combine("Styles", "app.css")));
    }

    [Fact]
    public async Task Uninstall_on_an_untouched_project_is_a_clean_noop()
    {
        using var dir = new TempDir();
        var (exit, stdout) = await RunAsync("uninstall", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(0, doc.RootElement.GetProperty("removed").GetArrayLength());
    }

    [Fact]
    public async Task Uninstall_tears_down_the_font_wiring()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("wwwroot/index.html", "<html>\n<head>\n</head>\n<body></body>\n</html>\n");
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "font-inter", "font-heading-lora", "--json", "-c", dir.Path);

        var (exit, _) = await RunAsync("uninstall", "-y", "-s", "-c", dir.Path);

        Assert.Equal(0, exit);
        // The overlay dies with Styles/blaizio, the Google Fonts link leaves the host page, and
        // the recorded heading/font pair dies with blaizio.json itself.
        Assert.False(File.Exists(dir.Combine("Styles", "blaizio", "fonts.css")));
        Assert.False(File.Exists(dir.Combine("blaizio.json")));
        var host = File.ReadAllText(dir.Combine("wwwroot", "index.html"));
        Assert.DoesNotContain("fonts.googleapis.com", host);
        Assert.DoesNotContain("data-blaizio", host);
    }

    [Fact]
    public async Task Uninstall_un_alias_still_works()
    {
        using var dir = new TempDir();
        var (exit, stdout) = await RunAsync("un", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(0, doc.RootElement.GetProperty("removed").GetArrayLength());
    }

    [Fact]
    public async Task Uninstall_deinit_spelling_is_gone()
    {
        using var dir = new TempDir();
        var (exit, _) = await RunAsync("deinit", "-y", "-c", dir.Path);

        Assert.NotEqual(0, exit); // fully removed, not even a hidden alias
    }

    // --- eject ---

    [Fact]
    public async Task Eject_inlines_the_contract_flags_the_config_and_refuses_a_second_run()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("eject", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("ejected").GetBoolean());
        Assert.Equal("Styles/app.css", doc.RootElement.GetProperty("input").GetString());
        // Never built here, so the CLI's embedded sheets are the source.
        Assert.Equal("embedded", doc.RootElement.GetProperty("source").GetString());

        var css = File.ReadAllText(dir.Combine("Styles", "app.css"));
        Assert.DoesNotContain(".blaizio/blaizio.css\";", css);
        Assert.DoesNotContain(".blaizio/animate.css\";", css);
        Assert.Contains("Ejected Blaizio contract", css);
        Assert.Contains("\"ejected\": true", File.ReadAllText(dir.Combine("blaizio.json")));

        // A second eject is a clean no-op, not an error.
        var (again, stdout2) = await RunAsync("eject", "-y", "--json", "-c", dir.Path);
        Assert.Equal(0, again);
        using var doc2 = System.Text.Json.JsonDocument.Parse(stdout2);
        Assert.True(doc2.RootElement.GetProperty("alreadyEjected").GetBoolean());
    }

    [Fact]
    public async Task Eject_prefers_the_materialized_sheets()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        dir.Write(Path.Combine(".blaizio", "blaizio.css"), "/* from the installed Base */\n");
        dir.Write(Path.Combine(".blaizio", "animate.css"), "/* from the installed Base (animate) */\n");

        var (exit, stdout) = await RunAsync("eject", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal("materialized", doc.RootElement.GetProperty("source").GetString());
        Assert.Contains("/* from the installed Base */", File.ReadAllText(dir.Combine("Styles", "app.css")));
    }

    [Fact]
    public async Task Eject_without_a_config_errors()
    {
        using var dir = new TempDir();
        var (exit, _) = await RunAsync("eject", "-y", "-c", dir.Path);
        Assert.Equal(1, exit);
    }

    [Fact]
    public async Task Wiring_top_up_leaves_an_ejected_tokens_file_alone()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("eject", "-y", "-s", "-c", dir.Path);
        var ejected = File.ReadAllText(dir.Combine("Styles", "app.css"));

        var (exit, _) = await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        // The dead .blaizio/ imports must not come back.
        Assert.Equal(ejected, File.ReadAllText(dir.Combine("Styles", "app.css")));
    }

    // --- exit codes ---

    [Fact]
    public async Task Add_without_init_bootstraps_the_config_first()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        // add on an uninitialized project adopts it: config-only init (no scaffold), then the add.
        var (exit, stdout) = await RunAsync("add", "button", "--json", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout); // one clean AddResult document
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("button", items);
        Assert.True(File.Exists(dir.Combine("blaizio.json")));
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
        Assert.True(File.Exists(dir.Combine("Styles", "app.css"))); // the init leg wired the CSS
    }

    [Fact]
    public async Task Add_carries_the_init_wiring_flags_on_a_fresh_project()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        // add is a superset of init: the wiring flags reach the bootstrap leg.
        var (exit, _) = await RunAsync("add", "button", "--style", "ash", "--rtl", "--tailwind", "none",
            "-y", "-s", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        var config = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dir.Combine("blaizio.json")));
        Assert.Equal("ash", config.RootElement.GetProperty("style").GetString());
        Assert.True(config.RootElement.GetProperty("rtl").GetBoolean());
    }

    [Fact]
    public async Task Add_wiring_flag_on_an_initialized_project_runs_the_init_top_up()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        // No components: `add --rtl` alone is a complete wiring-only operation.
        var (exit, _) = await RunAsync("add", "--rtl", "-y", "-s", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        var config = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dir.Combine("blaizio.json")));
        Assert.True(config.RootElement.GetProperty("rtl").GetBoolean());
    }

    [Fact]
    public async Task Add_on_an_rtl_project_auto_installs_direction_provider_once()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        // RTL wiring pulls the direction cascade component along with the requested add.
        var (exit, _) = await RunAsync("add", "button", "--rtl", "--tailwind", "none",
            "-y", "-s", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(dir.Combine("Components/Ui/DirectionProvider/BzDirectionProvider.razor")));
        var config = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dir.Combine("blaizio.json")));
        Assert.True(config.RootElement.GetProperty("installed").TryGetProperty("direction-provider", out _));

        // A later plain add on the (already-RTL) project must not re-trigger the auto-pull:
        // the provider only rides along when RTL is being enabled (bootstrap or --rtl).
        var (exit2, stdout2) = await RunAsync("add", "card", "--json", "-c", dir.Path, "--registry", registry);
        Assert.Equal(0, exit2);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout2);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.DoesNotContain("direction-provider", items);
    }

    [Fact]
    public async Task Add_dry_run_without_init_still_fails_and_writes_nothing()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync("add", "button", "--dry-run", "--json", "-c", dir.Path, "--registry", registry);

        Assert.Equal(1, exit);
        Assert.False(File.Exists(dir.Combine("blaizio.json")));
    }

    [Fact]
    public async Task Missing_registry_maps_to_exit_2()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, _) = await RunAsync("search", "--json", "-c", dir.Path, "--registry", dir.Combine("nope"));
        Assert.Equal(2, exit);
    }

    // --- init ---

    [Fact]
    public async Task NonInteractive_wiring_is_config_only()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(dir.Combine("blaizio.json")));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.csproj")); // the wiring leg never scaffolds
    }

    [Fact]
    public async Task New_library_template_scaffolds_a_razor_classlib()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync("new", "library", "-n", "My.Lib", "--json",
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
    public async Task Create_works_as_a_new_alias()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync("create", "library", "-n", "My.Lib", "--json",
            "--tailwind", "none", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(dir.Combine("My.Lib.csproj")));
    }

    [Fact]
    public async Task New_rejects_an_unknown_template()
    {
        using var dir = new TempDir();
        var (exit, _) = await RunAsync("new", "nope", "-y", "-c", dir.Path);
        Assert.Equal(1, exit);
    }

    [Fact]
    public async Task Init_is_not_a_command()
    {
        using var dir = new TempDir();
        // The wiring pipeline has no CLI surface of its own - `add` (and `new`) run it internally.
        var (exit, _) = await RunAsync("init", "-y", "-c", dir.Path);
        Assert.NotEqual(0, exit);
    }

    [Fact]
    public async Task Wiring_hardens_a_preexisting_bare_class_library()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("Old.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");

        var (exit, _) = await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        var csproj = File.ReadAllText(dir.Combine("Old.csproj"));
        Assert.Contains("Microsoft.NET.Sdk.Razor", csproj);
        Assert.Contains("Microsoft.AspNetCore.App", csproj);
    }

    [Fact]
    public async Task New_json_stdout_is_a_single_json_document()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("new", "library", "-n", "My.Lib", "--json",
            "--tailwind", "none", "--style", "EMBER", "--registry", registry, "-c", dir.Path);

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
        Assert.Contains("add [options] [components...]", result.Output);
        Assert.Contains("update [options] [components...]", result.Output);
        Assert.Contains("apply [options] [preset]", result.Output);
        Assert.Contains("search, list [options] [registries...]", result.Output);
        Assert.Contains("help [command]", result.Output);
        // Deprecated/legacy commands still run but stay out of the listing.
        Assert.DoesNotContain("init [options]", result.Output);
        Assert.DoesNotContain("diff [options]", result.Output);
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
        // The full catalogue: 3 ui components + 2 font items (fonts stay listable/searchable).
        Assert.Equal(5, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    // --- update / add --diff ---

    [Fact]
    public async Task Update_keeps_local_edits_until_forced_and_add_diff_reports_them()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "button", "--json", "-c", dir.Path);

        File.AppendAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor"), "// drift\n");
        var (driftExit, driftOut) = await RunAsync("add", "--diff", "--json", "-c", dir.Path);
        Assert.Equal(1, driftExit);
        using (var doc = System.Text.Json.JsonDocument.Parse(driftOut))
            Assert.True(doc.RootElement.GetProperty("hasDrift").GetBoolean());

        // Unattended (--json, nobody to ask): the edit survives, so the drift is still there.
        var (updateExit, updateOut) = await RunAsync("update", "--json", "-c", dir.Path);
        Assert.Equal(0, updateExit);
        using (var doc = System.Text.Json.JsonDocument.Parse(updateOut))
            Assert.Equal("button", doc.RootElement.GetProperty("updated")
                .GetProperty("keptLocal").EnumerateArray().Single().GetString());
        var (stillExit, _) = await RunAsync("add", "--diff", "--json", "-c", dir.Path);
        Assert.Equal(1, stillExit);

        // --force is the explicit "take upstream anyway".
        var (forcedExit, _) = await RunAsync("update", "--force", "--json", "-c", dir.Path);
        Assert.Equal(0, forcedExit);

        var (afterExit, _) = await RunAsync("add", "--diff", "--json", "-c", dir.Path);
        Assert.Equal(0, afterExit);
    }

    // --- fonts (preset codes carrying webfont selections) ---

    [Fact]
    public async Task Init_with_webfont_code_writes_overlay_and_host_link()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("wwwroot/index.html", "<html>\n<head>\n</head>\n<body></body>\n</html>\n");

        // 000h60: ember/nova, default chart/radius, heading space-grotesk (h), body inter (6).
        var (exit, _) = await RunAsync("add", "-y", "--tailwind", "none", "-s",
            "--registry", registry, "--preset", "000h60", "-c", dir.Path);

        Assert.Equal(0, exit);
        // v3: the selection patches straight into the tokens file - no fonts.css overlay.
        var tokens = File.ReadAllText(dir.Combine("Styles", "app.css"));
        Assert.Contains("--font-heading: \"Space Grotesk\"", tokens);
        Assert.Contains("font-family: \"Inter\"", tokens);
        Assert.False(File.Exists(dir.Combine("Styles", "blaizio", "fonts.css")));
        var host = File.ReadAllText(dir.Combine("wwwroot", "index.html"));
        Assert.Contains("data-blaizio=\"fonts\"", host);
        Assert.Contains("family=Space+Grotesk", host);
        Assert.Contains("family=Inter", host);
    }

    [Fact]
    public async Task Apply_swaps_and_removes_the_webfont_link()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("wwwroot/index.html", "<html>\n<head>\n</head>\n<body></body>\n</html>\n");
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry,
            "--preset", "000h60", "-c", dir.Path);

        // Swap to lora (r) heading + geist (5) body: the marked link is replaced, not duplicated.
        var (swapExit, _) = await RunAsync("apply", "000r50", "-y", "-s", "-c", dir.Path);
        Assert.Equal(0, swapExit);
        var host = File.ReadAllText(dir.Combine("wwwroot", "index.html"));
        Assert.Contains("family=Lora", host);
        Assert.Contains("family=Geist", host);
        Assert.DoesNotContain("family=Space+Grotesk", host);
        Assert.Equal(1, CountOf(host, "data-blaizio=\"fonts\""));

        // Back to system stacks (humanist=1, classic=2): the managed link goes away.
        var (sysExit, _) = await RunAsync("apply", "000120", "-y", "-s", "-c", dir.Path);
        Assert.Equal(0, sysExit);
        host = File.ReadAllText(dir.Combine("wwwroot", "index.html"));
        Assert.DoesNotContain("data-blaizio=\"fonts\"", host);
    }

    [Fact]
    public async Task Init_skips_preset_fonts_when_the_app_defines_its_own()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        // A user-authored Tailwind input with its own @font-face - the CLI must not stomp it.
        dir.Write("Styles/app.css",
            "@import \"tailwindcss\";\n@font-face { font-family: Dubai; src: url(./dubai.woff2); }\n");

        var (exit, _) = await RunAsync("add", "-y", "--tailwind", "none", "-s",
            "--registry", registry, "--preset", "000h60", "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.False(File.Exists(dir.Combine("Styles", "blaizio", "fonts.css")));
    }

    [Fact]
    public async Task Apply_only_fonts_overrides_a_user_font_setup()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("Styles/app.css",
            "@import \"tailwindcss\";\n@font-face { font-family: Dubai; src: url(./dubai.woff2); }\n");
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry,
            "--preset", "000h60", "-c", dir.Path);

        // Full apply keeps skipping...
        await RunAsync("apply", "000h60", "-y", "-s", "-c", dir.Path);
        Assert.DoesNotContain("Space Grotesk", File.ReadAllText(dir.Combine("Styles", "app.css")));

        // ...but --only fonts is the explicit override.
        var (exit, _) = await RunAsync("apply", "000h60", "--only", "fonts", "-y", "-s", "-c", dir.Path);
        Assert.Equal(0, exit);
        Assert.Contains("Space Grotesk", File.ReadAllText(dir.Combine("Styles", "app.css")));
    }

    [Fact]
    public async Task Add_font_items_write_the_overlay_and_merge_the_pair()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("wwwroot/index.html", "<html>\n<head>\n</head>\n<body></body>\n</html>\n");
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (bodyExit, _) = await RunAsync("add", "font-inter", "--json", "-c", dir.Path);
        Assert.Equal(0, bodyExit);
        var tokens = File.ReadAllText(dir.Combine("Styles", "app.css"));
        Assert.Contains("font-family: \"Inter\"", tokens);

        // The heading item replaces only its half - the body font survives.
        var (headExit, _) = await RunAsync("add", "font-heading-lora", "--json", "-c", dir.Path);
        Assert.Equal(0, headExit);
        tokens = File.ReadAllText(dir.Combine("Styles", "app.css"));
        Assert.Contains("--font-heading: \"Lora\"", tokens);
        Assert.Contains("font-family: \"Inter\"", tokens);

        var config = File.ReadAllText(dir.Combine("blaizio.json"));
        Assert.Contains("\"headingFont\": \"lora\"", config);
        Assert.Contains("\"bodyFont\": \"inter\"", config);
        var host = File.ReadAllText(dir.Combine("wwwroot", "index.html"));
        Assert.Contains("family=Lora", host);
        Assert.Contains("family=Inter", host);
        Assert.Contains("data-blaizio=\"fonts\"", host);
    }

    [Fact]
    public async Task Add_all_excludes_font_items()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "--all", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("button", items);
        Assert.DoesNotContain("font-inter", items);
        Assert.DoesNotContain("font-heading-lora", items);
    }

    [Fact]
    public void Preset_codes_round_trip_webfont_selections()
    {
        var selection = new Blaizio.Cli.Core.Styling.PresetSelection(
            "spark", "comet", Rtl: true, Chart: "ocean",
            Heading: "playfair-display", Font: "instrument-sans", Radius: "lg");
        var code = Blaizio.Cli.Core.Styling.PresetCode.Encode(selection);
        Assert.True(Blaizio.Cli.Core.Styling.PresetCode.TryDecode(code, out var decoded));
        Assert.Equal(selection, decoded);
    }

    private static int CountOf(string text, string token)
    {
        var count = 0;
        for (var i = text.IndexOf(token, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(token, i + token.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    // --- apply ---

    [Fact]
    public async Task Apply_restyles_and_records_the_preset()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

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
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

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
                            public EventCallback<MouseEventArgs> Click { get; set; }

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
        Assert.Equal("Click", parameters[2].Name);
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
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--style", "spark", "-p", "eclipse",
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
        Assert.Contains("https://blaiz.io/themes?preset=32r", stdout);
    }

    // --- registry ---

    [Fact]
    public async Task Registry_add_records_namespace_url_pairs()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

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
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var result = await App().RunAsync("registry", "add", "@acme", "-c", dir.Path);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Add_resolves_namespaced_components_and_their_deps_from_the_named_registry()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        var acme = LocalRegistry.CreateSecondary(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await App().RunAsync("registry", "add", $"@acme={acme}", "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "@acme/tag", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("@acme/tag", items);
        Assert.Contains("@acme/chip", items); // plain-name dep resolved inside @acme, not the default registry
        // Namespaced installs nest under their registry's folder, away from default-registry files.
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Acme", "Tag", "BzTag.razor")));
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Acme", "Chip", "BzChip.razor")));
    }

    [Fact]
    public async Task Add_with_an_unrecorded_namespace_maps_to_exit_2()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, _) = await RunAsync("add", "@nope/button", "--json", "-c", dir.Path);
        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task Search_resolves_a_namespace_source_through_the_registries_map()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        var acme = LocalRegistry.CreateSecondary(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await App().RunAsync("registry", "add", $"@acme={acme}", "-c", dir.Path);

        var (exit, stdout) = await RunAsync("search", "@acme", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var names = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToArray();
        Assert.Contains("tag", names);
        Assert.Contains("chip", names);
        Assert.DoesNotContain("button", names); // the default registry was not searched
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

    [Fact]
    public async Task Registry_validate_flags_escaping_paths_and_unsafe_names()
    {
        using var dir = new TempDir();
        dir.Write("outside.razor", "<secret />");
        dir.Write("registry/registry.json",
            """
            {
              "name": "test",
              "items": [
                { "name": "button", "type": "registry:ui",
                  "files": [{ "path": "../outside.razor", "type": "registry:ui" }] },
                { "name": "../evil", "type": "registry:ui",
                  "files": [{ "path": "../outside.razor", "type": "registry:ui" }] }
              ]
            }
            """);

        using var ansi = new AnsiCapture();
        var result = await App().RunAsync("registry", "validate", "registry/registry.json", "-c", dir.Path);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("escapes the manifest directory", ansi.Text);
        Assert.Contains("not a slug", ansi.Text);
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

    // --- bundler mode (init --css) ---

    [Fact]
    public async Task Init_css_records_the_input_and_syncs_it_without_writing_app_css()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("tailwind.css", "@import \"tailwindcss\";\n.hero { color: red; }\n");

        var (exit, _) = await RunAsync("add", "-y", "--css", "tailwind.css", "--tailwind", "none",
            "-s", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.Contains("\"css\": \"tailwind.css\"", File.ReadAllText(dir.Combine("blaizio.json")));
        var css = File.ReadAllText(dir.Combine("tailwind.css"));
        Assert.Contains("@import \"./.blaizio/blaizio.css\";", css);
        Assert.Contains(".hero { color: red; }", css);
        Assert.False(File.Exists(dir.Combine("Styles", "app.css"))); // no parallel CLI input
    }

    [Fact]
    public async Task Add_css_records_and_syncs_the_bundler_input_on_an_initialized_project()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        dir.Write("tailwind.css", "@import \"tailwindcss\";\n");

        var (exit, _) = await RunAsync("add", "--css", "tailwind.css", "-y", "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.Contains("\"css\": \"tailwind.css\"", File.ReadAllText(dir.Combine("blaizio.json")));
        Assert.Contains("@import \"./.blaizio/blaizio.css\";", File.ReadAllText(dir.Combine("tailwind.css")));
    }

    [Fact]
    public async Task Add_css_flows_through_the_uninitialized_bootstrap()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("tailwind.css", "@import \"tailwindcss\";\n");

        var (exit, _) = await RunAsync("add", "button", "--css", "tailwind.css", "--json", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        Assert.Contains("\"css\": \"tailwind.css\"", File.ReadAllText(dir.Combine("blaizio.json")));
        Assert.Contains("@import \"./.blaizio/blaizio.css\";", File.ReadAllText(dir.Combine("tailwind.css")));
        Assert.False(File.Exists(dir.Combine("Styles", "app.css")));
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
    }

    [Fact]
    public async Task Init_discovers_a_lone_tailwind_input_by_content_and_adopts_it()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write(Path.Combine("assets", "site.css"), "@import \"tailwindcss\";\n.hero { color: red; }\n");

        var (exit, _) = await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.Contains("\"css\": \"assets/site.css\"", File.ReadAllText(dir.Combine("blaizio.json")));
        var css = File.ReadAllText(dir.Combine("assets", "site.css"));
        Assert.Contains("@import \"../.blaizio/blaizio.css\";", css);
        Assert.Contains(".hero { color: red; }", css);
        Assert.False(File.Exists(dir.Combine("Styles", "app.css")));
    }

    [Fact]
    public async Task Init_with_multiple_inputs_falls_back_to_the_managed_default_non_interactively()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        dir.Write("a.css", "@import \"tailwindcss\";\n");
        dir.Write("b.css", "@import \"tailwindcss\";\n");

        var (exit, _) = await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.DoesNotContain("\"css\":", File.ReadAllText(dir.Combine("blaizio.json")));
        Assert.True(File.Exists(dir.Combine("Styles", "app.css"))); // ambiguous - stays CLI-managed
    }

    [Fact]
    public async Task Init_css_with_a_missing_file_fails_before_writing_anything()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync("add", "-y", "--css", "Styles/tailwind.css", "--tailwind", "none",
            "-s", "--registry", registry, "-c", dir.Path);

        Assert.Equal(1, exit);
        Assert.False(File.Exists(dir.Combine("blaizio.json"))); // nothing half-applied
    }

    // --- command-typed-as-flag guard (blaizio -add) ---

    [Theory]
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

    // --- apply --dry-run ---

    [Fact]
    public async Task Apply_dry_run_reports_without_touching_anything()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "button", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var cssBefore = File.ReadAllText(dir.Combine("Styles", "app.css"));
        var configBefore = File.ReadAllText(dir.Combine("blaizio.json"));
        var buttonPath = dir.Combine("Components", "Ui", "Button", "BzButton.razor");
        var buttonBefore = File.ReadAllText(buttonPath);

        var (exit, stdout) = await RunAsync("apply", "eclipse", "--dry-run", "--json", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.Equal("eclipse", doc.RootElement.GetProperty("preset").GetString());
        Assert.True(doc.RootElement.GetProperty("theme").GetBoolean());
        // Nothing on disk moved: tokens file, config and installed components are byte-identical.
        Assert.Equal(cssBefore, File.ReadAllText(dir.Combine("Styles", "app.css")));
        Assert.Equal(configBefore, File.ReadAllText(dir.Combine("blaizio.json")));
        Assert.Equal(buttonBefore, File.ReadAllText(buttonPath));
    }

    // --- registry validate --json ---

    [Fact]
    public async Task Registry_validate_json_reports_findings_and_exit_code()
    {
        using var dir = new TempDir();

        // Missing manifest: still one clean JSON document, exit 1.
        var (exit, stdout) = await RunAsync("registry", "validate", "--json", "-c", dir.Path);
        Assert.Equal(1, exit);
        using (var doc = System.Text.Json.JsonDocument.Parse(stdout))
        {
            Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
            Assert.True(doc.RootElement.GetProperty("problems").GetArrayLength() > 0);
        }

        // An invalid manifest yields the findings array.
        dir.Write("registry.json", """{"name":"","items":[{"name":"a"},{"name":"a"}]}""");
        var (exit2, stdout2) = await RunAsync("registry", "validate", "--json", "-c", dir.Path);
        Assert.Equal(1, exit2);
        using (var doc2 = System.Text.Json.JsonDocument.Parse(stdout2))
        {
            Assert.False(doc2.RootElement.GetProperty("valid").GetBoolean());
            var problems = doc2.RootElement.GetProperty("problems").EnumerateArray()
                .Select(p => p.GetString()).ToArray();
            Assert.Contains(problems, p => p!.Contains("duplicate item name"));
        }
    }

    // --- preset flags (shared surface) ---

    [Fact]
    public async Task Preset_url_supports_json_and_silent()
    {
        var (exit, stdout) = await RunAsync("preset", "url", "32r", "--json");
        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal("32r", doc.RootElement.GetProperty("code").GetString());
        Assert.StartsWith("https://", doc.RootElement.GetProperty("url").GetString());

        var (silentExit, silentStdout) = await RunAsync("preset", "url", "32r", "-s");
        Assert.Equal(0, silentExit);
        Assert.Equal(string.Empty, silentStdout.Trim());
    }
}
