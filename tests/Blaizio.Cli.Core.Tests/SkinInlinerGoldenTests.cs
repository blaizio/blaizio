using System.Text;
using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// Golden files for the v3 inliner over the REAL authored sheets: the full token map per skin,
/// plus fully inlined flagship component sources. Any parser or sheet change shows up as a
/// reviewable diff. Regenerate with BLAIZIO_UPDATE_GOLDENS=1 (dotnet test) after an intended
/// change and review the diff like any other code.
/// </summary>
public class SkinInlinerGoldenTests
{
    private static readonly string[] Skins = ["ash", "aura", "ember", "flint", "forge", "glow", "spark", "wisp"];

    private static readonly bool Update =
        Environment.GetEnvironmentVariable("BLAIZIO_UPDATE_GOLDENS") == "1";

    [Theory]
    [InlineData("ash")]
    [InlineData("aura")]
    [InlineData("ember")]
    [InlineData("flint")]
    [InlineData("forge")]
    [InlineData("glow")]
    [InlineData("spark")]
    [InlineData("wisp")]
    public void Token_map_matches_golden(string skin)
    {
        var inliner = CreateFor(skin);
        var sb = new StringBuilder();
        foreach (var token in inliner.Tokens)
            sb.Append(token).Append(" => ").Append(inliner.Resolve(token)).Append('\n');
        AssertGolden($"tokenmap-{skin}.txt", sb.ToString());
    }

    [Theory]
    [InlineData("ember", "Button/BzButton.razor")]
    [InlineData("ash", "Button/BzButton.razor")]
    [InlineData("ember", "Checkbox/BzCheckbox.razor")]
    [InlineData("ember", "Pagination/BzPaginationLink.razor")]
    public void Inlined_component_matches_golden(string skin, string file)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "Blaizio.Ui", "Components", file.Replace('/', Path.DirectorySeparatorChar)));
        var inlined = CreateFor(skin).Inline(source);
        AssertGolden($"{Path.GetFileName(file)}.{skin}.golden", inlined);
    }

    private static SkinInliner CreateFor(string skin)
    {
        var styles = Path.Combine(Root(), "src", "Blaizio.Ui", "Styles");
        return SkinInliner.Create(
            File.ReadAllText(Path.Combine(styles, "shared.css")),
            File.ReadAllText(Path.Combine(styles, $"style-{skin}.css")));
    }

    private static void AssertGolden(string name, string actual)
    {
        var path = Path.Combine(Root(), "tests", "Blaizio.Cli.Core.Tests", "Goldens", name);
        actual = actual.ReplaceLineEndings("\n");
        if (Update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }
        Assert.True(File.Exists(path), $"golden missing: {name} - run with BLAIZIO_UPDATE_GOLDENS=1");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Blaizio.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
