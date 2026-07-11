using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TailwindInputLocatorTests
{
    [Fact]
    public void Finds_inputs_by_content_regardless_of_name_or_location()
    {
        using var dir = new TempDir();
        dir.Write("assets/site.css", "@import \"tailwindcss\";\n.hero{}\n");
        dir.Write("plain.css", ".not-tailwind {}\n"); // no marker content - not a candidate

        var found = TailwindInputLocator.Discover(dir.Path);

        Assert.Equal(["assets/site.css"], found);
    }

    [Fact]
    public void Skips_managed_files_build_output_and_the_default_input()
    {
        using var dir = new TempDir();
        dir.Write("Styles/app.css", "@import \"tailwindcss\";\n");                    // default flow owns it
        dir.Write("Styles/blaizio/theme.css", "@import \"tailwindcss\";\n");          // managed asset
        dir.Write("wwwroot/compiled.css", "@import \"tailwindcss\";\n");              // build output
        dir.Write("bin/junk.css", "@import \"tailwindcss\";\n");
        dir.Write("node_modules/pkg/x.css", "@import \"tailwindcss\";\n");
        dir.Write("Styles/moved.css", "/* blaizio:managed */\n@import \"tailwindcss\";\n"); // relocated CLI input

        Assert.Empty(TailwindInputLocator.Discover(dir.Path));
    }

    [Fact]
    public void Orders_multiple_candidates_shallowest_first()
    {
        using var dir = new TempDir();
        dir.Write("deep/nested/other.css", "@tailwind base;\n"); // v3 directives count too
        dir.Write("main.css", "@import 'tailwindcss';\n");

        Assert.Equal(["main.css", "deep/nested/other.css"], TailwindInputLocator.Discover(dir.Path));
    }
}
