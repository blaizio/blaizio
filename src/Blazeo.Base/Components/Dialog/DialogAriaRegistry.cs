namespace Blazeo;

/// <summary>
/// Tracks whether a <see cref="BaseDialogTitle"/> / <see cref="BaseDialogDescription"/> is currently
/// rendered, so <see cref="BaseDialogContent"/> emits <c>aria-labelledby</c> / <c>aria-describedby</c>
/// only for an element that actually exists - a dangling reference is worse than none. Cascaded by the
/// content; the title/description register on mount and unregister on unmount (so these attributes
/// are gated on an element that actually exists).
/// </summary>
internal sealed class DialogAriaRegistry(Action onChanged)
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
