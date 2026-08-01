namespace Blaizio;

/// <summary>
/// The flattened visible list and its search overlay for a <see cref="BaseTree{TItem}"/>: which
/// nodes render and in what keyboard order, which branches the active search forces open, which
/// nodes are direct matches (highlighted), and the roving tab stop. Rebuilt lazily after
/// <see cref="Invalidate"/>; the tree stays the render owner and parameter holder - this class
/// only derives visibility from the tree's current items, expansion, search and focus.
/// </summary>
internal sealed class TreeVisibilityIndex<TItem>(BaseTree<TItem> tree)
{
    private List<BaseTree<TItem>.FlatNode>? _cache;

    // Populated while a Search is active: which nodes to keep, which branches to force-open, and
    // which nodes are direct matches. Rebuilt alongside the flat list.
    private readonly HashSet<string> _include = new(StringComparer.Ordinal);
    private readonly HashSet<string> _open = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hit = new(StringComparer.Ordinal);

    /// <summary>The single tab stop: the focused node while it is visible, else the first node.</summary>
    public string? TabStopValue { get; private set; }

    /// <summary>The current flat list, or <see langword="null"/> before the next rebuild.</summary>
    public List<BaseTree<TItem>.FlatNode>? Cached => _cache;

    public void Invalidate() => _cache = null;

    /// <summary>Whether the active search matched at least one node (always true when search is off).</summary>
    public bool HasVisibleNodes => !tree.Searching || _include.Count > 0;

    /// <summary>Whether the active search force-opens this branch (it leads to a match).</summary>
    public bool IsSearchOpen(string value) => _open.Contains(value);

    /// <summary>Whether this node is a direct search match (highlighted).</summary>
    public bool IsSearchHit(string value) => _hit.Contains(value);

    /// <summary>
    /// The children to actually render under a branch: all of them normally, only the kept ones
    /// (a match or an ancestor of one) during search.
    /// </summary>
    public IReadOnlyList<TItem> RenderableChildren(IList<TItem>? kids)
    {
        if (kids is null) return [];
        if (!tree.Searching) return kids as IReadOnlyList<TItem> ?? [.. kids];
        var list = new List<TItem>(kids.Count);
        foreach (var kid in kids)
            if (_include.Contains(tree.ValueSelector(kid))) list.Add(kid);
        return list;
    }

    /// <summary>The roots to render (search-filtered the same way as children).</summary>
    public IReadOnlyList<TItem> RenderableRoots()
    {
        if (!tree.Searching) return tree.RootNodes;
        var list = new List<TItem>();
        foreach (var root in tree.RootNodes)
            if (_include.Contains(tree.ValueSelector(root))) list.Add(root);
        return list;
    }

    /// <summary>Build (or return) the flat visible list and refresh the tab stop.</summary>
    public List<BaseTree<TItem>.FlatNode> EnsureVisible()
    {
        if (_cache is not null) return _cache;

        var list = new List<BaseTree<TItem>.FlatNode>();
        if (tree.Searching)
        {
            _include.Clear();
            _open.Clear();
            _hit.Clear();
            foreach (var item in tree.RootNodes) BuildSearch(item);
            WalkSearch(tree.RootNodes, 0, null);
        }
        else
        {
            Walk(tree.RootNodes, 0, null);
        }

        _cache = list;
        TabStopValue = tree.FocusedValue is not null && list.Exists(f => f.Value == tree.FocusedValue)
            ? tree.FocusedValue
            : list.Count > 0 ? list[0].Value : null;
        return list;

        void Walk(IReadOnlyList<TItem> items, int depth, string? parent)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var value = tree.ValueSelector(item);
                var branch = tree.IsBranchOf(item);
                list.Add(new BaseTree<TItem>.FlatNode(item, value, depth, branch, parent, i + 1, items.Count));
                if (branch && tree.IsExpanded(value) && tree.EffectiveChildrenOf(item) is { Count: > 0 } kids)
                    Walk(kids as IReadOnlyList<TItem> ?? [.. kids], depth + 1, value);
            }
        }

        // Post-order: a node is kept if it matches or has a kept descendant; branches on the way
        // to a match are force-opened, direct matches are flagged for highlighting.
        bool BuildSearch(TItem item)
        {
            var value = tree.ValueSelector(item);
            var branch = tree.IsBranchOf(item);
            var selfMatch = tree.IsSearchMatch(item);
            if (selfMatch) _hit.Add(value);

            var kidMatch = false;
            if (branch && tree.EffectiveChildrenOf(item) is { } kids)
                foreach (var kid in kids)
                    kidMatch |= BuildSearch(kid);

            if (selfMatch || kidMatch) _include.Add(value);
            if (branch && kidMatch) _open.Add(value);
            return selfMatch || kidMatch;
        }

        void WalkSearch(IEnumerable<TItem> items, int depth, string? parent)
        {
            var included = new List<TItem>();
            foreach (var item in items)
                if (_include.Contains(tree.ValueSelector(item))) included.Add(item);

            for (var i = 0; i < included.Count; i++)
            {
                var item = included[i];
                var value = tree.ValueSelector(item);
                var branch = tree.IsBranchOf(item);
                list.Add(new BaseTree<TItem>.FlatNode(item, value, depth, branch, parent, i + 1, included.Count));
                if (branch && _open.Contains(value) && tree.EffectiveChildrenOf(item) is { Count: > 0 } kids)
                    WalkSearch(kids, depth + 1, value);
            }
        }
    }
}
