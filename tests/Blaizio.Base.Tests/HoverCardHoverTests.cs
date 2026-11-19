using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The hover and focus paths of the hover card, with the delays set so nothing has to be waited
/// out: zero for a step that should happen, far longer than the test for one that should be
/// cancelled first (the handler then returns as soon as it is cancelled). Cards are either left to
/// themselves (no Open, no OpenChanged) or bound both ways, as <c>@bind-Open</c> does.
/// </summary>
public class HoverCardHoverTests : BunitContext
{
    private const int Never = 60_000;
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

    public HoverCardHoverTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    // A page around the cards: rendering it again runs its content again, so a test can take a
    // card off the page, and a bound card gets its new value the way a parent hands it down.
    private sealed class Page : ComponentBase
    {
        [Parameter] public RenderFragment<Page>? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content?.Invoke(this));
    }

    // The parent's side of @bind-Open. FeedBack off is a parent that listens and does not follow.
    private sealed class Binding(bool open, bool feedBack = true)
    {
        public bool Open { get; private set; } = open;
        public List<bool> Changes { get; } = [];

        public void Set(bool value)
        {
            Changes.Add(value);
            if (feedBack) Open = value;
        }
    }

    private static void AddCard(
        RenderTreeBuilder builder,
        Page page,
        string name,
        int openDelay = 0,
        int closeDelay = 0,
        bool defaultOpen = false,
        Binding? binding = null)
    {
        builder.OpenComponent<BaseHoverCard>(0);
        builder.AddComponentParameter(1, nameof(BaseHoverCard.OpenDelay), (int?)openDelay);
        builder.AddComponentParameter(2, nameof(BaseHoverCard.CloseDelay), (int?)closeDelay);
        if (binding is null)
        {
            builder.AddComponentParameter(3, nameof(BaseHoverCard.DefaultOpen), defaultOpen);
        }
        else
        {
            builder.AddComponentParameter(4, nameof(BaseHoverCard.Open), (bool?)binding.Open);
            builder.AddComponentParameter(5, nameof(BaseHoverCard.OpenChanged),
                EventCallback.Factory.Create<bool>(page, binding.Set));
        }
        builder.AddComponentParameter(6, nameof(BaseHoverCard.ChildContent), (RenderFragment)(card =>
        {
            card.OpenComponent<BaseHoverCardTrigger>(0);
            card.AddComponentParameter(1, "data-test-trigger", name);
            card.AddComponentParameter(2, nameof(BaseHoverCardTrigger.ChildContent),
                (RenderFragment)(t => t.AddContent(0, name)));
            card.CloseComponent();
            card.OpenComponent<BaseHoverCardContent>(3);
            card.AddComponentParameter(4, "data-test-card", name);
            card.AddComponentParameter(5, nameof(BaseHoverCardContent.Inline), true);
            card.AddComponentParameter(6, nameof(BaseHoverCardContent.ChildContent),
                (RenderFragment)(c => c.AddContent(0, $"About {name}")));
            card.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private IRenderedComponent<Page> RenderPage(Action<RenderTreeBuilder, Page> content) =>
        Render<Page>(p => p.Add(x => x.Content, page => builder => content(builder, page)));

    private IRenderedComponent<Page> RenderCard(
        int openDelay = 0, int closeDelay = 0, bool defaultOpen = false, Binding? binding = null) =>
        RenderPage((builder, page) => AddCard(builder, page, "ada", openDelay, closeDelay, defaultOpen, binding));

    private static IElement Trigger(IRenderedComponent<Page> cut, string name = "ada") =>
        cut.Find($"[data-test-trigger={name}]");

    private static IElement Card(IRenderedComponent<Page> cut, string name = "ada") =>
        cut.Find($"[data-test-card={name}]");

    // "open", "closed" (animating out) or null (not in the document).
    private static string? CardState(IRenderedComponent<Page> cut, string name = "ada") =>
        cut.FindAll($"[data-test-card={name}]").FirstOrDefault()?.GetAttribute("data-state");

    [Fact]
    public async Task Resting_the_pointer_on_the_trigger_opens_the_card()
    {
        var cut = RenderCard();

        await Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());

        Assert.Equal("open", CardState(cut));
        Assert.Equal("open", Trigger(cut).GetAttribute("data-state"));
        Assert.Equal("About ada", Card(cut).TextContent);
    }

    [Fact]
    public async Task A_bound_card_reports_each_change_and_follows_its_binding()
    {
        var binding = new Binding(open: false);
        var cut = RenderCard(binding: binding);

        await Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.Equal("open", CardState(cut));

        await Trigger(cut).TriggerEventAsync("onpointerleave", new PointerEventArgs());
        Assert.Equal("closed", CardState(cut));
        Assert.Equal([true, false], binding.Changes);
    }

    [Fact]
    public async Task A_parent_that_does_not_follow_keeps_the_card_as_its_parameter_says()
    {
        var binding = new Binding(open: false, feedBack: false);
        var cut = RenderCard(binding: binding);

        await Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());

        Assert.Equal([true], binding.Changes);
        Assert.Null(CardState(cut));
    }

    [Fact]
    public async Task A_pointer_that_leaves_before_the_open_delay_never_opens_the_card()
    {
        var binding = new Binding(open: false);
        var cut = RenderCard(openDelay: Never, binding: binding);

        var entering = Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.False(entering.IsCompleted); // still waiting out the delay
        Assert.Null(CardState(cut));

        await Trigger(cut).TriggerEventAsync("onpointerleave", new PointerEventArgs());
        await entering.WaitAsync(Soon);

        Assert.Null(CardState(cut));
        Assert.Empty(binding.Changes);
    }

    [Fact]
    public async Task Leaving_the_trigger_closes_the_card_after_the_close_delay()
    {
        var cut = RenderCard(defaultOpen: true);

        await Trigger(cut).TriggerEventAsync("onpointerleave", new PointerEventArgs());

        Assert.Equal("closed", CardState(cut));
        Assert.Equal("closed", Trigger(cut).GetAttribute("data-state"));
    }

    [Fact]
    public async Task Crossing_from_the_trigger_onto_the_card_keeps_it_open()
    {
        var binding = new Binding(open: true);
        var cut = RenderCard(closeDelay: Never, binding: binding);

        var leaving = Trigger(cut).TriggerEventAsync("onpointerleave", new PointerEventArgs());
        Assert.False(leaving.IsCompleted);
        await Card(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());
        await leaving.WaitAsync(Soon);

        Assert.Equal("open", CardState(cut));
        Assert.Empty(binding.Changes);
    }

    [Fact]
    public async Task Leaving_the_card_itself_closes_it()
    {
        var cut = RenderCard(defaultOpen: true);

        await Card(cut).TriggerEventAsync("onpointerleave", new PointerEventArgs());

        Assert.Equal("closed", CardState(cut));
    }

    [Fact]
    public async Task Keyboard_focus_opens_the_card_and_moving_focus_away_closes_it()
    {
        var cut = RenderCard();

        await Trigger(cut).TriggerEventAsync("onfocusin", new FocusEventArgs());
        Assert.Equal("open", CardState(cut));

        await Trigger(cut).TriggerEventAsync("onfocusout", new FocusEventArgs());
        Assert.Equal("closed", CardState(cut));
    }

    [Fact]
    public async Task Focus_moving_from_the_trigger_into_the_card_keeps_it_open()
    {
        var cut = RenderCard(closeDelay: Never, defaultOpen: true);

        var blurring = Trigger(cut).TriggerEventAsync("onfocusout", new FocusEventArgs());
        await Card(cut).TriggerEventAsync("onfocusin", new FocusEventArgs());
        await blurring.WaitAsync(Soon);
        Assert.Equal("open", CardState(cut));

        // Focus leaving the card for somewhere else starts the close as a pointer leave does.
        var leaving = Card(cut).TriggerEventAsync("onfocusout", new FocusEventArgs());
        Assert.False(leaving.IsCompleted);
        Assert.Equal("open", CardState(cut));
    }

    [Fact]
    public async Task A_press_on_the_trigger_does_not_count_as_leaving_it()
    {
        var cut = RenderCard(defaultOpen: true);

        // A pressed button can shift under the pointer, which the browser reports as a leave.
        await Trigger(cut).TriggerEventAsync("onpointerdown", new PointerEventArgs());
        await Trigger(cut).TriggerEventAsync("onpointerleave", new PointerEventArgs());
        Assert.Equal("open", CardState(cut));

        // The next leave is a real one.
        await Trigger(cut).TriggerEventAsync("onpointerleave", new PointerEventArgs());
        Assert.Equal("closed", CardState(cut));
    }

    [Fact]
    public async Task An_enter_reported_while_the_trigger_is_pressed_does_not_open_the_card()
    {
        var cut = RenderCard();

        await Trigger(cut).TriggerEventAsync("onpointerdown", new PointerEventArgs());
        await Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.Null(CardState(cut));

        await Trigger(cut).TriggerEventAsync("onpointerup", new PointerEventArgs());
        await Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.Equal("open", CardState(cut));
    }

    [Fact]
    public async Task Opening_a_second_card_closes_the_first_at_once()
    {
        var cut = RenderPage((builder, page) =>
        {
            builder.OpenRegion(0);
            AddCard(builder, page, "ada", closeDelay: Never);
            builder.CloseRegion();
            builder.OpenRegion(1);
            AddCard(builder, page, "grace");
            builder.CloseRegion();
        });
        await Trigger(cut, "ada").TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.Equal("open", CardState(cut, "ada"));

        // The pointer moves on: the first card would linger for its whole close delay and overlap
        // the second.
        var leaving = Trigger(cut, "ada").TriggerEventAsync("onpointerleave", new PointerEventArgs());
        await Trigger(cut, "grace").TriggerEventAsync("onpointerenter", new PointerEventArgs());
        await leaving.WaitAsync(Soon);

        Assert.Equal("open", CardState(cut, "grace"));
        Assert.Equal("closed", CardState(cut, "ada"));
    }

    [Fact]
    public async Task A_bound_card_is_left_alone_when_another_card_opens()
    {
        var ada = new Binding(open: true);
        var cut = RenderPage((builder, page) =>
        {
            builder.OpenRegion(0);
            AddCard(builder, page, "ada", binding: ada);
            builder.CloseRegion();
            builder.OpenRegion(1);
            AddCard(builder, page, "grace");
            builder.CloseRegion();
        });

        await Trigger(cut, "grace").TriggerEventAsync("onpointerenter", new PointerEventArgs());

        // Its state is the page's to own: the page did not ask for it to close.
        Assert.Equal("open", CardState(cut, "grace"));
        Assert.Equal("open", CardState(cut, "ada"));
        Assert.Empty(ada.Changes);
    }

    [Fact]
    public async Task A_card_that_leaves_the_page_while_open_gives_up_its_place()
    {
        var showAda = true;
        var cut = RenderPage((builder, page) =>
        {
            if (showAda) AddCard(builder, page, "ada");
        });
        var coordinator = Services.GetRequiredService<HoverCardCoordinator>();
        await Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.NotNull(CurrentCard(coordinator));

        showAda = false;
        cut.Render();

        // Still registered, the next card to open would call into a component that is gone.
        Assert.Null(CurrentCard(coordinator));
    }

    // The card the coordinator would dismiss next (its private registration).
    private static object? CurrentCard(HoverCardCoordinator coordinator) =>
        typeof(HoverCardCoordinator)
            .GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(coordinator);

    [Fact]
    public async Task A_card_removed_while_its_open_delay_runs_is_cancelled_quietly()
    {
        var show = true;
        var cut = RenderPage((builder, page) =>
        {
            if (show) AddCard(builder, page, "ada", openDelay: Never);
        });
        var entering = Trigger(cut).TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.False(entering.IsCompleted);

        show = false;
        cut.Render();

        // The wait ends with the card, not a minute later.
        await entering.WaitAsync(Soon);
        Assert.Empty(cut.FindAll("[data-test-trigger]"));
    }

    [Fact]
    public void The_card_starts_on_the_side_that_was_asked_for_and_follows_a_reported_flip()
    {
        var cut = Render<BaseHoverCard>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BaseHoverCardContent>(c => c
                .Add(x => x.Side, Side.Top)
                .Add(x => x.Align, Align.Start)
                .Add(x => x.Inline, true)
                .AddChildContent("Card body")));
        var content = cut.FindComponent<BaseHoverCardContent>();
        Assert.Equal("top", content.Find("[data-side]").GetAttribute("data-side"));
        Assert.Equal("start", content.Find("[data-side]").GetAttribute("data-align"));

        // No room above: the positioning script reports where the card really went.
        cut.InvokeAsync(() => content.Instance.OnPlaced("bottom", "start"));

        Assert.Equal("bottom", content.Find("[data-side]").GetAttribute("data-side"));
        var renders = content.RenderCount;
        cut.InvokeAsync(() => content.Instance.OnPlaced("bottom", "start"));
        Assert.Equal(renders, content.RenderCount);
    }

    [Fact]
    public void The_arrow_is_decorative_and_carries_the_hook_the_positioning_script_places()
    {
        var cut = Render<BaseHoverCardArrow>(p => p
            .Add(x => x.Class, "fill-popover")
            .AddChildContent("<svg></svg>"));

        var arrow = cut.Find("span");
        Assert.True(arrow.HasAttribute("data-bz-arrow"));
        Assert.Equal("true", arrow.GetAttribute("aria-hidden"));
        Assert.Equal("fill-popover", arrow.GetAttribute("class"));
        Assert.NotNull(arrow.QuerySelector("svg"));
    }
}

/// <summary>The hover card's delays when only the app-wide defaults set them.</summary>
public class HoverCardDefaultsTests : BunitContext
{
    public HoverCardDefaultsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio(options =>
        {
            options.HoverCard.OpenDelay = 0;
            options.HoverCard.CloseDelay = 60_000;
        });
    }

    [Fact]
    public async Task Cards_without_their_own_delays_use_the_app_wide_ones()
    {
        var cut = Render<BaseHoverCard>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseHoverCardTrigger>(0);
            builder.CloseComponent();
            builder.OpenComponent<BaseHoverCardContent>(1);
            builder.AddComponentParameter(2, nameof(BaseHoverCardContent.Inline), true);
            builder.CloseComponent();
        }));

        await cut.Find("[data-bz-hover-card-anchor]").TriggerEventAsync("onpointerenter", new PointerEventArgs());
        Assert.Equal("open", cut.Find("[data-side]").GetAttribute("data-state"));

        // The long close delay holds: the leave is still waiting, the card still open.
        var leaving = cut.Find("[data-bz-hover-card-anchor]").TriggerEventAsync("onpointerleave", new PointerEventArgs());
        Assert.False(leaving.IsCompleted);
        Assert.Equal("open", cut.Find("[data-side]").GetAttribute("data-state"));
    }
}

/// <summary>The registry that keeps one hover card open at a time, on its own.</summary>
public class HoverCardCoordinatorTests
{
    private sealed class FakeCard : IHoverCardController
    {
        public int Dismissals { get; private set; }

        public Task DismissFromCoordinatorAsync()
        {
            Dismissals++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_card_that_opens_dismisses_the_one_that_was_open()
    {
        var coordinator = new HoverCardCoordinator();
        var first = new FakeCard();
        var second = new FakeCard();

        await coordinator.OpenedAsync(first);
        await coordinator.OpenedAsync(second);

        Assert.Equal(1, first.Dismissals);
        Assert.Equal(0, second.Dismissals);
    }

    [Fact]
    public async Task A_card_reporting_itself_open_again_is_not_dismissed()
    {
        var coordinator = new HoverCardCoordinator();
        var card = new FakeCard();

        await coordinator.OpenedAsync(card);
        await coordinator.OpenedAsync(card);

        Assert.Equal(0, card.Dismissals);
    }

    [Fact]
    public async Task A_card_that_closed_is_not_dismissed_by_the_next_one()
    {
        var coordinator = new HoverCardCoordinator();
        var first = new FakeCard();
        var second = new FakeCard();
        await coordinator.OpenedAsync(first);

        coordinator.Closed(first);
        await coordinator.OpenedAsync(second);

        Assert.Equal(0, first.Dismissals);
    }

    [Fact]
    public async Task A_late_close_from_an_earlier_card_does_not_free_the_current_ones_place()
    {
        var coordinator = new HoverCardCoordinator();
        var first = new FakeCard();
        var second = new FakeCard();
        var third = new FakeCard();
        await coordinator.OpenedAsync(first);
        await coordinator.OpenedAsync(second);

        coordinator.Closed(first); // the dismissed card finishing its close
        await coordinator.OpenedAsync(third);

        Assert.Equal(1, second.Dismissals);
    }
}
