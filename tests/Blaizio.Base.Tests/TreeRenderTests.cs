using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Covers the C# contract of the tree. The pointer drag lives in ts/tree.ts and is verified
/// in-browser; here we test the pure move logic (<see cref="TreeChange"/>.Apply), the rendered
/// roles/aria/data contract, and the expansion / selection / checkbox / keyboard behaviour the
/// root drives. JSInterop is Loose so the module import in OnAfterRender is a no-op.
/// </summary>
public class TreeRenderTests : TestContext
{
    public TreeRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private sealed class Node
    {
        public required string Value { get; init; }
        public string Text => Value;
        public bool Disabled { get; init; }
        public List<Node>? Children { get; set; }
    }

    private static Node Leaf(string value, bool disabled = false) => new() { Value = value, Disabled = disabled };

    private static Node Branch(string value, params Node[] children) =>
        new() { Value = value, Children = [.. children] };

    // docs/ (getting-started, api) src/ (button, tree) readme
    private static List<Node> Sample() =>
    [
        Branch("docs", Leaf("getting-started"), Leaf("api")),
        Branch("src", Leaf("button"), Leaf("tree")),
        Leaf("readme"),
    ];

    private static TreeChange Change(string source, string? target, TreeDropPosition position, string from = "t", string? to = null) =>
        new() { SourceValue = source, TargetValue = target, Position = position, FromId = from, ToId = to ?? from };

    private static bool Apply(TreeChange change, List<Node> roots) =>
        change.Apply(roots, n => n.Children, n => n.Value);

    // ---- TreeChange.Apply (pure move logic) ------------------------------------------------------

    [Fact]
    public void Apply_moves_before_a_sibling()
    {
        var roots = Sample();
        Assert.True(Apply(Change("readme", "docs", TreeDropPosition.Before), roots));
        Assert.Equal(["readme", "docs", "src"], roots.Select(n => n.Value));
    }

    [Fact]
    public void Apply_moves_after_across_levels()
    {
        var roots = Sample();
        Assert.True(Apply(Change("getting-started", "src", TreeDropPosition.After), roots));
        Assert.Equal(["docs", "src", "getting-started", "readme"], roots.Select(n => n.Value));
        Assert.Equal(["api"], roots[0].Children!.Select(n => n.Value));
    }

    [Fact]
    public void Apply_moves_inside_a_branch()
    {
        var roots = Sample();
        Assert.True(Apply(Change("readme", "docs", TreeDropPosition.Inside), roots));
        Assert.Equal(["docs", "src"], roots.Select(n => n.Value));
        Assert.Equal(["getting-started", "api", "readme"], roots[0].Children!.Select(n => n.Value));
    }

    [Fact]
    public void Apply_rejects_drop_into_own_subtree()
    {
        var roots = Sample();
        Assert.False(Apply(Change("docs", "api", TreeDropPosition.After), roots));
        Assert.Equal(["docs", "src", "readme"], roots.Select(n => n.Value));
        Assert.Equal(["getting-started", "api"], roots[0].Children!.Select(n => n.Value));
    }

    [Fact]
    public void Apply_rejects_inside_a_leaf()
    {
        var roots = Sample();
        Assert.False(Apply(Change("api", "readme", TreeDropPosition.Inside), roots));
        Assert.Equal(["getting-started", "api"], roots[0].Children!.Select(n => n.Value));
    }

    [Fact]
    public void Apply_null_target_appends_to_roots()
    {
        var roots = Sample();
        Assert.True(Apply(Change("api", null, TreeDropPosition.After), roots));
        Assert.Equal(["docs", "src", "readme", "api"], roots.Select(n => n.Value));
    }

    [Fact]
    public void Apply_single_tree_overload_ignores_cross_tree_change()
    {
        var roots = Sample();
        Assert.False(Apply(Change("api", "readme", TreeDropPosition.Before, from: "left", to: "right"), roots));
        Assert.Equal(["getting-started", "api"], roots[0].Children!.Select(n => n.Value));
    }

    [Fact]
    public void Apply_two_tree_overload_transfers_between_trees()
    {
        var left = Sample();
        var right = new List<Node> { Branch("inbox", Leaf("hello")) };
        var change = Change("api", "hello", TreeDropPosition.After, from: "left", to: "right");

        Assert.True(change.IsCrossTree);
        Assert.True(change.Apply(left, right, n => n.Children, n => n.Value));
        Assert.Equal(["getting-started"], left[0].Children!.Select(n => n.Value));
        Assert.Equal(["hello", "api"], right[0].Children!.Select(n => n.Value));
    }

    // ---- rendering --------------------------------------------------------------------------------

    [Fact]
    public void Renders_tree_roles_and_levels()
    {
        var cut = RenderTreeComponent(Sample(), expanded: ["docs"]);

        var tree = cut.Find("[role=tree]");
        Assert.Equal("Tree", tree.GetAttribute("aria-label"));

        var items = cut.FindAll("[role=treeitem]");
        Assert.Equal(5, items.Count); // docs (+2 children), src (collapsed), readme

        var docs = cut.Find("[data-part=branch][data-value=docs]");
        Assert.Equal("true", docs.GetAttribute("aria-expanded"));
        Assert.Equal("1", docs.GetAttribute("aria-level"));
        Assert.Equal("3", docs.GetAttribute("aria-setsize"));

        var child = cut.Find("[data-part=item][data-value=api]");
        Assert.Equal("2", child.GetAttribute("aria-level"));
        Assert.Equal("2", child.GetAttribute("aria-posinset"));

        Assert.Single(cut.FindAll("[role=group]")); // only the expanded branch renders its group
    }

    [Fact]
    public void Roving_tab_stop_starts_on_first_node()
    {
        var cut = RenderTreeComponent(Sample());
        Assert.Equal("0", cut.Find("[data-value=docs]").GetAttribute("tabindex"));
        Assert.Equal("-1", cut.Find("[data-part=item][data-value=readme]").GetAttribute("tabindex"));
    }

    // ---- expansion + selection ----------------------------------------------------------------------

    [Fact]
    public void Clicking_a_branch_row_expands_and_selects_it()
    {
        var cut = RenderTreeComponent(Sample());
        cut.Find("[data-part=branch-control][data-value=docs]").Click();

        Assert.Equal("true", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("aria-expanded"));
        Assert.NotNull(cut.Find("[data-part=branch-control][data-value=docs]").GetAttribute("data-selected"));
        Assert.Equal(2, cut.FindAll("[data-part=item]").Count(e => e.GetAttribute("data-depth") == "1"));
    }

    [Fact]
    public void Single_mode_selection_replaces()
    {
        string? selected = null;
        var cut = RenderTreeComponent(Sample(), configure: ps => ps
            .Add(t => t.SelectedValueChanged, EventCallback.Factory.Create<string?>(this, v => selected = v)));

        cut.Find("[data-part=item][data-value=readme]").Click();
        Assert.Equal("readme", selected);

        cut.Find("[data-part=branch-control][data-value=src]").Click();
        Assert.Equal("src", selected);
    }

    [Fact]
    public void Multiple_mode_ctrl_click_toggles_and_shift_click_ranges()
    {
        IReadOnlyList<string> selected = [];
        var cut = RenderTreeComponent(Sample(), expanded: ["docs"], configure: ps => ps
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.ExpandOnClick, false)
            .Add(t => t.SelectedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => selected = v)));

        cut.Find("[data-part=branch-control][data-value=docs]").Click();
        cut.Find("[data-part=item][data-value=api]").Click(new MouseEventArgs { CtrlKey = true });
        Assert.Equal(["api", "docs"], selected.Order());

        // A plain click re-anchors on api; shift-click then ranges api..src in visible order.
        cut.Find("[data-part=item][data-value=api]").Click();
        cut.Find("[data-part=branch-control][data-value=src]").Click(new MouseEventArgs { ShiftKey = true });
        Assert.Equal(["api", "src"], selected);
    }

    [Fact]
    public void Disabled_node_is_not_selectable()
    {
        IReadOnlyList<string> selected = [];
        var items = new List<Node> { Leaf("a"), Leaf("locked", disabled: true) };
        var cut = RenderTreeComponent(items, configure: ps => ps
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.SelectedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => selected = v)));

        cut.Find("[data-part=item][data-value=locked]").Click();
        Assert.Empty(selected);
    }

    // ---- keyboard --------------------------------------------------------------------------------------

    [Fact]
    public void ArrowDown_moves_the_roving_focus()
    {
        var cut = RenderTreeComponent(Sample());
        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Equal("-1", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("tabindex"));
        Assert.Equal("0", cut.Find("[data-part=branch][data-value=src]").GetAttribute("tabindex"));
        Assert.NotNull(cut.Find("[data-part=branch-control][data-value=src]").GetAttribute("data-focus"));
    }

    [Fact]
    public void ArrowRight_expands_then_enters_a_branch()
    {
        var cut = RenderTreeComponent(Sample());
        var docs = cut.Find("[data-value=docs]");

        docs.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("true", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("aria-expanded"));

        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", cut.Find("[data-part=item][data-value=getting-started]").GetAttribute("tabindex"));
    }

    [Fact]
    public void ArrowLeft_collapses_then_climbs_to_the_parent()
    {
        var cut = RenderTreeComponent(Sample(), expanded: ["docs"]);
        var child = cut.Find("[data-part=item][data-value=api]");

        child.KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" }); // on a leaf: climb
        Assert.Equal("0", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("tabindex"));

        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" }); // on an open branch: collapse
        Assert.Equal("false", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Home_and_End_jump_to_the_edges()
    {
        var cut = RenderTreeComponent(Sample(), expanded: ["docs"]);
        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("0", cut.Find("[data-part=item][data-value=readme]").GetAttribute("tabindex"));

        cut.Find("[data-part=item][data-value=readme]").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("0", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("tabindex"));
    }

    [Fact]
    public void Enter_selects_the_focused_node()
    {
        string? selected = null;
        var cut = RenderTreeComponent(Sample(), configure: ps => ps
            .Add(t => t.ExpandOnClick, false)
            .Add(t => t.SelectedValueChanged, EventCallback.Factory.Create<string?>(this, v => selected = v)));

        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("docs", selected);
    }

    [Fact]
    public void Ctrl_A_selects_all_visible_in_multiple_mode()
    {
        IReadOnlyList<string> selected = [];
        var cut = RenderTreeComponent(Sample(), expanded: ["docs"], configure: ps => ps
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.SelectedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => selected = v)));

        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "a", CtrlKey = true });
        Assert.Equal(["docs", "getting-started", "api", "src", "readme"], selected);
    }

    [Fact]
    public void Asterisk_expands_all_sibling_branches()
    {
        var cut = RenderTreeComponent(Sample());
        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "*" });

        Assert.Equal("true", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("aria-expanded"));
        Assert.Equal("true", cut.Find("[data-part=branch][data-value=src]").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Typeahead_jumps_to_the_next_match()
    {
        var cut = RenderTreeComponent(Sample());
        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = "r" });
        Assert.Equal("0", cut.Find("[data-part=item][data-value=readme]").GetAttribute("tabindex"));
    }

    // ---- checkboxes ------------------------------------------------------------------------------------

    [Fact]
    public void Checking_a_leaf_makes_the_branch_indeterminate_then_checked()
    {
        IReadOnlyList<string> checkedValues = [];
        var cut = RenderTreeComponent(Sample(), expanded: ["docs"], configure: ps => ps
            .Add(t => t.Checkable, true)
            .Add(t => t.CheckedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => checkedValues = v)));

        cut.Find("[data-part=item][data-value=api] [data-part=checkbox]").Click();
        Assert.Equal(["api"], checkedValues);
        Assert.Equal("mixed", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("aria-checked"));

        cut.Find("[data-part=item][data-value=getting-started] [data-part=checkbox]").Click();
        Assert.Equal(["api", "getting-started"], checkedValues.Order());
        Assert.Equal("true", cut.Find("[data-part=branch][data-value=docs]").GetAttribute("aria-checked"));
    }

    [Fact]
    public void Checking_a_branch_toggles_its_whole_subtree()
    {
        IReadOnlyList<string> checkedValues = [];
        var cut = RenderTreeComponent(Sample(), expanded: ["docs"], configure: ps => ps
            .Add(t => t.Checkable, true)
            .Add(t => t.CheckedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => checkedValues = v)));

        cut.Find("[data-part=branch-control][data-value=docs] [data-part=checkbox]").Click();
        Assert.Equal(["api", "getting-started"], checkedValues.Order());

        cut.Find("[data-part=branch-control][data-value=docs] [data-part=checkbox]").Click();
        Assert.Empty(checkedValues);
    }

    [Fact]
    public void Space_toggles_the_check_when_checkable()
    {
        IReadOnlyList<string> checkedValues = [];
        var cut = RenderTreeComponent(Sample(), configure: ps => ps
            .Add(t => t.Checkable, true)
            .Add(t => t.CheckedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => checkedValues = v)));

        cut.Find("[data-part=item][data-value=readme]").KeyDown(new KeyboardEventArgs { Key = " " });
        Assert.Equal(["readme"], checkedValues);
    }

    [Fact]
    public void Space_toggles_selection_in_multiple_mode()
    {
        IReadOnlyList<string> selected = [];
        var cut = RenderTreeComponent(Sample(), configure: ps => ps
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.SelectedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => selected = v)));

        // Non-adjacent multi-select without a pointer: toggle docs, then toggle readme.
        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = " " });
        cut.Find("[data-part=item][data-value=readme]").KeyDown(new KeyboardEventArgs { Key = " " });
        Assert.Equal(["docs", "readme"], selected.Order());

        cut.Find("[data-value=docs]").KeyDown(new KeyboardEventArgs { Key = " " });
        Assert.Equal(["readme"], selected);
    }

    // ---- keyboard drag (grab mode) -----------------------------------------------------------------------

    [Fact]
    public void Ctrl_Space_grabs_and_arrows_move_the_node()
    {
        TreeChange? change = null;
        var cut = RenderTreeComponent(Sample(), configure: ps => ps
            .Add(t => t.Draggable, true)
            .Add(t => t.OnMove, EventCallback.Factory.Create<TreeChange>(this, c => change = c)));

        var readme = cut.Find("[data-part=item][data-value=readme]");
        readme.KeyDown(new KeyboardEventArgs { Key = " ", CtrlKey = true }); // grab
        Assert.NotNull(cut.Find("[data-part=item][data-value=readme]").GetAttribute("data-grabbed"));

        cut.Find("[data-part=item][data-value=readme]").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.NotNull(change);
        Assert.Equal("readme", change!.SourceValue);
        Assert.Equal("src", change.TargetValue);
        Assert.Equal(TreeDropPosition.Before, change.Position);

        cut.Find("[data-part=item][data-value=readme]").KeyDown(new KeyboardEventArgs { Key = "Escape" }); // release
        Assert.Null(cut.Find("[data-part=item][data-value=readme]").GetAttribute("data-grabbed"));
    }

    [Fact]
    public void Grabbed_ArrowRight_moves_inside_the_branch_above()
    {
        TreeChange? change = null;
        var cut = RenderTreeComponent(Sample(), configure: ps => ps
            .Add(t => t.Draggable, true)
            .Add(t => t.OnMove, EventCallback.Factory.Create<TreeChange>(this, c => change = c)));

        var readme = cut.Find("[data-part=item][data-value=readme]");
        readme.KeyDown(new KeyboardEventArgs { Key = " ", CtrlKey = true });
        cut.Find("[data-part=item][data-value=readme]").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.NotNull(change);
        Assert.Equal("src", change!.TargetValue); // the collapsed branch just above
        Assert.Equal(TreeDropPosition.Inside, change.Position);
        // The receiving branch was expanded so the moved node stays visible.
        Assert.Equal("true", cut.Find("[data-part=branch][data-value=src]").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Pinned_nodes_render_drag_disabled_and_refuse_the_grab()
    {
        var items = new List<Node> { Leaf("a"), Leaf("pinned"), Leaf("b") };
        var cut = RenderTreeComponent(items, configure: ps => ps
            .Add(t => t.Draggable, true)
            .Add(t => t.CanDrag, n => n.Value != "pinned")
            .Add(t => t.OnMove, EventCallback.Factory.Create<TreeChange>(this, _ => { })));

        var pinned = cut.Find("[data-part=item][data-value=pinned]");
        Assert.NotNull(pinned.GetAttribute("data-drag-disabled"));
        Assert.Null(cut.Find("[data-part=item][data-value=a]").GetAttribute("data-drag-disabled"));

        pinned.KeyDown(new KeyboardEventArgs { Key = " ", CtrlKey = true });
        Assert.Null(cut.Find("[data-part=item][data-value=pinned]").GetAttribute("data-grabbed"));
    }

    // ---- rename -----------------------------------------------------------------------------------------

    [Fact]
    public void F2_starts_a_rename_and_enter_commits_it()
    {
        TreeRename<Node>? rename = null;
        var cut = RenderTreeComponent(Sample(), configure: ps => ps
            .Add(t => t.Renamable, true)
            .Add(t => t.OnRename, EventCallback.Factory.Create<TreeRename<Node>>(this, r => rename = r)));

        cut.Find("[data-part=item][data-value=readme]").KeyDown(new KeyboardEventArgs { Key = "F2" });
        var input = cut.Find("[data-part=rename-input]");
        Assert.Equal("readme", input.GetAttribute("value"));

        input.Input("changelog");
        cut.Find("[data-part=rename-input]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotNull(rename);
        Assert.Equal("readme", rename!.Value);
        Assert.Equal("changelog", rename.Text);
        Assert.Empty(cut.FindAll("[data-part=rename-input]"));
    }

    // ---- cross-tree drops ----------------------------------------------------------------------------------

    [Fact]
    public async Task Receiving_a_cross_tree_drop_adopts_the_carried_expansion()
    {
        // Expansion is per-tree state: without the carried values, a subtree dropped in from another
        // tree would always arrive collapsed (its first visit here has no expansion record).
        var cut = RenderTreeComponent(Sample());
        Assert.Equal("false", cut.Find("[data-part=branch][data-value=src]").GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => cut.Instance.NotifyReceiveMove(
            "src", null, "after", fromId: "other", toId: "t", expandedValues: ["src"]));

        Assert.Equal("true", cut.Find("[data-part=branch][data-value=src]").GetAttribute("aria-expanded"));
    }

    // ---- virtualization guards -----------------------------------------------------------------------------

    [Fact]
    public void Virtualized_combined_with_Draggable_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RenderTreeComponent(Sample(), configure: ps => ps
                .Add(t => t.Virtualized, true)
                .Add(t => t.Draggable, true)));
        Assert.Contains("Virtualized", ex.Message);
        Assert.Contains("Draggable", ex.Message);
    }

    [Fact]
    public void Virtualized_renders_a_flat_list_of_treeitems_with_no_groups()
    {
        var cut = RenderTreeComponent(Sample(), expanded: ["docs", "src"],
            configure: ps => ps.Add(t => t.Virtualized, true));

        // Flattened: treeitems exist, but there are no nested role=group containers.
        Assert.NotEmpty(cut.FindAll("[role=treeitem]"));
        Assert.Empty(cut.FindAll("[role=group]"));

        // Every rendered row is a self-contained, focusable treeitem row.
        var rows = cut.FindAll("[data-tree-row]");
        Assert.NotEmpty(rows);
        Assert.All(rows, el => Assert.NotNull(el.GetAttribute("tabindex")));
    }

    [Fact]
    public void Virtualized_row_height_mismatch_throws()
    {
        var cut = RenderTreeComponent(Sample(), configure: ps => ps.Add(t => t.Virtualized, true));

        // Default RowHeightPx is 36; a measured 50px row is a misconfiguration.
        Assert.Throws<InvalidOperationException>(() => cut.Instance.OnVirtualMeasure(50));
    }

    [Fact]
    public void Virtualized_matching_row_height_does_not_throw()
    {
        var cut = RenderTreeComponent(Sample(), configure: ps => ps.Add(t => t.Virtualized, true));

        cut.Instance.OnVirtualMeasure(36); // equals the default RowHeightPx - no exception
    }

    // ---- helpers ------------------------------------------------------------------------------------------

    private IRenderedComponent<BaseTree<Node>> RenderTreeComponent(
        List<Node> items,
        IReadOnlyList<string>? expanded = null,
        Action<ComponentParameterCollectionBuilder<BaseTree<Node>>>? configure = null)
    {
        return RenderComponent<BaseTree<Node>>(ps =>
        {
            ps.Add(t => t.Items, items)
              .Add(t => t.ValueSelector, n => n.Value)
              .Add(t => t.TextSelector, n => n.Text)
              .Add(t => t.ChildrenSelector, n => n.Children)
              .Add(t => t.DisabledSelector, n => n.Disabled);
            if (expanded is not null) ps.Add(t => t.DefaultExpandedValues, expanded);
            configure?.Invoke(ps);
        });
    }
}
