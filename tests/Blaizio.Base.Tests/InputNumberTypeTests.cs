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
public class InputNumberTypeTests : BunitContext
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
        Render<BaseInputNumber<TValue>>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, onChange)
            .Add(x => x.Min, min)
            .Add(x => x.Max, max)
            .Add(x => x.Step, step)
            .Add(x => x.ChildContent, Field()));

    [Fact]
    public async Task An_int_field_steps_and_emits_ints()
    {
        var emitted = new List<int>();
        var cut = Render(30, EventCallback.Factory.Create<int>(this, emitted.Add));

        // Awaited: the synchronous trigger only guarantees the handler started, so the emission
        // this asserts on can still be in flight when the assertion runs.
        await cut.FindAll("button")[1].PointerDownAsync();
        await cut.FindAll("button")[1].PointerUpAsync();

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
    public async Task Decimal_steps_are_exact()
    {
        // The whole point of decimal-backed math: 0.1 + 0.1 + 0.1 is 0.3, not 0.30000000000000004.
        decimal bound = 0m;
        var emitted = new List<decimal>();
        var cut = Render(bound, EventCallback.Factory.Create<decimal>(this, v => { bound = v; emitted.Add(v); }),
            step: 0.1);
        var increment = cut.FindAll("button")[1];

        for (var i = 0; i < 3; i++)
        {
            await increment.PointerDownAsync();
            await increment.PointerUpAsync();
            cut.Render(p => p.Add(x => x.Value, bound));
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
    public async Task Stepping_clamps_to_the_types_own_range()
    {
        // No Max set: the int's own MaxValue is the ceiling, so converting back can never overflow.
        var bound = int.MaxValue - 1;
        var emitted = new List<int>();
        var cut = Render(bound, EventCallback.Factory.Create<int>(this, v => { bound = v; emitted.Add(v); }));
        var increment = cut.FindAll("button")[1];

        await increment.PointerDownAsync();
        await increment.PointerUpAsync();
        cut.Render(p => p.Add(x => x.Value, bound));
        await increment.PointerDownAsync();
        await increment.PointerUpAsync();

        Assert.Equal([int.MaxValue], emitted); // the second press is pinned at the type's edge
    }

    // Awaited dispatch throughout: a press commits through ControllableState.SetAsync, which can
    // yield before the parent's ValueChanged runs. The synchronous PointerDown() returned before
    // that continuation on a fast Linux runner and the assertion read the pre-press value.
    [Fact]
    public async Task Short_and_long_round_trip()
    {
        short shortBound = 5;
        var shortCut = Render(shortBound, EventCallback.Factory.Create<short>(this, v => shortBound = v));
        await shortCut.FindAll("button")[1].PointerDownAsync();
        await shortCut.FindAll("button")[1].PointerUpAsync();
        Assert.Equal((short)6, shortBound);

        var longBound = 5_000_000_000L; // past int range
        var longCut = Render(longBound, EventCallback.Factory.Create<long>(this, v => longBound = v));
        await longCut.FindAll("button")[1].PointerDownAsync();
        await longCut.FindAll("button")[1].PointerUpAsync();
        Assert.Equal(5_000_000_001L, longBound);
    }

    [Fact]
    public async Task Float_binds_and_steps()
    {
        var bound = 1.5f;
        var cut = Render(bound, EventCallback.Factory.Create<float>(this, v => bound = v), step: 0.5);
        await cut.FindAll("button")[1].PointerDownAsync();
        await cut.FindAll("button")[1].PointerUpAsync();
        Assert.Equal(2f, bound);
    }

    [Fact]
    public void An_unsupported_TValue_throws_the_Blazor_message()
    {
        // Same contract as Blazor's InputNumber<TValue>: a compile-time-open TValue is validated at
        // first use. The static ctor throw surfaces wrapped in a TypeInitializationException.
        var ex = Assert.ThrowsAny<Exception>(() =>
            Render<BaseInputNumber<byte>>(p => p.Add(x => x.ChildContent, Field())));

        Assert.Contains("is not a supported numeric type",
            (ex.InnerException ?? ex).InnerException?.Message ?? (ex.InnerException ?? ex).Message);
    }

    // The edit text never carries grouping (the script guard admits one separator, and "." in any
    // culture because keypads emit it), so "." is always the decimal point. Parsed with the culture
    // as-is, "1.5" read as 15 wherever "." groups thousands.
    [Theory]
    [InlineData("de-DE", "1.5", 1.5)]
    [InlineData("de-DE", "1,5", 1.5)]
    [InlineData("fr-FR", "0.25", 0.25)]
    [InlineData("en-US", "1.5", 1.5)]
    public void A_dot_is_the_decimal_point_in_every_culture(string culture, string text, double expected)
    {
        Assert.True(InputNumberValue<double>.TryParse(text, System.Globalization.CultureInfo.GetCultureInfo(culture), out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void An_integer_field_still_rejects_a_fraction_where_dot_groups_thousands() =>
        Assert.False(InputNumberValue<int>.TryParse("1.5", System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out _));

    // A float widened to double shows its binary tail: 0.1f became 0.100000001490116, and the edit
    // text prints every decimal the value has.
    [Theory]
    [InlineData(0.1f, "0.1")]
    [InlineData(2.675f, "2.675")]
    [InlineData(0.00001f, "0.00001")]
    [InlineData(1234.5f, "1234.5")]
    public void A_float_keeps_the_digits_it_was_given(float value, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), InputNumberValue<float>.ToDecimal(value));

    [Fact]
    public void A_float_survives_the_trip_through_the_field_unchanged()
    {
        var third = 1f / 3;

        Assert.Equal(third, InputNumberValue<float>.FromDecimal(InputNumberValue<float>.ToDecimal(third)));
    }

    [Fact]
    public void A_float_outside_decimal_range_saturates()
    {
        Assert.Equal(decimal.MaxValue, InputNumberValue<float>.ToDecimal(float.MaxValue));
        Assert.Equal(decimal.MinValue, InputNumberValue<float>.ToDecimal(float.NegativeInfinity));
        Assert.Equal(0m, InputNumberValue<float>.ToDecimal(float.NaN));
    }

    [Fact]
    public void A_float_field_shows_the_number_it_holds_when_focused()
    {
        var cut = Render<BaseInputNumber<float>>(p => p
            .Add(x => x.Value, 0.1f)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<float>(this, _ => { }))
            .Add(x => x.Culture, System.Globalization.CultureInfo.InvariantCulture)
            .Add(x => x.ChildContent, Field()));

        cut.Find("input").Focus();

        Assert.Equal("0.1", cut.Find("input").GetAttribute("value"));
    }
}
