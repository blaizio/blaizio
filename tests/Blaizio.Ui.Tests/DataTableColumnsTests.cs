using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The .NET half of BzDataTable's column features: what renders for sizing (the colgroup), pinning
/// (the insets and edges) and moving (the order), and what the column script's two reports
/// (ColumnResized, ColumnsReordered) do to the bound state. The drags, keys and layout live in
/// ts/dataTable.ts and are exercised in a browser; here the script is the caller.
/// Columns as declared: Name, Age, Score, Code.
/// </summary>
public class DataTableColumnsTests : BunitContext
{
    public DataTableColumnsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private IRenderedComponent<ColumnGrid> Grid(Action<ComponentParameterCollectionBuilder<ColumnGrid>>? configure = null) =>
        Render<ColumnGrid>(p =>
        {
            p.Add(x => x.Items, Person.Sample());
            configure?.Invoke(p);
        });

    private static string[] HeadKeys(IRenderedComponent<ColumnGrid> cut) =>
        cut.FindAll("thead th[data-bz-column]").Select(th => th.GetAttribute("data-bz-column")!).ToArray();

    private static string[] HeadTexts(IRenderedComponent<ColumnGrid> cut) =>
        cut.FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray();

    [Fact]
    public void A_table_without_column_features_renders_as_before()
    {
        var cut = Grid();

        Assert.Empty(cut.FindAll("colgroup"));
        Assert.Empty(cut.FindAll("[data-bz-column], [data-pinned], [data-bz-resizer]"));
        Assert.DoesNotContain("bz-table-cell-fixed", cut.Find("tbody td").ClassList);
        Assert.Equal(["Name", "Age", "Score", "Code"], HeadTexts(cut));
    }

    [Fact]
    public void Sizing_renders_a_col_per_column_with_the_widths_it_knows()
    {
        var cut = Grid(p => p.Add(x => x.NameWidth, 500).Add(x => x.NameMin, 80).Add(x => x.NameMax, 300).Add(x => x.FillScore, true).Add(x => x.Selectable, true));

        var cols = cut.FindAll("table > colgroup > col");
        // The selection column, then one per visible column.
        Assert.Equal(5, cols.Count);
        Assert.Null(cols[0].GetAttribute("style"));
        // Name's 500 is past its MaxWidth of 300; the filling Score column waits for the script.
        Assert.Equal("width:300px", cols[1].GetAttribute("style"));
        Assert.Null(cols[3].GetAttribute("style"));
        Assert.Contains("bz-table-cell-fixed", cut.Find("tbody td:nth-child(2)").ClassList);
        Assert.Contains("bz-table-head-fixed", cut.Find("thead th:nth-child(2)").ClassList);
        Assert.Equal(["Name", "Age", "Score", "Code"], HeadKeys(cut));
    }

    [Fact]
    public void A_laid_out_header_holds_its_content_in_a_row_that_can_shrink()
    {
        var cut = Grid(p => p.Add(x => x.Resizable, true));

        // The title truncates inside it, and the row keeps the column's alignment.
        Assert.NotNull(cut.Find("th[data-bz-column=Name] > div.bz-table-head-content > button[data-bz-sort] > span.truncate"));
        Assert.Equal("Age", cut.Find("th[data-bz-column=Age] > div.bz-table-head-content > span.truncate").TextContent);
        Assert.Null(cut.Find("th[data-bz-column=Age] > div.bz-table-head-content").GetAttribute("data-align"));
    }

    [Fact]
    public void The_last_column_can_start_hidden()
    {
        // Its key (the property's name) is only known after it registers: the display order and
        // the hidden set are settled at render time.
        var cut = Grid(p => p.Add(x => x.CodeVisible, false).Add(x => x.Order, ["Code", "Score"]));

        Assert.Equal(["Name", "Age", "Score"], HeadTexts(cut));
    }

    [Fact]
    public void Resizable_headers_carry_the_handle_unless_a_column_opts_out()
    {
        var cut = Grid(p => p.Add(x => x.Resizable, true).Add(x => x.CodeResizable, false));

        Assert.Equal(3, cut.FindAll("th[data-resizable] > [data-bz-resizer][aria-hidden=true]").Count);
        Assert.False(cut.Find("th[data-bz-column=Code]").HasAttribute("data-resizable"));
        // The keys act on a focused header: a header with no sort button gets a tab stop of its own.
        Assert.Null(cut.Find("th[data-bz-column=Name]").GetAttribute("tabindex"));
        Assert.NotNull(cut.Find("th[data-bz-column=Name] button[data-bz-sort]"));
        Assert.Equal("0", cut.Find("th[data-bz-column=Age]").GetAttribute("tabindex"));
        Assert.Null(cut.Find("th[data-bz-column=Code]").GetAttribute("tabindex"));
    }

    [Fact]
    public void Headers_carry_the_group_their_resize_handle_lights_up_from()
    {
        var cut = Grid(p => p.Add(x => x.Resizable, true).Add(x => x.Selectable, true));

        // The handle's line takes the ring color while its header is hovered or focused.
        Assert.All(cut.FindAll("thead th"), th => Assert.Contains("group/table-head", th.ClassList));
    }

    [Fact]
    public void Movable_headers_are_the_reorderable_ones_with_somewhere_to_go()
    {
        // Code stays put (Reorderable off) and sits second: Name, boxed in between the start edge
        // and Code, has nowhere to go.
        var cut = Grid(p => p
            .Add(x => x.Reorderable, true)
            .Add(x => x.CodeReorderable, false)
            .Add(x => x.Order, ["Name", "Code", "Age", "Score"]));

        Assert.Equal(["Name", "Code", "Age", "Score"], HeadKeys(cut));
        Assert.True(cut.Find("th[data-bz-column=Name]").HasAttribute("data-reorderable"));
        Assert.False(cut.Find("th[data-bz-column=Name]").HasAttribute("data-movable"));
        Assert.False(cut.Find("th[data-bz-column=Code]").HasAttribute("data-reorderable"));
        Assert.False(cut.Find("th[data-bz-column=Code]").HasAttribute("data-movable"));
        Assert.True(cut.Find("th[data-bz-column=Age]").HasAttribute("data-movable"));
        Assert.True(cut.Find("th[data-bz-column=Score]").HasAttribute("data-movable"));

        // Without ReorderableColumns nothing moves.
        cut.Render(p => p.Add(x => x.Reorderable, false).Add(x => x.Resizable, true));
        Assert.Empty(cut.FindAll("th[data-movable], th[data-reorderable]"));
    }

    [Fact]
    public void A_pinned_column_alone_in_its_group_cannot_move()
    {
        var cut = Grid(p => p.Add(x => x.Reorderable, true).Add(x => x.CodePin, DataTablePin.Start));

        // Code moves within its group, which holds only Code.
        Assert.Equal(["Code", "Name", "Age", "Score"], HeadKeys(cut));
        Assert.True(cut.Find("th[data-bz-column=Code]").HasAttribute("data-reorderable"));
        Assert.False(cut.Find("th[data-bz-column=Code]").HasAttribute("data-movable"));
        Assert.Equal(["Name", "Age", "Score"], cut.FindAll("th[data-movable]").Select(th => th.GetAttribute("data-bz-column")!).ToArray());

        // A second start column gives it somewhere to go.
        var both = Grid(p => p.Add(x => x.Reorderable, true).Add(x => x.CodePin, DataTablePin.Start).Add(x => x.NamePin, DataTablePin.Start));

        Assert.Equal(["Name", "Code", "Age", "Score"], HeadKeys(both));
        Assert.Equal(["Name", "Code", "Age", "Score"], both.FindAll("th[data-movable]").Select(th => th.GetAttribute("data-bz-column")!).ToArray());
    }

    [Fact]
    public void LiveReorder_reaches_the_column_script()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/dataTable.js");

        Grid(p => p.Add(x => x.Reorderable, true).Add(x => x.Live, true));

        var options = module.Invocations["createColumns"].Single().Arguments[2]!;
        Assert.Equal(true, options.GetType().GetProperty("live")!.GetValue(options));
    }

    [Fact]
    public void Pinned_columns_move_to_their_edges_with_the_selection_column_at_the_start()
    {
        var cut = Grid(p => p
            .Add(x => x.Selectable, true)
            .Add(x => x.AgePin, DataTablePin.End)
            .Add(x => x.CodePin, DataTablePin.Start)
            .Add(x => x.NamePin, DataTablePin.Start));

        // Name and Code to the start (in their own order), Age to the end.
        Assert.Equal(["Name", "Code", "Score", "Age"], HeadKeys(cut));

        var heads = cut.FindAll("thead th");
        Assert.Equal("start", heads[0].GetAttribute("data-pinned"));
        Assert.Equal("inset-inline-start:0px", heads[0].GetAttribute("style"));
        Assert.Equal("inset-inline-start:var(--bz-pin-s1,0px)", heads[1].GetAttribute("style"));
        Assert.Equal("inset-inline-start:var(--bz-pin-s2,0px)", heads[2].GetAttribute("style"));
        Assert.Null(heads[3].GetAttribute("data-pinned"));
        Assert.Equal("inset-inline-end:0px", heads[4].GetAttribute("style"));
        // The divider sits on each group's inner edge.
        Assert.Equal("start", heads[2].GetAttribute("data-pinned-edge"));
        Assert.Equal("end", heads[4].GetAttribute("data-pinned-edge"));
        Assert.Null(heads[1].GetAttribute("data-pinned-edge"));
        // A header that scrolls away needs a backdrop of its own.
        Assert.True(heads[1].HasAttribute("data-pin-backdrop"));

        // Every body row carries the same marks, down to the selection cell.
        var cells = cut.FindAll("tbody tr:first-child > td");
        Assert.Equal("start", cells[0].GetAttribute("data-pinned"));
        Assert.Equal("inset-inline-start:var(--bz-pin-s2,0px)", cells[2].GetAttribute("style"));
        Assert.Equal("start", cells[2].GetAttribute("data-pinned-edge"));
        Assert.Null(cells[3].GetAttribute("data-pinned"));
        Assert.Equal("end", cells[4].GetAttribute("data-pinned"));
        Assert.Equal("Ada", cells[1].TextContent);
    }

    [Fact]
    public void A_sticky_header_paints_its_own_pinned_cells()
    {
        var cut = Grid(p => p.Add(x => x.Sticky, true).Add(x => x.NamePin, DataTablePin.Start));

        var name = cut.Find("th[data-bz-column=Name]");
        Assert.Equal("start", name.GetAttribute("data-pinned"));
        Assert.False(name.HasAttribute("data-pin-backdrop"));
        // No sizing asked for: pinning alone renders no colgroup.
        Assert.Empty(cut.FindAll("colgroup"));
    }

    [Fact]
    public void ColumnOrder_reshuffles_the_columns_it_names_and_leaves_the_others_in_place()
    {
        // Age and Name trade the places they held; Score and Code (unlisted) and an unknown key stay out of it.
        var cut = Grid(p => p.Add(x => x.Order, ["Age", "Gone", "Name"]));

        Assert.Equal(["Age", "Name", "Score", "Code"], HeadTexts(cut));
        Assert.Equal("36", cut.Find("tbody tr:first-child > td").TextContent);
    }

    [Fact]
    public void DefaultColumnOrder_seeds_an_uncontrolled_table()
    {
        var cut = Grid(p => p.Add(x => x.Bind, false).Add(x => x.DefaultOrder, ["Code", "Score", "Age", "Name"]));

        Assert.Equal(["Code", "Score", "Age", "Name"], HeadTexts(cut));
    }

    [Fact]
    public async Task A_finished_resize_is_stored_clamped_and_raised_once()
    {
        var cut = Grid(p => p.Add(x => x.Resizable, true).Add(x => x.NameMin, 80));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.ColumnResized("Name", 41.6));

        // Name's MinWidth is 80.
        var widths = Assert.Single(cut.Instance.WidthChanges);
        Assert.Equal(80, widths!["Name"]);
        Assert.Equal("width:80px", cut.Find("table > colgroup > col").GetAttribute("style"));

        await cut.InvokeAsync(() => table.ColumnResized("Age", 120.4));

        Assert.Equal(2, cut.Instance.WidthChanges.Count);
        Assert.Equal(new Dictionary<string, double> { ["Name"] = 80, ["Age"] = 120 }, cut.Instance.WidthChanges[1]);
        Assert.Equal("width:120px", cut.FindAll("table > colgroup > col")[1].GetAttribute("style"));
    }

    [Fact]
    public async Task A_controlled_parent_that_keeps_its_widths_keeps_the_table_at_them()
    {
        var cut = Grid(p => p.Add(x => x.Resizable, true).Add(x => x.Reject, true).Add(x => x.Widths, new Dictionary<string, double> { ["Age"] = 90 }));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.ColumnResized("Age", 200));

        Assert.Equal(200, cut.Instance.WidthChanges.Single()!["Age"]);
        Assert.Equal("width:90px", cut.FindAll("table > colgroup > col")[1].GetAttribute("style"));
    }

    [Fact]
    public async Task Resetting_the_bound_widths_goes_back_to_the_columns_own()
    {
        var cut = Grid(p => p.Add(x => x.Resizable, true).Add(x => x.NameWidth, 150));
        var table = cut.Instance.Table!;
        await cut.InvokeAsync(() => table.ColumnResized("Name", 220));
        Assert.Equal("width:220px", cut.Find("table > colgroup > col").GetAttribute("style"));

        cut.Render(p => p.Add(x => x.Widths, null));

        Assert.Equal("width:150px", cut.Find("table > colgroup > col").GetAttribute("style"));
    }

    [Fact]
    public async Task A_move_raises_every_key_in_the_new_order_with_hidden_columns_kept_in_place()
    {
        var cut = Grid(p => p.Add(x => x.Reorderable, true).Add(x => x.ScoreVisible, false));
        var table = cut.Instance.Table!;
        Assert.Equal(["Name", "Age", "Code"], HeadKeys(cut));

        await cut.InvokeAsync(() => table.ColumnsReordered(["Code", "Name", "Age"]));

        // Score (hidden) keeps the third place; the visible columns fill the others in their new order.
        Assert.Equal(["Code", "Name", "Score", "Age"], cut.Instance.OrderChanges.Single());
        Assert.Equal(["Code", "Name", "Age"], HeadKeys(cut));
        Assert.Equal("C3", cut.FindAll("tbody tr")[2].QuerySelector("td")!.TextContent);
    }

    [Fact]
    public async Task A_move_out_of_step_with_the_columns_is_ignored()
    {
        var cut = Grid(p => p.Add(x => x.Reorderable, true));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.ColumnsReordered(["Code", "Name", "Age"]));
        await cut.InvokeAsync(() => table.ColumnsReordered(["Code", "Name", "Age", "Nope"]));

        Assert.Empty(cut.Instance.OrderChanges);
        Assert.Equal(["Name", "Age", "Score", "Code"], HeadKeys(cut));
    }

    [Fact]
    public async Task Pinned_groups_hold_whatever_order_comes_in()
    {
        var cut = Grid(p => p.Add(x => x.Reorderable, true).Add(x => x.CodePin, DataTablePin.Start));
        var table = cut.Instance.Table!;
        Assert.Equal(["Code", "Name", "Age", "Score"], HeadKeys(cut));

        // An order that puts the pinned column last: it stays at the start edge.
        await cut.InvokeAsync(() => table.ColumnsReordered(["Name", "Age", "Score", "Code"]));

        Assert.Equal(["Code", "Name", "Age", "Score"], HeadKeys(cut));
        Assert.Equal(["Name", "Age", "Score", "Code"], cut.Instance.OrderChanges.Single());
    }

    [Fact]
    public async Task Editing_follows_the_display_order()
    {
        var people = Person.Sample();
        var cut = Render<ColumnGrid>(p => p
            .Add(x => x.Items, people)
            .Add(x => x.Editable, true)
            .Add(x => x.Order, ["Age", "Name"]));
        var table = cut.Instance.Table!;

        // The grid's first column is now Age.
        Assert.True(await cut.InvokeAsync(() => table.BeginEdit(0, 0, null, "key", false)));
        Assert.NotNull(cut.Find("tbody tr:first-child > td:first-child [data-bz-cell-editor=number]"));
        await cut.InvokeAsync(() => table.EndEdit(true, new Dictionary<string, string?> { ["Age"] = "40" }, false));

        Assert.Equal(40, people[0].Age);
        Assert.Equal("Ada", people[0].Name);
    }

    // A child column gets each render's parameters after the table has drawn it: a change has to
    // show in the render that made it, as a first render with the new values would show it.

    [Fact]
    public void A_pin_set_at_runtime_arranges_the_columns_in_the_same_render()
    {
        var cut = Grid(p => p.Add(x => x.Reorderable, true).Add(x => x.CodePin, DataTablePin.Start));
        Assert.Equal(["Code", "Name", "Age", "Score"], HeadKeys(cut));

        cut.Render(p => p.Add(x => x.NamePin, DataTablePin.Start));

        // Name and Code at the start edge, in their declared order.
        Assert.Equal(["Name", "Code", "Age", "Score"], HeadKeys(cut));
        Assert.Equal("inset-inline-start:0px", cut.Find("th[data-bz-column=Name]").GetAttribute("style"));
        Assert.Equal("start", cut.Find("th[data-bz-column=Code]").GetAttribute("data-pinned-edge"));
        Assert.Equal("Ada", cut.Find("tbody tr:first-child > td").TextContent);

        cut.Render(p => p.Add(x => x.CodePin, DataTablePin.None));

        Assert.Equal(["Name", "Age", "Score", "Code"], HeadKeys(cut));
        Assert.Null(cut.Find("th[data-bz-column=Code]").GetAttribute("data-pinned"));
    }

    [Fact]
    public void Sizes_and_switches_changed_at_runtime_apply_in_the_same_render()
    {
        var cut = Grid(p => p.Add(x => x.Resizable, true).Add(x => x.Reorderable, true));
        Assert.Null(cut.Find("table > colgroup > col").GetAttribute("style"));

        cut.Render(p => p
            .Add(x => x.NameWidth, 200)
            .Add(x => x.CodeResizable, false)
            .Add(x => x.CodeReorderable, false));

        Assert.Equal("width:200px", cut.Find("table > colgroup > col").GetAttribute("style"));
        var code = cut.Find("th[data-bz-column=Code]");
        Assert.False(code.HasAttribute("data-resizable"));
        Assert.False(code.HasAttribute("data-reorderable"));
        Assert.False(code.HasAttribute("data-movable"));
    }

    [Fact]
    public void Sizing_asked_for_at_runtime_lays_the_table_out_in_the_same_render()
    {
        var cut = Grid();
        Assert.Empty(cut.FindAll("colgroup"));

        cut.Render(p => p.Add(x => x.FillScore, true));

        Assert.Equal(4, cut.FindAll("table > colgroup > col").Count);
        Assert.Contains("bz-table-cell-fixed", cut.Find("tbody td").ClassList);
    }

    [Fact]
    public void Editing_switched_on_at_runtime_makes_the_table_a_grid_in_the_same_render()
    {
        var cut = Grid();
        Assert.NotEqual("grid", cut.Find("table").GetAttribute("role"));

        cut.Render(p => p.Add(x => x.Editable, true));

        Assert.Equal("grid", cut.Find("table").GetAttribute("role"));
    }

    [Fact]
    public void Edge_to_edge_switched_on_at_runtime_drops_the_padding_in_the_same_render()
    {
        const string score = "tbody tr:first-child > td:nth-child(3)";
        var cut = Grid();
        Assert.DoesNotContain("bz-table-cell-edge", cut.Find(score).ClassList);

        cut.Render(p => p.Add(x => x.ScoreEdge, true));

        // The cell loses its padding in the render its content starts filling it.
        Assert.Contains("bz-table-cell-edge", cut.Find(score).ClassList);
        Assert.NotNull(cut.Find(score).QuerySelector(".bz-table-cell-fill"));
    }

    [Fact]
    public void A_sort_switch_changed_at_runtime_sorts_the_rows_in_the_same_render()
    {
        var cut = Grid(p => p.Add(x => x.NameSortable, false).Add(x => x.DefaultSort, new SortState("Name", SortDirection.Descending)));
        // The sort names a column that cannot sort: the rows keep their order.
        Assert.Equal("Ada", cut.Find("tbody tr:first-child > td").TextContent);

        cut.Render(p => p.Add(x => x.NameSortable, true));

        Assert.Equal("descending", cut.Find("thead th").GetAttribute("aria-sort"));
        Assert.Equal("Cy", cut.Find("tbody tr:first-child > td").TextContent);
    }

    [Fact]
    public void Only_a_changed_column_costs_the_table_a_second_render()
    {
        var cut = Grid(p => p.Add(x => x.Reorderable, true));

        var before = cut.Instance.Passes;
        cut.Render(p => p.Add(x => x.Sticky, true));
        Assert.Equal(before + 1, cut.Instance.Passes);

        before = cut.Instance.Passes;
        cut.Render(p => p.Add(x => x.AgePin, DataTablePin.End));
        Assert.Equal(before + 2, cut.Instance.Passes);
        Assert.Equal(["Name", "Score", "Code", "Age"], HeadKeys(cut));
    }

    [Fact]
    public void A_column_handed_a_new_value_on_every_pass_cannot_loop()
    {
        // Each render of the table hands the column a new width: it stops asking after two in a row.
        var cut = Render<RestlessGrid>(p => p.Add(x => x.Items, Person.Sample()));
        var first = cut.Instance.Passes;
        Assert.InRange(first, 2, 4);

        cut.Render();

        Assert.InRange(cut.Instance.Passes - first, 1, 3);
    }

    [Fact]
    public async Task A_column_in_a_component_of_its_own_rearranges_the_table_each_time_it_changes()
    {
        var cut = Render<WrappedGrid>(p => p.Add(x => x.Items, Person.Sample()));
        string[] Texts() => cut.FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray();
        Assert.Equal(["Name", "Age", "Code"], Texts());

        // Back and forth, with no render of the table's own in between.
        for (var i = 0; i < 3; i++)
        {
            await cut.InvokeAsync(cut.Instance.Code!.Toggle);
            Assert.Equal(i % 2 == 0 ? ["Code", "Name", "Age"] : ["Name", "Age", "Code"], Texts());
        }
    }

    // Code is the last column to register, and its key, sorting and text are known only after that.

    [Fact]
    public void A_search_only_the_last_column_matches_finds_its_row_on_the_first_render()
    {
        var cut = Grid(p => p.Add(x => x.DefaultSearch, "C3"));

        var row = Assert.Single(cut.FindAll("tbody tr"));
        Assert.Equal("Cy", row.QuerySelector("td")!.TextContent);
    }

    [Fact]
    public void A_sort_on_the_last_column_orders_the_rows_on_the_first_render()
    {
        var cut = Grid(p => p.Add(x => x.CodeSortable, true).Add(x => x.DefaultSort, new SortState("Code", SortDirection.Descending)));

        Assert.Equal(["Cy", "Bo", "Ada"], cut.FindAll("tbody tr").Select(tr => tr.QuerySelector("td")!.TextContent).ToArray());
    }

    [Fact]
    public void Columns_defined_as_data_size_and_pin_too()
    {
        var columns = new List<DataTableColumn<Person>>
        {
            new() { Title = "Name", Cell = p => b => b.AddContent(0, p.Name), Width = 140, Pinned = DataTablePin.End },
            new() { Title = "Code", Cell = p => b => b.AddContent(0, p.Code), MinWidth = 60, Fill = true },
        };
        var cut = Render<BzDataTable<Person>>(p => p.Add(x => x.Items, Person.Sample()).Add(x => x.Columns, columns));

        Assert.Equal(["Code", "Name"], cut.FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray());
        Assert.Equal("end", cut.Find("th[data-bz-column=Name]").GetAttribute("data-pinned"));
        var cols = cut.FindAll("colgroup > col");
        Assert.Null(cols[0].GetAttribute("style"));
        Assert.Equal("width:140px", cols[1].GetAttribute("style"));
    }
}
