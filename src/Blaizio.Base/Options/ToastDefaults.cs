namespace Blaizio;

/// <summary>App-wide defaults for the toast provider. Provider parameters override per app root.</summary>
public sealed class ToastDefaults
{
    /// <summary>The corner toasts stack in. Defaults to <see cref="ToastPosition.BottomRight"/>.</summary>
    public ToastPosition Position { get; set; } = ToastPosition.BottomRight;

    /// <summary>How many toasts stay fully visible before the rest collapse behind. Defaults to 3.</summary>
    public int VisibleToasts { get; set; } = 3;

    /// <summary>The auto-close duration. Defaults to 4 seconds.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>Show a corner close button on every toast. Defaults to <see langword="false"/>.</summary>
    public bool CloseButton { get; set; }

    /// <summary>Fill every toast with its type colour. Defaults to <see langword="false"/>.</summary>
    public bool RichColors { get; set; }
}
