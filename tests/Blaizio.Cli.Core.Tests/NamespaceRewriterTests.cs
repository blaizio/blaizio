using Blaizio.Cli.Core.Rewriting;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class NamespaceRewriterTests
{
    private readonly NamespaceRewriter _rewriter = new("MyApp.Components.Ui");

    [Fact]
    public void Rewrites_file_scoped_namespace()
    {
        Assert.Equal(
            "namespace MyApp.Components.Ui.Button;",
            _rewriter.Rewrite("namespace Blaizio.Ui.Button;"));
    }

    [Fact]
    public void Rewrites_using_and_namespace_directives()
    {
        var input = "@namespace Blaizio.Ui.Button\n@using Blaizio.Ui\n";
        var expected = "@namespace MyApp.Components.Ui.Button\n@using MyApp.Components.Ui\n";
        Assert.Equal(expected, _rewriter.Rewrite(input));
    }

    [Fact]
    public void Leaves_base_and_icons_namespaces_untouched()
    {
        var input = "@using Blaizio\n@using Blaizio.Base\n@using Blaizio.Icons\n";
        Assert.Equal(input, _rewriter.Rewrite(input));
    }

    [Fact]
    public void Does_not_rewrite_a_longer_identifier_that_only_starts_with_the_root()
    {
        Assert.Equal("using Blaizio.UiKit.Foo;", _rewriter.Rewrite("using Blaizio.UiKit.Foo;"));
    }

    [Fact]
    public void Rewrites_every_occurrence_on_a_line()
    {
        Assert.Equal(
            "MyApp.Ui.A x = new MyApp.Ui.A();",
            new NamespaceRewriter("MyApp.Ui").Rewrite("Blaizio.Ui.A x = new Blaizio.Ui.A();"));
    }
}
