using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BzSelect"/> cascades to its trigger, value, listbox content, and every option:
/// the selection (one value when single, a list when <see cref="Multiple"/>), the open state, the ARIA
/// ids wiring the trigger to the listbox, the anchor hook the content positions + dismisses against,
/// the open/close/select/deselect requests, and the option registry.
/// </summary>
/// <remarks>
/// The registry is what lets the trigger show the chosen option(s) even before the listbox has ever
/// been opened (a value set programmatically, e.g. a pre-filled form): every option registers its
/// display content keyed by value while it is mounted - and a <see cref="BzSelect"/>'s options stay
/// mounted (hidden) while closed, exactly so this registration is always available.
/// </remarks>
/// <param name="Value">The single selected value, or <see langword="null"/> when nothing is selected or when <see cref="Multiple"/>.</param>
/// <param name="Selected">Every selected value, in selection order (0 or 1 when single, 0..n when <see cref="Multiple"/>).</param>
/// <param name="Multiple">Whether more than one value can be selected at once.</param>
/// <param name="Open">Whether the listbox is open.</param>
/// <param name="ContentId">Id of the listbox element (the trigger's <c>aria-controls</c> and the content's <c>id</c>).</param>
/// <param name="TriggerId">Id of the trigger (the content's <c>aria-labelledby</c>).</param>
/// <param name="AnchorId">Value of the trigger's <c>data-bz-select-anchor</c> hook; the content resolves it to position against the trigger.</param>
/// <param name="SelectedContent">The selected option's display content (single-select), for <see cref="BzSelectValue"/> to render in the trigger.</param>
/// <param name="SelectedOptions">The selected options (value + display content), in selection order, for the trigger to render as tokens when <see cref="Multiple"/>.</param>
/// <param name="RequestOpen">Opens the listbox (trigger click / Arrow keys).</param>
/// <param name="RequestClose">Closes the listbox (Escape / Tab / outside press / trigger click while open).</param>
/// <param name="Select">Picks a value: single-select sets it and closes; <see cref="Multiple"/> toggles it and leaves the listbox open.</param>
/// <param name="Deselect">Removes a value from the selection (a token's remove button when <see cref="Multiple"/>).</param>
/// <param name="IsSelected">Whether the given value is selected.</param>
/// <param name="Register">Registers (or refreshes) an option's display content, keyed by value.</param>
/// <param name="Unregister">Drops an option's registration when it leaves the DOM.</param>
/// <param name="Position">How the content positions itself, reported up by the content so the trigger can match its chevron.</param>
/// <param name="SetPosition">Called by the content to report its <see cref="SelectPosition"/> to the root.</param>
public sealed record SelectContext(
    string? Value,
    IReadOnlyList<string> Selected,
    bool Multiple,
    bool Open,
    string ContentId,
    string TriggerId,
    string AnchorId,
    RenderFragment? SelectedContent,
    Func<IReadOnlyList<SelectedOption>> SelectedOptions,
    EventCallback RequestOpen,
    EventCallback RequestClose,
    EventCallback<string> Select,
    EventCallback<string> Deselect,
    Func<string, bool> IsSelected,
    Action<string, RenderFragment> Register,
    Action<string> Unregister,
    SelectPosition Position,
    Action<SelectPosition> SetPosition)
{
    /// <summary>Whether anything is selected at all.</summary>
    public bool HasSelection => Selected.Count > 0;
}

/// <summary>A selected option: its value and the display content registered by its option.</summary>
/// <param name="Value">The option's value.</param>
/// <param name="Content">The option's display content (its label), for rendering a token in the trigger.</param>
public sealed record SelectedOption(string Value, RenderFragment Content);

/// <summary>
/// The argument a multi-select trigger's token template receives: the selected options to render and a
/// callback to remove one (wired to a token's remove button).
/// </summary>
/// <param name="Options">The selected options, in selection order.</param>
/// <param name="Deselect">Removes a value from the selection.</param>
public sealed record SelectTokens(IReadOnlyList<SelectedOption> Options, EventCallback<string> Deselect);
