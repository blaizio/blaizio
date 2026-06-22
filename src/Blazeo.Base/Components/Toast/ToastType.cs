namespace Blazeo;

/// <summary>
/// The semantic kind of a toast. Surfaced as <c>data-bz-toast-type</c>; the styled layer picks the
/// icon and (when rich colors are on) the colour palette from it. <see cref="Loading"/> toasts are
/// persistent by default - they stay until updated or dismissed - and show a spinner.
/// </summary>
public enum ToastType
{
    /// <summary>A plain message with no icon or accent (the default).</summary>
    Normal,

    /// <summary>A success confirmation.</summary>
    Success,

    /// <summary>An informational note.</summary>
    Info,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>An error.</summary>
    Error,

    /// <summary>An in-progress operation - shows a spinner and persists until resolved.</summary>
    Loading,
}
