using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>BzInputDate's calendar popup, opened and followed from code (CalendarOpen).</summary>
public class InputDateTests : BunitContext
{
    public InputDateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    [Fact]
    public void DefaultCalendarOpen_opens_the_popup_from_the_start()
    {
        var cut = Render<BzInputDate>(p => p
            .Add(x => x.ShowCalendar, true)
            .Add(x => x.DefaultCalendarOpen, true)
            .Add(x => x.DefaultValue, new DateOnly(2026, 11, 7)));

        Assert.Equal("true", cut.Find("button[aria-label='Open calendar']").GetAttribute("aria-expanded"));
        Assert.NotEmpty(cut.FindAll("[data-slot=popover-content] button"));
    }

    [Fact]
    public void A_picked_day_raises_the_value_before_the_close()
    {
        var events = new List<string>();
        var open = true;
        DateOnly? value = new DateOnly(2026, 11, 7);
        var cut = Render<BzInputDate>(p => p
            .Add(x => x.ShowCalendar, true)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, (DateOnly? v) => { value = v; events.Add($"value {v:yyyy-MM-dd}"); })
            .Add(x => x.CalendarOpen, open)
            .Add(x => x.CalendarOpenChanged, (bool o) => { open = o; events.Add($"open {o}"); }));

        cut.FindAll("[data-slot=popover-content] button").First(b => b.TextContent.Trim() == "20").Click();

        Assert.Equal(["value 2026-11-20", "open False"], events);
        Assert.False(open);
    }

    [Fact]
    public void Pressing_the_selected_day_again_keeps_the_date_and_closes_the_popup()
    {
        var events = new List<string>();
        var open = true;
        DateOnly? value = new DateOnly(2026, 11, 7);
        var cut = Render<BzInputDate>(p => p
            .Add(x => x.ShowCalendar, true)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, (DateOnly? v) => { value = v; events.Add($"value {v:yyyy-MM-dd}"); })
            .Add(x => x.CalendarOpen, open)
            .Add(x => x.CalendarOpenChanged, (bool o) => { open = o; events.Add($"open {o}"); }));

        // A lone calendar toggles its date off on a second press; in a field that cleared the date.
        cut.FindAll("[data-slot=popover-content] button").First(b => b.TextContent.Trim() == "7").Click();

        Assert.Equal(["open False"], events);
        Assert.Equal(new DateOnly(2026, 11, 7), value);
    }

    [Fact]
    public void The_trigger_reports_opening_through_CalendarOpenChanged()
    {
        bool? reported = null;
        var cut = Render<BzInputDate>(p => p
            .Add(x => x.ShowCalendar, true)
            .Add(x => x.CalendarOpen, false)
            .Add(x => x.CalendarOpenChanged, (bool o) => reported = o));

        cut.Find("button[aria-label='Open calendar']").Click();

        Assert.True(reported);
        // Controlled: stays closed until the parent passes true.
        Assert.Equal("false", cut.Find("button[aria-label='Open calendar']").GetAttribute("aria-expanded"));
    }
}
