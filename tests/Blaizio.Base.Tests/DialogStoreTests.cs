using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Unit tests for the imperative dialog mechanism (DialogStore + DialogInstance) - the pure C#
/// contract: result resolution, idempotent close, deferred removal, stacking, and teardown. The
/// presence-aware removal through a host is covered by <see cref="DialogProviderRenderTests"/>.
/// </summary>
public class DialogStoreTests
{
    private static readonly RenderFragment Body = b => b.AddContent(0, "Body");

    [Fact]
    public void OpenAsync_adds_an_instance_and_raises_Changed()
    {
        var store = new DialogStore();
        var changed = 0;
        store.Changed += () => changed++;

        var task = store.OpenAsync(_ => Body, new DialogOptions());

        Assert.Single(store.Instances);
        Assert.True(store.Instances[0].IsOpen);
        Assert.False(task.IsCompleted);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void OpenAsync_passes_the_instance_to_the_content_factory()
    {
        var store = new DialogStore();
        DialogInstance? seen = null;

        store.OpenAsync(instance => { seen = instance; return Body; }, new DialogOptions());

        Assert.Same(store.Instances[0], seen);
    }

    [Fact]
    public async Task Close_resolves_with_the_value_and_flips_IsOpen()
    {
        var store = new DialogStore();
        var task = store.OpenAsync(_ => Body, new DialogOptions());

        store.Instances[0].Close(DialogResult.Ok("hi"));

        var result = await task;
        Assert.False(result.Cancelled);
        Assert.Equal("hi", result.Value);
        Assert.False(store.Instances[0].IsOpen);
    }

    [Fact]
    public async Task Cancel_resolves_cancelled()
    {
        var store = new DialogStore();
        var task = store.OpenAsync(_ => Body, new DialogOptions());

        store.Instances[0].Cancel();

        Assert.True((await task).Cancelled);
    }

    [Fact]
    public async Task Close_is_idempotent_first_outcome_wins()
    {
        var store = new DialogStore();
        var task = store.OpenAsync(_ => Body, new DialogOptions());
        var instance = store.Instances[0];

        instance.Close(DialogResult.Ok(1));
        instance.Close(DialogResult.Ok(2));   // ignored - already closed
        instance.Cancel();                    // ignored

        var result = await task;
        Assert.False(result.Cancelled);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void Close_does_not_remove_the_instance_removal_waits_for_the_exit()
    {
        var store = new DialogStore();
        store.OpenAsync(_ => Body, new DialogOptions());
        var instance = store.Instances[0];

        instance.Close(DialogResult.Ok(null));

        // Still listed (animating out); only NotifyExited drops it.
        Assert.Single(store.Instances);
    }

    [Fact]
    public void NotifyExited_removes_the_instance_once()
    {
        var store = new DialogStore();
        store.OpenAsync(_ => Body, new DialogOptions());
        var instance = store.Instances[0];
        instance.Close(DialogResult.Cancel());

        var changed = 0;
        store.Changed += () => changed++;

        instance.NotifyExited();
        Assert.Empty(store.Instances);
        Assert.Equal(1, changed);

        instance.NotifyExited();              // idempotent - no second removal/notification
        Assert.Empty(store.Instances);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Dispose_cancels_pending_dialogs_without_throwing()
    {
        var store = new DialogStore();
        var task = store.OpenAsync(_ => Body, new DialogOptions());

        store.Dispose();

        var result = await task;              // must complete, not throw
        Assert.True(result.Cancelled);
        Assert.Empty(store.Instances);
    }

    [Fact]
    public void OpenAsync_stacks_multiple_dialogs_in_order()
    {
        var store = new DialogStore();

        store.OpenAsync(_ => Body, new DialogOptions());
        store.OpenAsync(_ => Body, new DialogOptions());

        Assert.Equal(2, store.Instances.Count);
        Assert.NotSame(store.Instances[0], store.Instances[1]);
    }

    [Fact]
    public async Task TryDismiss_cancels_unless_PreventDismiss_is_set()
    {
        var store = new DialogStore();
        var task = store.OpenAsync(_ => Body, new DialogOptions());
        var instance = store.Instances[0];

        instance.PreventDismiss = true;
        Assert.False(instance.TryDismiss());   // blocked
        Assert.True(instance.IsOpen);
        Assert.False(task.IsCompleted);

        instance.PreventDismiss = false;
        Assert.True(instance.TryDismiss());    // now dismisses
        Assert.True((await task).Cancelled);
        Assert.False(instance.IsOpen);
    }

    [Fact]
    public void PreventDismiss_seeds_from_options()
    {
        var store = new DialogStore();
        store.OpenAsync(_ => Body, new DialogOptions { PreventDismiss = true });
        Assert.True(store.Instances[0].PreventDismiss);
    }

    [Fact]
    public void PreventDismiss_setter_raises_Changed_so_the_host_re_renders()
    {
        var store = new DialogStore();
        store.OpenAsync(_ => Body, new DialogOptions());
        var instance = store.Instances[0];
        var changed = 0;
        store.Changed += () => changed++;

        instance.PreventDismiss = true;
        Assert.Equal(1, changed);
        instance.PreventDismiss = true;   // unchanged -> no extra notification
        Assert.Equal(1, changed);
    }

    [Fact]
    public void DialogResult_helpers_round_trip()
    {
        Assert.True(DialogResult.Cancel().Cancelled);
        Assert.Equal(7, DialogResult.Ok(7).ValueAs<int>());
        Assert.Equal(0, DialogResult.Ok("not an int").ValueAs<int>());
        Assert.Null(DialogResult.Cancel().ValueAs<string>());
    }
}
