using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Unit tests for the imperative toast mechanism (ToastStore) - the pure C# contract: add / update by
/// id, dismiss requests, callback dispatch and removal on the engine's report-back, and teardown. The
/// on-screen lifecycle (stacking, animation, swipe) lives in ts/toast.ts and is exercised in-browser.
/// </summary>
public class ToastStoreTests
{
    [Fact]
    public void Show_adds_a_toast_returns_its_id_and_raises_Changed()
    {
        var store = new ToastStore();
        var changed = 0;
        store.Changed += () => changed++;

        var id = store.Show(new ToastData { Title = "Saved" });

        Assert.Single(store.Toasts);
        Assert.Equal(id, store.Toasts[0].Id);
        Assert.Equal("Saved", store.Toasts[0].Data.Title);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Show_generates_unique_ids_when_none_supplied()
    {
        var store = new ToastStore();

        var a = store.Show(new ToastData());
        var b = store.Show(new ToastData());

        Assert.NotEqual(a, b);
        Assert.Equal(2, store.Toasts.Count);
    }

    [Fact]
    public void Show_honours_a_supplied_id()
    {
        var store = new ToastStore();

        var id = store.Show(new ToastData { Title = "Pinned" }, "my-id");

        Assert.Equal("my-id", id);
        Assert.Equal("my-id", store.Toasts[0].Id);
    }

    [Fact]
    public void Show_with_an_existing_id_updates_in_place_and_clears_pending_dismissal()
    {
        var store = new ToastStore();
        var id = store.Show(new ToastData { Type = ToastType.Loading, Title = "Saving…" });
        store.Dismiss(id);
        Assert.True(store.Toasts[0].DismissRequested);

        // The promise resolve path: re-show with the same id.
        store.Show(new ToastData { Type = ToastType.Success, Title = "Saved" }, id);

        Assert.Single(store.Toasts);                       // updated, not duplicated
        Assert.Equal(ToastType.Success, store.Toasts[0].Data.Type);
        Assert.Equal("Saved", store.Toasts[0].Data.Title);
        Assert.False(store.Toasts[0].DismissRequested);    // resolved toast is no longer dismissing
    }

    [Fact]
    public void Update_replaces_data_when_the_toast_exists_and_is_a_no_op_otherwise()
    {
        var store = new ToastStore();
        var id = store.Show(new ToastData { Title = "Before" });

        store.Update(id, new ToastData { Title = "After" });
        Assert.Equal("After", store.Toasts[0].Data.Title);

        store.Update("missing", new ToastData { Title = "Nope" });   // no throw, no add
        Assert.Single(store.Toasts);
    }

    [Fact]
    public void Dismiss_by_id_flags_only_that_toast_and_raises_Changed()
    {
        var store = new ToastStore();
        var a = store.Show(new ToastData());
        store.Show(new ToastData());
        var changed = 0;
        store.Changed += () => changed++;

        store.Dismiss(a);

        Assert.True(store.Toasts[0].DismissRequested);
        Assert.False(store.Toasts[1].DismissRequested);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Dismiss_all_flags_every_toast()
    {
        var store = new ToastStore();
        store.Show(new ToastData());
        store.Show(new ToastData());

        store.Dismiss();

        Assert.All(store.Toasts, t => Assert.True(t.DismissRequested));
    }

    [Fact]
    public void NotifyRemoved_fires_OnDismiss_drops_the_toast_and_raises_Changed()
    {
        var store = new ToastStore();
        var dismissed = 0;
        var autoClosed = 0;
        var id = store.Show(new ToastData { OnDismiss = () => dismissed++, OnAutoClose = () => autoClosed++ });
        var changed = 0;
        store.Changed += () => changed++;

        store.NotifyRemoved(id, autoClose: false);

        Assert.Empty(store.Toasts);
        Assert.Equal(1, dismissed);
        Assert.Equal(0, autoClosed);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void NotifyRemoved_fires_OnAutoClose_when_the_timer_elapsed()
    {
        var store = new ToastStore();
        var dismissed = 0;
        var autoClosed = 0;
        var id = store.Show(new ToastData { OnDismiss = () => dismissed++, OnAutoClose = () => autoClosed++ });

        store.NotifyRemoved(id, autoClose: true);

        Assert.Equal(0, dismissed);
        Assert.Equal(1, autoClosed);
    }

    [Fact]
    public void NotifyRemoved_is_a_no_op_for_an_unknown_id()
    {
        var store = new ToastStore();
        store.Show(new ToastData());

        store.NotifyRemoved("gone", autoClose: false);   // no throw

        Assert.Single(store.Toasts);
    }

    [Fact]
    public void Dispose_clears_all_toasts()
    {
        var store = new ToastStore();
        store.Show(new ToastData());
        store.Show(new ToastData());

        store.Dispose();

        Assert.Empty(store.Toasts);
    }
}
