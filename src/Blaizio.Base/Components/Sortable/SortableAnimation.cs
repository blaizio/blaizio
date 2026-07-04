namespace Blaizio;

/// <summary>The motion preset for drag previews and drop settling. Whatever the setting, a user whose
/// OS asks for reduced motion (<c>prefers-reduced-motion: reduce</c>) gets instant snaps.</summary>
public enum SortableAnimation
{
    /// <summary>A quick decelerating glide (the default).</summary>
    Dynamic,

    /// <summary>A springy glide with a little overshoot.</summary>
    Spring,

    /// <summary>No motion - items snap instantly.</summary>
    None,
}
