using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The pointer paths of the tooltip and the provider that groups tooltips, with the delays set so
/// nothing has to be waited out: zero for a step that should happen, far longer than the test for
/// one that should not (the handler is then still waiting when the test looks). The tooltips are
/// left to themselves (no Open, no OpenChanged).
/// </summary>
public class TooltipHoverTests : BunitContext
{
    private const int Never = 60_000;
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

    public TooltipHoverTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // A page around the tooltips: rendering it again runs its content again, so a test can take
    // a tooltip off the page.
    private sealed class Page : ComponentBase
    {
        [Parameter] public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
    }

    private static void AddTooltip(RenderTreeBuilder builder, string name, int? delay = null, bool disabled = false)
    {
        builder.OpenComponent<BaseTooltip>(0);
        builder.AddComponentParameter(1, nameof(BaseTooltip.DelayDuration), delay);
        builder.AddComponentParameter(2, nameof(BaseTooltip.Disabled), disabled);
        builder.AddComponentParameter(3, nameof(BaseTooltip.ChildContent), (RenderFragment)(tooltip =>
        {
            tooltip.OpenComponent<BaseTooltipTrigger>(0);
            tooltip.AddComponentParameter(1, "data-test-trigger", name);
            tooltip.AddComponentParameter(2, nameof(BaseTooltipTrigger.ChildContent),
                (RenderFragment)(t => t.AddContent(0, name)));
            tooltip.CloseComponent();
            tooltip.OpenComponent<BaseTooltipContent>(3);
            tooltip.AddComponentParameter(4, "data-test-tip", name);
            tooltip.AddComponentParameter(5, nameof(BaseTooltipContent.Inline), true);
            tooltip.AddComponentParameter(6, nameof(BaseTooltipContent.ChildContent),
                (RenderFragment)(c => c.AddContent(0, $"Tip for {name}")));
            tooltip.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private IRenderedComponent<Page> RenderTooltip(int? delay = 0, bool disabled = false) =>
        Render<Page>(p => p.AddChildContent(builder => AddTooltip(builder, "save", delay, disabled)));

    // Two tooltips under one provider; the group's delay never ends within a test.
    private IRenderedComponent<Page> RenderGroup(
        int? firstDelay, int? secondDelay, int skipDelay, Func<bool>? showFirst = null, int groupDelay = Never) =>
        Render<Page>(p => p.AddChildContent(page =>
        {
            page.OpenComponent<BaseTooltipProvider>(0);
            page.AddComponentParameter(1, nameof(BaseTooltipProvider.DelayDuration), (int?)groupDelay);
            page.AddComponentParameter(2, nameof(BaseTooltipProvider.SkipDelayDuration), (int?)skipDelay);
            page.AddComponentParameter(3, nameof(BaseTooltipProvider.ChildContent), (RenderFragment)(group =>
            {
                if (showFirst?.Invoke() ?? true)
                {
                    group.OpenRegion(0);
                    AddTooltip(group, "save", firstDelay);
                    group.CloseRegion();
                }
                group.OpenRegion(1);
                AddTooltip(group, "print", secondDelay);
                group.CloseRegion();
            }));
            page.CloseComponent();
        }));

    private static IElement Trigger(IRenderedComponent<Page> cut, string name = "save") =>
        cut.Find($"[data-test-trigger={name}]");

    // "open", "closed" (animating out) or null (not in the document).
    private static string? TipState(IRenderedComponent<Page> cut, string name = "save") =>
        cut.FindAll($"[data-test-tip={name}]").FirstOrDefault()?.GetAttribute("data-state");

    private static Task Enter(IRenderedComponent<Page> cut, string name = "save") =>
        Trigger(cut, name).TriggerEventAsync("onpointerenter", new PointerEventArgs());

    private static Task Leave(IRenderedComponent<Page> cut, string name = "save") =>
        Trigger(cut, name).TriggerEventAsync("onpointerleave", new PointerEventArgs());

    [Fact]
    public async Task Resting_the_pointer_on_the_trigger_opens_the_tooltip_and_describes_the_trigger()
    {
        var cut = RenderTooltip();

        await Enter(cut);

        Assert.Equal("open", TipState(cut));
        var tip = cut.Find("[role=tooltip]");
        Assert.Equal("Tip for save", tip.TextContent);
        Assert.Equal(tip.Id, Trigger(cut).GetAttribute("aria-describedby"));
        Assert.Equal("open", Trigger(cut).GetAttribute("data-state"));
    }

    [Fact]
    public async Task A_pointer_that_leaves_before_the_delay_never_opens_the_tooltip()
    {
        var cut = RenderTooltip(delay: Never);

        var entering = Enter(cut);
        Assert.False(entering.IsCompleted); // still waiting out the delay
        await Leave(cut);
        await entering.WaitAsync(Soon);

        Assert.Null(TipState(cut));
    }

    [Fact]
    public async Task Leaving_the_trigger_closes_the_tooltip_at_once()
    {
        var cut = RenderTooltip();
        await Enter(cut);

        await Leave(cut);

        Assert.Equal("closed", TipState(cut));
        Assert.False(Trigger(cut).HasAttribute("aria-describedby"));
    }

    [Fact]
    public async Task Pressing_the_trigger_dismisses_the_tooltip_and_the_click_does_not_bring_it_back()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/tooltip.js");
        module.Setup<bool>("hasVisibleFocus", _ => true).SetResult(true);
        var cut = RenderTooltip();
        await Enter(cut);
        Assert.Equal("open", TipState(cut));

        await Trigger(cut).TriggerEventAsync("onpointerdown", new PointerEventArgs());
        Assert.Equal("closed", TipState(cut));

        // What a click does next: focus lands on the button, and a pressed button can shift under
        // the pointer, which the browser reports as a leave and an enter.
        await Trigger(cut).TriggerEventAsync("onfocusin", new FocusEventArgs());
        await Enter(cut);
        Assert.Equal("closed", TipState(cut));

        // After a real leave, the next hover opens it again.
        await Leave(cut);
        await Leave(cut);
        await Enter(cut);
        Assert.Equal("open", TipState(cut));
    }

    [Fact]
    public async Task A_press_during_the_delay_cancels_the_pending_open()
    {
        var cut = RenderTooltip(delay: Never);
        var entering = Enter(cut);

        await Trigger(cut).TriggerEventAsync("onpointerdown", new PointerEventArgs());
        await entering.WaitAsync(Soon);

        Assert.Null(TipState(cut));
    }

    [Fact]
    public async Task Escape_closes_the_tooltip_and_other_keys_leave_it()
    {
        var cut = RenderTooltip();
        await Enter(cut);

        await Trigger(cut).TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "a" });
        Assert.Equal("open", TipState(cut));

        await Trigger(cut).TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("closed", TipState(cut));
    }

    [Fact]
    public async Task A_disabled_tooltip_does_not_open_on_hover()
    {
        var cut = RenderTooltip(disabled: true);

        await Enter(cut);

        Assert.Null(TipState(cut));
    }

    [Fact]
    public async Task Tooltips_in_a_group_take_its_delay_unless_they_set_their_own()
    {
        var cut = RenderGroup(firstDelay: null, secondDelay: Never, skipDelay: 0, groupDelay: 0);

        var enteringPrint = Enter(cut, "print");
        Assert.False(enteringPrint.IsCompleted); // its own long delay wins over the group's zero
        await Leave(cut, "print");
        await enteringPrint.WaitAsync(Soon);

        await Enter(cut, "save");
        Assert.Equal("open", TipState(cut, "save"));
    }

    [Fact]
    public async Task While_one_tooltip_of_a_group_is_open_the_next_opens_without_its_delay()
    {
        var cut = RenderGroup(firstDelay: 0, secondDelay: null, skipDelay: 0);
        await Enter(cut, "save");
        Assert.Equal("open", TipState(cut, "save"));

        // The group delay never ends within the test: only the skip can open this one.
        await Enter(cut, "print").WaitAsync(Soon);

        Assert.Equal("open", TipState(cut, "print"));
    }

    [Fact]
    public async Task Just_after_the_last_tooltip_of_a_group_closed_the_next_still_opens_without_its_delay()
    {
        var cut = RenderGroup(firstDelay: 0, secondDelay: null, skipDelay: Never);
        await Enter(cut, "save");
        await Leave(cut, "save");
        Assert.Equal("closed", TipState(cut, "save"));

        await Enter(cut, "print").WaitAsync(Soon);

        Assert.Equal("open", TipState(cut, "print"));
    }

    [Fact]
    public async Task Once_the_skip_window_is_over_the_next_tooltip_waits_again()
    {
        var cut = RenderGroup(firstDelay: 0, secondDelay: null, skipDelay: 0);
        await Enter(cut, "save");
        await Leave(cut, "save");

        var entering = Enter(cut, "print");

        Assert.False(entering.IsCompleted);
        Assert.Null(TipState(cut, "print"));
    }

    [Fact]
    public async Task A_tooltip_removed_while_open_no_longer_counts_as_open_in_its_group()
    {
        var showFirst = true;
        var cut = RenderGroup(firstDelay: 0, secondDelay: null, skipDelay: 0, showFirst: () => showFirst);
        await Enter(cut, "save");
        Assert.Equal("open", TipState(cut, "save"));

        // Its row was deleted: no leave will ever close it.
        showFirst = false;
        cut.Render();
        var entering = Enter(cut, "print");

        // Still counted, it would open every later tooltip of the group with no delay.
        Assert.False(entering.IsCompleted);
        Assert.Null(TipState(cut, "print"));
    }

    [Fact]
    public async Task A_tooltip_removed_during_its_delay_is_cancelled_quietly()
    {
        var show = true;
        var cut = Render<Page>(p => p.AddChildContent(builder =>
        {
            if (show) AddTooltip(builder, "save", Never);
        }));
        var entering = Enter(cut);
        Assert.False(entering.IsCompleted);

        show = false;
        cut.Render();

        await entering.WaitAsync(Soon);
        Assert.Empty(cut.FindAll("[data-test-trigger]"));
    }

    [Fact]
    public void The_tooltip_starts_on_the_side_that_was_asked_for_and_follows_a_reported_flip()
    {
        var cut = Render<BaseTooltip>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BaseTooltipContent>(c => c
                .Add(x => x.Side, Side.Right)
                .Add(x => x.Align, Align.End)
                .Add(x => x.Inline, true)
                .AddChildContent("Tip body")));
        var content = cut.FindComponent<BaseTooltipContent>();
        Assert.Equal("right", content.Find("[role=tooltip]").GetAttribute("data-side"));
        Assert.Equal("end", content.Find("[role=tooltip]").GetAttribute("data-align"));

        // No room on the right: the positioning script reports where the tooltip really went.
        cut.InvokeAsync(() => content.Instance.OnPlaced("left", "end"));

        Assert.Equal("left", content.Find("[role=tooltip]").GetAttribute("data-side"));
        var renders = content.RenderCount;
        cut.InvokeAsync(() => content.Instance.OnPlaced("left", "end"));
        Assert.Equal(renders, content.RenderCount);
    }

    [Fact]
    public void A_close_report_for_a_tooltip_that_is_not_closing_changes_nothing()
    {
        var exits = 0;
        var cut = Render<BaseTooltip>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BaseTooltipContent>(c => c
                .Add(x => x.Inline, true)
                .Add(x => x.OnExitComplete, () => exits++)
                .AddChildContent("Tip body")));
        var content = cut.FindComponent<BaseTooltipContent>();

        // A late animation end from an earlier close, arriving after the tooltip opened again.
        cut.InvokeAsync(() => content.Instance.OnCloseFinished());
        Assert.Single(cut.FindAll("[role=tooltip]"));
        Assert.Equal(0, exits);

        cut.Render(p => p.Add(x => x.Open, false));
        cut.InvokeAsync(() => content.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=tooltip]"));
        Assert.Equal(1, exits);
    }

    [Fact]
    public void Closing_releases_the_positioning_and_presence_the_open_created()
    {
        var positioningModule = JSInterop.SetupModule("./_content/blaizio.base/dist/positioning.js");
        positioningModule.Mode = JSRuntimeMode.Loose;
        var positioning = positioningModule.SetupModule(i => i.Identifier == "createPositioning");
        positioning.Mode = JSRuntimeMode.Loose;
        var presenceModule = JSInterop.SetupModule("./_content/blaizio.base/dist/presence.js");
        presenceModule.Mode = JSRuntimeMode.Loose;
        var presence = presenceModule.SetupModule(i => i.Identifier == "createPresence");
        presence.Mode = JSRuntimeMode.Loose;
        var cut = Render<BaseTooltip>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BaseTooltipContent>(c => c.AddChildContent("Tip body")));
        Assert.Single(positioningModule.Invocations, i => i.Identifier == "createPositioning");
        Assert.Single(presenceModule.Invocations, i => i.Identifier == "createPresence");

        cut.Render(p => p.Add(x => x.Open, false));
        // The exit animation is the script's: it is told the tooltip is closing, and nothing is
        // released until it reports back.
        Assert.Single(presence.Invocations, i => i.Identifier == "onClosing");
        Assert.DoesNotContain(positioning.Invocations, i => i.Identifier == "dispose");

        cut.InvokeAsync(() => cut.FindComponent<BaseTooltipContent>().Instance.OnCloseFinished());

        Assert.Single(positioning.Invocations, i => i.Identifier == "dispose");
        Assert.Single(presence.Invocations, i => i.Identifier == "dispose");
    }

    [Fact]
    public async Task Leaving_the_page_with_a_tooltip_open_releases_what_it_created()
    {
        var positioningModule = JSInterop.SetupModule("./_content/blaizio.base/dist/positioning.js");
        positioningModule.Mode = JSRuntimeMode.Loose;
        var positioning = positioningModule.SetupModule(i => i.Identifier == "createPositioning");
        positioning.Mode = JSRuntimeMode.Loose;
        Render<BaseTooltip>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BaseTooltipContent>(c => c.AddChildContent("Tip body")));

        await DisposeComponentsAsync();

        Assert.Single(positioning.Invocations, i => i.Identifier == "dispose");
    }
}

/// <summary>The tooltip's hover delay when only the app-wide default sets it.</summary>
public class TooltipDefaultsTests : BunitContext
{
    public TooltipDefaultsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio(options => options.Tooltip.DelayDuration = 0);
    }

    [Fact]
    public async Task A_tooltip_with_no_delay_of_its_own_and_no_group_uses_the_app_wide_one()
    {
        var cut = Render<BaseTooltip>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseTooltipTrigger>(0);
            builder.CloseComponent();
            builder.OpenComponent<BaseTooltipContent>(1);
            builder.AddComponentParameter(2, nameof(BaseTooltipContent.Inline), true);
            builder.CloseComponent();
        }));

        await cut.Find("button").TriggerEventAsync("onpointerenter", new PointerEventArgs());

        Assert.Equal("open", cut.Find("[role=tooltip]").GetAttribute("data-state"));
    }

    [Fact]
    public async Task A_group_with_no_delay_of_its_own_uses_the_app_wide_one_too()
    {
        var cut = Render<BaseTooltipProvider>(p => p.AddChildContent<BaseTooltip>(t => t.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseTooltipTrigger>(0);
            builder.CloseComponent();
            builder.OpenComponent<BaseTooltipContent>(1);
            builder.AddComponentParameter(2, nameof(BaseTooltipContent.Inline), true);
            builder.CloseComponent();
        })));

        await cut.Find("button").TriggerEventAsync("onpointerenter", new PointerEventArgs());

        Assert.Equal("open", cut.Find("[role=tooltip]").GetAttribute("data-state"));
    }
}
