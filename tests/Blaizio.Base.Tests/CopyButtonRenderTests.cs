using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The headless copy button: the markers dist/clipboard.js copies from, and the state + announcement
/// it shows once JS reports the outcome.
/// </summary>
public class CopyButtonRenderTests : BunitContext
{
    public CopyButtonRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Renders_a_labelled_button_carrying_the_text_to_copy()
    {
        var cut = Render<BaseCopyButton>(p => p.Add(x => x.Value, "dotnet --version"));

        var button = cut.Find("button");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Copy", button.GetAttribute("aria-label"));
        Assert.Equal("dotnet --version", button.GetAttribute("data-bz-copy-text"));
        Assert.False(button.HasAttribute("data-bz-copy-from"));
        Assert.False(string.IsNullOrEmpty(button.GetAttribute("data-bz-copy")));
        Assert.False(button.HasAttribute("data-copy-state"));
    }

    [Fact]
    public void From_points_the_copy_at_an_element_when_there_is_no_value()
    {
        var cut = Render<BaseCopyButton>(p => p.Add(x => x.From, "#output"));

        Assert.Equal("#output", cut.Find("button").GetAttribute("data-bz-copy-from"));
        Assert.False(cut.Find("button").HasAttribute("data-bz-copy-text"));
    }

    [Fact]
    public void A_successful_copy_reads_copied_and_is_announced()
    {
        bool? reported = null;
        var cut = Render<BaseCopyButton>(p => p
            .Add(x => x.Value, "x")
            .Add(x => x.OnCopied, (bool copied) => reported = copied));

        // The handler then waits out the two-second feedback; the state is visible before that.
        _ = cut.InvokeAsync(() => cut.Instance.OnCopyResult(true));

        Assert.Equal("copied", cut.Find("button").GetAttribute("data-copy-state"));
        Assert.Equal("Copied", cut.Find("[role=status]").TextContent);
        Assert.True(reported);
    }

    [Fact]
    public void A_refused_write_reads_failed_and_is_announced()
    {
        var cut = Render<BaseCopyButton>(p => p
            .Add(x => x.Value, "x")
            .Add(x => x.FailedLabel, "Could not copy"));

        _ = cut.InvokeAsync(() => cut.Instance.OnCopyResult(false));

        Assert.Equal("failed", cut.Find("button").GetAttribute("data-copy-state"));
        Assert.Equal("Could not copy", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void RenderAs_carries_the_copy_markers_onto_your_element()
    {
        var cut = Render<BaseCopyButton>(p => p
            .Add(x => x.Value, "x")
            .Add(x => x.RenderAs, (RenderFragment<BzRenderProps>)(props => builder =>
            {
                builder.OpenElement(0, "a");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.CloseElement();
            })));

        var anchor = cut.Find("a");
        Assert.False(string.IsNullOrEmpty(anchor.GetAttribute("data-bz-copy")));
        Assert.Equal("x", anchor.GetAttribute("data-bz-copy-text"));
        Assert.False(anchor.HasAttribute("type"));
    }
}
