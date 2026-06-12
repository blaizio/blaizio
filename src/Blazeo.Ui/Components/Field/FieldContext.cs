namespace Blazeo.Ui;

/// <summary>
/// Per-field state a <see cref="Field"/> cascades to its <see cref="FieldLabel"/>,
/// <see cref="FieldControl"/>, <see cref="FieldDescription"/>, and <see cref="FieldError"/> — the
/// ids that wire them together for accessibility, plus the resolved validity/messages.
/// </summary>
/// <param name="ItemId">Id of the control; the label's <c>for</c> and the control's <c>id</c>.</param>
/// <param name="DescriptionId">Id of the description element.</param>
/// <param name="ErrorId">Id of the error element.</param>
/// <param name="IsInvalid">Whether the field currently has validation errors.</param>
/// <param name="Messages">The current validation messages (empty when valid).</param>
public sealed record FieldContext(
    string ItemId,
    string DescriptionId,
    string ErrorId,
    bool IsInvalid,
    IReadOnlyList<string> Messages)
{
    /// <summary>
    /// The <c>aria-describedby</c> value for the control: the description always, plus the error
    /// when invalid.
    /// </summary>
    public string DescribedBy => IsInvalid ? $"{DescriptionId} {ErrorId}" : DescriptionId;
}
