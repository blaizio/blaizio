using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <c>BaseInputNumber&lt;TValue&gt;</c> cascades to its group, input, and the increment /
/// decrement buttons. The root owns the numeric value and ALL the math (parse, clamp to <see cref="Min"/> /
/// <see cref="Max"/>, snap to the step grid) plus the press-and-hold repeat timer; the parts are thin -
/// they read this context to render and call its semantic callbacks to change the value. The value
/// rides as <see cref="decimal"/> - the root's internal math type, whatever its <c>TValue</c> - which
/// is what keeps this record, and every part reading it, non-generic.
/// </summary>
/// <param name="Value">The current numeric value, or <see langword="null"/> when the field is empty.</param>
/// <param name="Min">The lowest allowed value, or <see langword="null"/> when unbounded below.</param>
/// <param name="Max">The highest allowed value, or <see langword="null"/> when unbounded above.</param>
/// <param name="CanIncrement">Whether stepping up is possible right now (enabled, editable, not already at <see cref="Max"/>).</param>
/// <param name="CanDecrement">Whether stepping down is possible right now (enabled, editable, not already at <see cref="Min"/>).</param>
/// <param name="Disabled">Whether the whole field is disabled.</param>
/// <param name="ReadOnly">Whether the value can be read but not changed.</param>
/// <param name="InputId">Id of the input element (the buttons aim their <c>aria-controls</c> at it).</param>
/// <param name="Label">Accessible label for the input, or <see langword="null"/>.</param>
/// <param name="DisplayText">What the input should show now: the raw editable number while focused, the formatted value while blurred.</param>
/// <param name="OnInput">The input raised new text - the root parses it into the value, leaving the text as typed so the caret holds.</param>
/// <param name="OnFocus">The input gained focus - it switches to the raw, unformatted, editable string.</param>
/// <param name="OnBlur">The input lost focus - the root clamps, snaps, and reformats the committed value.</param>
/// <param name="Increment">Step up by one step (a keyboard ArrowUp).</param>
/// <param name="Decrement">Step down by one step (a keyboard ArrowDown).</param>
/// <param name="IncrementLarge">Step up by the large step (PageUp / Shift+ArrowUp).</param>
/// <param name="DecrementLarge">Step down by the large step (PageDown / Shift+ArrowDown).</param>
/// <param name="GoToMin">Jump to <see cref="Min"/> (Home), if it is set.</param>
/// <param name="GoToMax">Jump to <see cref="Max"/> (End), if it is set.</param>
/// <param name="PressStart">A pointer pressed a stepper button (<c>+1</c> up, <c>-1</c> down): step once, then hold-to-repeat.</param>
/// <param name="PressEnd">The pointer released / left the stepper button: stop repeating.</param>
/// <param name="AllowDecimal">Whether a decimal separator can be typed - false for a whole-number <c>TValue</c>.</param>
/// <param name="AllowNegative">Whether a minus sign can be typed - false when <see cref="Min"/> rules out negatives.</param>
/// <param name="DecimalSeparator">The culture's decimal separator, for the typing guard.</param>
public sealed record InputNumberContext(
    decimal? Value,
    decimal? Min,
    decimal? Max,
    bool CanIncrement,
    bool CanDecrement,
    bool Disabled,
    bool ReadOnly,
    string InputId,
    string? Label,
    string DisplayText,
    EventCallback<string?> OnInput,
    EventCallback OnFocus,
    EventCallback OnBlur,
    EventCallback Increment,
    EventCallback Decrement,
    EventCallback IncrementLarge,
    EventCallback DecrementLarge,
    EventCallback GoToMin,
    EventCallback GoToMax,
    EventCallback<int> PressStart,
    EventCallback PressEnd,
    bool AllowDecimal,
    bool AllowNegative,
    string DecimalSeparator);
