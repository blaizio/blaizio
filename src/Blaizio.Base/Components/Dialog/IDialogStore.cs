using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// The per-scope registry of open <see cref="DialogInstance"/>s behind the imperative dialog API. A
/// host component (the Ui <c>DialogProvider</c>) renders <see cref="Instances"/> and re-renders on
/// <see cref="Changed"/>. Registered by <c>AddBlaizioBase</c>; <see cref="IDialogService"/> wraps it.
/// </summary>
public interface IDialogStore
{
    /// <summary>The currently open dialogs, in stacking order (oldest first, newest on top).</summary>
    IReadOnlyList<DialogInstance> Instances { get; }

    /// <summary>Raised whenever <see cref="Instances"/> changes or an instance's open state flips.</summary>
    event Action? Changed;

    /// <summary>
    /// Opens a dialog whose content is produced by <paramref name="contentFactory"/> (it receives the new
    /// <see cref="DialogInstance"/> so the content can close itself). The returned task completes when the
    /// dialog resolves or is cancelled.
    /// </summary>
    Task<DialogResult> OpenAsync(Func<DialogInstance, RenderFragment> contentFactory, DialogOptions options);
}
