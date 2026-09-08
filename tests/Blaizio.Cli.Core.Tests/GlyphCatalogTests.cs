using Xunit;
using System.Text.RegularExpressions;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Rewriting;
using Blaizio.Cli.Core.Styling;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The glyph table is the whole "which set do the components use" feature: every row must name
/// a member that exists in every generated set, and the rewriter must move a glyph file between
/// sets without touching anything the table does not know.
/// </summary>
public class GlyphCatalogTests
{
    [Fact]
    public void Every_set_in_the_icon_catalog_has_a_family()
    {
        foreach (var set in IconSetCatalog.All)
            Assert.True(GlyphCatalog.Families.ContainsKey(set.Name), $"no glyph family for set '{set.Name}'");
    }

    [Fact]
    public void Every_glyph_names_a_member_for_every_set()
    {
        foreach (var glyph in GlyphCatalog.All)
            foreach (var set in IconSetCatalog.All)
                Assert.True(glyph.Members.ContainsKey(set.Name), $"glyph '{glyph.Name}' has no member for '{set.Name}'");
    }

    [Fact]
    public void Glyph_names_are_unique_and_tabler_members_keep_the_glyph_name()
    {
        Assert.Equal(GlyphCatalog.All.Count, GlyphCatalog.All.Select(g => g.Name).Distinct(StringComparer.Ordinal).Count());
        foreach (var glyph in GlyphCatalog.All)
            Assert.Equal($"{GlyphCatalog.Families["tabler"]}.{glyph.Name}", glyph.Members["tabler"]);
    }

    /// <summary>
    /// Holds the table to the generated icon classes in src/Blaizio.Icons.*: a member the table
    /// names but the set does not ship would only surface as a compile error in a consumer that
    /// picked that set.
    /// </summary>
    [Theory]
    [InlineData("tabler", "Blaizio.Icons.Tabler")]
    [InlineData("lucide", "Blaizio.Icons.Lucide")]
    [InlineData("phosphor", "Blaizio.Icons.Phosphor")]
    [InlineData("remix", "Blaizio.Icons.Remix")]
    [InlineData("hugeicons", "Blaizio.Icons.HugeIcons")]
    public void Every_member_exists_in_the_generated_set(string set, string project)
    {
        var dir = Path.Combine(Root(), "src", project);
        Assert.True(Directory.Exists(dir), $"missing {dir}");

        // "Tabler.Outline.Check" -> the file Tabler.Outline.cs must declare `public static Icon Check`.
        var byFamily = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var glyph in GlyphCatalog.All)
        {
            var member = glyph.Members[set];
            var lastDot = member.LastIndexOf('.');
            var family = member[..lastDot];
            var icon = member[(lastDot + 1)..];
            if (!byFamily.TryGetValue(family, out var declared))
            {
                var file = Path.Combine(dir, family + ".cs");
                Assert.True(File.Exists(file), $"glyph '{glyph.Name}': no generated family file {family}.cs for '{set}'");
                declared = [.. Regex.Matches(File.ReadAllText(file), @"public static Icon (\w+)").Select(m => m.Groups[1].Value)];
                byFamily[family] = declared;
            }
            Assert.True(declared.Contains(icon), $"glyph '{glyph.Name}': '{member}' is not a member of the generated '{set}' set");
        }
    }

    /// <summary>The BzGlyphs file the registry ships must be exactly the table's Tabler column:
    /// a glyph added to one and not the other is a rewrite the CLI cannot make.</summary>
    [Fact]
    public void The_shipped_glyph_file_matches_the_table()
    {
        var file = Path.Combine(Root(), "src", "Blaizio.Ui", GlyphRewriter.FileName);
        var members = Regex.Matches(File.ReadAllText(file), @"public static Icon (\w+) => ([\w.]+);")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);

        Assert.Equal(
            GlyphCatalog.All.Select(g => g.Name).Order(StringComparer.Ordinal),
            members.Keys.Order(StringComparer.Ordinal));
        foreach (var glyph in GlyphCatalog.All)
            Assert.Equal(glyph.Members["tabler"], members[glyph.Name]);
    }

    [Fact]
    public void Rewriter_retargets_known_members_and_leaves_the_rest()
    {
        const string source = """
            public static class BzGlyphs
            {
                /// <summary>Selected.</summary>
                public static Icon Check => Tabler.Outline.Check;
                public static Icon X => Tabler.Outline.X;
                public static Icon MyOwn => Tabler.Outline.Anchor;
            }
            """;

        var lucide = new GlyphRewriter("lucide").Rewrite(source);
        Assert.Contains("public static Icon Check => Lucide.Outline.Check;", lucide);
        Assert.Contains("public static Icon X => Lucide.Outline.X;", lucide);
        Assert.Contains("public static Icon MyOwn => Tabler.Outline.Anchor;", lucide);

        // From any set back to any other - the member NAME drives it, not the current expression.
        var remix = new GlyphRewriter("remix").Rewrite(lucide);
        Assert.Contains("public static Icon X => Remix.Line.Close;", remix);
        var back = new GlyphRewriter("tabler").Rewrite(remix);
        Assert.Equal(source, back);
    }

    [Fact]
    public void Rewriter_for_the_default_set_or_an_unknown_one_is_null()
    {
        Assert.Null(GlyphRewriter.For(null));
        Assert.Null(GlyphRewriter.For("tabler"));
        Assert.Null(GlyphRewriter.For("Tabler"));
        Assert.Null(GlyphRewriter.For("nope"));
        Assert.NotNull(GlyphRewriter.For("Lucide"));
    }

    [Fact]
    public void Glyph_file_is_recognised_by_name_only()
    {
        Assert.True(GlyphRewriter.IsGlyphFile("BzGlyphs.cs"));
        Assert.True(GlyphRewriter.IsGlyphFile("Ui/BzGlyphs.cs"));
        Assert.True(GlyphRewriter.IsGlyphFile(@"Components\Ui\BzGlyphs.cs"));
        Assert.False(GlyphRewriter.IsGlyphFile("NotBzGlyphs.cs"));
        Assert.False(GlyphRewriter.IsGlyphFile("Ui/BzGlyphs.razor"));
    }

    [Fact]
    public void Package_substitution_swaps_only_the_default_set()
    {
        var packages = new[]
        {
            new NugetDependency("Blaizio.Base", "1.0"),
            new NugetDependency("Blaizio.Icons.Tabler", "1.0"),
            new NugetDependency("TailwindMerge.NET"),
        };

        Assert.Equal(packages, IconSetPackages.Substitute(packages, null));
        Assert.Equal(packages, IconSetPackages.Substitute(packages, "tabler"));
        Assert.Equal(packages, IconSetPackages.Substitute(packages, "nope"));

        var lucide = IconSetPackages.Substitute(packages, "lucide");
        Assert.Equal(["Blaizio.Base", "Blaizio.Icons.Lucide", "TailwindMerge.NET"], lucide.Select(p => p.Id));
        Assert.Equal("1.0", lucide[1].Version);
    }

    private static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Directory.Build.props")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }
}
