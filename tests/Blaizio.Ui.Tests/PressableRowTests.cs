using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// BzItem and BzMarker with OnClick. The default element is a div; with a click handler and
/// nothing else it could be pressed with a pointer only. It now says it is a button, takes a tab
/// stop, and carries the mark core.js turns Enter and Space into a click for.
/// </summary>
public sealed class PressableRowTests : BunitContext
{
    public PressableRowTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private EventCallback<MouseEventArgs> Click(Action? action = null) =>
        EventCallback.Factory.Create<MouseEventArgs>(this, _ => action?.Invoke());

    [Fact]
    public void An_item_with_OnClick_is_a_button_to_the_keyboard()
    {
        var clicks = 0;
        var cut = Render<BzItem>(p => p.Add(x => x.OnClick, Click(() => clicks++)).AddChildContent("Open"));

        var item = cut.Find("[data-slot=item]");
        Assert.Equal("button", item.GetAttribute("role"));
        Assert.Equal("0", item.GetAttribute("tabindex"));
        Assert.True(item.HasAttribute("data-bz-key-click"));

        // The key press becomes an ordinary click in the script, so one handler serves both.
        item.Click();
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void A_marker_with_OnClick_is_a_button_to_the_keyboard()
    {
        var cut = Render<BzMarker>(p => p.Add(x => x.OnClick, Click()).AddChildContent("Explored 4 files"));

        var marker = cut.Find("[data-slot=marker]");
        Assert.Equal("button", marker.GetAttribute("role"));
        Assert.Equal("0", marker.GetAttribute("tabindex"));
        Assert.True(marker.HasAttribute("data-bz-key-click"));
    }

    [Fact]
    public void Without_OnClick_the_row_stays_a_plain_element()
    {
        var item = Render<BzItem>(p => p.AddChildContent("Row")).Find("[data-slot=item]");
        Assert.False(item.HasAttribute("role"));
        Assert.False(item.HasAttribute("tabindex"));
        Assert.False(item.HasAttribute("data-bz-key-click"));

        var marker = Render<BzMarker>(p => p.AddChildContent("Note")).Find("[data-slot=marker]");
        Assert.False(marker.HasAttribute("role"));
        Assert.False(marker.HasAttribute("tabindex"));
        Assert.False(marker.HasAttribute("data-bz-key-click"));
    }

    [Fact]
    public void An_element_of_your_own_keeps_its_own_semantics()
    {
        // RenderAs with a real link: no button role on an anchor, no second tab stop.
        var cut = Render<BzItem>(p => p
            .Add(x => x.OnClick, Click())
            .Add(x => x.RenderAs, props => b =>
            {
                b.OpenElement(0, "a");
                b.AddAttribute(1, "href", "/profile");
                b.AddMultipleAttributes(2, props.Attributes);
                b.AddContent(3, "Profile");
                b.CloseElement();
            }));

        var link = cut.Find("a");
        Assert.False(link.HasAttribute("role"));
        Assert.False(link.HasAttribute("tabindex"));
        Assert.False(link.HasAttribute("data-bz-key-click"));
    }

    [Fact]
    public void A_role_of_your_own_wins()
    {
        var cut = Render<BzItem>(p => p
            .Add(x => x.OnClick, Click())
            .AddUnmatched("role", "option")
            .AddChildContent("Row"));

        Assert.Equal("option", cut.Find("[data-slot=item]").GetAttribute("role"));
    }
}
