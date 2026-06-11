namespace Blazeo.Ui;

/// <summary>
/// Per-field state a <see cref="FormField"/> cascades to its <see cref="FormLabel"/>,
/// <see cref="FormControl"/>, <see cref="FormDescription"/>, and <see cref="FormMessage"/> — the ids
/// that wire them together for accessibility, plus the resolved validity/messages. Mirrors the
/// context shadcn's <c>useFormField</c> exposes.
/// </summary>
/// <param name="ItemId">Id of the control; the label's <c>for</c> and the control's <c>id</c>.</param>
/// <param name="DescriptionId">Id of the description element.</param>
/// <param name="MessageId">Id of the message element.</param>
/// <param name="IsInvalid">Whether the field currently has validation errors.</param>
/// <param name="Messages">The current validation messages (empty when valid).</param>
public sealed record FormFieldContext(
    string ItemId,
    string DescriptionId,
    string MessageId,
    bool IsInvalid,
    IReadOnlyList<string> Messages)
{
    /// <summary>
    /// The <c>aria-describedby</c> value for the control: the description always, plus the message
    /// when invalid — matching shadcn's wiring.
    /// </summary>
    public string DescribedBy => IsInvalid ? $"{DescriptionId} {MessageId}" : DescriptionId;
}
