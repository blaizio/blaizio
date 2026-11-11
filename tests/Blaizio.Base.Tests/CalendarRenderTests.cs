using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + interaction tests for the headless calendar. The grid keyboard navigation is JS (it only
/// preventDefaults the nav keys; verified in-browser), so these cover the C# contract: the month
/// matrix, the weekday order, the three selection modes and their modifiers (today / outside / range
/// edges / disabled), the bounds, and month paging. JSInterop is Loose so the module import is a no-op.
/// </summary>
public class CalendarRenderTests : BunitContext
{
    public CalendarRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // A fixed "today" and culture so day labels, captions and the grid are deterministic.
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private IRenderedComponent<BaseCalendar> Render(Action<ComponentParameterCollectionBuilder<BaseCalendar>>? extra = null) =>
        Render<BaseCalendar>(p =>
        {
            p.Add(x => x.Today, Today);
            p.Add(x => x.Culture, Inv);
            p.Add(x => x.DefaultMonth, new DateOnly(2026, 6, 1));
            extra?.Invoke(p);
        });

    private static IElement Day(IRenderedComponent<BaseCalendar> cut, DateOnly date) =>
        cut.FindAll("[data-slot=calendar-day-button]")
            .Single(b => b.GetAttribute("aria-label") == date.ToString("D", Inv));

    [Fact]
    public void Renders_a_single_month_with_seven_weekday_headers()
    {
        var cut = Render();

        Assert.Equal("calendar", cut.Find("[data-slot=calendar]").GetAttribute("data-slot"));
        Assert.Equal(7, cut.FindAll("[data-slot=calendar-weekday]").Count);
        Assert.Equal("June 2026", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());
    }

    [Fact]
    public void Day_name_uses_the_digits_the_cell_shows()
    {
        var cut = Render<BaseCalendar>(p => p
            .Add(x => x.Today, Today)
            .Add(x => x.Culture, new CultureInfo("ar-EG"))
            .Add(x => x.DefaultMonth, new DateOnly(2026, 6, 1))
            .Add(x => x.NativeDigits, true));

        var day = cut.FindAll("[data-slot=calendar-day-button]")[10];

        Assert.Contains(day.TextContent.Trim(), day.GetAttribute("aria-label"));
    }

    [Fact]
    public void WeekStartsOn_orders_the_weekday_headers()
    {
        var cut = Render(p => p.Add(x => x.WeekStartsOn, DayOfWeek.Monday));

        Assert.Equal("Mo", cut.FindAll("[data-slot=calendar-weekday]")[0].TextContent.Trim());
    }

    [Fact]
    public void Single_click_selects_a_day()
    {
        var cut = Render();

        Day(cut, new DateOnly(2026, 6, 10)).Click();

        var button = Day(cut, new DateOnly(2026, 6, 10));
        Assert.True(button.HasAttribute("data-selected"));
        Assert.True(button.HasAttribute("data-selected-single"));
    }

    [Fact]
    public void Controlled_announces_the_pick_without_self_updating()
    {
        DateOnly? changed = null;
        var cut = Render(p => p
            .Add(x => x.Value, (DateOnly?)null)
            .Add(x => x.ValueChanged, (DateOnly? v) => changed = v));

        Day(cut, new DateOnly(2026, 6, 10)).Click();

        Assert.Equal(new DateOnly(2026, 6, 10), changed);                              // the pick was announced
        Assert.False(Day(cut, new DateOnly(2026, 6, 10)).HasAttribute("data-selected")); // but state stays controlled
    }

    [Fact]
    public void Single_click_on_the_selected_day_clears_it()
    {
        var cut = Render(p => p.Add(x => x.DefaultValue, new DateOnly(2026, 6, 10)));

        Day(cut, new DateOnly(2026, 6, 10)).Click();

        Assert.False(Day(cut, new DateOnly(2026, 6, 10)).HasAttribute("data-selected"));
    }

    [Fact]
    public void Multiple_mode_toggles_days_independently()
    {
        var cut = Render(p => p.Add(x => x.Mode, CalendarMode.Multiple));

        Day(cut, new DateOnly(2026, 6, 10)).Click();
        Day(cut, new DateOnly(2026, 6, 12)).Click();

        Assert.True(Day(cut, new DateOnly(2026, 6, 10)).HasAttribute("data-selected"));
        Assert.True(Day(cut, new DateOnly(2026, 6, 12)).HasAttribute("data-selected"));

        Day(cut, new DateOnly(2026, 6, 10)).Click(); // toggle the first back off
        Assert.False(Day(cut, new DateOnly(2026, 6, 10)).HasAttribute("data-selected"));
        Assert.True(Day(cut, new DateOnly(2026, 6, 12)).HasAttribute("data-selected"));
    }

    [Fact]
    public void Range_mode_marks_start_middle_and_end()
    {
        var cut = Render(p => p.Add(x => x.Mode, CalendarMode.Range));

        Day(cut, new DateOnly(2026, 6, 10)).Click();
        Day(cut, new DateOnly(2026, 6, 12)).Click();

        Assert.True(Day(cut, new DateOnly(2026, 6, 10)).HasAttribute("data-range-start"));
        Assert.True(Day(cut, new DateOnly(2026, 6, 11)).HasAttribute("data-range-middle"));
        Assert.True(Day(cut, new DateOnly(2026, 6, 12)).HasAttribute("data-range-end"));
    }

    [Fact]
    public void Range_mode_backwards_pick_swaps_to_run_forward()
    {
        var cut = Render(p => p.Add(x => x.Mode, CalendarMode.Range));

        Day(cut, new DateOnly(2026, 6, 12)).Click();
        Day(cut, new DateOnly(2026, 6, 10)).Click(); // ends before the start - the edges swap

        Assert.True(Day(cut, new DateOnly(2026, 6, 10)).HasAttribute("data-range-start"));
        Assert.True(Day(cut, new DateOnly(2026, 6, 11)).HasAttribute("data-range-middle"));
        Assert.True(Day(cut, new DateOnly(2026, 6, 12)).HasAttribute("data-range-end"));
    }

    [Fact]
    public void Range_mode_third_click_starts_over()
    {
        var cut = Render(p => p.Add(x => x.Mode, CalendarMode.Range));

        Day(cut, new DateOnly(2026, 6, 10)).Click();
        Day(cut, new DateOnly(2026, 6, 12)).Click();
        Day(cut, new DateOnly(2026, 6, 20)).Click(); // a complete range exists, so this restarts

        Assert.False(Day(cut, new DateOnly(2026, 6, 11)).HasAttribute("data-range-middle"));
        Assert.True(Day(cut, new DateOnly(2026, 6, 20)).HasAttribute("data-selected"));
    }

    [Fact]
    public void Today_is_marked()
    {
        var cut = Render();

        Assert.True(Day(cut, Today).HasAttribute("data-today"));
    }

    [Fact]
    public void Outside_days_are_marked_and_can_be_hidden()
    {
        // June 2026 starts on a Monday; with a Sunday week start the grid leads with 31 May (outside).
        var withOutside = Render(p => p.Add(x => x.WeekStartsOn, DayOfWeek.Sunday));
        Assert.Contains(withOutside.FindAll("[data-slot=calendar-day-button]"),
            b => b.HasAttribute("data-outside"));

        var hidden = Render(p => p
            .Add(x => x.WeekStartsOn, DayOfWeek.Sunday)
            .Add(x => x.ShowOutsideDays, false));
        Assert.DoesNotContain(hidden.FindAll("[data-slot=calendar-day-button]"),
            b => b.HasAttribute("data-outside"));
    }

    [Fact]
    public void Min_and_Max_disable_out_of_range_days_and_block_selection()
    {
        var cut = Render(p => p
            .Add(x => x.Min, new DateOnly(2026, 6, 10))
            .Add(x => x.Max, new DateOnly(2026, 6, 20)));

        var early = Day(cut, new DateOnly(2026, 6, 5));
        Assert.Equal("true", early.GetAttribute("aria-disabled"));

        early.Click();
        Assert.False(Day(cut, new DateOnly(2026, 6, 5)).HasAttribute("data-selected"));
    }

    [Fact]
    public void DisabledSelector_disables_individual_days()
    {
        // 13 June 2026 is a Saturday.
        var cut = Render(p => p.Add(x => x.DisabledSelector,
            (Func<DateOnly, bool>)(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)));

        Assert.Equal("true", Day(cut, new DateOnly(2026, 6, 13)).GetAttribute("aria-disabled"));
        Assert.Null(Day(cut, new DateOnly(2026, 6, 12)).GetAttribute("aria-disabled")); // Friday stays enabled
    }

    [Fact]
    public void Nav_buttons_page_the_month()
    {
        var cut = Render();

        cut.Find("[data-slot=calendar-button-previous]").Click();
        Assert.Equal("May 2026", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());

        cut.Find("[data-slot=calendar-button-next]").Click();
        cut.Find("[data-slot=calendar-button-next]").Click();
        Assert.Equal("July 2026", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());
    }

    [Fact]
    public void NumberOfMonths_renders_several_grids()
    {
        var cut = Render(p => p.Add(x => x.NumberOfMonths, 2));

        var captions = cut.FindAll("[data-slot=calendar-caption-label]");
        Assert.Equal(2, captions.Count);
        Assert.Equal("June 2026", captions[0].TextContent.Trim());
        Assert.Equal("July 2026", captions[1].TextContent.Trim());
    }

    [Fact]
    public void Disabled_calendar_marks_the_root_and_blocks_selection()
    {
        var cut = Render(p => p.Add(x => x.Disabled, true));

        Assert.True(cut.Find("[data-slot=calendar]").HasAttribute("data-disabled"));

        Day(cut, new DateOnly(2026, 6, 10)).Click();
        Assert.False(Day(cut, new DateOnly(2026, 6, 10)).HasAttribute("data-selected"));
    }

    // A calendar rendered in a specific culture, anchored on 26 June 2026 (= 4 Tir 1405 / 11 Muharram 1448).
    private IRenderedComponent<BaseCalendar> RenderCulture(string culture, Action<ComponentParameterCollectionBuilder<BaseCalendar>>? extra = null) =>
        Render<BaseCalendar>(p =>
        {
            p.Add(x => x.Today, Today);
            p.Add(x => x.Culture, new CultureInfo(culture));
            p.Add(x => x.DefaultMonth, Today);
            extra?.Invoke(p);
        });

    [Fact]
    public void German_culture_localizes_the_caption_and_starts_the_week_on_monday()
    {
        var cut = RenderCulture("de-DE");

        Assert.Equal("Juni 2026", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());
        // The German week starts on Monday (the abbr is the unambiguous full name; the shortest name is "M").
        Assert.Equal("Montag", cut.FindAll("[data-slot=calendar-weekday]")[0].GetAttribute("abbr"));
    }

    [Fact]
    public void Persian_culture_lays_out_the_jalali_calendar_in_native_script()
    {
        var cut = RenderCulture("fa-IR");

        // 15 June 2026 = 25 Khordad 1405 - a native Persian month name, year in Persian digits, not Gregorian.
        var caption = cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim();
        Assert.Contains("خرداد", caption);                                          // Khordad, the 3rd Persian month
        Assert.Contains(CalendarSystem.Digits(1405, new CultureInfo("fa-IR")), caption); // "۱۴۰۵"
        Assert.DoesNotContain("2026", caption);
        // The Persian week starts on Saturday.
        Assert.Equal("شنبه", cut.FindAll("[data-slot=calendar-weekday]")[0].GetAttribute("abbr"));
        // Day cells render Persian digits (۰-۹), not Latin.
        Assert.Contains(cut.FindAll("[data-slot=calendar-day-button]"),
            b => b.TextContent.Any(ch => ch is >= '۰' and <= '۹'));
    }

    [Fact]
    public void Hijri_culture_lays_out_the_umm_al_qura_calendar_in_native_script()
    {
        var cut = RenderCulture("ar-SA");

        // 15 June 2026 = Dhu al-Hijjah 1447 - a native Arabic month name, year in Arabic-Indic digits.
        var caption = cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim();
        Assert.Contains("ذو الحجة", caption);                                       // Dhu al-Hijjah, the 12th Hijri month
        Assert.Contains(CalendarSystem.Digits(1447, new CultureInfo("ar-SA")), caption); // "١٤٤٧"
        Assert.DoesNotContain("2026", caption);
    }

    [Fact]
    public void Week_numbers_render_a_leading_column()
    {
        var cut = Render(p => p.Add(x => x.ShowWeekNumber, true));

        var weekNumbers = cut.FindAll("[data-slot=calendar-week-number]");
        var weekRows = cut.FindAll("[data-slot=calendar-week]");
        Assert.NotEmpty(weekNumbers);
        Assert.Equal(weekRows.Count, weekNumbers.Count); // one per week row
        Assert.NotNull(cut.Find("[data-slot=calendar-week-number-header]"));
    }

    // A MonthCaption template that captures the caption context (so a test can read its option lists
    // and fire its select callbacks) and renders nothing of its own.
    private static RenderFragment<CalendarCaptionContext> CaptureCaption(Action<CalendarCaptionContext> capture) =>
        ctx => builder => capture(ctx);

    [Fact]
    public void Dropdown_caption_without_a_template_falls_back_to_the_label()
    {
        // The headless calendar renders NO native <select>; the dropdown layouts render through the styled
        // layer's MonthCaption template (which uses BzSelect). Bare, it degrades to the plain caption label.
        var cut = Render(p => p.Add(x => x.CaptionLayout, CalendarCaptionLayout.Dropdown));

        Assert.Empty(cut.FindAll("select"));
        Assert.Equal("June 2026", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());
    }

    [Fact]
    public void Year_caption_callback_navigates_to_the_chosen_year()
    {
        CalendarCaptionContext? caption = null;
        var cut = Render(p => p
            .Add(x => x.CaptionLayout, CalendarCaptionLayout.Dropdown)
            .Add(x => x.FromYear, 2020)
            .Add(x => x.ToYear, 2030)
            .Add(x => x.MonthCaption, CaptureCaption(c => caption = c)));

        Assert.NotNull(caption);
        Assert.Equal(11, caption!.Years.Count); // 2020..2030 inclusive

        cut.InvokeAsync(() => caption!.YearSelected.InvokeAsync(2028));

        // The grid now shows June 2028 - every in-month day's label carries that year.
        Assert.Contains(cut.FindAll("[data-slot=calendar-day-button]"),
            b => b.GetAttribute("aria-label")?.Contains("2028") == true);
    }

    [Fact]
    public void Month_caption_callback_navigates_to_the_chosen_month()
    {
        CalendarCaptionContext? caption = null;
        var cut = Render(p => p
            .Add(x => x.CaptionLayout, CalendarCaptionLayout.DropdownMonths)
            .Add(x => x.MonthCaption, CaptureCaption(c => caption = c)));

        cut.InvokeAsync(() => caption!.MonthSelected.InvokeAsync(1)); // January

        Assert.Contains(cut.FindAll("[data-slot=calendar-day-button]"),
            b => b.GetAttribute("aria-label")?.Contains("January") == true);
    }

    [Fact]
    public void DayContent_renders_custom_cell_content()
    {
        var cut = Render(p => p.Add(x => x.DayContent, (RenderFragment<CalendarDayContext>)(day =>
            builder => builder.AddMarkupContent(0, $"<span>#{day.DayLabel}</span>"))));

        var day = cut.FindAll("[data-slot=calendar-day-button]")
            .Single(b => b.TextContent.Contains("#10"));

        Assert.Null(day.GetAttribute("aria-label"));
        Assert.Contains(new DateOnly(2026, 6, 10).ToString("D", Inv), day.TextContent);
    }

    [Fact]
    public void MonthCaption_template_replaces_the_native_dropdowns()
    {
        var cut = Render(p => p
            .Add(x => x.CaptionLayout, CalendarCaptionLayout.Dropdown)
            .Add(x => x.MonthCaption, (RenderFragment<CalendarCaptionContext>)(cap =>
                builder => builder.AddMarkupContent(0, $"<div data-testid=\"cap\">{cap.MonthName}-{cap.Year}-{cap.Months.Count}</div>"))));

        Assert.Empty(cut.FindAll("select"));                       // the native selects are gone
        Assert.Equal("June-2026-12", cut.Find("[data-testid=cap]").TextContent); // template got the data
    }

    // ---- labels ---------------------------------------------------------------------------------

    [Fact]
    public void Month_buttons_are_named_in_English_by_default()
    {
        var cut = Render();

        Assert.Equal("Go to the previous month", cut.Find("[data-slot=calendar-button-previous]").GetAttribute("aria-label"));
        Assert.Equal("Go to the next month", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Month_buttons_take_their_names_from_the_parameters()
    {
        var cut = Render(p => p
            .Add(x => x.NumberOfMonths, 2)
            .Add(x => x.PreviousMonthAriaLabel, "الشهر السابق")
            .Add(x => x.NextMonthAriaLabel, "الشهر التالي"));

        Assert.Equal("الشهر السابق", Assert.Single(cut.FindAll("[data-slot=calendar-button-previous]")).GetAttribute("aria-label"));
        Assert.Equal("الشهر التالي", Assert.Single(cut.FindAll("[data-slot=calendar-button-next]")).GetAttribute("aria-label"));
    }

    [Fact]
    public void Next_stops_at_the_month_Max_falls_in()
    {
        var cut = Render(p => p.Add(x => x.Max, new DateOnly(2026, 6, 20)));

        // June is showing and Max is in June: July would be a month of disabled days.
        Assert.Equal("true", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Next_is_available_while_Max_is_in_a_later_month()
    {
        var cut = Render(p => p.Add(x => x.Max, new DateOnly(2026, 7, 2)));

        Assert.NotEqual("true", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void The_grid_keeps_one_tab_stop_after_paging_away_from_the_focused_day()
    {
        var cut = Render();
        Day(cut, new DateOnly(2026, 6, 10)).Click(); // the roving focus now sits on 10 June

        cut.Find("[data-slot=calendar-button-next]").Click();
        cut.Find("[data-slot=calendar-button-next]").Click(); // August: 10 June is off screen

        var stops = cut.FindAll("[data-slot=calendar-day-button]").Count(b => b.GetAttribute("tabindex") == "0");
        Assert.Equal(1, stops);
    }

    private IRenderedComponent<BaseCalendar> RenderAt(DateOnly month, Action<ComponentParameterCollectionBuilder<BaseCalendar>>? extra = null) =>
        Render<BaseCalendar>(p =>
        {
            p.Add(x => x.Today, Today);
            p.Add(x => x.Culture, Inv);
            p.Add(x => x.DefaultMonth, month);
            extra?.Invoke(p);
        });

    private static string? TabStop(IRenderedComponent<BaseCalendar> cut) =>
        cut.FindAll("[data-slot=calendar-day-button]").Single(b => b.GetAttribute("tabindex") == "0").GetAttribute("aria-label");

    [Fact]
    public async Task Arrow_keys_start_from_the_tab_stop_after_paging_away_from_the_focused_day()
    {
        var cut = Render();
        Day(cut, new DateOnly(2026, 6, 10)).Click();

        cut.Find("[data-slot=calendar-button-next]").Click();
        cut.Find("[data-slot=calendar-button-next]").Click(); // August: the tab stop is now 1 August

        // The key arrives from the day that has focus, which is the tab stop. Starting from the
        // day focused last (10 June) sent the view back to June.
        await cut.InvokeAsync(() => cut.Instance.OnNavigate("ArrowRight", false));

        Assert.Equal("August 2026", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());
        Assert.Equal(new DateOnly(2026, 8, 2).ToString("D", Inv), TabStop(cut));
    }

    [Fact]
    public void The_last_month_a_calendar_can_show_renders()
    {
        // 31 December 9999 is a Friday: its week runs into year 10000, which no date can express.
        var cut = RenderAt(new DateOnly(9999, 12, 1));

        Assert.Equal("December 9999", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());
        Assert.NotNull(Day(cut, new DateOnly(9999, 12, 31)));
        Assert.Equal("true", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-disabled"));
        // The days past the end are empty cells, not buttons.
        Assert.Equal(7, cut.FindAll("[data-slot=calendar-week]").Last().QuerySelectorAll("td[data-slot=calendar-day]").Length);
        Assert.Equal(6, cut.FindAll("[data-slot=calendar-week]").Last().QuerySelectorAll("[data-slot=calendar-day-button]").Length);
    }

    [Fact]
    public void The_first_month_a_calendar_can_show_renders()
    {
        // 1 January 0001 is a Monday: with the week starting on Sunday there is no day before it.
        var cut = RenderAt(new DateOnly(1, 1, 1), p => p.Add(x => x.WeekStartsOn, DayOfWeek.Sunday));

        Assert.NotNull(Day(cut, new DateOnly(1, 1, 1)));
        Assert.Equal("true", cut.Find("[data-slot=calendar-button-previous]").GetAttribute("aria-disabled"));
        Assert.Equal(6, cut.FindAll("[data-slot=calendar-week]").First().QuerySelectorAll("[data-slot=calendar-day-button]").Length);
    }

    [Theory]
    [InlineData("ArrowRight")]
    [InlineData("ArrowDown")]
    [InlineData("End")]
    [InlineData("PageDown")]
    public async Task Keys_stop_at_the_last_day_instead_of_throwing(string key)
    {
        var cut = RenderAt(new DateOnly(9999, 12, 1));
        Day(cut, new DateOnly(9999, 12, 31)).Click();

        await cut.InvokeAsync(() => cut.Instance.OnNavigate(key, key == "PageDown"));

        Assert.Equal("December 9999", cut.Find("[data-slot=calendar-caption-label]").TextContent.Trim());
        Assert.Equal(new DateOnly(9999, 12, 31).ToString("D", Inv), TabStop(cut));
    }

    [Fact]
    public void Bounds_outside_the_culture_calendar_do_not_throw()
    {
        // Umm al-Qura covers 30 April 1900 to 16 November 2077; Min and Max here are both outside it.
        var cut = RenderCulture("ar-SA", p => p
            .Add(x => x.Min, new DateOnly(1800, 1, 1))
            .Add(x => x.Max, new DateOnly(2200, 1, 1)));

        Assert.Contains("ذو الحجة", cut.Find("[data-slot=calendar-caption-label]").TextContent);
    }

    [Fact]
    public void A_calendar_system_stops_at_its_own_ends()
    {
        var gregorian = new CalendarSystem(Inv);

        Assert.Equal(DateOnly.MaxValue, gregorian.AddDays(new DateOnly(9999, 12, 30), 7));
        Assert.Equal(DateOnly.MinValue, gregorian.AddDays(new DateOnly(1, 1, 3), -7));
        Assert.Equal(DateOnly.MaxValue, gregorian.AddMonths(new DateOnly(9999, 12, 1), 1));
        Assert.Equal(DateOnly.MinValue, gregorian.AddYears(new DateOnly(1, 6, 1), -1));

        var hijri = new CalendarSystem(new CultureInfo("ar-SA"));
        Assert.Equal(new DateOnly(1900, 4, 30), hijri.MinDate);
        Assert.False(hijri.Supports(new DateOnly(1800, 1, 1)));
        Assert.Equal(hijri.GetYear(hijri.MinDate), hijri.GetYear(new DateOnly(1800, 1, 1)));
    }
}
