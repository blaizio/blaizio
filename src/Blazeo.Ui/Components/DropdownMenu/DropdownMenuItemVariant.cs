namespace Blazeo.Ui;

/// <summary>Visual intent of a <see cref="DropdownMenuItem"/>, surfaced as <c>data-variant</c>.</summary>
public enum DropdownMenuItemVariant
{
    /// <summary>The standard item.</summary>
    Default,

    /// <summary>A destructive action (e.g. Delete) - tinted with the destructive color, including its icon.</summary>
    Destructive,
}
