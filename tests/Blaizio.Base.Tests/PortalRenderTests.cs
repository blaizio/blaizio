using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Parameter-plumbing tests for the body portal. The actual DOM move (ts/portal.ts /
/// ts/positioning.ts) is JS, verified in-browser; these guard the C# side: the <c>inline</c>
/// option reaching the positioning module, the dialog surfaces attaching ts/portal.js only when
/// not <c>Inline</c>, and the cascaded direction being stamped onto a portaled surface (which
/// leaves its direction provider's DOM ancestry) but not onto an inline one.
/// JSInterop is Loose so the module imports are no-ops.
/// </summary>
public class PortalRenderTests : TestContext
{
    public PortalRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment TooltipBody(bool inline) => builder =>
    {
        builder.OpenComponent<BaseTooltipTrigger>(0);
        builder.AddComponentParameter(1, nameof(BaseTooltipTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Trigger")));
        builder.CloseComponent();
        builder.OpenComponent<BaseTooltipContent>(2);
        builder.AddComponentParameter(3, nameof(BaseTooltipContent.Inline), inline);
        builder.AddComponentParameter(4, nameof(BaseTooltipContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Tip body")));
        builder.CloseComponent();
    };

    private IRenderedFragment RenderTooltipInDirection(Direction direction, bool inline)
    {
        return RenderComponent<BaseDirectionProvider>(p => p
            .Add(x => x.Direction, direction)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<BaseTooltip>(0);
                builder.AddComponentParameter(1, nameof(BaseTooltip.Open), (bool?)true);
                builder.AddComponentParameter(2, nameof(BaseTooltip.ChildContent), TooltipBody(inline));
                builder.CloseComponent();
            }));
    }

    [Fact]
    public void Portaled_content_stamps_the_cascaded_direction()
    {
        var cut = RenderTooltipInDirection(Direction.Rtl, inline: false);

        Assert.Equal("rtl", cut.Find("[role=tooltip]").GetAttribute("dir"));
    }

    [Fact]
    public void Inline_content_inherits_direction_from_its_dom_ancestry()
    {
        var cut = RenderTooltipInDirection(Direction.Rtl, inline: true);

        Assert.Null(cut.Find("[role=tooltip]").GetAttribute("dir"));
    }

    [Fact]
    public void No_direction_cascade_leaves_the_portaled_content_unstamped()
    {
        var cut = RenderComponent<BaseTooltip>(p => p
            .Add(x => x.Open, true)
            .AddChildContent(TooltipBody(inline: false)));

        // With no provider the direction lives on <html>, which a body-level node still inherits.
        Assert.Null(cut.Find("[role=tooltip]").GetAttribute("dir"));
    }

    private static RenderFragment DialogParts() => builder =>
    {
        builder.OpenComponent<BaseDialogOverlay>(0);
        builder.CloseComponent();
        builder.OpenComponent<BaseDialogContent>(1);
        builder.AddComponentParameter(2, nameof(BaseDialogContent.ChildContent),
            (RenderFragment)(c =>
            {
                c.OpenComponent<BaseDialogTitle>(0);
                c.AddComponentParameter(1, nameof(BaseDialogTitle.ChildContent),
                    (RenderFragment)(t => t.AddContent(0, "Title")));
                c.CloseComponent();
            }));
        builder.CloseComponent();
    };

    private static RenderFragment InlineDialogParts() => builder =>
    {
        builder.OpenComponent<BaseDialogOverlay>(0);
        builder.AddComponentParameter(1, nameof(BaseDialogOverlay.Inline), true);
        builder.CloseComponent();
        builder.OpenComponent<BaseDialogContent>(2);
        builder.AddComponentParameter(3, nameof(BaseDialogContent.Inline), true);
        builder.AddComponentParameter(4, nameof(BaseDialogContent.ChildContent),
            (RenderFragment)(c =>
            {
                c.OpenComponent<BaseDialogTitle>(0);
                c.AddComponentParameter(1, nameof(BaseDialogTitle.ChildContent),
                    (RenderFragment)(t => t.AddContent(0, "Title")));
                c.CloseComponent();
            }));
        builder.CloseComponent();
    };

    [Fact]
    public void Open_dialog_attaches_the_portal_module_for_content_and_overlay()
    {
        RenderComponent<BaseDialog>(p => p
            .Add(x => x.Open, true)
            .AddChildContent(DialogParts()));

        var portals = JSInterop.Invocations.Where(i => i.Identifier == "createPortal").ToList();
        Assert.Equal(2, portals.Count);
    }

    [Fact]
    public void Inline_dialog_never_touches_the_portal_module()
    {
        RenderComponent<BaseDialog>(p => p
            .Add(x => x.Open, true)
            .AddChildContent(InlineDialogParts()));

        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "createPortal");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tooltip_forwards_inline_to_the_positioning_module(bool inline)
    {
        RenderComponent<BaseTooltip>(p => p
            .Add(x => x.Open, true)
            .AddChildContent(TooltipBody(inline)));

        var create = JSInterop.Invocations.Single(i => i.Identifier == "createPositioning");
        var options = create.Arguments[2];
        Assert.NotNull(options);
        var forwarded = (bool)options!.GetType().GetProperty("inline")!.GetValue(options)!;
        Assert.Equal(inline, forwarded);
    }
}
