namespace Blaizio;

/// <summary>
/// Handed to a <see cref="BaseToastProvider"/> template for each rendered toast: the <see cref="Instance"/> to
/// dress, plus the effective <see cref="CloseButton"/> / <see cref="RichColors"/> flags already resolved
/// against the toaster defaults, so the styled layer doesn't repeat that resolution.
/// </summary>
/// <param name="Instance">The toast to render.</param>
/// <param name="CloseButton">Whether to render the corner close button for this toast.</param>
/// <param name="RichColors">Whether this toast is filled with its type's colour.</param>
public sealed record ToastRenderContext(ToastInstance Instance, bool CloseButton, bool RichColors);
