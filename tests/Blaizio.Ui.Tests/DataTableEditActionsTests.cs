using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The edit actions column (BzEditActionsColumn and a template column marked EditActions), its
/// context, EditOnClick reaching the grid script, and custom cells (CellContent, EdgeToEdge). What
/// the marks do in the browser (a press on Save or Cancel keeps the editors open, Tab reaches them)
/// lives in ts/dataTable.ts and the E2E suite; here the .NET half: the markup the script keys on
/// and what the buttons do to the edit.
/// </summary>
public class DataTableEditActionsTests : BunitContext
{
    public DataTableEditActionsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private IRenderedComponent<ActionsGrid> Grid(
        List<Person> people,
        DataTableEditMode mode = DataTableEditMode.Row,
        Action<ComponentParameterCollectionBuilder<ActionsGrid>>? more = null) =>
        Render<ActionsGrid>(p =>
        {
            p.Add(x => x.Items, people).Add(x => x.Mode, mode);
            more?.Invoke(p);
        });

    private static string Row(int index) => $"tbody tr[data-bz-row-index=\"{index}\"]";

    private static int CellIndex(AngleSharp.Dom.IElement td) => ((AngleSharp.Html.Dom.IHtmlTableCellElement)td).Index;

    [Fact]
    public async Task Each_row_gets_Edit_and_the_editing_row_Save_and_Cancel()
    {
        var people = Person.Sample();
        var cut = Grid(people);

        // An icon button per row, named by its label; the header names the column for screen readers.
        Assert.Equal(3, cut.FindAll("button[aria-label=Change]").Count);
        Assert.Equal("Actions", cut.Find("thead th:last-child .sr-only").TextContent);

        await cut.Find($"{Row(1)} button[aria-label=Change]").ClickAsync(new());

        Assert.True(cut.Instance.Table!.IsEditing(people[1]));
        Assert.NotEmpty(cut.FindAll($"{Row(1)} [data-bz-cell-editor]"));
        Assert.NotNull(cut.Find($"{Row(1)} button[aria-label=Save]"));
        Assert.NotNull(cut.Find($"{Row(1)} button[aria-label=Cancel]"));
        Assert.Empty(cut.FindAll($"{Row(1)} button[aria-label=Change]"));
        Assert.NotNull(cut.Find($"{Row(0)} button[aria-label=Change]")); // the other rows keep Edit

        await cut.Find($"{Row(1)} button[aria-label=Cancel]").ClickAsync(new());

        Assert.False(cut.Instance.Table!.IsEditing(people[1]));
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));
        Assert.Empty(cut.Instance.Edits);
    }

    [Fact]
    public async Task In_Row_mode_both_sets_share_the_cell_so_it_has_room_for_Save_and_Cancel_from_the_start()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        // The set not in use holds its room: invisible (data-hidden), inert, its buttons unnamed.
        static string Sets(AngleSharp.Dom.IElement row) => string.Join(" | ", row.QuerySelectorAll(".bz-table-edit-actions-set").Select(set =>
            (set.HasAttribute("inert") && set.HasAttribute("data-hidden") ? "hidden:" : "shown:") +
            string.Join(",", set.QuerySelectorAll("button").Select(b => b.GetAttribute("aria-label") ?? "-"))));

        Assert.Equal("shown:Change | hidden:-,-", Sets(cut.Find(Row(1))));

        await cut.Find($"{Row(1)} button[aria-label=Change]").ClickAsync(new());

        Assert.Equal("hidden:- | shown:Save,Cancel", Sets(cut.Find(Row(1))));
        Assert.Equal("shown:Change | hidden:-,-", Sets(cut.Find(Row(0))));

        // Dialog mode never shows Save and Cancel in the row: just Edit.
        var dialog = Grid(Person.Sample(), DataTableEditMode.Dialog);
        Assert.Empty(dialog.FindAll(".bz-table-edit-actions-set"));
        Assert.NotNull(dialog.Find($"{Row(0)} .bz-table-edit-actions button[aria-label=Change]"));
    }

    [Fact]
    public async Task Save_commits_the_row_with_the_text_its_editors_hold()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        // The built-in text boxes keep their text in the browser: the grid script hands it over.
        JSInterop.Setup<Dictionary<string, string?>>("values").SetResult(new() { ["Name"] = "Bea", ["Age"] = "29" });

        await cut.Find($"{Row(1)} button[aria-label=Change]").ClickAsync(new());
        await cut.Find($"{Row(1)} button[aria-label=Save]").ClickAsync(new());

        Assert.Equal(("Bea", 29), (people[1].Name, people[1].Age));
        var edit = Assert.Single(cut.Instance.Edits);
        Assert.Equal(["Name", "Age"], edit.Changes.Select(c => c.ColumnKey));
        Assert.Empty(cut.FindAll("[data-bz-cell-editor]"));
    }

    [Fact]
    public async Task Save_spins_and_both_buttons_wait_while_OnEdit_runs()
    {
        var people = Person.Sample();
        var save = new TaskCompletionSource();
        var cut = Grid(people, more: p => p.Add(x => x.Handler, _ => save.Task));
        JSInterop.Setup<Dictionary<string, string?>>("values").SetResult(new() { ["Name"] = "Bea" });

        await cut.Find($"{Row(1)} button[aria-label=Change]").ClickAsync(new());
        var saving = cut.Find($"{Row(1)} button[aria-label=Save]").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find($"{Row(1)} button[aria-label=Save]").GetAttribute("aria-busy"));
            Assert.True(cut.Find($"{Row(1)} button[aria-label=Save]").HasAttribute("disabled"));
            Assert.True(cut.Find($"{Row(1)} button[aria-label=Cancel]").HasAttribute("disabled"));
        });

        save.SetResult();
        await saving;
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-bz-cell-editor]")));
        Assert.Equal("Bea", people[1].Name);
    }

    [Fact]
    public void The_column_is_hidden_in_Cell_mode_and_in_a_table_with_nothing_to_edit()
    {
        var cell = Grid(Person.Sample(), DataTableEditMode.Cell);
        Assert.Empty(cell.FindAll("button[aria-label=Change]"));
        Assert.Equal(4, cell.FindAll("thead th").Count);

        var readOnly = Grid(Person.Sample(), more: p => p.Add(x => x.Editable, false));
        Assert.Empty(readOnly.FindAll("button[aria-label=Change]"));
        Assert.Equal(4, readOnly.FindAll("thead th").Count);
    }

    [Fact]
    public async Task In_Dialog_mode_Edit_opens_the_form()
    {
        var people = Person.Sample();
        var cut = Grid(people, DataTableEditMode.Dialog);

        await cut.Find($"{Row(2)} button[aria-label=Change]").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.bz-data-table-edit-form")));
        Assert.True(cut.Instance.Table!.IsEditing(people[2]));
        Assert.Empty(cut.FindAll("button[aria-label=Save]")); // Save and Cancel live in the form
    }

    [Fact]
    public void A_Title_shows_in_the_header_instead_of_the_hidden_name()
    {
        var cut = Grid(Person.Sample(), more: p => p.Add(x => x.ActionsTitle, "Edit"));

        var head = cut.Find("thead th:last-child");
        Assert.Equal("Edit", head.TextContent.Trim());
        Assert.Empty(head.QuerySelectorAll(".sr-only"));
    }

    [Fact]
    public async Task ChildContent_gets_the_rows_edit_actions()
    {
        var people = Person.Sample();
        var save = new TaskCompletionSource();
        var cut = Grid(people, more: p => p.Add(x => x.CustomActions, true).Add(x => x.Handler, _ => save.Task));
        JSInterop.Setup<Dictionary<string, string?>>("values").SetResult(new() { ["Name"] = "Bea" });
        var actions = cut.Instance.Contexts;

        Assert.Equal("Save Bo", cut.Find($"{Row(1)} .custom-save").TextContent);
        Assert.Equal((false, false, DataTableEditMode.Row), (actions[2].IsEditing, actions[2].Busy, actions[2].Mode));
        Assert.False(await cut.InvokeAsync(() => actions[2].SaveAsync())); // nothing open on the row

        await cut.InvokeAsync(() => actions[2].BeginAsync("Age"));
        Assert.True(actions[2].IsEditing);
        Assert.False(actions[1].IsEditing);
        var editor = cut.Find($"{Row(1)} [data-bz-cell-editor][data-bz-focus]");
        Assert.Equal(1, CellIndex(editor.Closest("td")!));

        var saving = cut.InvokeAsync(() => cut.Find($"{Row(1)} .custom-save").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(actions[2].Busy));
        save.SetResult();
        await saving;
        cut.WaitForAssertion(() => Assert.False(actions[2].IsEditing));
        Assert.Equal("Bea", people[1].Name);

        await cut.InvokeAsync(() => actions[3].BeginAsync());
        await cut.InvokeAsync(() => actions[1].CancelAsync()); // another row: not this one's edit
        Assert.True(actions[3].IsEditing);
        await cut.InvokeAsync(() => actions[3].CancelAsync());
        Assert.False(actions[3].IsEditing);
    }

    [Fact]
    public void Edit_action_cells_carry_the_mark_the_grid_script_keys_on()
    {
        var cut = Grid(Person.Sample());

        // The own column (EditActions) and the built-in one, on every row.
        var marked = cut.FindAll("td[data-bz-edit-actions]");
        Assert.Equal(6, marked.Count);
        Assert.All(marked, td => Assert.True(CellIndex(td) is 3 or 4));
        Assert.Contains("bz-table-edit-actions-cell", cut.Find("thead th:last-child").ClassList);

        // Cell mode keeps the own column's mark.
        var cell = Grid(Person.Sample(), DataTableEditMode.Cell);
        Assert.Equal(3, cell.FindAll("td[data-bz-edit-actions]").Count);
    }

    [Fact]
    public async Task BeginEditAsync_with_a_column_marks_that_editor_for_the_focus()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEditAsync(people[0], "Score"));
        var marked = Assert.Single(cut.FindAll("[data-bz-cell-editor][data-bz-focus]"));
        Assert.Equal(2, CellIndex(marked.Closest("td")!));

        // Without a column the script picks (the cursor's column, else the first): no mark.
        await cut.InvokeAsync(() => table.CancelEditAsync());
        await cut.InvokeAsync(() => table.BeginEditAsync(people[0]));
        Assert.Empty(cut.FindAll("[data-bz-focus]"));
    }

    [Fact]
    public void EditOnClick_reaches_the_grid_script()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/dataTable.js");

        var cut = Grid(Person.Sample(), DataTableEditMode.Cell, p => p.Add(x => x.EditOnClick, true));

        var options = module.Invocations["createGrid"].Single().Arguments[2]!;
        Assert.Equal(true, options.GetType().GetProperty("editOnClick")!.GetValue(options));

        cut.Render(p => p.Add(x => x.EditOnClick, false));
        var update = JSInterop.Invocations.Last(i => i.Identifier == "update").Arguments[0]!;
        Assert.Equal(false, update.GetType().GetProperty("editOnClick")!.GetValue(update));
    }

    [Fact]
    public async Task CellContent_gets_the_value_its_text_and_the_rows_state()
    {
        var people = Person.Sample();
        var cut = Grid(people);
        var table = cut.Instance.Table!;

        Assert.Equal("36 years (36)", cut.Find($"{Row(0)} .age").TextContent);
        Assert.Equal("Ada", cut.Find($"{Row(0)} button.own").TextContent);

        await cut.InvokeAsync(() => table.BeginEditAsync(people[0]));

        // IsEditing is the row's: the template column's cell on the editing row says so.
        Assert.True(cut.Find($"{Row(0)} button.own").HasAttribute("data-row-editing"));
        Assert.False(cut.Find($"{Row(1)} button.own").HasAttribute("data-row-editing"));
    }

    [Fact]
    public async Task CellContent_sees_a_save_pending_then_its_refusal()
    {
        var people = Person.Sample();
        var save = new TaskCompletionSource();
        var cut = Grid(people, DataTableEditMode.Cell, p => p.Add(x => x.Handler, async e =>
        {
            await save.Task;
            e.Reject("Not now.");
        }));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(1, 1, "4", "typing", false));
        await cut.InvokeAsync(() => table.EndEdit(true, new() { ["Age"] = "40" }, false));

        Assert.True(cut.Find($"{Row(1)} .age").HasAttribute("data-pending"));
        Assert.Equal("40 years (40)", cut.Find($"{Row(1)} .age").TextContent);

        save.SetResult();
        cut.WaitForAssertion(() =>
        {
            var age = cut.Find($"{Row(1)} .age");
            Assert.False(age.HasAttribute("data-pending"));
            Assert.Equal("Not now.", age.GetAttribute("data-error"));
            Assert.Equal("28 years (28)", age.TextContent);
        });
    }

    [Fact]
    public void EdgeToEdge_drops_the_padding_and_fills_the_cell_in_any_table()
    {
        foreach (var editable in new[] { true, false })
        {
            var cut = Grid(Person.Sample(), DataTableEditMode.Cell, p => p.Add(x => x.Editable, editable));

            var td = cut.Find("tbody tr:first-child td:nth-child(3)");
            Assert.Contains("bz-table-cell-edge", td.ClassList);
            var fill = td.Children.Single(c => c.ClassList.Contains("bz-table-cell-fill"));
            Assert.Equal("90", fill.Children.Single().TextContent);
            Assert.DoesNotContain("bz-table-cell-edge", cut.Find("tbody tr:first-child td:nth-child(1)").ClassList);
        }
    }

    [Fact]
    public void Cells_of_an_editable_table_name_their_group_and_Cell_mode_tints_the_hovered_one()
    {
        // Cell mode: the body carries the hovered-cell tint (one class, not one per cell).
        var cell = Grid(Person.Sample(), DataTableEditMode.Cell);
        Assert.Contains("group/table-cell", cell.Find($"{Row(0)} td:nth-child(1)").ClassList);
        Assert.Contains("bz-table-body-hover", cell.Find("tbody").ClassList);

        var row = Grid(Person.Sample());
        Assert.Contains("group/table-cell", row.Find($"{Row(0)} td:nth-child(1)").ClassList);
        Assert.DoesNotContain("bz-table-body-hover", row.Find("tbody").ClassList);

        var readOnly = Grid(Person.Sample(), more: p => p.Add(x => x.Editable, false));
        Assert.DoesNotContain("group/table-cell", readOnly.Find("tbody tr:first-child td:nth-child(1)").ClassList);
        Assert.DoesNotContain("bz-table-body-hover", readOnly.Find("tbody").ClassList);
    }
}
