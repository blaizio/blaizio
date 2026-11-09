using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// Everything BzDataTable says on its own takes the app's words: the selection labels and summary,
/// the pager, the scroll box's name, the form's close button, the actions column's header, a pick
/// list's blank option, the messages for refused values and saves, and what screen readers hear
/// after a key resizes or moves a column. Without them, the English stays as it was.
/// </summary>
public class DataTableLocalizationTests : BunitContext
{
    public DataTableLocalizationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private IRenderedComponent<LocalizedGrid> Grid(bool localized, Action<ComponentParameterCollectionBuilder<LocalizedGrid>>? configure = null) =>
        Render<LocalizedGrid>(p =>
        {
            p.Add(x => x.Items, Ticket.Sample()).Add(x => x.Localized, localized);
            configure?.Invoke(p);
        });

    private static Dictionary<string, string?> Values(params (string Key, string? Text)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Text);

    private static string Text(AngleSharp.Dom.IElement element) => element.TextContent.Trim();

    // The page number links' names (Previous and Next are pagination links too, named otherwise).
    private static string[] PageLinkNames(IRenderedComponent<LocalizedGrid> cut, string prefix) =>
        cut.FindAll("[data-slot=pagination-link]")
            .Select(a => a.GetAttribute("aria-label") ?? "")
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

    // ---- selection and the pager ----

    [Fact]
    public void Without_words_of_its_own_the_table_speaks_english_as_before()
    {
        // One row a page: five pages, so the pager shows 1, 2, an ellipsis and 5.
        var cut = Grid(false, p => p.Add(x => x.Selectable, true).Add(x => x.PageSize, 1));

        Assert.Equal("Select all rows", cut.Find("thead [role=checkbox]").GetAttribute("aria-label"));
        Assert.Equal("Select row", cut.Find("tbody [role=checkbox]").GetAttribute("aria-label"));
        Assert.Equal("0 of 1 row(s) selected.", Text(cut.Find(".bz-data-table-status")));

        Assert.Equal("pagination", cut.Find("nav[data-slot=pagination]").GetAttribute("aria-label"));
        var previous = cut.Find(".bz-pagination-prev");
        Assert.Equal("Go to previous page", previous.GetAttribute("aria-label"));
        Assert.Equal("Previous", Text(previous.QuerySelector(".bz-pagination-label")!));
        var next = cut.Find(".bz-pagination-next");
        Assert.Equal("Go to next page", next.GetAttribute("aria-label"));
        Assert.Equal("Next", Text(next.QuerySelector(".bz-pagination-label")!));
        Assert.Equal("More pages", Text(cut.Find("[data-slot=pagination-ellipsis] .sr-only")));
        Assert.Equal(["Go to page 1", "Go to page 2", "Go to page 5"], PageLinkNames(cut, "Go to page "));
    }

    [Fact]
    public void The_selection_labels_and_the_pager_take_the_apps_words()
    {
        var cut = Grid(true, p => p.Add(x => x.Selectable, true).Add(x => x.PageSize, 1));

        Assert.Equal("he:select-all", cut.Find("thead [role=checkbox]").GetAttribute("aria-label"));
        Assert.Equal("he:select-row", cut.Find("tbody [role=checkbox]").GetAttribute("aria-label"));

        Assert.Equal("he:pages", cut.Find("nav[data-slot=pagination]").GetAttribute("aria-label"));
        var previous = cut.Find(".bz-pagination-prev");
        Assert.Equal("he:go-prev", previous.GetAttribute("aria-label"));
        Assert.Equal("he:prev", Text(previous.QuerySelector(".bz-pagination-label")!));
        var next = cut.Find(".bz-pagination-next");
        Assert.Equal("he:go-next", next.GetAttribute("aria-label"));
        Assert.Equal("he:next", Text(next.QuerySelector(".bz-pagination-label")!));
        Assert.Equal("he:more", Text(cut.Find("[data-slot=pagination-ellipsis] .sr-only")));
        Assert.Equal(["he:page-1", "he:page-2", "he:page-5"], PageLinkNames(cut, "he:page-"));
    }

    [Fact]
    public void The_selection_summary_template_gets_the_counts_inside_the_live_region()
    {
        var cut = Grid(true, p => p.Add(x => x.Selectable, true));

        var rows = cut.FindAll("tbody [role=checkbox]");
        rows[0].Click();
        cut.FindAll("tbody [role=checkbox]")[2].Click();

        var status = cut.Find(".bz-data-table-status");
        Assert.Equal("polite", status.GetAttribute("aria-live"));
        // The template replaces the English line entirely.
        Assert.Equal("he:2/5", Text(status.QuerySelector(".summary")!));
        Assert.DoesNotContain("selected", status.TextContent);
    }

    [Fact]
    public void The_scroll_box_takes_the_apps_name()
    {
        var english = Grid(false, p => p.Add(x => x.MaxHeight, "10rem"));
        var hebrew = Grid(true, p => p.Add(x => x.MaxHeight, "10rem"));

        Assert.Equal("Table", english.Find("[data-slot=table-container]").GetAttribute("aria-label"));
        var region = hebrew.Find("[data-slot=table-container]");
        Assert.Equal("region", region.GetAttribute("role"));
        Assert.Equal("he:table", region.GetAttribute("aria-label"));
    }

    // ---- editing ----

    [Fact]
    public async Task Refused_values_are_worded_by_the_app()
    {
        var cut = Grid(true, p => p.Add(x => x.Editable, true));
        var table = cut.Instance.Table!;

        async Task<string> Refuse(int col, string key, string text)
        {
            await cut.InvokeAsync(() => table.BeginEdit(0, col, null, "key", false));
            await cut.InvokeAsync(() => table.EndEdit(true, Values((key, text)), false));
            var message = Text(cut.Find(".bz-table-cell-error"));
            await cut.InvokeAsync(() => table.EndEdit(false, null, false));
            return message;
        }

        Assert.Equal("he:Number:Hours:", await Refuse(1, "Hours", "abc"));
        Assert.Equal("he:Required:Hours:", await Refuse(1, "Hours", " "));
        Assert.Equal("he:Date:Due:", await Refuse(2, "Due", "soon"));
        // An enum's options are its names; a column's Options are their labels.
        Assert.Equal("he:Choice:Priority:Low|High", await Refuse(3, "Priority", "Urgent"));
        Assert.Equal("he:Choice:Status:Open|Closed", await Refuse(4, "Status", "Parked"));

        // A paste words what it skipped the same way.
        await cut.InvokeAsync(() => table.Paste(1, 1, "lots"));
        Assert.Equal("he:Number:Hours:", Text(cut.Find(".bz-data-table-live")));
    }

    [Fact]
    public async Task Without_a_message_builder_refusals_stay_english()
    {
        var cut = Grid(false, p => p.Add(x => x.Editable, true));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 4, null, "key", false));
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Status", "Parked")), false));

        Assert.Equal("The Status field must be one of: Open, Closed.", Text(cut.Find(".bz-table-cell-error")));
    }

    [Fact]
    public async Task A_date_left_half_typed_is_worded_by_the_app()
    {
        var cut = Grid(true, p => p.Add(x => x.Editable, true).Add(x => x.Mode, DataTableEditMode.Dialog).Add(x => x.OwnForm, true));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEditAsync(cut.Instance.Items[0], "Due"));
        var form = cut.Instance.LastForm!;
        // An incomplete date is a null draft, which a DateOnly column refuses.
        await cut.InvokeAsync(() => form.SetValue<DateOnly?>("Due", null));

        Assert.False(await cut.InvokeAsync(() => form.SaveAsync()));
        Assert.Equal("he:Date:Due:", cut.Instance.LastForm!.ErrorFor("Due"));
    }

    [Fact]
    public async Task Refused_saves_and_discarded_edits_are_worded_by_the_app()
    {
        var cut = Grid(true, p => p
            .Add(x => x.Editable, true)
            .Add(x => x.Handler, e =>
            {
                e.Reject();
                return Task.CompletedTask;
            }));
        var table = cut.Instance.Table!;

        // A Reject with no message of its own (Cell mode saves in the background).
        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Title", "Logout")), false));
        cut.WaitForAssertion(() => Assert.Equal("he:not-saved", cut.Find("tbody td[data-invalid]").GetAttribute("title")));
        Assert.Equal("he:not-saved", Text(cut.Find(".bz-data-table-live")));

        // Focus leaving the table drops an invalid edit and says so after its message.
        await cut.InvokeAsync(() => table.BeginEdit(1, 1, null, "key", false));
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Hours", "abc")), true));
        Assert.Equal("he:Number:Hours: he:discarded", Text(cut.Find(".bz-data-table-live")));
    }

    [Theory]
    [InlineData(false, "Close")]
    [InlineData(true, "he:close")]
    public async Task The_dialogs_close_button_takes_the_apps_name(bool localized, string name)
    {
        var cut = Grid(localized, p => p.Add(x => x.Editable, true).Add(x => x.Mode, DataTableEditMode.Dialog));
        var table = cut.Instance.Table!;

        Assert.True(await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false)));

        cut.WaitForAssertion(() => Assert.Equal(name, Text(cut.Find(".bz-dialog-close .sr-only"))));
    }

    [Theory]
    [InlineData(false, "Close")]
    [InlineData(true, "he:close")]
    public async Task The_drawers_close_button_takes_the_apps_name(bool localized, string name)
    {
        var cut = Grid(localized, p => p.Add(x => x.Editable, true).Add(x => x.Mode, DataTableEditMode.Drawer));
        var table = cut.Instance.Table!;

        Assert.True(await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false)));

        cut.WaitForAssertion(() => Assert.Equal(name, Text(cut.Find(".bz-data-table-edit-drawer-actions .sr-only"))));
    }

    [Theory]
    [InlineData(false, "None")]
    [InlineData(true, "he:none")]
    public async Task A_nullable_enums_blank_option_takes_the_apps_label(bool localized, string label)
    {
        var cut = Grid(localized, p => p.Add(x => x.Editable, true));
        var table = cut.Instance.Table!;

        // Bo's Priority is empty: the list opens on the blank option.
        await cut.InvokeAsync(() => table.BeginEdit(1, 3, null, "key", true));

        var options = cut.FindAll("[role=option]").Select(Text).ToArray();
        Assert.Equal([label, "Low", "High"], options);
    }

    [Fact]
    public void The_actions_column_names_its_header_for_screen_readers()
    {
        var english = Grid(false, p => p.Add(x => x.Editable, true).Add(x => x.Mode, DataTableEditMode.Row).Add(x => x.Actions, true));
        var hebrew = Grid(true, p => p.Add(x => x.Editable, true).Add(x => x.Mode, DataTableEditMode.Row).Add(x => x.Actions, true));

        Assert.Equal("Actions", Text(english.Find("thead th:last-child .sr-only")));
        Assert.Equal("he:actions", Text(hebrew.Find("thead th:last-child .sr-only")));
    }

    // A column gets its parameters after the table's own render (the columns are its child content):
    // its words have to be read where the rows and headers render, after that, to show at once.

    [Fact]
    public void A_language_switch_reaches_the_actions_column_in_the_render_that_made_it()
    {
        var cut = Grid(false, p => p.Add(x => x.Editable, true).Add(x => x.Mode, DataTableEditMode.Row).Add(x => x.Actions, true));
        Assert.Equal("Actions", Text(cut.Find("thead th:last-child .sr-only")));
        Assert.Equal("Edit", cut.Find("tbody tr:first-child button[aria-label]").GetAttribute("aria-label"));

        cut.Render(p => p.Add(x => x.ActionWords, true));

        Assert.Equal("he:actions", Text(cut.Find("thead th:last-child .sr-only")));
        Assert.Equal("he:edit", cut.Find("tbody tr:first-child button[aria-label]").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task A_language_switch_reaches_an_open_pick_list_in_the_render_that_made_it()
    {
        var cut = Grid(false, p => p.Add(x => x.Editable, true));
        var table = cut.Instance.Table!;
        await cut.InvokeAsync(() => table.BeginEdit(1, 3, null, "key", true));
        Assert.Equal("None", Text(cut.FindAll("[role=option]")[0]));

        cut.Render(p => p.Add(x => x.NoneWords, true));

        Assert.Equal("he:none", Text(cut.FindAll("[role=option]")[0]));
    }

    // ---- what screen readers hear after a key resizes or moves a column ----

    [Fact]
    public void Column_announcements_stay_english_without_a_builder()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/dataTable.js");
        var cut = Grid(false, p => p.Add(x => x.Resizable, true));
        var table = cut.Instance.Table!;

        var options = module.Invocations["createColumns"].Single().Arguments[2]!;
        Assert.Equal(false, options.GetType().GetProperty("announce")!.GetValue(options));
        Assert.Equal("Title: 120 pixels", table.ColumnAnnouncementText("resized", "Title", 120, 0));
        Assert.Equal("Hours moved to column 3 of 5", table.ColumnAnnouncementText("moved", "Hours", 3, 5));
        Assert.Equal("Status cannot move further", table.ColumnAnnouncementText("blocked", "Status", 0, 0));
    }

    [Fact]
    public void Column_announcements_are_worded_by_the_app()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/dataTable.js");
        var cut = Grid(true, p => p.Add(x => x.Resizable, true));
        var table = cut.Instance.Table!;

        // The script asks .NET for the words only when the table has a builder.
        var options = module.Invocations["createColumns"].Single().Arguments[2]!;
        Assert.Equal(true, options.GetType().GetProperty("announce")!.GetValue(options));
        Assert.Equal("he:Resized:Title:120:0:0", table.ColumnAnnouncementText("resized", "Title", 120, 0));
        Assert.Equal("he:Moved:Hours:0:3:5", table.ColumnAnnouncementText("moved", "Hours", 3, 5));
        Assert.Equal("he:CannotMove:Status:0:0:0", table.ColumnAnnouncementText("blocked", "Status", 0, 0));
    }
}
