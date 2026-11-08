using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// How an edit starts (what BeginEdit tells .NET and what the editors receive), the form modes
/// (Dialog and Drawer, built in or your own), the close guard and Previous / Next. The pointer and
/// the keyboard live in ts/dataTable.ts and are exercised in a browser; here the script is the caller.
/// </summary>
public class DataTableEditFormTests : BunitContext
{
    public DataTableEditFormTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private IRenderedComponent<TemplateGrid> Templates(DataTableEditMode mode = DataTableEditMode.Cell) =>
        Render<TemplateGrid>(p => p.Add(x => x.Items, Person.Sample()).Add(x => x.Mode, mode));

    private IRenderedComponent<DrawerGrid> Drawer(List<Person> people, bool custom = false,
        DataTableEditMode mode = DataTableEditMode.Drawer, SortState? sort = null,
        Func<DataTableEditEventArgs<Person>, Task>? handler = null) =>
        Render<DrawerGrid>(p => p
            .Add(x => x.Items, people)
            .Add(x => x.Custom, custom)
            .Add(x => x.Mode, mode)
            .Add(x => x.DefaultSort, sort)
            .Add(x => x.Handler, handler));

    // ---- how an edit starts ----

    [Fact]
    public async Task An_editor_of_your_own_hears_how_its_edit_started()
    {
        var cut = Templates();
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, "Z", "typing", false));
        Assert.Equal(new TemplateGrid.Start(DataTableEditStart.Typing, "Z", false, false), cut.Instance.Starts["Name"]);
        await cut.InvokeAsync(() => table.CancelEditAsync());

        // A double-click asks for the popup; F2 does not; code says Code.
        await cut.InvokeAsync(() => table.BeginEdit(1, 1, null, "pointer", true));
        Assert.Equal(new TemplateGrid.Start(DataTableEditStart.Pointer, null, true, false), cut.Instance.Starts["Role"]);
        await cut.InvokeAsync(() => table.CancelEditAsync());
        await cut.InvokeAsync(() => table.BeginEdit(1, 1, null, "key", false));
        Assert.Equal(DataTableEditStart.Key, cut.Instance.Starts["Role"].By);
        await cut.InvokeAsync(() => table.CancelEditAsync());
        await cut.InvokeAsync(() => table.BeginEditAsync(cut.Instance.Items[2], "Name"));
        Assert.Equal(new TemplateGrid.Start(DataTableEditStart.Code, null, false, false), cut.Instance.Starts["Name"]);
    }

    [Fact]
    public async Task In_row_mode_only_the_editor_the_edit_started_in_gets_the_typed_text_and_the_popup()
    {
        var cut = Templates(DataTableEditMode.Row);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, "a", "typing", true));

        Assert.Equal(new TemplateGrid.Start(DataTableEditStart.Typing, "a", true, false), cut.Instance.Starts["Role"]);
        Assert.Equal(new TemplateGrid.Start(DataTableEditStart.Typing, null, false, false), cut.Instance.Starts["Name"]);
    }

    [Fact]
    public async Task A_form_never_opens_a_popup_or_takes_typed_text()
    {
        var cut = Templates(DataTableEditMode.Dialog);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "pointer", true));

        cut.WaitForAssertion(() => Assert.True(cut.Instance.Starts["Role"].InDialog));
        Assert.Equal(new TemplateGrid.Start(DataTableEditStart.Pointer, null, false, true), cut.Instance.Starts["Role"]);
    }

    [Fact]
    public async Task A_popup_start_opens_the_pick_list_and_the_calendar()
    {
        var people = Person.Sample();
        var cut = Render<TypedGrid>(p => p.Add(x => x.Items, people));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "pointer", true));
        Assert.Equal("true", cut.Find("[data-bz-cell-editor=select] [data-slot=select-trigger]").GetAttribute("aria-expanded"));
        await cut.InvokeAsync(() => table.CancelEditAsync());

        // A date's calendar: open from the start; a day picked there commits the cell.
        await cut.InvokeAsync(() => table.BeginEdit(0, 2, null, "pointer", true));
        Assert.Equal("true", cut.Find("[data-bz-cell-editor=date] button[aria-label='Open calendar']").GetAttribute("aria-expanded"));
        var commits = JSInterop.Invocations.Count(i => i.Identifier == "commit");
        cut.FindAll("[data-slot=popover-content] button").First(b => b.TextContent.Trim() == "20").Click();
        Assert.Equal(commits + 1, JSInterop.Invocations.Count(i => i.Identifier == "commit"));
        await cut.InvokeAsync(() => table.EndEdit(true, new Dictionary<string, string?> { ["Joined"] = null }, false));
        Assert.Equal(new DateOnly(2024, 1, 20), people[0].Joined);
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));

        // Started from F2: closed.
        await cut.InvokeAsync(() => table.BeginEdit(0, 2, null, "key", false));
        Assert.Equal("false", cut.Find("[data-bz-cell-editor=date] button[aria-label='Open calendar']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Typing_over_a_date_starts_it_empty()
    {
        var people = Person.Sample();
        var cut = Render<TypedGrid>(p => p.Add(x => x.Items, people));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 2, "1", "typing", false));

        Assert.All(cut.FindAll("[data-bz-cell-editor=date] input"), input => Assert.Equal("", input.GetAttribute("value")));
        // Nothing typed into it: a date the column needs is missing.
        var result = await cut.InvokeAsync(() => table.EndEdit(true, new Dictionary<string, string?> { ["Joined"] = null }, false));
        Assert.False((bool)result.GetType().GetProperty("ok")!.GetValue(result)!);
        Assert.Equal("The Joined field must be a date.", cut.Find(".bz-table-cell-error").TextContent);
        Assert.Equal(new DateOnly(2024, 1, 15), people[0].Joined);
    }

    [Fact]
    public void Pick_list_and_date_cells_carry_the_mark_that_opens_their_popup()
    {
        var cut = Render<TypedGrid>(p => p.Add(x => x.Items, Person.Sample()));

        var first = cut.FindAll("tbody tr")[0];
        Assert.Equal("select", first.QuerySelector("td:nth-child(1) [data-bz-pick]")!.GetAttribute("data-bz-pick"));
        Assert.Equal("date", first.QuerySelector("td:nth-child(3) [data-bz-pick]")!.GetAttribute("data-bz-pick"));
        Assert.Null(first.QuerySelector("td:nth-child(2) [data-bz-pick]")); // the switch
        Assert.Contains("group/table-cell", first.QuerySelector("td:nth-child(1)")!.ClassName);

        // Forms open no popup by themselves: no mark.
        var dialog = Render<TypedGrid>(p => p.Add(x => x.Items, Person.Sample()).Add(x => x.Mode, DataTableEditMode.Dialog));
        Assert.Empty(dialog.FindAll("[data-bz-pick]"));
    }

    // ---- the drawer ----

    [Fact]
    public async Task Drawer_mode_edits_the_row_in_a_sheet_at_the_inline_end_edge()
    {
        var people = Person.Sample();
        var cut = Drawer(people);
        var table = cut.Instance.Table!;

        Assert.True(await cut.InvokeAsync(() => table.BeginEdit(1, 1, null, "key", false)));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-slot=sheet-content][data-side=end] form.bz-data-table-edit-form")));
        var sheet = cut.Find("[data-slot=sheet-content]");
        Assert.Contains("w-full", sheet.ClassName);
        Assert.Equal("Edit row", sheet.QuerySelector("[data-slot=sheet-header] h2, [data-slot=sheet-header] [id]")!.TextContent.Trim());
        Assert.NotNull(sheet.QuerySelector(".bz-data-table-edit-drawer-body.scrollbar-thin"));
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]")); // nothing opens in the table itself
        // The edited row is marked while the form is open.
        Assert.Equal("1", cut.Find("tbody tr[data-editing]").GetAttribute("data-bz-row-index"));

        cut.Find("form.bz-data-table-edit-form input[value=\"28\"]").Input("29");
        cut.Find("form.bz-data-table-edit-form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(29, people[1].Age));
        Assert.Single(cut.Instance.Edits);
        Assert.Empty(cut.FindAll("tbody tr[data-editing]"));
    }

    [Fact]
    public async Task Previous_and_Next_save_and_move_in_the_current_order()
    {
        var people = Person.Sample();
        // Sorted by name, descending: Cy, Bo, Ada.
        var cut = Drawer(people, sort: new SortState("Name", SortDirection.Descending));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.bz-data-table-edit-form")));
        Assert.True(cut.Find("button[aria-label='Previous row']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Next row']").HasAttribute("disabled"));

        cut.Find("form.bz-data-table-edit-form input[value=\"51\"]").Input("52");
        cut.Find("button[aria-label='Next row']").Click();

        // Cy saved, Bo open in the same drawer.
        cut.WaitForAssertion(() => Assert.Equal(52, people[2].Age));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.bz-data-table-edit-form input[value=\"28\"]")));
        Assert.Equal("1", cut.Find("tbody tr[data-editing]").GetAttribute("data-bz-row-index"));
        Assert.True(table.IsEditing(people[1]));

        cut.Find("button[aria-label='Next row']").Click();
        cut.WaitForAssertion(() => Assert.True(table.IsEditing(people[0])));
        Assert.True(cut.Find("button[aria-label='Next row']").HasAttribute("disabled"));
        Assert.Single(cut.Instance.Edits); // Bo had no change
    }

    [Fact]
    public async Task An_invalid_value_keeps_the_drawer_on_its_row()
    {
        var people = Person.Sample();
        var cut = Drawer(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.bz-data-table-edit-form")));
        cut.Find("form.bz-data-table-edit-form input[value=\"36\"]").Input("13");
        cut.Find("button[aria-label='Next row']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Unlucky.", cut.Find("form.bz-data-table-edit-form").TextContent));
        Assert.True(table.IsEditing(people[0]));
        Assert.Equal(36, people[0].Age);
        Assert.Empty(cut.Instance.Edits);
    }

    // ---- the close guard ----

    [Theory]
    [InlineData(DataTableEditMode.Dialog)]
    [InlineData(DataTableEditMode.Drawer)]
    public async Task Closing_a_changed_form_asks_first(DataTableEditMode mode)
    {
        var people = Person.Sample();
        var cut = Drawer(people, mode: mode);
        var table = cut.Instance.Table!;

        // A clean form closes at once.
        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.bz-data-table-edit-form")));
        cut.Find("[data-bz-edit-cancel]").Click();
        Assert.False(table.IsEditing(people[0]));

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.bz-data-table-edit-form input[value=\"36\"]")));
        cut.Find("form.bz-data-table-edit-form input[value=\"36\"]").Input("40");
        cut.Find("[data-bz-edit-cancel]").Click();

        // The footer asks instead of closing, and says so to screen readers.
        Assert.True(table.IsEditing(people[0]));
        var confirm = cut.Find(".bz-data-table-edit-confirm");
        Assert.Equal("Discard your changes?", confirm.QuerySelector(".bz-data-table-edit-confirm-text")!.TextContent);
        Assert.Equal("Discard your changes?", cut.Find(".bz-data-table-live").TextContent);
        Assert.Empty(cut.FindAll("[data-bz-edit-cancel]"));

        // Keep editing: back to the form, the change still there.
        confirm.QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Keep editing").Click();
        Assert.Empty(cut.FindAll(".bz-data-table-edit-confirm"));
        Assert.NotNull(cut.Find("form.bz-data-table-edit-form input[value=\"40\"]"));

        // Discard: closed, nothing saved.
        cut.Find("[data-bz-edit-cancel]").Click();
        cut.FindAll(".bz-data-table-edit-confirm button").Single(b => b.TextContent.Trim() == "Discard").Click();
        Assert.False(table.IsEditing(people[0]));
        Assert.Equal(36, people[0].Age);
        Assert.Empty(cut.Instance.Edits);
    }

    [Fact]
    public async Task Your_own_drawer_shares_the_form_context_and_its_guard()
    {
        var people = Person.Sample();
        var cut = Drawer(people, custom: true);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));
        Assert.Equal("true", cut.Find(".custom-form").GetAttribute("data-open"));
        Assert.Empty(cut.FindAll("[data-slot=sheet-content]")); // no built-in drawer
        Assert.Equal("|next|", cut.Find(".flags").TextContent);

        var context = cut.Instance.LastContext!;
        Assert.IsType<DataTableEditForm<Person>>(context);
        await cut.InvokeAsync(() => context.SetValue("Age", 41));
        Assert.True(context.HasChanges);

        // OpenChanged(false) from the surface (Escape, a press outside) asks first; asked again, it keeps editing.
        await cut.InvokeAsync(() => context.OpenChanged.InvokeAsync(false));
        Assert.Equal("|next|asking", cut.Find(".flags").TextContent);
        Assert.NotNull(cut.Find(".custom-form .actions .bz-data-table-edit-confirm"));
        await cut.InvokeAsync(() => cut.Instance.LastContext!.OpenChanged.InvokeAsync(false));
        Assert.Equal("|next|", cut.Find(".flags").TextContent);

        // Next saves and moves on.
        cut.Find(".next").Click();
        cut.WaitForAssertion(() => Assert.Equal("Bo", cut.Find(".custom-form h2").TextContent));
        Assert.Equal(41, people[0].Age);
        Assert.Equal("prev|next|", cut.Find(".flags").TextContent);

        await cut.InvokeAsync(() => cut.Instance.LastContext!.DiscardAsync());
        Assert.Equal("false", cut.Find(".custom-form").GetAttribute("data-open"));
    }

    [Fact]
    public async Task Your_own_dialog_gets_the_same_form_context()
    {
        var cut = Drawer(Person.Sample(), custom: true, mode: DataTableEditMode.Dialog);
        await cut.InvokeAsync(() => cut.Instance.Table!.BeginEdit(1, 0, null, "key", false));

        Assert.Equal("Bo", cut.Find(".custom-form h2").TextContent);
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal(["Name", "Age"], cut.Instance.LastContext!.ColumnKeys);
    }
}
