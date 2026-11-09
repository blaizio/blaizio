using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The words the shared controls say on their own: each has a parameter, English by default. The
/// date and time fields, the calendar, the sheet, drawer and panel close buttons, and the carousel,
/// sortable and tree wrappers that hand their labels and announcements to the Base parts.
/// </summary>
public class ControlLabelsTests : BunitContext
{
    public ControlLabelsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    // ---- BzInputDate --------------------------------------------------------------------------

    [Fact]
    public void InputDate_names_its_parts_in_English_by_default()
    {
        var cut = Render<BzInputDate>(p => p
            .Add(x => x.ShowCalendar, true)
            .Add(x => x.DefaultCalendarOpen, true)
            .Add(x => x.DefaultValue, new DateOnly(2026, 11, 8)));

        Assert.Equal(new[] { "Year", "Month", "Day" }, cut.FindAll("[data-slot=input-date-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.NotNull(cut.Find("button[aria-label='Open calendar']"));
        Assert.Equal("Calendar", cut.Find("[data-slot=popover-content]").GetAttribute("aria-label"));
        Assert.Equal("Go to the previous month", cut.Find("[data-slot=calendar-button-previous]").GetAttribute("aria-label"));
        Assert.Equal("Go to the next month", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-label"));
    }

    [Fact]
    public void InputDate_takes_every_name_from_its_parameters()
    {
        var cut = Render<BzInputDate>(p => p
            .Add(x => x.Format, "dd/MM/yyyy")
            .Add(x => x.ShowCalendar, true)
            .Add(x => x.DefaultCalendarOpen, true)
            .Add(x => x.DefaultValue, new DateOnly(2026, 11, 8))
            .Add(x => x.DayAriaLabel, "اليوم")
            .Add(x => x.MonthAriaLabel, "الشهر")
            .Add(x => x.YearAriaLabel, "السنة")
            .Add(x => x.CalendarButtonAriaLabel, "فتح التقويم")
            .Add(x => x.CalendarAriaLabel, "التقويم")
            .Add(x => x.PreviousMonthAriaLabel, "الشهر السابق")
            .Add(x => x.NextMonthAriaLabel, "الشهر التالي"));

        Assert.Equal(new[] { "اليوم", "الشهر", "السنة" }, cut.FindAll("[data-slot=input-date-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.NotNull(cut.Find("button[aria-label='فتح التقويم']"));
        Assert.Equal("التقويم", cut.Find("[data-slot=popover-content]").GetAttribute("aria-label"));
        Assert.Equal("الشهر السابق", cut.Find("[data-slot=calendar-button-previous]").GetAttribute("aria-label"));
        Assert.Equal("الشهر التالي", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-label"));
        Assert.DoesNotContain("aria-label=\"Day\"", cut.Markup);
    }

    // ---- BzInputTime --------------------------------------------------------------------------

    [Fact]
    public void InputTime_names_its_segments_in_English_by_default()
    {
        var cut = Render<BzInputTime>(p => p
            .Add(x => x.HourCycle, InputTimeHourCycle.TwelveHour)
            .Add(x => x.ShowSeconds, true)
            .Add(x => x.DefaultValue, new TimeOnly(9, 30)));

        Assert.Equal(new[] { "Hour", "Minute", "Second" }, cut.FindAll("[data-slot=input-time-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.Equal("AM", cut.Find("button[aria-label='AM or PM']").TextContent.Trim());
    }

    [Fact]
    public void InputTime_takes_its_names_and_AM_PM_text_from_its_parameters()
    {
        var cut = Render<BzInputTime>(p => p
            .Add(x => x.HourCycle, InputTimeHourCycle.TwelveHour)
            .Add(x => x.ShowSeconds, true)
            .Add(x => x.DefaultValue, new TimeOnly(9, 30))
            .Add(x => x.HourAriaLabel, "الساعة")
            .Add(x => x.MinuteAriaLabel, "الدقيقة")
            .Add(x => x.SecondAriaLabel, "الثانية")
            .Add(x => x.MeridiemAriaLabel, "صباحا أو مساء")
            .Add(x => x.AmLabel, "ص")
            .Add(x => x.PmLabel, "م"));

        Assert.Equal(new[] { "الساعة", "الدقيقة", "الثانية" }, cut.FindAll("[data-slot=input-time-input] input").Select(i => i.GetAttribute("aria-label")));
        var toggle = cut.Find("button[aria-label='صباحا أو مساء']");
        Assert.Equal("ص", toggle.TextContent.Trim());

        toggle.KeyDown(new KeyboardEventArgs { Key = "p" });

        Assert.Equal("م", cut.Find("button[aria-label='صباحا أو مساء']").TextContent.Trim());
    }

    [Fact]
    public void InputTime_AM_PM_toggle_flips_on_a_click()
    {
        TimeOnly? value = new TimeOnly(9, 30);
        var cut = Render<BzInputTime>(p => p
            .Add(x => x.HourCycle, InputTimeHourCycle.TwelveHour)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, (TimeOnly? v) => value = v));

        cut.Find("button[aria-label='AM or PM']").Click();

        Assert.Equal(new TimeOnly(21, 30), value);
        Assert.Equal("PM", cut.Find("button[aria-label='AM or PM']").TextContent.Trim());
    }

    // ---- BzCalendar ---------------------------------------------------------------------------

    [Fact]
    public void Calendar_passes_its_week_and_month_button_names_to_the_grid()
    {
        var cut = Render<BzCalendar>(p => p
            .Add(x => x.DefaultMonth, new DateOnly(2026, 11, 1))
            .Add(x => x.ShowWeekNumber, true)
            .Add(x => x.WeekNumberLabel, "الأسبوع")
            .Add(x => x.PreviousMonthAriaLabel, "الشهر السابق")
            .Add(x => x.NextMonthAriaLabel, "الشهر التالي"));

        Assert.Equal("الأسبوع", cut.Find("[data-slot=calendar-week-number-header]").TextContent.Trim());
        Assert.Equal("الشهر السابق", cut.Find("[data-slot=calendar-button-previous]").GetAttribute("aria-label"));
        Assert.Equal("الشهر التالي", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-label"));
    }

    // ---- close buttons ------------------------------------------------------------------------

    [Fact]
    public void Sheet_close_button_takes_its_name_from_CloseLabel()
    {
        var english = Render<BzSheet>(p => p.Add(x => x.Open, true).AddChildContent<BzSheetContent>());
        english.WaitForAssertion(() => Assert.Equal("Close", english.Find(".bz-sheet-close .sr-only").TextContent));

        var arabic = Render<BzSheet>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BzSheetContent>(c => c.Add(x => x.CloseLabel, "إغلاق")));
        arabic.WaitForAssertion(() => Assert.Equal("إغلاق", arabic.Find(".bz-sheet-close .sr-only").TextContent));
    }

    [Fact]
    public void Drawer_close_button_takes_its_name_from_CloseLabel()
    {
        var english = Render<BzDrawer>(p => p.Add(x => x.Open, true).AddChildContent<BzDrawerContent>());
        english.WaitForAssertion(() => Assert.Equal("Close", english.Find(".bz-drawer-close .sr-only").TextContent));

        var arabic = Render<BzDrawer>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BzDrawerContent>(c => c.Add(x => x.CloseLabel, "إغلاق")));
        arabic.WaitForAssertion(() => Assert.Equal("إغلاق", arabic.Find(".bz-drawer-close .sr-only").TextContent));
    }

    [Fact]
    public void A_sheet_shown_from_the_dialog_service_takes_CloseLabel_from_its_options()
    {
        var cut = Render<BzDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();

        _ = cut.InvokeAsync(() => dialogs.ShowSheetAsync(
            _ => b => b.AddContent(0, "Filters"), options: new BzDialogOptions { CloseLabel = "إغلاق" }));

        cut.WaitForAssertion(() => Assert.Equal("إغلاق", cut.Find(".bz-sheet-close .sr-only").TextContent));
    }

    [Fact]
    public void A_dialog_shown_from_the_dialog_service_takes_CloseLabel_from_its_options()
    {
        var cut = Render<BzDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();

        _ = cut.InvokeAsync(() => dialogs.ShowAsync(
            _ => b => b.AddContent(0, "Body"), new BzDialogOptions { CloseLabel = "إغلاق" }));

        cut.WaitForAssertion(() => Assert.Equal("إغلاق", cut.Find(".bz-dialog-close .sr-only").TextContent));
    }

    [Fact]
    public void Panel_close_button_takes_its_name_from_Label()
    {
        Assert.Equal("Close panel", Render<BzPanelClose>().Find(".sr-only").TextContent);
        Assert.Equal("إغلاق اللوحة", Render<BzPanelClose>(p => p.Add(x => x.Label, "إغلاق اللوحة")).Find(".sr-only").TextContent);
    }

    // ---- carousel -----------------------------------------------------------------------------

    [Fact]
    public async Task Carousel_words_its_region_slides_and_dots_from_its_parameters()
    {
        RenderFragment content = b =>
        {
            b.OpenComponent<BzCarouselContent>(0);
            b.AddAttribute(1, nameof(BzCarouselContent.ChildContent), (RenderFragment)(slides =>
            {
                for (var i = 0; i < 3; i++)
                {
                    slides.OpenComponent<BzCarouselItem>(0);
                    slides.CloseComponent();
                }
            }));
            b.CloseComponent();
            b.OpenComponent<BzCarouselDots>(2);
            b.AddAttribute(3, nameof(BzCarouselDots.DotAriaLabel), (Func<int, string>)(n => $"الانتقال إلى الشريحة {n}"));
            b.CloseComponent();
        };
        var cut = Render<BzCarousel>(p => p
            .Add(x => x.AriaLabel, "منتجات")
            .Add(x => x.RoleDescription, "دوار")
            .Add(x => x.SlideRoleDescription, "شريحة")
            .Add(x => x.SlideAriaLabel, slide => $"{slide.Position} من {slide.Count}")
            .AddChildContent(content));

        // The dots appear once the scroll engine reports the slide count.
        var engine = cut.FindComponent<BaseCarousel>().Instance;
        await cut.InvokeAsync(() => engine.OnScrollState(0, false, true, 3));

        Assert.Equal("دوار", cut.Find("[data-slot=carousel]").GetAttribute("aria-roledescription"));
        var slides = cut.FindAll("[data-slot=carousel-item]");
        Assert.All(slides, s => Assert.Equal("شريحة", s.GetAttribute("aria-roledescription")));
        Assert.Equal(new[] { "1 من 3", "2 من 3", "3 من 3" }, slides.Select(s => s.GetAttribute("aria-label")));
        Assert.Equal(
            new[] { "الانتقال إلى الشريحة 1", "الانتقال إلى الشريحة 2", "الانتقال إلى الشريحة 3" },
            cut.FindAll("[data-slot=carousel-dots] button").Select(d => d.GetAttribute("aria-label")));
    }

    [Fact]
    public async Task Carousel_dots_are_named_in_English_by_default()
    {
        RenderFragment content = b =>
        {
            b.OpenComponent<BzCarouselContent>(0);
            b.AddAttribute(1, nameof(BzCarouselContent.ChildContent), (RenderFragment)(slides =>
            {
                for (var i = 0; i < 2; i++)
                {
                    slides.OpenComponent<BzCarouselItem>(0);
                    slides.CloseComponent();
                }
            }));
            b.CloseComponent();
            b.OpenComponent<BzCarouselDots>(2);
            b.CloseComponent();
        };
        var cut = Render<BzCarousel>(p => p.Add(x => x.AriaLabel, "Products").AddChildContent(content));
        var engine = cut.FindComponent<BaseCarousel>().Instance;
        await cut.InvokeAsync(() => engine.OnScrollState(0, false, true, 2));

        Assert.Equal(new[] { "Go to slide 1", "Go to slide 2" },
            cut.FindAll("[data-slot=carousel-dots] button").Select(d => d.GetAttribute("aria-label")));
    }

    // ---- sortable -----------------------------------------------------------------------------

    [Fact]
    public async Task Sortable_hands_its_role_description_and_announcement_to_the_list()
    {
        RenderFragment items = b =>
        {
            for (var i = 0; i < 3; i++)
            {
                b.OpenComponent<BzSortableItem>(0);
                b.AddAttribute(1, nameof(BzSortableItem.Index), i);
                b.CloseComponent();
            }
        };
        var cut = Render<BzSortable>(p => p
            .Add(x => x.ItemRoleDescription, "عنصر")
            .Add(x => x.Announcement, a => $"{a.Kind} {a.Position}/{a.Count}")
            .AddChildContent(items));

        Assert.All(cut.FindAll("[data-sortable-item]"), i => Assert.Equal("عنصر", i.GetAttribute("aria-roledescription")));

        var id = cut.FindAll("[data-sortable-item]")[1].GetAttribute("data-sortable-item-id")!;
        var list = cut.FindComponent<BaseSortable>().Instance;
        await cut.InvokeAsync(() => list.NotifyItemKeyDown(id, " "));

        Assert.Equal("Grabbed 2/3", cut.Find("[data-slot=sortable] [role=status]").TextContent.Trim());
    }

    [Fact]
    public void SortableList_hands_its_role_description_to_the_items()
    {
        var cut = Render<BzSortableList<string>>(p => p
            .Add(x => x.Items, ["a", "b"])
            .Add(x => x.ItemContent, item => b => b.AddContent(0, item))
            .Add(x => x.ItemRoleDescription, "عنصر"));

        Assert.All(cut.FindAll("[data-sortable-item]"), i => Assert.Equal("عنصر", i.GetAttribute("aria-roledescription")));
    }

    // ---- tree ---------------------------------------------------------------------------------

    private sealed record Folder(string Name, List<Folder>? Children = null);

    [Fact]
    public void Tree_hands_its_rename_name_and_announcement_to_the_base()
    {
        var cut = Render<BzTree<Folder>>(p => p
            .Add(x => x.Items, new List<Folder> { new("Reports"), new("Invoices") })
            .Add(x => x.ValueSelector, f => f.Name)
            .Add(x => x.TextSelector, f => f.Name)
            .Add(x => x.ChildrenSelector, f => f.Children)
            .Add(x => x.Renamable, true)
            .Add(x => x.OnRename, (TreeRename<Folder> _) => { })
            .Add(x => x.RenameAriaLabel, "إعادة التسمية")
            .Add(x => x.Announcement, a => $"{a.Kind}: {a.Node}"));

        cut.Find("[data-part=item][data-value=Reports]").KeyDown(new KeyboardEventArgs { Key = "F2" });
        Assert.Equal("إعادة التسمية", cut.Find("[data-part=rename-input]").GetAttribute("aria-label"));

        cut.Find("[data-part=rename-input]").Input("Archive");
        cut.Find("[data-part=rename-input]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Renamed: Archive", cut.Find("[role=status]").TextContent.Trim());
    }
}
