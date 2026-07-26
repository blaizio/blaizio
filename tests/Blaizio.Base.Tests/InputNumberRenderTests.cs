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
public class InputNumberRenderTests : TestContext
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
            inner.AddAttribute(1, nameof(BaseInputNumberStep.Direction), InputNumberStepDirection.Decrement);
            inner.CloseComponent();
            inner.OpenComponent<BaseInputNumberInput>(2);
            inner.CloseComponent();
            inner.OpenComponent<BaseInputNumberStep>(3);
            inner.AddAttribute(4, nameof(BaseInputNumberStep.Direction), InputNumberStepDirection.Increment);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private IRenderedComponent<BaseInputNumber<double?>> Controlled(
        double? value, EventCallback<double?> onChange, double? min = null, double? max = null) =>
        RenderComponent<BaseInputNumber<double?>>(p => p
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
    public void A_parent_push_while_focused_reaches_the_display()
    {
        // The defect this guards: the text used to be reconciled only while blurred, so a value the
        // parent pushed mid-edit (a reset button, a form load) never reached the box - the field
        // showed one number while the binding held another.
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        cut.Find("input").Focus();

        cut.SetParametersAndRender(p => p.Add(x => x.Value, 7d));

        Assert.Equal("7", Text(cut));
    }

    [Fact]
    public void An_echo_of_our_own_commit_does_not_disturb_the_typed_text()
    {
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        cut.Find("input").Focus();

        cut.Find("input").Input("40");
        cut.SetParametersAndRender(p => p.Add(x => x.Value, bound)); // the parent's render lands

        Assert.Equal(40d, bound);
        Assert.Equal("40", Text(cut));
    }

    [Fact]
    public void A_transformed_echo_does_not_disturb_the_typed_text()
    {
        // A @bind-Value:set may not store what it is handed. Clearing the field emits null, this parent
        // coalesces it to 1, and that 1 comes back as a parameter - it must NOT be mistaken for an
        // external push and refill the box the user just emptied.
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v ?? 1));
        cut.Find("input").Focus();

        cut.Find("input").Input("");
        cut.SetParametersAndRender(p => p.Add(x => x.Value, bound));

        Assert.Equal(1d, bound);
        Assert.Equal("", Text(cut));
    }

    [Fact]
    public void A_late_echo_carrying_an_older_value_does_not_rewind_the_display()
    {
        // Blazor Server: a render batch is built before a later keystroke and delivered after it, so
        // the value arriving can be an EARLIER one of ours. Reconciling against it would show "4"
        // while the binding already holds 40 - the exact value/text split this test pins down.
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        cut.Find("input").Focus();

        cut.Find("input").Input("4");
        cut.Find("input").Input("40");
        cut.SetParametersAndRender(p => p.Add(x => x.Value, 4d)); // the stale batch, delivered late

        Assert.Equal(40d, bound);
        Assert.Equal("40", Text(cut));
    }

    [Fact]
    public void A_partial_decimal_survives_an_echo_landing_mid_type()
    {
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));
        cut.Find("input").Focus();

        cut.Find("input").Input("40.");
        cut.SetParametersAndRender(p => p.Add(x => x.Value, bound));

        Assert.Equal("40.", Text(cut));
    }

    [Fact]
    public void Discrete_presses_each_advance_by_one_step()
    {
        // A circuit serializes events, so each press is answered by a render before the next arrives:
        // reading the value per press is right here, and the presses must not collapse into one.
        double? bound = 30;
        var emitted = new List<double?>();
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => { bound = v; emitted.Add(v); }));
        var increment = cut.FindAll("button")[1];

        for (var i = 0; i < 3; i++)
        {
            increment.PointerDown();
            increment.PointerUp();
            cut.SetParametersAndRender(p => p.Add(x => x.Value, bound)); // the parent answers
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

        increment.PointerDown();           // 300ms lead-in, then a tick every 60ms
        // Poll rather than sleep a fixed span: the cadence is real time, and a loaded test host can
        // stretch it well past any span worth hard-coding.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (emitted.Count < 3 && DateTime.UtcNow < deadline) await Task.Delay(30);
        increment.PointerUp();
        var held = emitted.ToArray();

        Assert.True(held.Length >= 3, $"the hold produced {held.Length} step(s)");
        Assert.Equal(held.OrderBy(v => v).Distinct(), held); // strictly climbing, never the same twice
    }

    [Fact]
    public void Stepping_stops_at_Max()
    {
        double? bound = 364;
        var emitted = new List<double?>();
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => { bound = v; emitted.Add(v); }),
            max: 365);
        var increment = cut.FindAll("button")[1];

        increment.PointerDown();
        increment.PointerUp();
        cut.SetParametersAndRender(p => p.Add(x => x.Value, bound));
        increment.PointerDown();
        increment.PointerUp();

        Assert.Equal([365d], emitted); // the second press is already pinned - nothing to emit
    }

    [Fact]
    public void Blur_reformats_and_the_text_follows_the_value_again()
    {
        double? bound = 30;
        var cut = Controlled(bound, EventCallback.Factory.Create<double?>(this, v => bound = v));

        cut.Find("input").Focus();
        cut.Find("input").Input("40");
        cut.Find("input").Blur();
        cut.SetParametersAndRender(p => p.Add(x => x.Value, bound));

        Assert.Equal(40d, bound);
        Assert.Equal("40", Text(cut));
    }

    [Fact]
    public void Uncontrolled_keeps_its_own_value()
    {
        var cut = RenderComponent<BaseInputNumber<double?>>(p => p
            .Add(x => x.DefaultValue, 5d)
            .Add(x => x.ChildContent, Field()));

        cut.FindAll("button")[1].PointerDown();

        Assert.Equal("6", Text(cut));
    }
}
