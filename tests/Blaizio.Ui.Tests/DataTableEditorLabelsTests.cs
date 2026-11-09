using System.Globalization;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// BzDataTable's EditorLabels: the built-in date and time editors' screen-reader names and the AM
/// and PM text, English while it is unset. Runs under en-US so the time editor has its 12-hour
/// face (the editor follows the current culture's clock).
/// </summary>
public sealed class DataTableEditorLabelsTests : BunitContext
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public DataTableEditorLabelsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        base.Dispose(disposing);
    }

    private async Task<IRenderedComponent<ShiftGrid>> OpenRow(DataTableEditorLabels? labels)
    {
        var cut = Render<ShiftGrid>(p => p
            .Add(x => x.Items, [new ShiftGrid.Shift { Id = 1 }])
            .Add(x => x.Labels, labels));
        await cut.InvokeAsync(() => cut.Instance.Table!.BeginEdit(0, 0, null, "key", true));
        return cut;
    }

    [Fact]
    public async Task The_date_and_time_editors_speak_English_without_EditorLabels()
    {
        var cut = await OpenRow(null);

        Assert.Equal(new[] { "Month", "Day", "Year" },
            cut.FindAll("[data-bz-cell-editor=date] [data-slot=input-date-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.NotNull(cut.Find("[data-bz-cell-editor=date] button[aria-label='Open calendar']"));
        Assert.Equal(new[] { "Hour", "Minute" },
            cut.FindAll("[data-bz-cell-editor=time] [data-slot=input-time-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.Equal("AM", cut.Find("[data-bz-cell-editor=time] button[aria-label='AM or PM']").TextContent.Trim());
    }

    [Fact]
    public async Task EditorLabels_words_the_date_and_time_editors()
    {
        var cut = await OpenRow(new DataTableEditorLabels
        {
            Day = "اليوم",
            Month = "الشهر",
            Year = "السنة",
            CalendarButton = "فتح التقويم",
            Calendar = "التقويم",
            PreviousMonth = "الشهر السابق",
            NextMonth = "الشهر التالي",
            Hour = "الساعة",
            Minute = "الدقيقة",
            Meridiem = "صباحا أو مساء",
            Am = "ص",
            Pm = "م",
        });

        // The popup flag opened the date editor's calendar, so its names show too.
        Assert.Equal(new[] { "الشهر", "اليوم", "السنة" },
            cut.FindAll("[data-bz-cell-editor=date] [data-slot=input-date-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.NotNull(cut.Find("[data-bz-cell-editor=date] button[aria-label='فتح التقويم']"));
        Assert.Equal("التقويم", cut.Find("[data-slot=popover-content]").GetAttribute("aria-label"));
        Assert.Equal("الشهر السابق", cut.Find("[data-slot=calendar-button-previous]").GetAttribute("aria-label"));
        Assert.Equal("الشهر التالي", cut.Find("[data-slot=calendar-button-next]").GetAttribute("aria-label"));
        Assert.Equal(new[] { "الساعة", "الدقيقة" },
            cut.FindAll("[data-bz-cell-editor=time] [data-slot=input-time-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.Equal("ص", cut.Find("[data-bz-cell-editor=time] button[aria-label='صباحا أو مساء']").TextContent.Trim());
    }

    [Fact]
    public async Task A_partial_EditorLabels_keeps_English_for_the_rest()
    {
        var cut = await OpenRow(new DataTableEditorLabels { Hour = "Stunde" });

        Assert.Equal(new[] { "Stunde", "Minute" },
            cut.FindAll("[data-bz-cell-editor=time] [data-slot=input-time-input] input").Select(i => i.GetAttribute("aria-label")));
        Assert.NotNull(cut.Find("[data-bz-cell-editor=date] input[aria-label=Day]"));
    }
}
