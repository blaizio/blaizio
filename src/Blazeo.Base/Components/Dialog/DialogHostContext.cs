using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// Passed to a <see cref="BlazeDialogHost"/> template for each open dialog: the instance to render, its
/// <see cref="Index"/> in the stack (for z-index layering), and the <see cref="OnExitComplete"/> callback
/// the skin MUST wire to its content's exit-complete so the host can drop the entry once the close
/// animation has played.
/// </summary>
/// <param name="Instance">The dialog to render.</param>
/// <param name="Index">Zero-based position in the stack (oldest first); use it to layer stacked dialogs.</param>
/// <param name="OnExitComplete">Wire this to the content's <c>OnExitComplete</c>; forgetting it leaks the entry.</param>
public sealed record DialogHostContext(DialogInstance Instance, int Index, EventCallback OnExitComplete);
