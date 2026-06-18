using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BzSelect"/> cascades to its trigger, value, listbox content, and every option:
/// the selected value (and its display content for the trigger), the open state, the ARIA ids wiring
/// the trigger to the listbox, the anchor hook the content positions + dismisses against, the
/// open/close/select requests, and the option registry.
/// </summary>
/// <remarks>
/// The registry is what lets the trigger show the chosen option even before the listbox has ever been
/// opened (a value set programmatically, e.g. a pre-filled form): every option registers its display
/// content keyed by value while it is mounted - and a <see cref="BzSelect"/>'s options stay mounted
/// (hidden) while closed, exactly so this registration is always available.
/// </remarks>
/// <param name="Value">The currently selected value, or <see langword="null"/> when nothing is selected.</param>
/// <param name="Open">Whether the listbox is open.</param>
/// <param name="ContentId">Id of the listbox element (the trigger's <c>aria-controls</c> and the content's <c>id</c>).</param>
/// <param name="TriggerId">Id of the trigger (the content's <c>aria-labelledby</c>).</param>
/// <param name="AnchorId">Value of the trigger's <c>data-bz-select-anchor</c> hook; the content resolves it to position against the trigger.</param>
/// <param name="SelectedContent">The selected option's display content, for <see cref="BzSelectValue"/> to render in the trigger.</param>
/// <param name="RequestOpen">Opens the listbox (trigger click / Arrow keys).</param>
/// <param name="RequestClose">Closes the listbox (Escape / Tab / outside press / trigger click while open).</param>
/// <param name="Select">Picks a value: sets the selection and closes the listbox.</param>
/// <param name="IsSelected">Whether the given value is the selected one.</param>
/// <param name="Register">Registers (or refreshes) an option's display content, keyed by value.</param>
/// <param name="Unregister">Drops an option's registration when it leaves the DOM.</param>
public sealed record SelectContext(
    string? Value,
    bool Open,
    string ContentId,
    string TriggerId,
    string AnchorId,
    RenderFragment? SelectedContent,
    EventCallback RequestOpen,
    EventCallback RequestClose,
    EventCallback<string> Select,
    Func<string, bool> IsSelected,
    Action<string, RenderFragment> Register,
    Action<string> Unregister);
