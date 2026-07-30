using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The generic surface of the headless number field: the TValue whitelist (Blazor's own -
/// int/long/short/float/double/decimal, bare or nullable), per-type parsing, exact decimal math,
/// the nullable-only empty state, and clamping to the type's own range.
/// </summary>
public class InputNumberTypeTests : TestContext
{
    public InputNumberTypeTests()
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

    private IRenderedComponent<BaseInputNumber<TValue>> Render<TValue>(
        TValue value, EventCallback<TValue> onChange, double? min = null, double? max = null, double step = 1) =>
        RenderComponent<BaseInputNumber<TValue>>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, onChange)
            .Add(x => x.Min, min)
            .Add(x => x.Max, max)
            .Add(x => x.Step, step)
            .Add(x => x.ChildContent, Field()));

    [Fact]
    public void An_int_field_steps_and_emits_ints()
    {
        var emitted = new List<int>();
        var cut = Render(30, EventCallback.Factory.Create<int>(this, emitted.Add));

        cut.FindAll("button")[1].PointerDown();
        cut.FindAll("button")[1].PointerUp();

        Assert.Equal([31], emitted);
    }

    [Fact]
    public void An_int_field_rejects_fractional_input_like_Blazor()
    {
        var emitted = new List<int>();
        var cut = Render(30, EventCallback.Factory.Create<int>(this, emitted.Add));
        cut.Find("input").Focus();

        cut.Find("input").Input("2.7"); // int.TryParse fails - invalid, not "2"

        Assert.Empty(emitted);
    }

    [Fact]
    public void A_non_nullable_field_does_not_commit_an_empty_value()
    {
        var emitted = new List<int>();
        var cut = Render(30, EventCallback.Factory.Create<int>(this, emitted.Add));
        cut.Find("input").Focus();

        cut.Find("input").Input("");

        Assert.Empty(emitted); // int has no null to emit - the field just sits empty mid-edit
    }

    [Fact]
    public void A_non_nullable_field_blurred_empty_reverts_to_the_last_good_value()
    {
        var bound = 30;
        var cut = Render(bound, EventCallback.Factory.Create<int>(this, v => bound = v));
        cut.Find("input").Focus();

        cut.Find("input").Input("");
        cut.Find("input").Blur();

        Assert.Equal(30, bound);
        Assert.Equal("30", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void A_nullable_field_commits_null_for_an_empty_value()
    {
        int? bound = 30;
        var cut = Render<int?>(bound, EventCallback.Factory.Create<int?>(this, v => bound = v));
        cut.Find("input").Focus();

        cut.Find("input").Input("");

        Assert.Null(bound);
    }

    [Fact]
    public void Decimal_steps_are_exact()
    {
        // The whole point of decimal-backed math: 0.1 + 0.1 + 0.1 is 0.3, not 0.30000000000000004.
        decimal bound = 0m;
        var emitted = new List<decimal>();
        var cut = Render(bound, EventCallback.Factory.Create<decimal>(this, v => { bound = v; emitted.Add(v); }),
            step: 0.1);
        var increment = cut.FindAll("button")[1];

        for (var i = 0; i < 3; i++)
        {
            increment.PointerDown();
            increment.PointerUp();
            cut.SetParametersAndRender(p => p.Add(x => x.Value, bound));
        }

        Assert.Equal([0.1m, 0.2m, 0.3m], emitted);
    }

    [Fact]
    public void Decimal_typed_input_keeps_its_exact_value()
    {
        decimal? bound = null;
        var cut = Render<decimal?>(bound, EventCallback.Factory.Create<decimal?>(this, v => bound = v));
        cut.Find("input").Focus();

        cut.Find("input").Input("19.99");

        Assert.Equal(19.99m, bound);
    }

    [Fact]
    public void Stepping_clamps_to_the_types_own_range()
    {
        // No Max set: the int's own MaxValue is the ceiling, so converting back can never overflow.
        var bound = int.MaxValue - 1;
        var emitted = new List<int>();
        var cut = Render(bound, EventCallback.Factory.Create<int>(this, v => { bound = v; emitted.Add(v); }));
        var increment = cut.FindAll("button")[1];

        increment.PointerDown();
        increment.PointerUp();
        cut.SetParametersAndRender(p => p.Add(x => x.Value, bound));
        increment.PointerDown();
        increment.PointerUp();

        Assert.Equal([int.MaxValue], emitted); // the second press is pinned at the type's edge
    }

    [Fact]
    public void Short_and_long_round_trip()
    {
        short shortBound = 5;
        var shortCut = Render(shortBound, EventCallback.Factory.Create<short>(this, v => shortBound = v));
        shortCut.FindAll("button")[1].PointerDown();
        shortCut.FindAll("button")[1].PointerUp();
        Assert.Equal((short)6, shortBound);

        var longBound = 5_000_000_000L; // past int range
        var longCut = Render(longBound, EventCallback.Factory.Create<long>(this, v => longBound = v));
        longCut.FindAll("button")[1].PointerDown();
        longCut.FindAll("button")[1].PointerUp();
        Assert.Equal(5_000_000_001L, longBound);
    }

    [Fact]
    public void Float_binds_and_steps()
    {
        var bound = 1.5f;
        var cut = Render(bound, EventCallback.Factory.Create<float>(this, v => bound = v), step: 0.5);
        cut.FindAll("button")[1].PointerDown();
        cut.FindAll("button")[1].PointerUp();
        Assert.Equal(2f, bound);
    }

    [Fact]
    public void An_unsupported_TValue_throws_the_Blazor_message()
    {
        // Same contract as Blazor's InputNumber<TValue>: a compile-time-open TValue is validated at
        // first use. The static ctor throw surfaces wrapped in a TypeInitializationException.
        var ex = Assert.ThrowsAny<Exception>(() =>
            RenderComponent<BaseInputNumber<byte>>(p => p.Add(x => x.ChildContent, Field())));

        Assert.Contains("is not a supported numeric type",
            (ex.InnerException ?? ex).InnerException?.Message ?? (ex.InnerException ?? ex).Message);
    }
}
