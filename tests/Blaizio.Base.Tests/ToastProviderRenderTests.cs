using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The headless toast host over a real store: the live region, one stack per occupied corner, and
/// the static data contract on each toast that ts/toast.ts reads (id, type, duration, revision,
/// dismissal). What moves on screen is the script's and is exercised in a browser; here the script
/// is a recording stand-in.
/// </summary>
public class ToastProviderRenderTests : BunitContext
{
    private readonly ToastStore _store = new();

    public ToastProviderRenderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IToastStore>(_store);
    }

    // What a styled layer does at its simplest: the title, and markers for the two resolved flags.
    private static readonly RenderFragment<ToastRenderContext> Content = context => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-test-close", context.CloseButton ? "yes" : "no");
        builder.AddAttribute(2, "data-test-rich", context.RichColors ? "yes" : "no");
        builder.AddContent(3, context.Instance.Data.Title);
        builder.CloseElement();
    };

    private IRenderedComponent<BaseToastProvider> RenderProvider(
        Action<ComponentParameterCollectionBuilder<BaseToastProvider>>? parameters = null) =>
        Render<BaseToastProvider>(p =>
        {
            p.Add(x => x.ChildContent, Content);
            parameters?.Invoke(p);
        });

    private string Show(IRenderedComponent<BaseToastProvider> cut, ToastData data, string? id = null)
    {
        string shown = "";
        cut.InvokeAsync(() => shown = _store.Show(data, id));
        return shown;
    }

    private static IElement Toast(IRenderedComponent<BaseToastProvider> cut, string id) =>
        cut.Find($"li[data-bz-toast-id='{id}']");

    [Fact]
    public void With_no_toasts_only_the_live_region_renders()
    {
        var cut = RenderProvider();

        var region = cut.Find("section[data-bz-toaster-root]");
        Assert.Equal("Notifications", region.GetAttribute("aria-label"));
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Equal("additions text", region.GetAttribute("aria-relevant"));
        Assert.Equal("false", region.GetAttribute("aria-atomic"));
        Assert.Equal("-1", region.GetAttribute("tabindex"));
        Assert.Empty(cut.FindAll("ol"));
    }

    [Fact]
    public void Label_class_style_and_extra_attributes_reach_the_live_region()
    {
        var cut = RenderProvider(p => p
            .Add(x => x.AriaLabel, "Alerts")
            .Add(x => x.Class, "toaster")
            .Add(x => x.Style, "z-index:90")
            .AddUnmatched("data-slot", "toaster"));

        var region = cut.Find("section");
        Assert.Equal("Alerts", region.GetAttribute("aria-label"));
        Assert.Equal("toaster", region.GetAttribute("class"));
        Assert.Equal("z-index:90", region.GetAttribute("style"));
        Assert.Equal("toaster", region.GetAttribute("data-slot"));
    }

    [Fact]
    public void A_shown_toast_renders_in_the_default_corner_with_its_contract()
    {
        var cut = RenderProvider(p => p
            .Add(x => x.ListClass, "stack")
            .Add(x => x.ToastClass, "toast"));

        var id = Show(cut, new ToastData { Title = "Saved" });

        var list = Assert.Single(cut.FindAll("ol[data-bz-toaster]"));
        Assert.Equal("stack", list.GetAttribute("class"));
        Assert.Equal("bottom", list.GetAttribute("data-y-position"));
        Assert.Equal("right", list.GetAttribute("data-x-position"));
        Assert.Equal("3", list.GetAttribute("data-bz-visible-toasts"));
        Assert.Equal("ltr", list.GetAttribute("dir"));

        var toast = Toast(cut, id);
        Assert.Equal("toast", toast.GetAttribute("class"));
        Assert.Equal("normal", toast.GetAttribute("data-bz-toast-type"));
        Assert.Equal("4000", toast.GetAttribute("data-bz-toast-duration"));
        Assert.Equal("true", toast.GetAttribute("data-dismissible"));
        Assert.Equal("false", toast.GetAttribute("data-rich-colors"));
        Assert.Equal("bottom", toast.GetAttribute("data-y-position"));
        Assert.Equal("right", toast.GetAttribute("data-x-position"));
        Assert.Equal("0", toast.GetAttribute("tabindex"));
        // Not set until asked for, or changed: the script reads their absence.
        Assert.False(toast.HasAttribute("data-bz-toast-dismiss"));
        Assert.False(toast.HasAttribute("data-bz-toast-revision"));
        Assert.False(toast.HasAttribute("dir"));
        Assert.Equal("Saved", toast.TextContent);
    }

    [Theory]
    [InlineData(ToastType.Normal, "normal")]
    [InlineData(ToastType.Success, "success")]
    [InlineData(ToastType.Info, "info")]
    [InlineData(ToastType.Warning, "warning")]
    [InlineData(ToastType.Error, "error")]
    [InlineData(ToastType.Loading, "loading")]
    public void The_kind_is_published_on_the_toast(ToastType type, string expected)
    {
        var cut = RenderProvider();

        var id = Show(cut, new ToastData { Type = type, Title = "x" });

        Assert.Equal(expected, Toast(cut, id).GetAttribute("data-bz-toast-type"));
    }

    [Theory]
    [InlineData(ToastPosition.TopLeft, "top", "left")]
    [InlineData(ToastPosition.TopCenter, "top", "center")]
    [InlineData(ToastPosition.TopRight, "top", "right")]
    [InlineData(ToastPosition.BottomLeft, "bottom", "left")]
    [InlineData(ToastPosition.BottomCenter, "bottom", "center")]
    [InlineData(ToastPosition.BottomRight, "bottom", "right")]
    public void The_position_parameter_moves_the_default_corner(ToastPosition position, string y, string x)
    {
        var cut = RenderProvider(p => p.Add(v => v.Position, position));

        var id = Show(cut, new ToastData { Title = "x" });

        var list = cut.Find("ol");
        Assert.Equal(y, list.GetAttribute("data-y-position"));
        Assert.Equal(x, list.GetAttribute("data-x-position"));
        Assert.Equal(y, Toast(cut, id).GetAttribute("data-y-position"));
        Assert.Equal(x, Toast(cut, id).GetAttribute("data-x-position"));
    }

    [Fact]
    public void The_newest_toast_comes_first_in_its_stack()
    {
        var cut = RenderProvider();

        var first = Show(cut, new ToastData { Title = "first" });
        var second = Show(cut, new ToastData { Title = "second" });
        var third = Show(cut, new ToastData { Title = "third" });

        var ids = cut.FindAll("li[data-bz-toast]").Select(li => li.GetAttribute("data-bz-toast-id"));
        Assert.Equal([third, second, first], ids);
    }

    [Fact]
    public void A_toast_with_its_own_position_opens_a_second_stack_after_the_default_one()
    {
        var cut = RenderProvider();

        // Shown first, yet its corner is listed second: the default corner leads.
        var elsewhere = Show(cut, new ToastData { Title = "top", Position = ToastPosition.TopLeft });
        var usual = Show(cut, new ToastData { Title = "bottom" });

        var lists = cut.FindAll("ol[data-bz-toaster]");
        Assert.Equal(2, lists.Count);
        Assert.Equal("bottom", lists[0].GetAttribute("data-y-position"));
        Assert.Equal(usual, lists[0].QuerySelector("li")!.GetAttribute("data-bz-toast-id"));
        Assert.Equal("top", lists[1].GetAttribute("data-y-position"));
        Assert.Equal("left", lists[1].GetAttribute("data-x-position"));
        Assert.Equal(elsewhere, lists[1].QuerySelector("li")!.GetAttribute("data-bz-toast-id"));
    }

    [Fact]
    public void A_stack_goes_away_with_its_last_toast()
    {
        var cut = RenderProvider();
        var elsewhere = Show(cut, new ToastData { Title = "top", Position = ToastPosition.TopLeft });
        Show(cut, new ToastData { Title = "bottom" });

        cut.InvokeAsync(() => cut.Instance.Remove(elsewhere, autoClose: true));

        var list = Assert.Single(cut.FindAll("ol[data-bz-toaster]"));
        Assert.Equal("bottom", list.GetAttribute("data-y-position"));
    }

    [Fact]
    public void A_duration_is_published_in_milliseconds()
    {
        var cut = RenderProvider();

        var own = Show(cut, new ToastData { Title = "x", Duration = TimeSpan.FromSeconds(1.5) });

        Assert.Equal("1500", Toast(cut, own).GetAttribute("data-bz-toast-duration"));
    }

    [Fact]
    public void The_duration_parameter_is_the_default_for_toasts_without_their_own()
    {
        var cut = RenderProvider(p => p.Add(x => x.Duration, TimeSpan.FromSeconds(10)));

        var inherits = Show(cut, new ToastData { Title = "x" });
        var own = Show(cut, new ToastData { Title = "y", Duration = TimeSpan.FromSeconds(2) });

        Assert.Equal("10000", Toast(cut, inherits).GetAttribute("data-bz-toast-duration"));
        Assert.Equal("2000", Toast(cut, own).GetAttribute("data-bz-toast-duration"));
    }

    [Fact]
    public void Persistent_and_loading_toasts_never_close_on_their_own()
    {
        var cut = RenderProvider();

        var persistent = Show(cut, new ToastData { Title = "a", Persistent = true, Duration = TimeSpan.FromSeconds(2) });
        var loading = Show(cut, new ToastData { Title = "b", Type = ToastType.Loading });
        var forever = Show(cut, new ToastData { Title = "c", Duration = TimeSpan.MaxValue });
        // A loading toast that was given a time keeps it.
        var timedLoading = Show(cut, new ToastData { Title = "d", Type = ToastType.Loading, Duration = TimeSpan.FromSeconds(3) });

        Assert.Equal("infinity", Toast(cut, persistent).GetAttribute("data-bz-toast-duration"));
        Assert.Equal("infinity", Toast(cut, loading).GetAttribute("data-bz-toast-duration"));
        Assert.Equal("infinity", Toast(cut, forever).GetAttribute("data-bz-toast-duration"));
        Assert.Equal("3000", Toast(cut, timedLoading).GetAttribute("data-bz-toast-duration"));
    }

    [Fact]
    public void A_loading_toast_that_resolves_gets_a_duration_and_the_next_revision()
    {
        var cut = RenderProvider();
        var id = Show(cut, new ToastData { Title = "Saving", Type = ToastType.Loading });
        Assert.Equal("infinity", Toast(cut, id).GetAttribute("data-bz-toast-duration"));

        Show(cut, new ToastData { Title = "Saved", Type = ToastType.Success }, id);

        var toast = Assert.Single(cut.FindAll("li[data-bz-toast]"));
        Assert.Equal(id, toast.GetAttribute("data-bz-toast-id"));
        Assert.Equal("success", toast.GetAttribute("data-bz-toast-type"));
        Assert.Equal("4000", toast.GetAttribute("data-bz-toast-duration"));
        Assert.Equal("1", toast.GetAttribute("data-bz-toast-revision"));
        Assert.Equal("Saved", toast.TextContent);

        cut.InvokeAsync(() => _store.Update(id, new ToastData { Title = "Saved twice", Type = ToastType.Success }));
        Assert.Equal("2", Toast(cut, id).GetAttribute("data-bz-toast-revision"));
    }

    [Fact]
    public void A_dismissal_from_code_is_flagged_and_the_toast_stays_until_the_script_reports_back()
    {
        var cut = RenderProvider();
        var dismissed = 0;
        var id = Show(cut, new ToastData { Title = "x", OnDismiss = () => dismissed++ });
        var other = Show(cut, new ToastData { Title = "y" });

        cut.InvokeAsync(() => _store.Dismiss(id));

        // Still rendered: the exit animation is the script's, and it runs on this flag.
        Assert.Equal("true", Toast(cut, id).GetAttribute("data-bz-toast-dismiss"));
        Assert.False(Toast(cut, other).HasAttribute("data-bz-toast-dismiss"));
        Assert.Equal(0, dismissed);

        cut.InvokeAsync(() => cut.Instance.Remove(id, autoClose: false));

        Assert.Empty(cut.FindAll($"li[data-bz-toast-id='{id}']"));
        Assert.Single(cut.FindAll("li[data-bz-toast]"));
        Assert.Equal(1, dismissed);
    }

    [Fact]
    public void Dismissing_everything_flags_every_toast()
    {
        var cut = RenderProvider();
        Show(cut, new ToastData { Title = "x" });
        Show(cut, new ToastData { Title = "y", Position = ToastPosition.TopLeft });

        cut.InvokeAsync(() => _store.Dismiss());

        Assert.All(cut.FindAll("li[data-bz-toast]"),
            li => Assert.Equal("true", li.GetAttribute("data-bz-toast-dismiss")));
    }

    [Fact]
    public void The_script_reporting_a_timed_close_runs_the_auto_close_callback()
    {
        var cut = RenderProvider();
        var autoClosed = 0;
        var dismissed = 0;
        var id = Show(cut, new ToastData { Title = "x", OnAutoClose = () => autoClosed++, OnDismiss = () => dismissed++ });

        cut.InvokeAsync(() => cut.Instance.Remove(id, autoClose: true));

        Assert.Equal(1, autoClosed);
        Assert.Equal(0, dismissed);
        Assert.Empty(cut.FindAll("ol"));
    }

    [Fact]
    public void A_report_for_a_toast_that_is_already_gone_changes_nothing()
    {
        var cut = RenderProvider();
        var id = Show(cut, new ToastData { Title = "x" });

        // The script can report the same toast twice (a swipe ending as its timer fires).
        cut.InvokeAsync(() => cut.Instance.Remove(id, autoClose: false));
        cut.InvokeAsync(() => cut.Instance.Remove(id, autoClose: true));

        Assert.Empty(cut.FindAll("li[data-bz-toast]"));
    }

    [Fact]
    public void A_toast_that_cannot_be_dismissed_says_so()
    {
        var cut = RenderProvider();

        var id = Show(cut, new ToastData { Title = "x", Dismissible = false });

        Assert.Equal("false", Toast(cut, id).GetAttribute("data-dismissible"));
    }

    [Fact]
    public void The_close_button_default_applies_unless_the_toast_says_otherwise_and_never_to_loading()
    {
        var cut = RenderProvider(p => p.Add(x => x.CloseButton, true));

        var inherits = Show(cut, new ToastData { Title = "a" });
        var optsOut = Show(cut, new ToastData { Title = "b", CloseButton = false });
        var loading = Show(cut, new ToastData { Title = "c", Type = ToastType.Loading, CloseButton = true });

        Assert.Equal("yes", Toast(cut, inherits).QuerySelector("span")!.GetAttribute("data-test-close"));
        Assert.Equal("no", Toast(cut, optsOut).QuerySelector("span")!.GetAttribute("data-test-close"));
        Assert.Equal("no", Toast(cut, loading).QuerySelector("span")!.GetAttribute("data-test-close"));
    }

    [Fact]
    public void A_toast_can_ask_for_a_close_button_the_provider_does_not_show_by_default()
    {
        var cut = RenderProvider();

        var plain = Show(cut, new ToastData { Title = "a" });
        var asks = Show(cut, new ToastData { Title = "b", CloseButton = true });

        Assert.Equal("no", Toast(cut, plain).QuerySelector("span")!.GetAttribute("data-test-close"));
        Assert.Equal("yes", Toast(cut, asks).QuerySelector("span")!.GetAttribute("data-test-close"));
    }

    [Fact]
    public void Rich_colors_resolve_the_same_way_and_are_published_on_the_toast()
    {
        var cut = RenderProvider(p => p.Add(x => x.RichColors, true));

        var inherits = Show(cut, new ToastData { Title = "a" });
        var optsOut = Show(cut, new ToastData { Title = "b", RichColors = false });

        Assert.Equal("true", Toast(cut, inherits).GetAttribute("data-rich-colors"));
        Assert.Equal("yes", Toast(cut, inherits).QuerySelector("span")!.GetAttribute("data-test-rich"));
        Assert.Equal("false", Toast(cut, optsOut).GetAttribute("data-rich-colors"));
        Assert.Equal("no", Toast(cut, optsOut).QuerySelector("span")!.GetAttribute("data-test-rich"));
    }

    [Fact]
    public void The_visible_count_parameter_reaches_every_stack()
    {
        var cut = RenderProvider(p => p.Add(x => x.VisibleToasts, 5));
        Show(cut, new ToastData { Title = "a" });
        Show(cut, new ToastData { Title = "b", Position = ToastPosition.TopCenter });

        Assert.All(cut.FindAll("ol"), ol => Assert.Equal("5", ol.GetAttribute("data-bz-visible-toasts")));
    }

    [Fact]
    public void A_toast_can_read_in_its_own_direction_inside_a_stack_that_follows_the_provider()
    {
        var cut = RenderProvider(p => p.Add(x => x.Dir, Direction.Rtl));

        var inherits = Show(cut, new ToastData { Title = "a" });
        var own = Show(cut, new ToastData { Title = "b", Direction = Direction.Ltr });

        Assert.Equal("rtl", cut.Find("ol").GetAttribute("dir"));
        Assert.False(Toast(cut, inherits).HasAttribute("dir"));
        Assert.Equal("ltr", Toast(cut, own).GetAttribute("dir"));
    }

    [Fact]
    public void The_script_is_created_once_and_told_to_sync_after_every_render()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/toast.js");
        module.Mode = JSRuntimeMode.Loose;
        var toaster = module.SetupModule(invocation => invocation.Identifier == "createToaster");
        toaster.Mode = JSRuntimeMode.Loose;

        var cut = RenderProvider();
        Assert.Single(module.Invocations, i => i.Identifier == "createToaster");
        var syncs = toaster.Invocations.Count(i => i.Identifier == "sync");
        Assert.True(syncs >= 1);

        // A new toast, then a dismissal: each is a render the script has to reconcile with.
        var id = Show(cut, new ToastData { Title = "x" });
        var afterShow = toaster.Invocations.Count(i => i.Identifier == "sync");
        Assert.True(afterShow > syncs);

        cut.InvokeAsync(() => _store.Dismiss(id));
        Assert.True(toaster.Invocations.Count(i => i.Identifier == "sync") > afterShow);
        Assert.Single(module.Invocations, i => i.Identifier == "createToaster");
    }

    [Fact]
    public async Task Leaving_the_page_releases_the_script_and_stops_listening_to_the_store()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/toast.js");
        module.Mode = JSRuntimeMode.Loose;
        var toaster = module.SetupModule(invocation => invocation.Identifier == "createToaster");
        toaster.Mode = JSRuntimeMode.Loose;
        var cut = RenderProvider();
        var renders = cut.RenderCount;

        await DisposeComponentsAsync();

        Assert.Single(toaster.Invocations, i => i.Identifier == "dispose");
        // The store outlives the host (it is scoped to the app): a later toast must not reach a
        // host that is gone.
        _store.Show(new ToastData { Title = "late" });
        Assert.Equal(renders, cut.RenderCount);
    }
}

/// <summary>The same host under app-wide defaults set through <c>AddBlaizio</c>.</summary>
public class ToastProviderDefaultsTests : BunitContext
{
    private static readonly RenderFragment<ToastRenderContext> Content = context => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-test-close", context.CloseButton ? "yes" : "no");
        builder.AddContent(2, context.Instance.Data.Title);
        builder.CloseElement();
    };

    public ToastProviderDefaultsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio(options =>
        {
            options.Toast.Position = ToastPosition.TopCenter;
            options.Toast.VisibleToasts = 5;
            options.Toast.Duration = TimeSpan.FromSeconds(8);
            options.Toast.CloseButton = true;
            options.Toast.RichColors = true;
        });
    }

    [Fact]
    public void App_wide_defaults_apply_when_the_provider_sets_nothing()
    {
        var toasts = Services.GetRequiredService<IToastService>();
        var cut = Render<BaseToastProvider>(p => p.Add(x => x.ChildContent, Content));

        string id = "";
        cut.InvokeAsync(() => id = toasts.Success("Saved"));

        var list = cut.Find("ol");
        Assert.Equal("top", list.GetAttribute("data-y-position"));
        Assert.Equal("center", list.GetAttribute("data-x-position"));
        Assert.Equal("5", list.GetAttribute("data-bz-visible-toasts"));
        var toast = cut.Find($"li[data-bz-toast-id='{id}']");
        Assert.Equal("8000", toast.GetAttribute("data-bz-toast-duration"));
        Assert.Equal("true", toast.GetAttribute("data-rich-colors"));
        Assert.Equal("yes", toast.QuerySelector("span")!.GetAttribute("data-test-close"));
    }

    [Fact]
    public void Provider_parameters_win_over_the_app_wide_defaults()
    {
        var toasts = Services.GetRequiredService<IToastService>();
        var cut = Render<BaseToastProvider>(p => p
            .Add(x => x.ChildContent, Content)
            .Add(x => x.Position, ToastPosition.BottomLeft)
            .Add(x => x.VisibleToasts, 2)
            .Add(x => x.Duration, TimeSpan.FromSeconds(1))
            .Add(x => x.CloseButton, false)
            .Add(x => x.RichColors, false));

        string id = "";
        cut.InvokeAsync(() => id = toasts.Success("Saved"));

        var list = cut.Find("ol");
        Assert.Equal("bottom", list.GetAttribute("data-y-position"));
        Assert.Equal("left", list.GetAttribute("data-x-position"));
        Assert.Equal("2", list.GetAttribute("data-bz-visible-toasts"));
        var toast = cut.Find($"li[data-bz-toast-id='{id}']");
        Assert.Equal("1000", toast.GetAttribute("data-bz-toast-duration"));
        Assert.Equal("false", toast.GetAttribute("data-rich-colors"));
        Assert.Equal("no", toast.QuerySelector("span")!.GetAttribute("data-test-close"));
    }

    [Fact]
    public async Task A_tracked_operation_is_one_toast_from_spinner_to_result()
    {
        var toasts = Services.GetRequiredService<IToastService>();
        var cut = Render<BaseToastProvider>(p => p.Add(x => x.ChildContent, Content));
        var work = new TaskCompletionSource<string>();

        Task tracking = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            tracking = toasts.Track(() => work.Task, new ToastTrackOptions<string>
            {
                Loading = "Publishing",
                Success = name => $"Published {name}",
            });
        });

        var loading = Assert.Single(cut.FindAll("li[data-bz-toast]"));
        var id = loading.GetAttribute("data-bz-toast-id");
        Assert.Equal("loading", loading.GetAttribute("data-bz-toast-type"));
        Assert.Equal("infinity", loading.GetAttribute("data-bz-toast-duration"));
        // No close button on a spinner, even with the default on.
        Assert.Equal("no", loading.QuerySelector("span")!.GetAttribute("data-test-close"));

        await cut.InvokeAsync(() =>
        {
            work.SetResult("v2");
            return tracking;
        });

        var done = Assert.Single(cut.FindAll("li[data-bz-toast]"));
        Assert.Equal(id, done.GetAttribute("data-bz-toast-id"));
        Assert.Equal("success", done.GetAttribute("data-bz-toast-type"));
        Assert.Equal("8000", done.GetAttribute("data-bz-toast-duration"));
        Assert.Equal("1", done.GetAttribute("data-bz-toast-revision"));
        Assert.Equal("Published v2", done.TextContent);
    }
}
