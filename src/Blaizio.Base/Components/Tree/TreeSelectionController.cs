using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// Selection state for a <see cref="BaseTree{TItem}"/>: the controlled/uncontrolled selected-values
/// list, the shift-range anchor, and the single/multiple gestures (set, toggle, range, select-all).
/// The tree stays the render owner and the consumer-facing notifier - this class only owns the list
/// and the rules for changing it; every change flows back out through
/// <see cref="BaseTree{TItem}.NotifySelectionAsync"/>.
/// </summary>
internal sealed class TreeSelectionController<TItem>(BaseTree<TItem> tree)
{
    private readonly ControllableState<IReadOnlyList<string>> _state = new();

    /// <summary>The shift-range anchor: the last plainly clicked/toggled node.</summary>
    public string? Anchor { get; set; }

    /// <summary>The currently selected values (zero or one of them in Single mode).</summary>
    public IReadOnlyList<string> Values => _state.Value;

    public bool IsSelected(string value) => _state.Value.Contains(value);

    /// <summary>Parameter sync, forwarded from the tree's OnParametersSet.</summary>
    public void Sync(bool controlled, IReadOnlyList<string> value, IReadOnlyList<string> defaultValue) =>
        _state.Sync(controlled, value, defaultValue);

    public Task SetAsync(IReadOnlyList<string> next) =>
        _state.SetAsync(next, EventCallback.Factory.Create<IReadOnlyList<string>>(tree, tree.NotifySelectionAsync));

    public Task ToggleAsync(string value) =>
        SetAsync(IsSelected(value)
            ? _state.Value.Where(v => v != value).ToArray()
            : [.. _state.Value, value]);

    /// <summary>Selects the visible span between two values (both inclusive), skipping disabled nodes.</summary>
    public async Task SelectRangeAsync(string from, string to)
    {
        var visible = tree.EnsureVisible();
        var a = visible.FindIndex(f => f.Value == from);
        var b = visible.FindIndex(f => f.Value == to);
        if (a < 0 || b < 0) return;
        if (a > b) (a, b) = (b, a);

        var next = new List<string>();
        for (var i = a; i <= b; i++)
        {
            if (!tree.IsDisabledNode(visible[i].Item)) next.Add(visible[i].Value);
        }
        await SetAsync(next);
    }

    /// <summary>Selects every visible, enabled node.</summary>
    public Task SelectAllVisibleAsync() =>
        SetAsync(tree.EnsureVisible().Where(f => !tree.IsDisabledNode(f.Item)).Select(f => f.Value).ToArray());
}
