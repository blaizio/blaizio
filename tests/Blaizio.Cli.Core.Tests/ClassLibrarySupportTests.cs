using Blaizio.Cli.Core.Projects;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>Bare class-library hardening: SDK swap, framework reference, implicit usings, imports.</summary>
public class ClassLibrarySupportTests
{
    private const string BareCsproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
          </PropertyGroup>

        </Project>
        """;

    [Fact]
    public async Task Hardens_a_bare_class_library_in_place()
    {
        using var dir = new TempDir();
        dir.Write("Lib.csproj", BareCsproj);
        var project = ProjectContext.Discover(dir.Path);
        Assert.True(project.IsBareClassLibrary);

        var changes = await ClassLibrarySupport.HardenCsprojAsync(project);

        Assert.Equal(3, changes.Count);
        var text = dir.Read("Lib.csproj");
        Assert.Contains("Sdk=\"Microsoft.NET.Sdk.Razor\"", text);
        Assert.Contains("<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />", text);
        Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", text);
        // Original content preserved, not reformatted away.
        Assert.Contains("<Nullable>enable</Nullable>", text);

        var reloaded = ProjectContext.Discover(dir.Path);
        Assert.False(reloaded.IsBareClassLibrary);
        Assert.True(reloaded.HasAspNetCoreFrameworkRef);
    }

    [Fact]
    public async Task Hardening_is_idempotent()
    {
        using var dir = new TempDir();
        dir.Write("Lib.csproj", BareCsproj);
        await ClassLibrarySupport.HardenCsprojAsync(ProjectContext.Discover(dir.Path));
        var afterFirst = dir.Read("Lib.csproj");

        var second = await ClassLibrarySupport.HardenCsprojAsync(ProjectContext.Discover(dir.Path));

        Assert.Empty(second); // no longer bare — nothing to do
        Assert.Equal(afterFirst, dir.Read("Lib.csproj"));
    }

    [Fact]
    public async Task Razor_and_blazor_sdks_are_left_alone()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"></Project>");
        var project = ProjectContext.Discover(dir.Path);

        Assert.False(project.IsBareClassLibrary);
        Assert.Empty(await ClassLibrarySupport.HardenCsprojAsync(project));
    }

    [Fact]
    public async Task Standard_imports_are_seeded_and_idempotent()
    {
        using var dir = new TempDir();
        dir.Write("_Imports.razor", "@using Microsoft.AspNetCore.Components.Web\n");

        var changed = await ClassLibrarySupport.EnsureStandardImportsAsync(dir.Path);
        Assert.True(changed);

        var lines = (await File.ReadAllLinesAsync(dir.Combine("_Imports.razor")))
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        foreach (var ns in ClassLibrarySupport.StandardUsings)
            Assert.Contains($"@using {ns}", lines);
        // The pre-existing line wasn't duplicated.
        Assert.Single(lines, l => l == "@using Microsoft.AspNetCore.Components.Web");

        Assert.False(await ClassLibrarySupport.EnsureStandardImportsAsync(dir.Path));
    }

    [Fact]
    public void Discover_reads_the_sdk_and_framework_reference()
    {
        using var dir = new TempDir();
        dir.Write("Lib.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            </Project>
            """);

        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal("Microsoft.NET.Sdk.Razor", project.Sdk);
        Assert.True(project.HasAspNetCoreFrameworkRef);
        Assert.False(project.IsBareClassLibrary);
    }
}
