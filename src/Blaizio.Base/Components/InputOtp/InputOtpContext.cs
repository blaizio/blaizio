namespace Blaizio;

/// <summary>The display state of one Input OTP slot, derived from the value and the input's selection.</summary>
/// <param name="Char">The character in this slot, or <see langword="null"/> when empty.</param>
/// <param name="Active">Whether this slot is the focused/selected one (gets the active ring).</param>
/// <param name="ShowCaret">Whether this slot shows the blinking caret (active and empty).</param>
public sealed record OtpSlotState(char? Char, bool Active, bool ShowCaret);

/// <summary>
/// State a <see cref="BaseInputOtp"/> cascades to its slots: the per-slot display states (recomputed
/// from the value plus the hidden input's mirrored selection) and whether the field is focused. A fresh
/// instance is cascaded whenever any of these change so the slots re-render.
/// </summary>
/// <param name="Slots">One entry per slot, in order.</param>
/// <param name="Focused">Whether the hidden input currently has focus.</param>
/// <param name="Disabled">Whether the field is disabled.</param>
public sealed record InputOtpContext(IReadOnlyList<OtpSlotState> Slots, bool Focused, bool Disabled);
