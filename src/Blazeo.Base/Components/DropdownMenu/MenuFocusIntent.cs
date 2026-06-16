namespace Blazeo;

/// <summary>
/// Where a menu places keyboard focus the instant it opens, decided by how it was opened: a pointer
/// click focuses the surface itself (<see cref="None"/>) and the first Arrow press steps onto an
/// item, whereas opening with the keyboard lands directly on the first or last item.
/// </summary>
public enum MenuFocusIntent
{
    /// <summary>Focus the surface; the first Arrow key moves onto an item (pointer-opened menus).</summary>
    None,

    /// <summary>Focus the first item (opened with Enter / Space / ArrowDown).</summary>
    First,

    /// <summary>Focus the last item (opened with ArrowUp).</summary>
    Last,
}
