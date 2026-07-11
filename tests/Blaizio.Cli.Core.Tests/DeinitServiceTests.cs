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

    [Fact]
    public async Task Strips_the_managed_lines_from_a_bundler_input_and_keeps_the_rest()
    {
        using var dir = new TempDir();
        dir.Write("blaizio.json", """{ "namespace": "App.Components.Ui", "css": "tailwind.css" }""");
        dir.Write("tailwind.css",
            "@import \"tailwindcss\";\n" +
            "@import \"./Styles/blaizio/theme.css\";\n" +
            "@import \"./Styles/blaizio/style-ember.css\" layer(components);\n" +
            ".hero { color: red; }\n");
        dir.Write("Styles/blaizio/theme.css", "/* tokens */");

        var result = await new DeinitService().RunAsync(dir.Path);

        var css = dir.Read("tailwind.css");
        Assert.Contains("@import \"tailwindcss\";", css);
        Assert.Contains(".hero { color: red; }", css);
        Assert.DoesNotContain("blaizio/", css);
        Assert.Contains("tailwind.css", result.Changed);
        Assert.False(Directory.Exists(dir.Combine("Styles", "blaizio")));
        Assert.False(dir.Exists("blaizio.json"));
    }
}
