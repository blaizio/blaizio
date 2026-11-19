using AngleSharp.Dom;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// BzToastProvider: what a toast raised through IToastService looks like (icon, title, description)
/// and what its action, cancel and close buttons do.
/// </summary>
public class ToastTests : BunitContext
{
    private readonly IToastService _toasts;
    private readonly IToastStore _store;

    public ToastTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
        _toasts = Services.GetRequiredService<IToastService>();
        _store = Services.GetRequiredService<IToastStore>();
    }

    private IRenderedComponent<BzToastProvider> Provider(Action<ComponentParameterCollectionBuilder<BzToastProvider>>? more = null) =>
        Render<BzToastProvider>(p => more?.Invoke(p));

    // Raise a toast the way a page does, from an event handler.
    private static IElement Raise(IRenderedComponent<BzToastProvider> cut, Func<string> show)
    {
        var id = "";
        cut.InvokeAsync(() => id = show());
        return cut.Find($"li[data-bz-toast-id='{id}']");
    }

    private static string Drawing(IElement toast) => toast.QuerySelector(".bz-toast-icon svg")!.InnerHtml;

    private string Drawing(Icon icon) => Render<BzIcon>(p => p.Add(x => x.Icon, icon)).Find("svg").InnerHtml;

    [Fact]
    public void With_no_toasts_the_provider_is_an_empty_named_live_region()
    {
        var cut = Provider(p => p.Add(x => x.Class, "z-[60]").AddUnmatched("id", "toasts"));

        var region = cut.Find("section[data-bz-toaster-root]");
        Assert.Equal("Notifications", region.GetAttribute("aria-label"));
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Equal("toasts", region.Id);
        Assert.Contains("z-[60]", region.ClassList);
        Assert.Equal(0, region.ChildElementCount);
    }

    [Fact]
    public void The_region_can_be_named_in_another_language()
    {
        Assert.Equal("Benachrichtigungen",
            Provider(p => p.Add(x => x.AriaLabel, "Benachrichtigungen")).Find("section").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_plain_toast_is_a_title_with_no_icon_and_no_buttons()
    {
        var cut = Provider();

        var toast = Raise(cut, () => _toasts.Show("Draft saved"));

        Assert.Contains("bz-toast", toast.ClassList);
        Assert.Contains("bz-toaster", toast.ParentElement!.ClassList);
        Assert.Equal("Draft saved", toast.QuerySelector(".bz-toast-title")!.TextContent.Trim());
        Assert.Null(toast.QuerySelector(".bz-toast-icon"));
        Assert.Null(toast.QuerySelector(".bz-toast-description"));
        Assert.Empty(toast.QuerySelectorAll("button"));
    }

    [Fact]
    public void A_description_goes_under_the_title_as_text_or_as_markup()
    {
        var cut = Provider();

        var text = Raise(cut, () => _toasts.Show("Event created", new ToastOptions { Description = "Monday at 9:00" }));
        Assert.Equal("Monday at 9:00", text.QuerySelector(".bz-toast-description")!.TextContent.Trim());

        RenderFragment rich = builder => builder.AddMarkupContent(0, "<a id=\"view\" href=\"/calendar\">View</a>");
        var markup = Raise(cut, () => _toasts.Show("Event created", new ToastOptions { DescriptionContent = rich }));
        Assert.Equal("/calendar", markup.QuerySelector(".bz-toast-description #view")!.GetAttribute("href"));
    }

    [Fact]
    public void A_title_can_be_markup()
    {
        var cut = Provider();
        RenderFragment title = builder => builder.AddMarkupContent(0, "<strong id=\"t\">3 files</strong> uploaded");

        var toast = Raise(cut, () => _toasts.Show(title));

        Assert.Equal("3 files uploaded", toast.QuerySelector(".bz-toast-title")!.TextContent.Trim());
        Assert.NotNull(toast.QuerySelector(".bz-toast-title #t"));
    }

    [Fact]
    public void Each_kind_of_toast_has_its_own_icon()
    {
        var cut = Provider();

        Assert.Equal(Drawing(BzGlyphs.CircleCheck), Drawing(Raise(cut, () => _toasts.Success("Saved"))));
        Assert.Equal(Drawing(BzGlyphs.InfoCircle), Drawing(Raise(cut, () => _toasts.Info("Heads up"))));
        Assert.Equal(Drawing(BzGlyphs.AlertTriangle), Drawing(Raise(cut, () => _toasts.Warning("Careful"))));
        Assert.Equal(Drawing(BzGlyphs.AlertCircle), Drawing(Raise(cut, () => _toasts.Error("Failed"))));
        Assert.Equal(
            ["error", "info", "success", "warning"],
            cut.FindAll("li[data-bz-toast]").Select(toast => toast.GetAttribute("data-bz-toast-type")).Order());
    }

    [Fact]
    public void An_icon_of_its_own_replaces_the_kind_icon_and_ShowIcon_off_removes_it()
    {
        var cut = Provider();
        RenderFragment bell = builder => builder.AddMarkupContent(0, "<i id=\"bell\"></i>");

        var custom = Raise(cut, () => _toasts.Success("Saved", new ToastOptions { Icon = bell }));
        Assert.NotNull(custom.QuerySelector(".bz-toast-icon #bell"));
        Assert.Null(custom.QuerySelector(".bz-toast-icon svg"));

        // A plain toast shows an icon once it is given one.
        var plain = Raise(cut, () => _toasts.Show("Reminder", new ToastOptions { Icon = bell }));
        Assert.NotNull(plain.QuerySelector(".bz-toast-icon #bell"));

        var none = Raise(cut, () => _toasts.Error("Failed", new ToastOptions { ShowIcon = false }));
        Assert.Null(none.QuerySelector(".bz-toast-icon"));
    }

    [Fact]
    public void A_loading_toast_spins_and_has_no_close_button_even_when_every_toast_gets_one()
    {
        var cut = Provider(p => p.Add(x => x.CloseButton, true));

        var toast = Raise(cut, () => _toasts.Loading("Uploading"));

        var spinner = toast.QuerySelector(".bz-toast-icon .bz-toast-spinner")!;
        Assert.Equal("true", spinner.GetAttribute("aria-hidden"));
        Assert.Equal(12, spinner.QuerySelectorAll(".bz-toast-spinner-bar").Length);
        Assert.Null(toast.QuerySelector("[data-close-button]"));
    }

    [Fact]
    public void The_spinner_can_be_replaced_for_every_loading_toast()
    {
        var cut = Provider(p => p.Add(x => x.LoadingIcon, "<i id=\"own-spinner\"></i>"));

        var toast = Raise(cut, () => _toasts.Loading("Uploading"));

        Assert.NotNull(toast.QuerySelector(".bz-toast-icon #own-spinner"));
        Assert.Null(toast.QuerySelector(".bz-toast-spinner"));
    }

    [Fact]
    public void The_action_runs_its_handler_and_dismisses_the_toast()
    {
        var cut = Provider();
        var undone = 0;
        var toast = Raise(cut, () => _toasts.Show("Message archived", new ToastOptions { Action = new("Undo", () => undone++) }));

        var action = toast.QuerySelector("button[data-action]")!;
        Assert.Equal("Undo", action.TextContent.Trim());
        action.Click();

        Assert.Equal(1, undone);
        Assert.True(_store.Toasts.Single().DismissRequested);
        Assert.Equal("true", cut.Find("li[data-bz-toast]").GetAttribute("data-bz-toast-dismiss"));
    }

    [Fact]
    public void Cancel_runs_its_handler_and_dismisses_a_toast_that_can_be_dismissed()
    {
        var cut = Provider();
        var cancelled = 0;
        var toast = Raise(cut, () => _toasts.Show("Delete project?", new ToastOptions
        {
            Cancel = new("Keep", () => cancelled++),
            Action = new("Delete", () => { }),
        }));

        // Cancel comes before the action, in the row and for the Tab key.
        Assert.Equal(["Keep", "Delete"], toast.QuerySelectorAll("button").Select(button => button.TextContent.Trim()));
        toast.QuerySelector("button[data-cancel]")!.Click();

        Assert.Equal(1, cancelled);
        Assert.True(_store.Toasts.Single().DismissRequested);
    }

    [Fact]
    public void Cancel_on_a_toast_that_cannot_be_dismissed_runs_its_handler_and_leaves_the_toast()
    {
        var cut = Provider();
        var cancelled = 0;
        var toast = Raise(cut, () => _toasts.Show("Sync required", new ToastOptions
        {
            Dismissible = false,
            Cancel = new("Later", () => cancelled++),
        }));
        Assert.Equal("false", toast.GetAttribute("data-dismissible"));

        toast.QuerySelector("button[data-cancel]")!.Click();

        Assert.Equal(1, cancelled);
        Assert.False(_store.Toasts.Single().DismissRequested);
    }

    [Fact]
    public void A_close_button_is_shown_when_the_provider_or_the_toast_asks_for_one()
    {
        var plain = Provider();
        Assert.Null(Raise(plain, () => _toasts.Show("One")).QuerySelector("[data-close-button]"));
        var asked = Raise(plain, () => _toasts.Show("Two", new ToastOptions { CloseButton = true }));
        Assert.Equal("Close notification", asked.QuerySelector("[data-close-button]")!.GetAttribute("aria-label"));
    }

    [Fact]
    public void A_toast_can_decline_the_close_button_every_toast_gets()
    {
        var cut = Provider(p => p.Add(x => x.CloseButton, true));

        Assert.NotNull(Raise(cut, () => _toasts.Show("One")).QuerySelector("[data-close-button]"));
        Assert.Null(Raise(cut, () => _toasts.Show("Two", new ToastOptions { CloseButton = false })).QuerySelector("[data-close-button]"));
    }

    [Fact]
    public void The_close_button_is_last_in_the_toast_and_dismisses_only_its_own()
    {
        var cut = Provider(p => p.Add(x => x.CloseButton, true));
        Raise(cut, () => _toasts.Show("First", new ToastOptions { Id = "first" }));
        var second = Raise(cut, () => _toasts.Show("Second", new ToastOptions { Id = "second", Action = new("Open", () => { }) }));

        Assert.True(second.LastElementChild!.HasAttribute("data-close-button"));
        second.QuerySelector("[data-close-button]")!.Click();

        Assert.Equal(
            [("first", false), ("second", true)],
            _store.Toasts.Select(toast => (toast.Id, toast.DismissRequested)));
    }

    [Fact]
    public void Position_count_and_colors_set_on_the_provider_apply_to_every_toast()
    {
        var cut = Provider(p => p
            .Add(x => x.Position, ToastPosition.TopCenter)
            .Add(x => x.VisibleToasts, 5)
            .Add(x => x.RichColors, true)
            .Add(x => x.Duration, TimeSpan.FromSeconds(9)));

        var toast = Raise(cut, () => _toasts.Success("Saved"));

        var list = toast.ParentElement!;
        Assert.Equal(("top", "center", "5"), (list.GetAttribute("data-y-position"), list.GetAttribute("data-x-position"), list.GetAttribute("data-bz-visible-toasts")));
        Assert.Equal("true", toast.GetAttribute("data-rich-colors"));
        Assert.Equal("9000", toast.GetAttribute("data-bz-toast-duration"));
    }
}
