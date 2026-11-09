namespace Blaizio;

/// <summary>Why a cell's text did not parse into a value: what a <see cref="CellParseError"/> reports.</summary>
public enum CellParseErrorKind
{
    /// <summary>Blank text for a member that needs a value.</summary>
    Required,

    /// <summary>Text that is not a number.</summary>
    Number,

    /// <summary>Text that is not a date.</summary>
    Date,

    /// <summary>Text that is not a time.</summary>
    Time,

    /// <summary>Text that is neither true nor false.</summary>
    Boolean,

    /// <summary>Text longer than the single character the member holds.</summary>
    Character,

    /// <summary>Text that names none of the options the member takes (see <see cref="CellParseError.Options"/>).</summary>
    Choice,

    /// <summary>Any other text the member's type cannot take.</summary>
    Invalid,
}

/// <summary>
/// A cell's refused text: why it was refused, the name of the field it was typed into, and the
/// options a <see cref="CellParseErrorKind.Choice"/> takes. A message builder of your own words
/// the error in the reader's language from it; <see cref="Message"/> is the English default.
/// </summary>
/// <param name="Kind">Why the text was refused.</param>
/// <param name="Field">The field's name as the message shows it: a column's title, a display name or the member name.</param>
/// <param name="Options">The labels of the options a choice takes, in order; empty for the other kinds.</param>
public readonly record struct CellParseError(CellParseErrorKind Kind, string Field, IReadOnlyList<string> Options)
{
    /// <summary>The English message, for example <c>The Age field must be a number.</c></summary>
    public string Message => Kind switch
    {
        CellParseErrorKind.Required => $"The {Field} field is required.",
        CellParseErrorKind.Number => $"The {Field} field must be a number.",
        CellParseErrorKind.Date => $"The {Field} field must be a date.",
        CellParseErrorKind.Time => $"The {Field} field must be a time.",
        CellParseErrorKind.Boolean => $"The {Field} field must be true or false.",
        CellParseErrorKind.Character => $"The {Field} field must be a single character.",
        CellParseErrorKind.Choice => $"The {Field} field must be one of: {string.Join(", ", Options ?? [])}.",
        _ => $"The {Field} field has an invalid value.",
    };

    /// <summary>
    /// The message <paramref name="messages"/> words for this error, or <see cref="Message"/> when
    /// there is no builder (or it gives back nothing).
    /// </summary>
    public string Describe(Func<CellParseError, string>? messages) =>
        messages?.Invoke(this) is { Length: > 0 } text ? text : Message;
}
