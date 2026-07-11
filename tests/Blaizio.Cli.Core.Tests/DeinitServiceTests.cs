using Blaizio.Cli.Core.Operations;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class DeinitServiceTests
{
    [Fact]
    public async Task Removes_the_standalone_targets_dir_and_csproj_import()
    {
        using var dir = new TempDir();
        dir.Write("blaizio.json", """{ "namespace": "App.Components.Ui" }""");
        dir.Write(".blaizio/Blaizio.Tailwind.targets", "<Project></Project>");
        dir.Write("App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <Import Project=".blaizio/Blaizio.Tailwind.targets" />
            </Project>
            """);
        dir.Write("wwwroot/app.css", "/* compiled */");

        var result = await new DeinitService().RunAsync(dir.Path);

        Assert.False(Directory.Exists(dir.Combine(".blaizio")));
        Assert.Contains(".blaizio/Blaizio.Tailwind.targets", result.Removed);
        Assert.DoesNotContain("Blaizio.Tailwind.targets", dir.Read("App.csproj"));
        Assert.Contains("App.csproj", result.Changed);
        // The standalone pipeline owned wwwroot/app.css — its compiled output goes too.
        Assert.False(dir.Exists("wwwroot/app.css"));
    }
}
