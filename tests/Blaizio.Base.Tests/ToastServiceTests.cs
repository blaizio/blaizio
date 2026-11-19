using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The imperative toast API (ToastService) over a real store: which kind each method shows, how the
/// per-toast options reach the toast, the update-in-place id, and the tracked operation that turns
/// one loading toast into its success or error message.
/// </summary>
public class ToastServiceTests
{
    private static (ToastService Service, ToastStore Store) Create()
    {
        var store = new ToastStore();
        return (new ToastService(store), store);
    }

    public static readonly TheoryData<string, ToastType> Kinds = new()
    {
        { "Show", ToastType.Normal },
        { "Message", ToastType.Normal },
        { "Success", ToastType.Success },
        { "Info", ToastType.Info },
        { "Warning", ToastType.Warning },
        { "Error", ToastType.Error },
        { "Loading", ToastType.Loading },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Each_method_shows_its_own_kind_with_the_title(string method, ToastType expected)
    {
        var (service, store) = Create();

        var id = method switch
        {
            "Show" => service.Show("Hello"),
            "Message" => service.Message("Hello"),
            "Success" => service.Success("Hello"),
            "Info" => service.Info("Hello"),
            "Warning" => service.Warning("Hello"),
            "Error" => service.Error("Hello"),
            _ => service.Loading("Hello"),
        };

        var toast = Assert.Single(store.Toasts);
        Assert.Equal(id, toast.Id);
        Assert.Equal(expected, toast.Data.Type);
        Assert.Equal("Hello", toast.Data.Title);
        Assert.Null(toast.Data.TitleContent);
    }

    [Fact]
    public void Without_options_a_toast_takes_the_defaults()
    {
        var (service, store) = Create();

        service.Show("Plain");

        var data = store.Toasts[0].Data;
        Assert.True(data.Dismissible);
        Assert.True(data.ShowIcon);
        Assert.False(data.Persistent);
        // Left unset so the provider's own defaults apply.
        Assert.Null(data.Duration);
        Assert.Null(data.CloseButton);
        Assert.Null(data.RichColors);
        Assert.Null(data.Position);
        Assert.Null(data.Direction);
    }

    [Fact]
    public void Every_option_reaches_the_toast()
    {
        var (service, store) = Create();
        var action = new ToastAction("Undo", () => { });
        var cancel = new ToastAction("Keep", () => { });
        RenderFragment description = b => b.AddContent(0, "details");
        RenderFragment icon = b => b.AddContent(0, "!");
        Action onDismiss = () => { };
        Action onAutoClose = () => { };

        service.Warning("Disk almost full", new ToastOptions
        {
            Description = "2 GB left",
            DescriptionContent = description,
            Duration = TimeSpan.FromSeconds(9),
            Action = action,
            Cancel = cancel,
            CloseButton = true,
            Dismissible = false,
            RichColors = true,
            Position = ToastPosition.TopCenter,
            Direction = Direction.Rtl,
            Icon = icon,
            ShowIcon = false,
            Persistent = true,
            OnDismiss = onDismiss,
            OnAutoClose = onAutoClose,
        });

        var data = store.Toasts[0].Data;
        Assert.Equal("2 GB left", data.Description);
        Assert.Same(description, data.DescriptionContent);
        Assert.Equal(TimeSpan.FromSeconds(9), data.Duration);
        Assert.Same(action, data.Action);
        Assert.Same(cancel, data.Cancel);
        Assert.True(data.CloseButton);
        Assert.False(data.Dismissible);
        Assert.True(data.RichColors);
        Assert.Equal(ToastPosition.TopCenter, data.Position);
        Assert.Equal(Direction.Rtl, data.Direction);
        Assert.Same(icon, data.Icon);
        Assert.False(data.ShowIcon);
        Assert.True(data.Persistent);
        Assert.Same(onDismiss, data.OnDismiss);
        Assert.Same(onAutoClose, data.OnAutoClose);
    }

    [Fact]
    public void Custom_content_becomes_the_title_content()
    {
        var (service, store) = Create();
        RenderFragment content = b => b.AddContent(0, "custom");

        service.Show(content);

        var data = store.Toasts[0].Data;
        Assert.Same(content, data.TitleContent);
        Assert.Null(data.Title);
        Assert.Equal(ToastType.Normal, data.Type);
    }

    [Fact]
    public void Null_custom_content_is_refused_and_shows_nothing()
    {
        var (service, store) = Create();

        Assert.Throws<ArgumentNullException>(() => service.Show((RenderFragment)null!));

        Assert.Empty(store.Toasts);
    }

    [Fact]
    public void The_id_option_updates_the_toast_that_already_has_it()
    {
        var (service, store) = Create();

        var first = service.Loading("Uploading", new ToastOptions { Id = "upload" });
        var second = service.Success("Uploaded", new ToastOptions { Id = "upload" });

        Assert.Equal("upload", first);
        Assert.Equal("upload", second);
        var toast = Assert.Single(store.Toasts);
        Assert.Equal(ToastType.Success, toast.Data.Type);
        Assert.Equal("Uploaded", toast.Data.Title);
    }

    [Fact]
    public void Dismiss_asks_for_one_toast_or_for_all()
    {
        var (service, store) = Create();
        var a = service.Show("A");
        service.Show("B");

        service.Dismiss(a);
        Assert.True(store.Toasts[0].DismissRequested);
        Assert.False(store.Toasts[1].DismissRequested);

        service.Dismiss();
        Assert.All(store.Toasts, t => Assert.True(t.DismissRequested));
    }

    [Fact]
    public async Task A_tracked_operation_shows_loading_then_success_in_the_same_toast()
    {
        var (service, store) = Create();
        var work = new TaskCompletionSource<int>();

        var tracking = service.Track(() => work.Task, new ToastTrackOptions<int>
        {
            Loading = "Saving",
            Success = rows => $"Saved {rows} rows",
            Position = ToastPosition.TopRight,
        });

        // In flight: one loading toast, which the provider keeps until it is resolved.
        var toast = Assert.Single(store.Toasts);
        Assert.Equal(ToastType.Loading, toast.Data.Type);
        Assert.Equal("Saving", toast.Data.Title);
        Assert.Equal(ToastPosition.TopRight, toast.Data.Position);
        Assert.False(tracking.IsCompleted);

        work.SetResult(12);
        await tracking;

        Assert.Same(toast, Assert.Single(store.Toasts));
        Assert.Equal(ToastType.Success, toast.Data.Type);
        Assert.Equal("Saved 12 rows", toast.Data.Title);
        Assert.Equal(ToastPosition.TopRight, toast.Data.Position);
    }

    [Fact]
    public async Task A_failed_operation_becomes_the_error_toast_and_does_not_throw()
    {
        var (service, store) = Create();

        await service.Track<int>(
            () => Task.FromException<int>(new InvalidOperationException("disk full")),
            new ToastTrackOptions<int> { Loading = "Saving", Error = ex => $"Failed: {ex.Message}" });

        var toast = Assert.Single(store.Toasts);
        Assert.Equal(ToastType.Error, toast.Data.Type);
        Assert.Equal("Failed: disk full", toast.Data.Title);
    }

    [Fact]
    public async Task An_operation_that_throws_before_its_first_await_is_reported_the_same_way()
    {
        var (service, store) = Create();

        await service.Track<int>(
            () => throw new InvalidOperationException("no connection"),
            new ToastTrackOptions<int> { Loading = "Saving" });

        var toast = Assert.Single(store.Toasts);
        Assert.Equal(ToastType.Error, toast.Data.Type);
    }

    [Fact]
    public async Task Tracked_messages_have_defaults()
    {
        var (service, store) = Create();

        await service.Track(() => Task.FromResult(1), new ToastTrackOptions<int> { Loading = "Working" });
        Assert.Equal("Success", store.Toasts[0].Data.Title);

        await service.Track<int>(
            () => Task.FromException<int>(new InvalidOperationException()),
            new ToastTrackOptions<int> { Loading = "Working" });
        Assert.Equal("Something went wrong", store.Toasts[1].Data.Title);
    }

    [Fact]
    public async Task A_dismissal_asked_for_while_the_work_runs_is_dropped_when_the_result_arrives()
    {
        var (service, store) = Create();
        var work = new TaskCompletionSource<int>();
        var tracking = service.Track(() => work.Task, new ToastTrackOptions<int> { Loading = "Saving" });
        service.Dismiss();
        Assert.True(store.Toasts[0].DismissRequested);

        work.SetResult(1);
        await tracking;

        // The result is the feedback: it must not animate out under a dismissal meant for the spinner.
        Assert.False(store.Toasts[0].DismissRequested);
        Assert.Equal(ToastType.Success, store.Toasts[0].Data.Type);
    }

    [Fact]
    public async Task A_result_still_shows_when_the_loading_toast_was_already_removed()
    {
        var (service, store) = Create();
        var work = new TaskCompletionSource<int>();
        var tracking = service.Track(() => work.Task, new ToastTrackOptions<int> { Loading = "Saving" });
        var id = store.Toasts[0].Id;
        store.NotifyRemoved(id, autoClose: false); // swiped away while the work ran
        Assert.Empty(store.Toasts);

        work.SetResult(1);
        await tracking;

        var toast = Assert.Single(store.Toasts);
        Assert.Equal(id, toast.Id);
        Assert.Equal(ToastType.Success, toast.Data.Type);
    }

    [Fact]
    public async Task Track_refuses_a_missing_operation_or_missing_options()
    {
        var (service, store) = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.Track<int>(null!, new ToastTrackOptions<int> { Loading = "x" }));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.Track(() => Task.FromResult(1), null!));

        Assert.Empty(store.Toasts);
    }
}
