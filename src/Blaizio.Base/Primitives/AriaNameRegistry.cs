namespace Blaizio;

/// <summary>
/// Tracks whether a title / description child is currently rendered inside a named surface
/// (dialog, popover), so the surface emits <c>aria-labelledby</c> / <c>aria-describedby</c>
/// only for an element that actually exists - a dangling reference is worse than none. Cascaded
/// by the surface content; the title/description register on mount and unregister on unmount.
/// </summary>
internal sealed class AriaNameRegistry(Action onChanged)
{
    private bool _hasTitle;
    private bool _hasDescription;

    /// <summary>Whether a title is currently rendered.</summary>
    public bool HasTitle => _hasTitle;

    /// <summary>Whether a description is currently rendered.</summary>
    public bool HasDescription => _hasDescription;

    /// <summary>Register (<see langword="true"/>) or unregister the title; re-renders the content on change.</summary>
    public void SetTitle(bool present) => Set(ref _hasTitle, present);

    /// <summary>Register (<see langword="true"/>) or unregister the description; re-renders the content on change.</summary>
    public void SetDescription(bool present) => Set(ref _hasDescription, present);

    private void Set(ref bool field, bool present)
    {
        if (field == present) return;
        field = present;
        onChanged();
    }
}
