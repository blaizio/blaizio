using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + open/close tests for the headless dialog. Focus trapping, exit-animation presence,
/// and scroll locking are JS (ts/focusScope.ts, presence.ts, scrollLock.ts - verified in-browser);
/// these cover the C# contract: the trigger↔content ARIA wiring, role/aria-modal, presence of
/// overlay+content while open, Escape/overlay/close dismissal, and controlled binding.
/// JSInterop is Loose so the module imports are no-ops.
/// </summary>
public class DialogRenderTests : TestContext
{
    public DialogRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Parts(bool description = true) => builder =>
    {
        builder.OpenComponent<BaseDialogTrigger>(0);
        builder.AddComponentParameter(1, nameof(BaseDialogTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Open")));
        builder.CloseComponent();

        builder.OpenComponent<BaseDialogOverlay>(2);
        builder.CloseComponent();

        builder.OpenComponent<BaseDialogContent>(3);
        builder.AddComponentParameter(4, nameof(BaseDialogContent.ChildContent), (RenderFragment)(c =>
        {
            c.OpenComponent<BaseDialogTitle>(0);
            c.AddComponentParameter(1, nameof(BaseDialogTitle.ChildContent),
                (RenderFragment)(t => t.AddContent(0, "Title")));
            c.CloseComponent();

            if (description)
            {
                c.OpenComponent<BaseDialogDescription>(2);
                c.AddComponentParameter(3, nameof(BaseDialogDescription.ChildContent),
                    (RenderFragment)(d => d.AddContent(0, "Description")));
                c.CloseComponent();
            }

            c.OpenComponent<BaseDialogClose>(4);
            c.AddComponentParameter(5, nameof(BaseDialogClose.ChildContent),
                (RenderFragment)(x => x.AddContent(0, "Close")));
            c.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_renders_only_the_trigger_with_collapsed_aria()
    {
        var cut = RenderComponent<BaseDialog>(p => p.AddChildContent(Parts()));

        var trigger = cut.Find("button");
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Trigger_opens_and_wires_content_aria()
    {
        var cut = RenderComponent<BaseDialog>(p => p.AddChildContent(Parts()));

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
        var cut = RenderComponent<BaseDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        // The close button is the second button (trigger is first).
        cut.FindAll("button")[1].Click();

        // Presence keeps it mounted with data-state="closed" until JS reports animationend.
        var content = cut.Find("[role=dialog]");
        Assert.Equal("closed", content.GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BaseDialogContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Aria_describedby_is_omitted_when_there_is_no_description()
    {
        var cut = RenderComponent<BaseDialog>(p => p.AddChildContent(Parts(description: false)));
        cut.Find("button").Click();

        var content = cut.Find("[role=dialog]");
        // Title is present, so the dialog is labelled; with no description rendered, aria-describedby
        // must be absent rather than dangling at a non-existent id.
        Assert.NotNull(content.GetAttribute("aria-labelledby"));
        Assert.Null(content.GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll("p"));
    }

    [Fact]
    public void Escape_closes_the_dialog()
    {
        var cut = RenderComponent<BaseDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));
        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Overlay_pointerdown_closes_the_dialog()
    {
        var cut = RenderComponent<BaseDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        cut.Find("[aria-hidden=true]").PointerDown();

        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));
    }

    [Fact]
    public void Escape_does_not_close_when_PreventDismiss()
    {
        var cut = RenderComponent<BaseDialog>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.PreventDismiss, true)
            .AddChildContent(Parts()));

        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("open", cut.Find("[role=dialog]").GetAttribute("data-state"));
    }

    [Fact]
    public void Overlay_pointerdown_does_not_close_when_PreventDismiss()
    {
        var cut = RenderComponent<BaseDialog>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.PreventDismiss, true)
            .AddChildContent(Parts()));

        cut.Find("[aria-hidden=true]").PointerDown();

        Assert.Equal("open", cut.Find("[role=dialog]").GetAttribute("data-state"));
    }

    [Fact]
    public void Non_modal_renders_no_overlay_and_no_aria_modal()
    {
        var cut = RenderComponent<BaseDialog>(p => p
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
        var cut = RenderComponent<BaseDialog>(p => p
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
        var cut = RenderComponent<BaseDialog>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Parts()));

        Assert.Single(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Modal_locks_scroll_exactly_once_and_unlocks_after_close()
    {
        var scrollLock = JSInterop.SetupModule("./_content/Blaizio.Base/dist/scrollLock.js");
        var cut = RenderComponent<BaseDialog>(p => p.AddChildContent(Parts()));

        cut.Find("button").Click();
        cut.Render();
        cut.Render(); // extra re-renders while open must not lock again
        Assert.Single(scrollLock.Invocations, i => i.Identifier == "lock");
        Assert.DoesNotContain(scrollLock.Invocations, i => i.Identifier == "unlock");

        cut.FindAll("button")[1].Click();
        cut.InvokeAsync(() => cut.FindComponent<BaseDialogContent>().Instance.OnCloseFinished());
        Assert.Single(scrollLock.Invocations, i => i.Identifier == "unlock");
    }

    [Fact]
    public void OnCloseFinished_invokes_OnExitComplete_after_the_content_unmounts()
    {
        var exited = 0;
        var cut = RenderComponent<BaseDialog>(p => p
            .Add(d => d.DefaultOpen, true)
            .AddChildContent(b =>
            {
                b.OpenComponent<BaseDialogContent>(0);
                b.AddComponentParameter(1, nameof(BaseDialogContent.OnExitComplete),
                    EventCallback.Factory.Create(this, () => exited++));
                b.AddComponentParameter(2, nameof(BaseDialogContent.ChildContent),
                    (RenderFragment)(c => c.AddContent(0, "Body")));
                b.CloseComponent();
            }));

        // Begin closing (Escape); the exit "animation" is still playing, so nothing has fired yet.
        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));
        Assert.Equal(0, exited);

        // animationend -> the content unmounts and reports OnExitComplete exactly once.
        cut.InvokeAsync(() => cut.FindComponent<BaseDialogContent>().Instance.OnCloseFinished());
        Assert.Equal(1, exited);
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }
}
