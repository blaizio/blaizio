using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + open/close tests for the headless dialog. Focus trapping, exit-animation presence,
/// and scroll locking are JS (ts/focusScope.ts, presence.ts, scrollLock.ts — verified in-browser);
/// these cover the C# contract: the trigger↔content ARIA wiring, role/aria-modal, presence of
/// overlay+content while open, Escape/overlay/close dismissal, and controlled binding.
/// JSInterop is Loose so the module imports are no-ops.
/// </summary>
public class DialogRenderTests : TestContext
{
    public DialogRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Parts(bool description = true) => builder =>
    {
        builder.OpenComponent<BlazeDialogTrigger>(0);
        builder.AddComponentParameter(1, nameof(BlazeDialogTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Open")));
        builder.CloseComponent();

        builder.OpenComponent<BlazeDialogOverlay>(2);
        builder.CloseComponent();

        builder.OpenComponent<BlazeDialogContent>(3);
        builder.AddComponentParameter(4, nameof(BlazeDialogContent.ChildContent), (RenderFragment)(c =>
        {
            c.OpenComponent<BlazeDialogTitle>(0);
            c.AddComponentParameter(1, nameof(BlazeDialogTitle.ChildContent),
                (RenderFragment)(t => t.AddContent(0, "Title")));
            c.CloseComponent();

            if (description)
            {
                c.OpenComponent<BlazeDialogDescription>(2);
                c.AddComponentParameter(3, nameof(BlazeDialogDescription.ChildContent),
                    (RenderFragment)(d => d.AddContent(0, "Description")));
                c.CloseComponent();
            }

            c.OpenComponent<BlazeDialogClose>(4);
            c.AddComponentParameter(5, nameof(BlazeDialogClose.ChildContent),
                (RenderFragment)(x => x.AddContent(0, "Close")));
            c.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_renders_only_the_trigger_with_collapsed_aria()
    {
        var cut = RenderComponent<BlazeDialog>(p => p.AddChildContent(Parts()));

        var trigger = cut.Find("button");
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Trigger_opens_and_wires_content_aria()
    {
        var cut = RenderComponent<BlazeDialog>(p => p.AddChildContent(Parts()));

        cut.Find("button").Click();

        var trigger = cut.FindAll("button")[0];
        var content = cut.Find("[role=dialog]");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("open", trigger.GetAttribute("data-state"));
        Assert.Equal(trigger.GetAttribute("aria-controls"), content.GetAttribute("id"));
        Assert.Equal("true", content.GetAttribute("aria-modal"));
        Assert.Equal("open", content.GetAttribute("data-state"));

        var title = cut.Find("h2");
        Assert.Equal(content.GetAttribute("aria-labelledby"), title.GetAttribute("id"));
        var description = cut.Find("p");
        Assert.Equal(content.GetAttribute("aria-describedby"), description.GetAttribute("id"));

        // Modal dialogs render the dismissable backdrop.
        Assert.Single(cut.FindAll("div[aria-hidden=true]"));
    }

    [Fact]
    public void Close_button_closes_via_presence_handshake()
    {
        var cut = RenderComponent<BlazeDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        // The close button is the second button (trigger is first).
        cut.FindAll("button")[1].Click();

        // Presence keeps it mounted with data-state="closed" until JS reports animationend.
        var content = cut.Find("[role=dialog]");
        Assert.Equal("closed", content.GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BlazeDialogContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Escape_closes_the_dialog()
    {
        var cut = RenderComponent<BlazeDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));
        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Overlay_pointerdown_closes_the_dialog()
    {
        var cut = RenderComponent<BlazeDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        cut.Find("[aria-hidden=true]").PointerDown();

        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));
    }

    [Fact]
    public void Non_modal_renders_no_overlay_and_no_aria_modal()
    {
        var cut = RenderComponent<BlazeDialog>(p => p
            .Add(x => x.Modal, false)
            .AddChildContent(Parts()));
        cut.Find("button").Click();

        Assert.Empty(cut.FindAll("[aria-hidden=true]"));
        Assert.Null(cut.Find("[role=dialog]").GetAttribute("aria-modal"));
    }

    [Fact]
    public void Controlled_binding_drives_and_reports_state()
    {
        var open = false;
        var cut = RenderComponent<BlazeDialog>(p => p
            .Add(x => x.Open, open)
            .Add(x => x.OpenChanged, (bool v) => open = v)
            .AddChildContent(Parts()));

        cut.Find("button").Click();
        Assert.True(open);
        // Controlled: state only moves when the parent flows the new value back in.
        cut.SetParametersAndRender(p => p.Add(x => x.Open, open));
        Assert.Single(cut.FindAll("[role=dialog]"));

        open = false;
        cut.SetParametersAndRender(p => p.Add(x => x.Open, open));
        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));
    }

    [Fact]
    public void DefaultOpen_starts_open_uncontrolled()
    {
        var cut = RenderComponent<BlazeDialog>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Parts()));

        Assert.Single(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Modal_locks_scroll_exactly_once_and_unlocks_after_close()
    {
        var scrollLock = JSInterop.SetupModule("./_content/Blazeo.Base/dist/scrollLock.js");
        var cut = RenderComponent<BlazeDialog>(p => p.AddChildContent(Parts()));

        cut.Find("button").Click();
        cut.Render();
        cut.Render(); // extra re-renders while open must not lock again
        Assert.Single(scrollLock.Invocations, i => i.Identifier == "lock");
        Assert.DoesNotContain(scrollLock.Invocations, i => i.Identifier == "unlock");

        cut.FindAll("button")[1].Click();
        cut.InvokeAsync(() => cut.FindComponent<BlazeDialogContent>().Instance.OnCloseFinished());
        Assert.Single(scrollLock.Invocations, i => i.Identifier == "unlock");
    }
}
