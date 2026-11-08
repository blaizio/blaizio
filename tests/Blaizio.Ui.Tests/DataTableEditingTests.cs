using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The .NET half of an editable BzDataTable: what the keyboard grid's calls (BeginEdit, EndEdit,
/// Toggle, Clear, Paste) do to the rows, the rendered editors and OnEdit. The keyboard itself lives
/// in ts/dataTable.ts and is exercised in a browser; here the script is the caller.
/// Columns by cell index: Name 0, Age 1, Score 2, Active 3, Role 4, Code 5 (read-only).
/// </summary>
public class DataTableEditingTests : BunitContext
{
    public DataTableEditingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private IRenderedComponent<EditableGrid> Grid(
        List<Person> people,
        DataTableEditMode mode = DataTableEditMode.Cell,
        Func<DataTableEditEventArgs<Person>, Task>? handler = null,
        SortState? sort = null) =>
        Render<EditableGrid>(p => p
            .Add(x => x.Items, people)
            .Add(x => x.Mode, mode)
            .Add(x => x.Handler, handler)
            .Add(x => x.DefaultSort, sort));

    private static bool Ok(object result) => (bool)result.GetType().GetProperty("ok")!.GetValue(result)!;

    private static string? FocusKey(object result) => (string?)result.GetType().GetProperty("focus")!.GetValue(result);

    private static Dictionary<string, string?> Values(params (string Key, string? Text)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Text);

    [Fact]
    public void An_editable_column_turns_the_table_into_a_grid()
    {
        var cut = Grid(Person.Sample());

        Assert.Equal("grid", cut.Find("table").GetAttribute("role"));
        Assert.Equal("0", cut.Find("tbody tr").GetAttribute("data-bz-row-index"));
        // A bool shows as a checkbox. The first cell is the grid's tab stop from the first render, so
        // Tab reaches the table before its script attaches; the script roves it from there.
        Assert.Equal("true", cut.Find("tbody tr td:nth-child(4) [role=checkbox]").GetAttribute("aria-checked"));
        var stop = Assert.Single(cut.FindAll("[tabindex=\"0\"]"));
        Assert.Equal("Ada", stop.TextContent);
    }

    [Fact]
    public async Task Quick_entry_seeds_the_editor_and_a_commit_writes_through()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        Assert.True(await cut.InvokeAsync(() => table.BeginEdit(0, 0, "Z", "typing", false)));
        // The library's own text box, laid over the cell's display (kept underneath, hidden and inert).
        var input = cut.Find("[data-bz-cell-editor=text] input");
        Assert.Equal("Z", input.GetAttribute("value"));
        Assert.Contains("bz-input", input.ClassList);
        Assert.Equal("Ada", cut.Find("td[data-editing] .bz-table-cell-ghost[inert]").TextContent);

        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", "Zoe")), false));

        Assert.True(Ok(result));
        Assert.Equal("Zoe", people[0].Name);
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));
        var edit = Assert.Single(cut.Instance.Edits);
        Assert.Equal(DataTableEditReason.Edit, edit.Reason);
        var change = Assert.Single(edit.Changes);
        Assert.Equal(("Name", "Ada", "Zoe"), (change.ColumnKey, change.OldValue, change.NewValue));
    }

    [Fact]
    public async Task An_unparseable_value_keeps_the_editor_open_with_its_message()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false));
        // An int edits in the library's number field; its text still travels with the commit.
        Assert.Equal("36", cut.Find("[data-bz-cell-editor=number] .bz-input-number input").GetAttribute("value"));
        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Age", "abc")), false));

        Assert.False(Ok(result));
        Assert.Equal("Age", FocusKey(result));
        Assert.Equal(36, people[0].Age);
        Assert.Equal("The Age field must be a number.", cut.Find(".bz-table-cell-error").TextContent);
        Assert.Equal("true", cut.Find("[data-bz-cell-editor=number] input").GetAttribute("aria-invalid"));
        Assert.NotNull(cut.Find("td[data-editing][data-invalid]"));
        Assert.Empty(cut.Instance.Edits);
    }

    [Theory]
    [InlineData("200", "The field Age must be between 0 and 150.")] // the property's [Range]
    [InlineData("13", "Unlucky.")]                                   // the column's Validate
    public async Task Rules_refuse_a_value_before_anything_is_written(string text, string message)
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false));
        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Age", text)), false));

        Assert.False(Ok(result));
        Assert.Equal(message, cut.Find(".bz-table-cell-error").TextContent);
        Assert.Equal(36, people[0].Age);
    }

    [Fact]
    public async Task Focus_leaving_the_table_discards_an_invalid_edit_and_says_so()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false));
        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Age", "abc")), true));

        Assert.True(Ok(result));
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));
        Assert.Equal("The Age field must be a number. The edit was discarded.", cut.Find(".bz-data-table-live").TextContent);
    }

    [Fact]
    public async Task Cancel_and_unchanged_commits_raise_nothing()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));
        await cut.InvokeAsync(() => table.EndEdit(false, null, false));
        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));
        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", "Ada")), false));

        Assert.True(Ok(result));
        Assert.Empty(cut.Instance.Edits);
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));
    }

    [Fact]
    public async Task A_rejected_save_puts_the_old_value_back_and_marks_the_cell()
    {
        var people = Person.Sample();
        var cut = Grid(people, handler: e =>
        {
            e.Reject("Server said no.");
            return Task.CompletedTask;
        });
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", "Zoe")), false));

        Assert.Equal("Ada", people[0].Name);
        var cell = cut.Find("tbody tr td[data-invalid]");
        Assert.Equal("Server said no.", cell.GetAttribute("title"));
        Assert.Equal("Server said no.", cut.Find(".bz-data-table-live").TextContent);
    }

    [Fact]
    public async Task A_slow_save_shows_the_cell_pending_while_the_cursor_moves_on()
    {
        var people = Person.Sample();
        var save = new TaskCompletionSource();
        var cut = Grid(people, handler: _ => save.Task);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));
        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", "Zoe")), false));

        // EndEdit answered before the save finished: the keyboard is not held up.
        Assert.True(Ok(result));
        Assert.Equal("Zoe", people[0].Name);
        Assert.NotNull(cut.Find("tbody tr td[data-pending]"));

        await cut.InvokeAsync(() => save.SetResult());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("td[data-pending]")));
    }

    [Fact]
    public async Task Toggle_flips_a_bool_in_place()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.Toggle(0, 3));

        Assert.False(people[0].Active);
        Assert.Equal((true, false), ((bool)Assert.Single(cut.Instance.Edits).Changes[0].OldValue!, people[0].Active));
        Assert.Equal("false", cut.Find("tbody tr td:nth-child(4) [role=checkbox]").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Clear_empties_a_nullable_cell_but_not_a_plain_number()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.Clear(0, 2));
        await cut.InvokeAsync(() => table.Clear(0, 1));

        Assert.Null(people[0].Score);
        Assert.Equal(36, people[0].Age);
        var edit = Assert.Single(cut.Instance.Edits);
        Assert.Equal(DataTableEditReason.Clear, edit.Reason);
    }

    [Fact]
    public async Task A_pasted_block_is_one_edit_and_skips_what_does_not_parse()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.Paste(0, 0, "Xena\t40\r\nYuri\tabc\r\n"));

        Assert.Equal(("Xena", 40), (people[0].Name, people[0].Age));
        Assert.Equal(("Yuri", 28), (people[1].Name, people[1].Age));
        var edit = Assert.Single(cut.Instance.Edits);
        Assert.Equal(DataTableEditReason.Paste, edit.Reason);
        Assert.Equal(3, edit.Changes.Count);
        Assert.Equal("The Age field must be a number.", cut.Find(".bz-data-table-live").TextContent);
    }

    [Fact]
    public async Task A_paste_stops_at_the_table_edges_and_skips_read_only_columns()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        // From Role (col 4): Role takes "Admin", the read-only Code skips, the extra cell falls off.
        await cut.InvokeAsync(() => table.Paste(2, 4, "Admin\tZZ\textra\r\nAdmin\r\n"));

        Assert.Equal(Role.Admin, people[2].Role);
        Assert.Equal("C3", people[2].Code);
        Assert.Single(Assert.Single(cut.Instance.Edits).Changes);
    }

    [Fact]
    public async Task Typing_over_a_pick_list_opens_its_list_and_a_pick_commits()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        // Typing "a" over Bo's Role opens the list (the script types the key into its type-ahead);
        // nothing is picked yet, so the trigger still shows Member.
        await cut.InvokeAsync(() => table.BeginEdit(1, 4, "a", "typing", false));
        var trigger = cut.Find("[data-bz-cell-editor=select] [data-slot=select-trigger]");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("Member", trigger.TextContent.Trim());

        // A pick is the cell's draft and asks the grid script to commit, which it does as Enter would.
        cut.FindAll("[role=option]").Single(o => o.TextContent.Trim() == "Admin").Click();
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "commit");
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Role", null)), false));
        Assert.Equal(Role.Admin, people[1].Role);
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));

        // Text for a pick list (a paste, the script's text) parses by name.
        await cut.InvokeAsync(() => table.BeginEdit(1, 4, null, "key", false));
        Assert.Equal("false", cut.Find("[data-bz-cell-editor=select] [data-slot=select-trigger]").GetAttribute("aria-expanded"));
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Role", "member")), false));
        Assert.Equal(Role.Member, people[1].Role);
    }

    [Fact]
    public async Task Read_only_cells_and_bool_cells_open_no_editor_in_cell_mode()
    {
        var cut = Grid(Person.Sample());
        var table = cut.Instance.Table!;

        Assert.False(await cut.InvokeAsync(() => table.BeginEdit(0, 5, null, "key", false)));
        Assert.False(await cut.InvokeAsync(() => table.BeginEdit(0, 3, null, "key", false)));
        Assert.False(await cut.InvokeAsync(() => table.BeginEdit(9, 0, null, "key", false)));
    }

    [Fact]
    public async Task Row_mode_opens_every_editable_cell_and_commits_the_row_once()
    {
        var people = Person.Sample();
        var cut = Grid(people, DataTableEditMode.Row);
        var table = cut.Instance.Table!;

        Assert.True(await cut.InvokeAsync(() => table.BeginEdit(1, 0, null, "key", false)));
        Assert.NotNull(cut.Find("tbody tr[data-editing]"));
        Assert.Equal(5, cut.FindAll("tr[data-editing] [data-bz-cell-editor]").Count);

        var result = await cut.InvokeAsync(() => table.EndEdit(true,
            Values(("Name", "Bo"), ("Age", "29"), ("Score", "75"), ("Active", null), ("Role", "Admin")), false));

        Assert.True(Ok(result));
        Assert.Equal((29, 75, Role.Admin), (people[1].Age, people[1].Score, people[1].Role));
        var edit = Assert.Single(cut.Instance.Edits);
        Assert.Equal(["Age", "Score", "Role"], edit.Changes.Select(c => c.ColumnKey));
        Assert.Empty(cut.FindAll("tr[data-editing]"));
    }

    [Fact]
    public async Task Row_mode_reject_keeps_the_row_open_with_the_message_on_its_field()
    {
        var people = Person.Sample();
        var cut = Grid(people, DataTableEditMode.Row, handler: e =>
        {
            e.Reject(e.Changes.First(c => c.ColumnKey == "Age"), "Too old for this team.");
            return Task.CompletedTask;
        });
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(2, 0, null, "key", false));
        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", "Cyrus"), ("Age", "99")), false));

        Assert.False(Ok(result));
        Assert.Equal("Age", FocusKey(result));
        Assert.Equal(("Cy", 51), (people[2].Name, people[2].Age)); // rolled back
        Assert.NotNull(cut.Find("tr[data-editing]"));
        Assert.Equal("Too old for this team.", cut.Find(".bz-table-cell-error").TextContent);
        // The editors keep what was typed: their starting text does not follow the rollback.
        Assert.Equal("51", cut.Find("tr[data-editing] td:nth-child(2) [data-bz-cell-editor] input").GetAttribute("value"));
    }

    [Fact]
    public async Task Space_on_a_bool_in_row_mode_opens_the_row_with_the_box_flipped()
    {
        var people = Person.Sample();
        var cut = Grid(people, DataTableEditMode.Row);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.Toggle(0, 3));

        Assert.True(people[0].Active); // nothing written until the row commits
        Assert.Equal("false", cut.Find("tr[data-editing] td:nth-child(4) [data-bz-cell-editor] [role=checkbox]").GetAttribute("aria-checked"));
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Active", null)), false));
        Assert.False(people[0].Active);
    }

    [Fact]
    public async Task Dialog_mode_edits_the_row_in_a_form_with_every_editable_column()
    {
        var people = Person.Sample();
        var cut = Grid(people, DataTableEditMode.Dialog);
        var table = cut.Instance.Table!;

        Assert.True(await cut.InvokeAsync(() => table.BeginEdit(0, 1, null, "key", false)));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.bz-data-table-edit-form")));
        Assert.Equal(["Name", "Age", "Score", "Active", "Role"],
            cut.FindAll("form.bz-data-table-edit-form label").Select(l => l.TextContent.Trim()).Where(t => t.Length > 0));
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]")); // nothing opens in the table itself

        cut.Find("form.bz-data-table-edit-form input[value=\"36\"]").Input("37");
        cut.Find("form.bz-data-table-edit-form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(37, people[0].Age));
        Assert.Single(cut.Instance.Edits);
        Assert.False(table.IsEditing(people[0]));
    }

    [Fact]
    public async Task Begin_and_cancel_from_code()
    {
        var people = Person.Sample();
        var cut = Grid(people, DataTableEditMode.Row);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEditAsync(people[2], "Age"));
        Assert.True(table.IsEditing(people[2]));
        Assert.NotNull(cut.Find("tbody tr[data-bz-row-index=\"2\"][data-editing]"));

        await cut.InvokeAsync(() => table.CancelEditAsync());
        Assert.False(table.IsEditing(people[2]));
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));
    }

    [Fact]
    public async Task An_edit_does_not_re_sort_the_row_away_until_the_next_sort()
    {
        var people = Person.Sample();
        var cut = Grid(people, sort: new SortState("Name", SortDirection.Ascending));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", "Zed")), false));

        // The parent re-rendered in answer to OnEdit; the row stays where the cursor is.
        Assert.Equal(["Zed", "Bo", "Cy"], cut.FindAll("tbody tr td:first-child").Select(td => td.TextContent));

        cut.Render(); // any later render of the page sorts again
        Assert.Equal(["Bo", "Cy", "Zed"], cut.FindAll("tbody tr td:first-child").Select(td => td.TextContent));
    }

    [Fact]
    public void An_editable_record_column_without_OnEdit_is_a_configuration_error()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<RecordGrid>());
        Assert.Contains("Handle the table's OnEdit", error.Message);
    }

    [Fact]
    public async Task Row_mode_edits_each_column_with_the_control_its_type_gets()
    {
        var cut = Grid(Person.Sample(), DataTableEditMode.Row);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));

        Assert.Equal(["text", "number", "number", "checkbox", "select"],
            cut.FindAll("tr[data-editing] [data-bz-cell-editor]").Select(e => e.GetAttribute("data-bz-cell-editor")));
        Assert.Contains("bz-input", cut.Find("[data-bz-cell-editor=text] input").ClassList);
        Assert.Equal(2, cut.FindAll("[data-bz-cell-editor=number] .bz-input-number").Count);
        Assert.NotNull(cut.Find("[data-bz-cell-editor=checkbox] .bz-checkbox"));
        Assert.NotNull(cut.Find("[data-bz-cell-editor=select] .bz-select-trigger"));
        // Every editing cell keeps its display underneath, hidden: nothing shows beside a control.
        Assert.Equal(5, cut.FindAll("tr[data-editing] td[data-editing] > .bz-table-cell-ghost").Count);
    }

    [Fact]
    public async Task Options_make_a_pick_list_for_any_type()
    {
        var people = Person.Sample();
        var cut = Render<TypedGrid>(p => p.Add(x => x.Items, people));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(1, 0, "d", "typing", false));
        Assert.Equal("true", cut.Find("[data-bz-cell-editor=select] [data-slot=select-trigger]").GetAttribute("aria-expanded"));
        cut.FindAll("[role=option]").Single(o => o.TextContent.Trim() == "Dee").Click();
        await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", null)), false));
        Assert.Equal("Dee", people[1].Name);

        // A pasted value must be one of the options.
        await cut.InvokeAsync(() => table.Paste(0, 0, "Zed"));
        Assert.Equal("Ada", people[0].Name);
        Assert.Equal("The Name field must be one of: Ada, Bo, Cy, Dee.", cut.Find(".bz-data-table-live").TextContent);
    }

    [Fact]
    public async Task Editor_Switch_shows_a_bool_as_a_switch_that_flips_in_place()
    {
        var people = Person.Sample();
        var cut = Render<TypedGrid>(p => p.Add(x => x.Items, people));
        var table = cut.Instance.Table!;

        Assert.Equal("true", cut.Find("tbody tr td:nth-child(2) [role=switch]").GetAttribute("aria-checked"));
        Assert.Empty(cut.FindAll("tbody [role=checkbox]"));

        await cut.InvokeAsync(() => table.Toggle(0, 1));
        Assert.False(people[0].Active);
        Assert.Equal("false", cut.Find("tbody tr td:nth-child(2) [role=switch]").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task A_date_edits_in_a_date_field_and_a_half_typed_date_is_refused()
    {
        var people = Person.Sample();
        var cut = Render<TypedGrid>(p => p.Add(x => x.Items, people).Add(x => x.Mode, DataTableEditMode.Row));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, 2, null, "key", false));
        Assert.NotNull(cut.Find("[data-bz-cell-editor=date] .bz-input-date"));
        Assert.Equal("2024", cut.Find("[data-bz-cell-editor=date] input[aria-label=Year]").GetAttribute("value"));

        // Clearing the day leaves no date, and Joined needs one.
        cut.Find("[data-bz-cell-editor=date] input[aria-label=Day]").Input("");
        var result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", null), ("Active", null), ("Joined", null)), false));
        Assert.False(Ok(result));
        Assert.Equal("The Joined field must be a date.", cut.Find(".bz-table-cell-error").TextContent);

        cut.Find("[data-bz-cell-editor=date] input[aria-label=Day]").Input("20");
        result = await cut.InvokeAsync(() => table.EndEdit(true, Values(("Name", null), ("Active", null), ("Joined", null)), false));
        Assert.True(Ok(result));
        Assert.Equal(new DateOnly(2024, 1, 20), people[0].Joined);
    }

    [Fact]
    public void An_Editor_that_does_not_fit_the_type_is_a_configuration_error()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<MisfitGrid>());
        Assert.Contains("does not fit a String value", error.Message);
    }

    [Fact]
    public async Task EditDialogContent_replaces_the_dialog_and_saves_through_its_context()
    {
        var people = Person.Sample();
        var cut = Render<DialogTemplateGrid>(p => p.Add(x => x.Items, people));
        var table = cut.Instance.Table!;

        Assert.Empty(cut.FindAll(".custom-editor"));
        await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false));

        Assert.Equal("true", cut.Find(".custom-editor").GetAttribute("data-open"));
        Assert.Equal("Ada", cut.Find(".custom-editor h2").TextContent);
        Assert.Equal("Name,Age", cut.Find(".custom-editor .keys").TextContent);
        Assert.Empty(cut.FindAll("[role=dialog]")); // no built-in dialog
        // context.Field: the built-in field, label and editor.
        Assert.Equal("Ada", cut.Find(".custom-editor input.bz-input").GetAttribute("value"));

        cut.Find(".set-age").Click();
        Assert.Equal("50", cut.Find(".custom-editor .age").TextContent);
        Assert.Equal(36, people[0].Age); // a draft only

        cut.Find(".save").Click();
        cut.WaitForAssertion(() => Assert.Equal(50, people[0].Age));
        Assert.True(cut.Instance.Saved);
        Assert.Equal("false", cut.Find(".custom-editor").GetAttribute("data-open"));
        Assert.Equal(["Age"], Assert.Single(cut.Instance.Edits).Changes.Select(c => c.ColumnKey));
    }

    [Fact]
    public async Task EditDialogContent_refuses_an_unknown_column_or_a_value_of_another_type()
    {
        var cut = Render<DialogTemplateGrid>(p => p.Add(x => x.Items, Person.Sample()));
        await cut.InvokeAsync(() => cut.Instance.Table!.BeginEdit(0, 0, null, "key", false));
        var context = cut.Instance.LastContext!;

        Assert.Throws<ArgumentException>(() => context.SetValue("Age", "fifty"));
        Assert.Throws<ArgumentException>(() => context.GetValue<int>("Height"));
        Assert.Throws<ArgumentException>(() => context.Field("Height"));
        Assert.Null(context.ErrorFor("Age"));
    }
}
