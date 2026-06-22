using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// The content and behaviour of a single toast - what <see cref="IToastStore.Show"/> is handed. It is
/// deliberately icon-agnostic (the headless layer carries no icon set): a type-based icon is chosen by
/// the styled layer, or a custom one supplied via <see cref="Icon"/>. Immutable; build a changed copy
/// with <c>with</c> (the store does this for promise/update flows).
/// </summary>
public sealed record ToastData
{
    /// <summary>The semantic kind, driving the icon and (with rich colors) the palette. Defaults to <see cref="ToastType.Normal"/>.</summary>
    public ToastType Type { get; init; } = ToastType.Normal;

    /// <summary>The headline text. Ignored when <see cref="TitleContent"/> is set.</summary>
    public string? Title { get; init; }

    /// <summary>Custom headline content, overriding <see cref="Title"/> (e.g. markup or a component).</summary>
    public RenderFragment? TitleContent { get; init; }

    /// <summary>Secondary line under the title. Ignored when <see cref="DescriptionContent"/> is set.</summary>
    public string? Description { get; init; }

    /// <summary>Custom description content, overriding <see cref="Description"/>.</summary>
    public RenderFragment? DescriptionContent { get; init; }

    /// <summary>
    /// How long the toast stays before auto-closing. <see langword="null"/> uses the toaster default
    /// (4s). Use <see cref="Persistent"/> to keep it until dismissed (the default for
    /// <see cref="ToastType.Loading"/>).
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The primary action button (e.g. "Undo").</summary>
    public ToastAction? Action { get; init; }

    /// <summary>The secondary cancel button.</summary>
    public ToastAction? Cancel { get; init; }

    /// <summary>Show the corner close button. <see langword="null"/> inherits the toaster default.</summary>
    public bool? CloseButton { get; init; }

    /// <summary>Whether the toast can be dismissed by the user (swipe / close button). Defaults to <see langword="true"/>.</summary>
    public bool Dismissible { get; init; } = true;

    /// <summary>Fill the toast with the type's colour. <see langword="null"/> inherits the toaster default.</summary>
    public bool? RichColors { get; init; }

    /// <summary>Override the toaster's position for this toast, opening a stack at that corner.</summary>
    public ToastPosition? Position { get; init; }

    /// <summary>
    /// Override the reading direction for this toast (mirrors the icon, buttons and text). Unset inherits
    /// the provider's ambient direction. Useful for a single localized toast on an otherwise LTR page.
    /// </summary>
    public Direction? Direction { get; init; }

    /// <summary>A custom icon, overriding the type icon. Icon-agnostic so the headless layer stays icon-free.</summary>
    public RenderFragment? Icon { get; init; }

    /// <summary>Show an icon at all. Set <see langword="false"/> to suppress the type/loading icon. Defaults to <see langword="true"/>.</summary>
    public bool ShowIcon { get; init; } = true;

    /// <summary>Invoked when the toast is dismissed by the user (swipe, close button, cancel).</summary>
    public Action? OnDismiss { get; init; }

    /// <summary>Invoked when the toast auto-closes after its duration elapses.</summary>
    public Action? OnAutoClose { get; init; }

    /// <summary>Keep the toast on screen until explicitly dismissed (no auto-close timer).</summary>
    public bool Persistent { get; init; }
}
