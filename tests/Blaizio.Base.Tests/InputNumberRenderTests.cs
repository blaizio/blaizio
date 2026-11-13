using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + interaction tests for the headless number field, with the emphasis on CONTROLLED mode:
/// the parent owns the value, so what the field displays and what the binding holds can only be kept
/// together by reconciling correctly - including when the parent transforms the value it is handed
/// (clamp / round / coalesce null) and when its echo arrives late, as it does over a Blazor Server
/// circuit. See <see cref="ControllableState{T}"/> for why a controlled set does not mutate state.
/// </summary>
public class InputNumberRenderTests : BunitContext
{
    public InputNumberRenderTests()
    {
        Services.AddBlaizio();
        // The input imports ts/inputNumber.js for its typing guard.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static RenderFragment Field() => builder =>
    {
        builder.OpenComponent<BaseInputNumberGroup>(0);
        builder.AddAttribute(1, nameof(BaseInputNumberGroup.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<BaseInputNumberStep>(0);
            inner.AddAttribute(1, nameof(BaseInputNumberStep.StepDirection), InputNumberStepDirection.Decrement);
            inner.CloseComponent();
            inner.OpenComponent<BaseInputNumberInput>(2);
            inner.CloseComponent();
            inner.OpenComponent<BaseInputNumberStep>(3);
            inner.AddAttribute(4, nameof(BaseInputNumberStep.StepDirection), InputNumberStepDirection.Increment);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private IRenderedComponent<BaseInputNumber<double?>> Controlled(
        double? value, EventCallback<double?> onChange, double? min = null, double? max = null) =>
        Render<BaseInputNumber<double?>>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, onChange)
            .Add(x => x.Min, min)
            .Add(x => x.Max, max)
            .Add(x => x.ChildContent, Field()));

    private static string Text(IRenderedComponent<BaseInputNumber<double?>> cut) =>
        cut.Find("input").GetAttribute("value") ?? "";

    [Fact]
    public void Shows_the_controlled_value()
    {
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));

        Assert.Equal("30", Text(cut));
    }

    [Fact]
    public async Task A_parent_push_while_focused_reaches_the_display()
    {
        // The defect this guards: the text used to be reconciled only while blurred, so a value the
        // parent pushed mid-edit (a reset button, a form load) never reached the box - the field
        // showed one number while the binding held another.
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        await cut.Find("input").FocusAsync();

        cut.Render(p => p.Add(x => x.Value, 7d));

        Assert.Equal("7", Text(cut));
    }

    [Fact]
    public async Task An_echo_of_our_own_commit_does_not_disturb_the_typed_text()
    {
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        await cut.Find("input").FocusAsync();

        await cut.Find("input").InputAsync("40");
        cut.Render(p => p.Add(x => x.Value, bound)); // the parent's render lands

        Assert.Equal(40d, bound);
        Assert.Equal("40", Text(cut));
    }

    [Fact]
    public async Task A_transformed_echo_does_not_disturb_the_typed_text()
    {
        // A @bind-Value:set may not store what it is handed. Clearing the field emits null, this parent
        // coalesces it to 1, and that 1 comes back as a parameter - it must NOT be mistaken for an
        // external push and refill the box the user just emptied.
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v ?? 1));
        await cut.Find("input").FocusAsync();

        await cut.Find("input").InputAsync("");
        cut.Render(p => p.Add(x => x.Value, bound));

        Assert.Equal(1d, bound);
        Assert.Equal("", Text(cut));
    }

    [Fact]
    public async Task A_late_echo_carrying_an_older_value_does_not_rewind_the_display()
    {
        // Blazor Server: a render batch is built before a later keystroke and delivered after it, so
        // the value arriving can be an EARLIER one of ours. Reconciling against it would show "4"
        // while the binding already holds 40 - the exact value/text split this test pins down.
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        await cut.Find("input").FocusAsync();

        await cut.Find("input").InputAsync("4");
        await cut.Find("input").InputAsync("40");
        cut.Render(p => p.Add(x => x.Value, 4d)); // the stale batch, delivered late

        Assert.Equal(40d, bound);
        Assert.Equal("40", Text(cut));
    }

    [Fact]
    public async Task A_partial_decimal_survives_an_echo_landing_mid_type()
    {
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        await cut.Find("input").FocusAsync();

        await cut.Find("input").InputAsync("40.");
        cut.Render(p => p.Add(x => x.Value, bound));

        Assert.Equal("40.", Text(cut));
    }

    [Fact]
    public async Task Discrete_presses_each_advance_by_one_step()
    {
        // A circuit serializes events, so each press is answered by a render before the next arrives:
        // reading the value per press is right here, and the presses must not collapse into one.
        // AWAIT each dispatch: the synchronous trigger only guarantees the handler STARTED, so a press
        // whose emit completes asynchronously could land after the parent answered - the field would
        // then be told the pre-press value and the next press would repeat a step ([31, 32, 32]).
        // Awaiting is what models the circuit's ordering; it is not a delay.
        double? bound = 30;
        var emitted = new List<double?>();
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => { bound = v; emitted.Add(v); }));
        var increment = cut.FindAll("button")[1];

        for (var i = 0; i < 3; i++)
        {
            await increment.PointerDownAsync();
            await increment.PointerUpAsync();
            cut.Render(p => p.Add(x => x.Value, bound)); // the parent answers
        }

        Assert.Equal([31d, 32d, 33d], emitted);
    }

    [Fact]
    public async Task A_hold_accumulates_instead_of_repeating_one_stale_step()
    {
        // The repeat loop ticks every 60ms, far faster than a Server render can answer, so it cannot
        // recompute from the controlled value: it used to read the same pre-press number every tick
        // and emit the same result, turning a whole hold into a single step. It carries its own
        // running total now, which is what makes the emissions climb.
        var emitted = new List<double?>();
        var cut = Controlled(30, EventCallback.Factory.Create<double?>(this, v => emitted.Add(v)));
        var increment = cut.FindAll("button")[1];

        await increment.PointerDownAsync();  // 300ms lead-in, then a tick every 60ms
        // Poll rather than sleep a fixed span: the cadence is real time, and a loaded test host can
        // stretch it well past any span worth hard-coding.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (emitted.Count < 3 && DateTime.UtcNow < deadline) await Task.Delay(30);
        await increment.PointerUpAsync();
        var held = emitted.ToArray();

        Assert.True(held.Length >= 3, $"the hold produced {held.Length} step(s)");
        Assert.Equal(held.OrderBy(v => v).Distinct(), held); // strictly climbing, never the same twice
    }

    [Fact]
    public async Task Stepping_stops_at_Max()
    {
        double? bound = 364;
        var emitted = new List<double?>();
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => { bound = v; emitted.Add(v); }),
            max: 365);
        var increment = cut.FindAll("button")[1];

        // Awaited for the same reason as the discrete-press test: the parent must not answer with a
        // value the first press has not finished producing.
        await increment.PointerDownAsync();
        await increment.PointerUpAsync();
        cut.Render(p => p.Add(x => x.Value, bound));
        await increment.PointerDownAsync();
        await increment.PointerUpAsync();

        Assert.Equal([365d], emitted); // the second press is already pinned - nothing to emit
    }

    [Fact]
    public async Task Blur_reformats_and_the_text_follows_the_value_again()
    {
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));

        await cut.Find("input").FocusAsync();
        await cut.Find("input").InputAsync("40");
        await cut.Find("input").BlurAsync();
        cut.Render(p => p.Add(x => x.Value, bound));

        Assert.Equal(40d, bound);
        Assert.Equal("40", Text(cut));
    }

    [Fact]
    public async Task Uncontrolled_keeps_its_own_value()
    {
        var cut = Render<BaseInputNumber<double?>>(p => p
            .Add(x => x.DefaultValue, 5d)
            .Add(x => x.ChildContent, Field()));

        await cut.FindAll("button")[1].PointerDownAsync();

        Assert.Equal("6", Text(cut));
    }

    [Fact]
    public async Task Leaving_a_read_only_field_commits_nothing()
    {
        double? bound = 150;
        var raised = 0;
        var cut = Render<BaseInputNumber<double?>>(p => p
            .Add(x => x.Value, bound)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<double?>(this, v => { bound = v; raised++; }))
            .Add(x => x.Max, 100d)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.ChildContent, Field()));

        await cut.Find("input").FocusAsync();
        await cut.Find("input").BlurAsync();

        // Out of range, but the field was only showing it: no clamp, no event.
        Assert.Equal(0, raised);
        Assert.Equal(150d, bound);
    }

    [Theory]
    [InlineData(2.5, null)]  // off the default step grid of 1: used to become 3 (or 2)
    [InlineData(150d, 100d)] // above Max: used to be clamped to 100
    public async Task Passing_through_a_field_without_typing_commits_nothing(double start, double? max)
    {
        double? bound = start;
        var raised = 0;
        var cut = Render<BaseInputNumber<double?>>(p => p
            .Add(x => x.Value, bound)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<double?>(this, v => { bound = v; raised++; }))
            .Add(x => x.Max, max)
            .Add(x => x.ChildContent, Field()));

        await cut.Find("input").FocusAsync();
        await cut.Find("input").BlurAsync();

        Assert.Equal(0, raised);
        Assert.Equal(start, bound);
    }

    [Fact]
    public async Task Leaving_a_field_after_typing_still_snaps_what_was_typed()
    {
        double? bound = 2.5;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));

        await cut.Find("input").FocusAsync();
        await cut.Find("input").InputAsync("4.4");
        Assert.Equal(4.4, bound); // live: clamped only
        await cut.Find("input").BlurAsync();

        Assert.Equal(4d, bound);

        // And the next visit, with nothing typed, leaves it alone again.
        cut.Render(p => p.Add(x => x.Value, 6.5));
        bound = 6.5;
        await cut.Find("input").FocusAsync();
        await cut.Find("input").BlurAsync();
        Assert.Equal(6.5, bound);
    }

    [Fact]
    public async Task The_step_grid_starts_at_Min_and_keeps_its_decimals()
    {
        double? bound = 0.5;
        var cut = Render<BaseInputNumber<double?>>(p => p
            .Add(x => x.Value, bound)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<double?>(this, v => bound = v))
            .Add(x => x.Min, 0.5d)
            .Add(x => x.Step, 1d)
            .Add(x => x.ChildContent, Field()));

        await cut.Find("input").FocusAsync();
        await cut.Find("input").InputAsync("2.4");
        await cut.Find("input").BlurAsync();

        // Grid points are 0.5, 1.5, 2.5: rounding to the step's zero decimals would give 2.
        Assert.Equal(2.5d, bound);
    }

    [Fact]
    public async Task A_release_during_a_slow_change_handler_still_ends_the_hold()
    {
        // The repeat's cancellation source used to be created after the first step's ValueChanged
        // came back. A release that arrived before that found nothing to cancel, and the repeat
        // then started and never stopped.
        var gate = new TaskCompletionSource();
        var emitted = new List<double?>();
        var cut = Controlled(30, EventCallback.Factory.Create<double?>(this, async v =>
        {
            emitted.Add(v);
            await gate.Task;
        }));
        var increment = cut.FindAll("button")[1];

        var press = increment.PointerDownAsync();
        await increment.PointerUpAsync();
        gate.SetResult();
        await press;

        await Task.Delay(600); // past the 300ms lead-in and several 60ms ticks
        Assert.Equal([31d], emitted);
    }
}
