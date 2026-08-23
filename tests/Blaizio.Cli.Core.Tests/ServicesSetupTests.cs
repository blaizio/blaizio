using Blaizio.Cli.Core.Projects;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// Program.cs gets the one registration every Blaizio app needs. Found the hard way: a showcase
/// project that never called AddBlaizio() compiled and ran until its first component that
/// injects ICore rendered - "No registered service of type 'Blaizio.ICore'" - and nothing in the
/// install pipeline had ever said a word about it.
/// </summary>
public class ServicesSetupTests
{
    private const string WasmProgram =
        """
        using Microsoft.AspNetCore.Components.Web;
        using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

        await builder.Build().RunAsync();
        """;

    private const string ServerProgram =
        """
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        var app = builder.Build();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        app.Run();
        """;

    [Fact]
    public async Task Registers_above_the_build_line_in_a_wasm_program()
    {
        using var dir = new TempDir();
        dir.Write("Program.cs", WasmProgram);

        var result = await new ServicesSetup().EnsureAsync(dir.Path);

        Assert.Equal("Program.cs", result.Path);
        Assert.True(result.Registered);
        Assert.Equal(["builder.Services.AddBlaizio() registered"], result.Changes);
        var program = dir.Read("Program.cs");
        Assert.Contains("builder.Services.AddBlaizio();", program);
        Assert.True(
            program.IndexOf("AddBlaizio();", StringComparison.Ordinal) < program.IndexOf("builder.Build()", StringComparison.Ordinal),
            "the registration must precede the host build");
        // Lands after the app's own registrations, not in front of them.
        Assert.True(program.IndexOf("AddScoped", StringComparison.Ordinal) < program.IndexOf("AddBlaizio", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Uses_the_builder_variable_the_program_actually_has()
    {
        using var dir = new TempDir();
        dir.Write("Program.cs", ServerProgram.Replace("builder", "host"));

        await new ServicesSetup().EnsureAsync(dir.Path);

        Assert.Contains("host.Services.AddBlaizio();", dir.Read("Program.cs"));
    }

    [Fact]
    public async Task Second_run_changes_nothing()
    {
        using var dir = new TempDir();
        dir.Write("Program.cs", WasmProgram);
        await new ServicesSetup().EnsureAsync(dir.Path);
        var once = dir.Read("Program.cs");

        var result = await new ServicesSetup().EnsureAsync(dir.Path);

        Assert.True(result.Registered);
        Assert.Empty(result.Changes);
        Assert.Equal(once, dir.Read("Program.cs"));
    }

    [Fact]
    public async Task An_app_authored_registration_counts_and_is_left_alone()
    {
        using var dir = new TempDir();
        var program = WasmProgram.Replace(
            "await builder.Build()",
            "builder.Services.AddBlaizio(o => o.Toasts.Position = \"top\");\n\nawait builder.Build()");
        dir.Write("Program.cs", program);

        var result = await new ServicesSetup().EnsureAsync(dir.Path);

        Assert.True(result.Registered);
        Assert.Empty(result.Changes);
        Assert.Equal(program, dir.Read("Program.cs"));
        Assert.True(new ServicesSetup().IsRegistered(dir.Path));
    }

    [Fact]
    public async Task A_program_with_no_build_line_is_reported_not_patched()
    {
        using var dir = new TempDir();
        dir.Write("Program.cs", "System.Console.WriteLine(\"hello\");\n");

        var result = await new ServicesSetup().EnsureAsync(dir.Path);

        Assert.Equal("Program.cs", result.Path);
        Assert.False(result.Registered);
        Assert.Empty(result.Changes);
        Assert.False(new ServicesSetup().IsRegistered(dir.Path));
    }

    [Fact]
    public async Task No_program_file_is_not_a_missing_registration()
    {
        using var dir = new TempDir();

        var result = await new ServicesSetup().EnsureAsync(dir.Path);

        Assert.Null(result.Path);
        Assert.True(new ServicesSetup().IsRegistered(dir.Path));
    }

    [Fact]
    public async Task Remove_strips_exactly_what_the_cli_wrote()
    {
        using var dir = new TempDir();
        dir.Write("Program.cs", WasmProgram);
        await new ServicesSetup().EnsureAsync(dir.Path);

        var result = await new ServicesSetup().RemoveAsync(dir.Path);

        Assert.Equal(["AddBlaizio() registration removed"], result.Changes);
        Assert.False(result.Registered);
        Assert.Equal(WasmProgram, dir.Read("Program.cs"));
    }

    [Fact]
    public async Task Remove_leaves_an_app_authored_registration_alone()
    {
        using var dir = new TempDir();
        var program = WasmProgram.Replace("await builder.Build()", "builder.Services.AddBlaizio();\n\nawait builder.Build()");
        dir.Write("Program.cs", program);

        var result = await new ServicesSetup().RemoveAsync(dir.Path);

        Assert.Empty(result.Changes);
        Assert.True(result.Registered);
        Assert.Equal(program, dir.Read("Program.cs"));
    }
}
