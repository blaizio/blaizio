using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>Unit tests for the controlled/uncontrolled state bridge behind every stateful primitive.</summary>
public class ControllableStateTests
{
    [Fact]
    public void Uncontrolled_seeds_from_default_once()
    {
        var state = new ControllableState<bool>();
        state.Sync(controlled: false, controlledValue: false, defaultValue: true);

        Assert.True(state.Value);
        Assert.False(state.IsControlled);

        // The default is the initial seed only - a later sync must not re-apply it.
        state.Sync(controlled: false, controlledValue: false, defaultValue: false);
        Assert.True(state.Value);
    }

    [Fact]
    public async Task Uncontrolled_set_updates_value_and_announces_change()
    {
        var state = new ControllableState<bool>();
        state.Sync(controlled: false, controlledValue: false, defaultValue: false);

        bool? captured = null;
        var onChange = EventCallback.Factory.Create<bool>(this, v => captured = v);
        await state.SetAsync(true, onChange);

        Assert.True(state.Value);
        Assert.True(captured);
    }

    [Fact]
    public void Controlled_value_tracks_the_prop()
    {
        var state = new ControllableState<bool>();
        state.Sync(controlled: true, controlledValue: true, defaultValue: false);

        Assert.True(state.Value);
        Assert.True(state.IsControlled);
    }

    [Fact]
    public async Task Controlled_set_does_not_mutate_value_but_announces_change()
    {
        var state = new ControllableState<bool>();
        state.Sync(controlled: true, controlledValue: true, defaultValue: false);

        bool? captured = null;
        var onChange = EventCallback.Factory.Create<bool>(this, v => captured = v);
        await state.SetAsync(false, onChange);

        Assert.True(state.Value);    // unchanged: the parent owns the value
        Assert.False(captured);      // but the intended change was announced
    }

    // ---- the one rule ---------------------------------------------------------------------------

    // Every row of the truth table: controlled when the value is set, or when the listener is bound
    // and no default was supplied.
    [Theory]
    [InlineData(false, false, false, false)] // nothing passed: the component keeps the state
    [InlineData(false, false, true, false)]  // DefaultValue="a"
    [InlineData(false, true, false, true)]   // @bind-Value on a field that holds nothing yet
    [InlineData(false, true, true, false)]   // DefaultValue="a" ValueChanged="Log": listening only
    [InlineData(true, false, false, true)]   // Value="x" with no listener
    [InlineData(true, false, true, true)]    // Value="x" DefaultValue="a": the value wins
    [InlineData(true, true, false, true)]    // @bind-Value on a field that holds a value
    [InlineData(true, true, true, true)]     // everything forwarded by a wrapper, value set
    public void A_value_is_controlled_when_set_or_when_bound_with_no_default(
        bool valueSet, bool listenerBound, bool defaultSupplied, bool expected) =>
        Assert.Equal(expected, ControllableState.IsControlled(valueSet, listenerBound, defaultSupplied));

    [Fact]
    public void Set_means_differs_from_the_default_of_the_type()
    {
        Assert.False(ControllableState.IsSet<string>(null));
        Assert.True(ControllableState.IsSet(""));              // an empty string is a value
        Assert.False(ControllableState.IsSet<int?>(null));
        Assert.True(ControllableState.IsSet<int?>(0));         // a nullable zero is a value
        Assert.False(ControllableState.IsSet(0));              // a plain zero cannot be told from unset
        Assert.True(ControllableState.IsSet(5));
        Assert.False(ControllableState.IsSet(default(DateRange)));
        Assert.True(ControllableState.IsSet(new DateRange(new DateOnly(2026, 1, 1), null)));
        Assert.False(ControllableState.IsSet<IReadOnlyList<string>>(null));
        Assert.True(ControllableState.IsSet<IReadOnlyList<string>>([]));  // an empty list is a value
    }

    // A binding next to a default: the parent stops controlling when the bound value goes back to
    // unset. The component then falls back to what it last committed, not to the value from before
    // the parent took over (an accordion bound this way could not close its last item).
    [Fact]
    public async Task Falling_back_to_uncontrolled_keeps_the_value_committed_while_controlled()
    {
        var state = new ControllableState<string?>();
        string? bound = null;
        var onChange = EventCallback.Factory.Create<string?>(this, v => bound = v);
        void Sync() => state.Sync(
            ControllableState.IsControlled(bound is not null, listenerBound: true, defaultSupplied: true),
            bound,
            "a");

        Sync();
        Assert.False(state.IsControlled);
        Assert.Equal("a", state.Value);

        await state.SetAsync("b", onChange);   // uncontrolled: moves, and the binding receives "b"
        Sync();
        Assert.True(state.IsControlled);
        Assert.Equal("b", state.Value);

        await state.SetAsync(null, onChange);  // controlled: the user closes the item
        Sync();
        Assert.False(state.IsControlled);
        Assert.Null(state.Value);              // closed, not back to "b"
    }
}
