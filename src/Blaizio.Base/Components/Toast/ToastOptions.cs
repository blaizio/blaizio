using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// Options for an imperatively shown toast - the per-toast knobs passed to
/// <see cref="IToastService"/> methods. Anything left unset falls back to the toast provider's defaults.
/// </summary>
public record ToastOptions
{
    /// <summary>A stable id. Pass an existing toast's id to update it in place (the basis of promise flows).</summary>
    public string? Id { get; init; }

    /// <summary>Secondary line under the title.</summary>
    public string? Description { get; init; }

    /// <summary>Custom description content, overriding <see cref="Description"/>.</summary>
    public RenderFragment? DescriptionContent { get; init; }

    /// <summary>How long before the toast auto-closes. Unset uses the toaster default (4s).</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The primary action button (e.g. "Undo").</summary>
    public ToastAction? Action { get; init; }

    /// <summary>The secondary cancel button.</summary>
    public ToastAction? Cancel { get; init; }

    /// <summary>Show the close button. Unset inherits the toaster default.</summary>
    public bool? CloseButton { get; init; }

    /// <summary>Whether the user can dismiss the toast (swipe / close). Defaults to <see langword="true"/>.</summary>
    public bool Dismissible { get; init; } = true;

    /// <summary>Fill the toast with its type colour. Unset inherits the toaster default.</summary>
    public bool? RichColors { get; init; }

    /// <summary>Override the toaster's position for this toast.</summary>
    public ToastPosition? Position { get; init; }

    /// <summary>Override the reading direction for this toast (mirrors icon, buttons and text); unset inherits the provider's.</summary>
    public Direction? Direction { get; init; }

    /// <summary>
    /// A custom icon, overriding the type icon. Icon-agnostic (the headless layer carries no icon
    /// set) - render any markup or icon component here.
    /// </summary>
    public RenderFragment? Icon { get; init; }

    /// <summary>Show an icon at all. Set <see langword="false"/> to suppress it. Defaults to <see langword="true"/>.</summary>
    public bool ShowIcon { get; init; } = true;

    /// <summary>Keep the toast on screen until dismissed (no auto-close).</summary>
    public bool Persistent { get; init; }

    /// <summary>Invoked when the user dismisses the toast (swipe / close button).</summary>
    public Action? OnDismiss { get; init; }

    /// <summary>Invoked when the toast auto-closes after its duration.</summary>
    public Action? OnAutoClose { get; init; }
}
