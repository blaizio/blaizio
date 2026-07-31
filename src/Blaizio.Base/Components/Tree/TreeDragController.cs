namespace Blaizio;

/// <summary>
/// The keyboard drag ("grab mode") for a <see cref="BaseTree{TItem}"/>, plus the
/// <see cref="TreeChange"/> construction both drag paths share. Ctrl+Space grabs the focused node,
/// the arrows propose a move relative to the visible order, Ctrl+Space/Enter drops, Escape cancels;
/// every proposed move runs through the tree's CanDrop veto and is raised as OnMove - nothing here
/// mutates the consumer's data. (The pointer drag lives in ts/tree.ts; its committed drops arrive
/// through the tree's JSInvokable notifiers, which call <see cref="BuildChange"/> here.)
/// </summary>
internal sealed class TreeDragController<TItem>(BaseTree<TItem> tree)
{
    /// <summary>The value of the node currently held by a keyboard grab, or null.</summary>
    public string? GrabbedValue { get; private set; }

    public bool IsGrabbed(string value) => GrabbedValue == value;

    /// <summary>Called when a node unmounts: a grabbed node that left the tree is released silently.</summary>
    public void OnNodeGone(string value)
    {
        if (GrabbedValue == value) GrabbedValue = null;
    }

    public void Grab(TItem item, string value)
    {
        if (!tree.CanDragNode(item) || !tree.OnMove.HasDelegate) return;
        GrabbedValue = value;
        tree.Announce($"Grabbed {tree.TextOf(item)}. Use the arrow keys to move, Control Space or Enter to drop, Escape to cancel.");
        tree.Rerender();
    }

    public void Release(bool cancelled)
    {
        if (GrabbedValue is null) return;
        tree.RequestRefocus(GrabbedValue);
        GrabbedValue = null;
        tree.Announce(cancelled ? "Move cancelled." : "Dropped.");
        tree.Rerender();
    }

    /// <summary>Routes a keydown while a node is grabbed: the arrows MOVE the node instead of moving focus.</summary>
    public async Task HandleGrabbedKeyAsync(string key, string expandKey, string collapseKey)
    {
        switch (key)
        {
            case "Escape":
                Release(cancelled: true);
                return;
            case " ":
            case "Enter":
                Release(cancelled: false);
                return;
        }

        var visible = tree.EnsureVisible();
        var index = visible.FindIndex(f => f.Value == GrabbedValue);
        if (index < 0) { Release(cancelled: true); return; }
        var node = visible[index];

        // A grabbed node's visible descendants sit right below it (depth-first order).
        var subtreeEnd = index + 1;
        while (subtreeEnd < visible.Count && visible[subtreeEnd].Depth > node.Depth) subtreeEnd++;

        TreeChange? change = null;
        if (key == "ArrowUp" && index > 0)
        {
            change = MakeChange(node.Value, visible[index - 1].Value, TreeDropPosition.Before);
        }
        else if (key == "ArrowDown" && subtreeEnd < visible.Count)
        {
            change = MakeChange(node.Value, visible[subtreeEnd].Value, TreeDropPosition.After);
        }
        else if (key == expandKey && index > 0)
        {
            // Step into the branch just above (its parent would be a no-op).
            var above = visible[index - 1];
            if (above.IsBranch && above.Value != node.ParentValue && !tree.IsDisabledNode(above.Item))
            {
                change = MakeChange(node.Value, above.Value, TreeDropPosition.Inside);
                if (!tree.IsExpanded(above.Value)) await tree.SetExpandedAsync([.. tree.ExpandedSnapshot, above.Value]);
            }
        }
        else if (key == collapseKey && node.ParentValue is { } parent)
        {
            change = MakeChange(node.Value, parent, TreeDropPosition.After);
        }

        if (change is null || (tree.CanDrop is not null && !tree.CanDrop(change))) return;
        tree.Announce($"Moved {tree.TextOf(node.Item)}.");
        tree.RequestRefocus(node.Value);
        await tree.OnMove.InvokeAsync(change);
        tree.InvalidateVisible();
        tree.Rerender();
    }

    private TreeChange MakeChange(string source, string? target, TreeDropPosition position) =>
        new() { SourceValue = source, TargetValue = target, Position = position, FromId = tree.ResolvedId, ToId = tree.ResolvedId };

    /// <summary>A <see cref="TreeChange"/> from the raw strings a JS drop reports.</summary>
    public TreeChange BuildChange(string sourceValue, string? targetValue, string position, string fromId, string toId) =>
        new()
        {
            SourceValue = sourceValue,
            TargetValue = targetValue,
            Position = position switch
            {
                "before" => TreeDropPosition.Before,
                "inside" => TreeDropPosition.Inside,
                _ => TreeDropPosition.After,
            },
            FromId = string.IsNullOrEmpty(fromId) ? tree.ResolvedId : fromId,
            ToId = string.IsNullOrEmpty(toId) ? tree.ResolvedId : toId,
        };
}
