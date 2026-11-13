using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// What a code block line tells the copy button. A removed diff line is not part of the code the
/// diff produces: it is marked as left out, which is what tells a diff of removed lines only
/// (nothing to copy) from plain text that was never split into lines (copy all of it).
/// </summary>
public sealed class CodeBlockCopyMarkTests : BunitContext
{
    public CodeBlockCopyMarkTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    [Theory]
    [InlineData(CodeBlockLineVariant.Default)]
    [InlineData(CodeBlockLineVariant.Added)]
    [InlineData(CodeBlockLineVariant.Highlighted)]
    public void A_line_that_is_part_of_the_code_is_copied(CodeBlockLineVariant variant)
    {
        var content = Render<BzCodeBlockLine>(p => p.Add(x => x.Variant, variant).AddChildContent("var x = 1;"))
            .Find("[data-slot=code-block-line-content]");

        Assert.True(content.HasAttribute("data-bz-copy-line"));
        Assert.False(content.HasAttribute("data-bz-copy-skip"));
    }

    [Fact]
    public void A_removed_line_is_marked_as_left_out()
    {
        var content = Render<BzCodeBlockLine>(p => p
                .Add(x => x.Variant, CodeBlockLineVariant.Removed)
                .AddChildContent("var x = 0;"))
            .Find("[data-slot=code-block-line-content]");

        Assert.False(content.HasAttribute("data-bz-copy-line"));
        Assert.True(content.HasAttribute("data-bz-copy-skip"));
    }
}
