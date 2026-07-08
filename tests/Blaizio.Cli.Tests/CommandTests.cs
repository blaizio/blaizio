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

    // --- list ---

    [Fact]
    public async Task List_json_emits_the_index()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("list", "--json", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(2, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task List_query_filters_items()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("list", "--json", "-q", "card", "-c", dir.Path, "--registry", registry);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("card", items[0].GetProperty("name").GetString());
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

    // --- diff / update ---

    [Fact]
    public async Task Diff_reports_clean_then_drift_then_update_restores()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "button", "--json", "-c", dir.Path);

        var (cleanExit, _) = await RunAsync("diff", "--json", "-c", dir.Path);
        Assert.Equal(0, cleanExit);

        File.AppendAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor"), "// drift\n");
        var (driftExit, driftOut) = await RunAsync("diff", "--json", "-c", dir.Path);
        Assert.Equal(1, driftExit);
        using (var doc = System.Text.Json.JsonDocument.Parse(driftOut))
            Assert.True(doc.RootElement.GetProperty("hasDrift").GetBoolean());

        var (updateExit, _) = await RunAsync("update", "--json", "-c", dir.Path);
        Assert.Equal(0, updateExit);

        var (afterExit, _) = await RunAsync("diff", "--json", "-c", dir.Path);
        Assert.Equal(0, afterExit);
    }

    // --- upgrade ---

    [Fact]
    public async Task Upgrade_without_csproj_skips_packages_but_repulls_components()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("init", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        await RunAsync("add", "button", "--json", "-c", dir.Path);
        File.AppendAllText(dir.Combine("Components", "Ui", "Button", "BzButton.razor"), "// drift\n");

        var (exit, stdout) = await RunAsync("upgrade", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("packagesBumped").GetBoolean()); // no csproj
        Assert.True(doc.RootElement.GetProperty("updated").GetProperty("items").GetArrayLength() > 0);

        var (diffExit, _) = await RunAsync("diff", "--json", "-c", dir.Path);
        Assert.Equal(0, diffExit); // drift healed
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

        var (exit, _) = await RunAsync("list", "--json", "-c", dir.Path, "--registry", dir.Combine("nope"));
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

        var (exit, stdout) = await RunAsync("init", "--json", "--tailwind", "none", "--theme", "EMBER", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout); // throws if not one clean document
        Assert.Equal("ember", doc.RootElement.GetProperty("css").GetProperty("skin").GetString());
    }

    // --- command-typed-as-flag guard (blaizio -update) ---

    [Theory]
    [InlineData("-update", "update")]
    [InlineData("--update", "update")]
    [InlineData("--upgrade", "upgrade")]
    [InlineData("-add", "add")]
    [InlineData("--TAILWIND", "tailwind")] // case-insensitive
    public void Flagged_command_is_detected(string arg, string expected) =>
        Assert.Equal(expected, CliApp.DetectFlaggedCommand([arg, "extra"]));

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    [InlineData("-y")]
    [InlineData("--namespace")]
    [InlineData("update")] // the correct form is a command, not a flag
    [InlineData("button")] // a plain component argument
    public void Real_flags_and_commands_pass_through(string arg) =>
        Assert.Null(CliApp.DetectFlaggedCommand([arg]));

    [Fact]
    public void Flagged_command_only_guards_the_command_slot() =>
        // `add -update` is add's problem (a bad option), not a mistyped command.
        Assert.Null(CliApp.DetectFlaggedCommand(["add", "-update"]));

    [Fact]
    public void Empty_args_are_safe() =>
        Assert.Null(CliApp.DetectFlaggedCommand([]));
}
