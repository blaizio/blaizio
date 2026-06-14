using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Tests for the two knobs the Ui AlertDialog adds on top of the shared Dialog machinery:
/// BzDialogContent's <c>Role</c> (alertdialog) and BzDialogOverlay's <c>DismissOnClick</c>
/// (off, so the backdrop is inert). Everything else - presence, focus trap, scroll lock, Escape -
/// is the same code the Dialog tests already cover. JSInterop is Loose so module imports no-op.
/// </summary>
public class AlertDialogRenderTests : TestContext
{
    public AlertDialogRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // An alert dialog: overlay with dismissal off, content with role=alertdialog.
    private static RenderFragment Parts() => builder =>
    {
        builder.OpenComponent<BzDialogTrigger>(0);
        builder.AddComponentParameter(1, nameof(BzDialogTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Open")));
        builder.CloseComponent();

        builder.OpenComponent<BzDialogOverlay>(2);
        builder.AddComponentParameter(3, nameof(BzDialogOverlay.DismissOnClick), false);
        builder.CloseComponent();

        builder.OpenComponent<BzDialogContent>(4);
        builder.AddComponentParameter(5, nameof(BzDialogContent.Role), "alertdialog");
        builder.AddComponentParameter(6, nameof(BzDialogContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Body")));
        builder.CloseComponent();
    };

    [Fact]
    public void Content_renders_the_alertdialog_role()
    {
        var cut = RenderComponent<BzDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        var content = cut.Find("[role=alertdialog]");
        Assert.Equal("true", content.GetAttribute("aria-modal"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Overlay_has_no_dismiss_handler_when_dismissal_is_off()
    {
        var cut = RenderComponent<BzDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        // The inert backdrop wires no onpointerdown at all - so triggering one finds no handler.
        var overlay = cut.Find("[aria-hidden=true]");
        Assert.Throws<MissingEventHandlerException>(() => overlay.PointerDown());
        Assert.Equal("open", cut.Find("[role=alertdialog]").GetAttribute("data-state"));
    }

    [Fact]
    public void Escape_still_closes_an_alert_dialog()
    {
        var cut = RenderComponent<BzDialog>(p => p.AddChildContent(Parts()));
        cut.Find("button").Click();

        cut.Find("[role=alertdialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("closed", cut.Find("[role=alertdialog]").GetAttribute("data-state"));
    }
}
