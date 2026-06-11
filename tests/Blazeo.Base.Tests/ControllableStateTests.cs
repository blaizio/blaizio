using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blazeo.Base.Tests;

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

        // The default is the initial seed only — a later sync must not re-apply it.
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
}
