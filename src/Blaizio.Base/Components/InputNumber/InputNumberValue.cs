using System.Globalization;

namespace Blaizio;

/// <summary>
/// The per-<typeparamref name="TValue"/> seam of <c>BaseInputNumber&lt;TValue&gt;</c>. Supports the
/// types Blazor's own <c>InputNumber&lt;TValue&gt;</c> supports - <see cref="int"/>, <see cref="long"/>,
/// <see cref="short"/>, <see cref="float"/>, <see cref="double"/>, <see cref="decimal"/>, bare or
/// nullable - validated once here, and converts between <typeparamref name="TValue"/> and the
/// <see cref="decimal"/> the root does ALL of its math in. decimal keeps integers and money exact
/// (no binary-float drift in the step grid); a float/double magnitude beyond decimal's ±7.9e28
/// saturates, a range no spinner UI reaches. Parsing is also per-type, exactly like Blazor's
/// <c>BindConverter</c> path: an integral <typeparamref name="TValue"/> rejects "2.7" instead of
/// silently truncating it.
/// </summary>
internal static class InputNumberValue<TValue>
{
    /// <summary>Whether <typeparamref name="TValue"/> is nullable - only then does an empty field mean "no value".</summary>
    public static readonly bool Nullable;

    /// <summary>Whether <typeparamref name="TValue"/> is a whole-number type, so a decimal
    /// separator can never be part of a valid entry (parsing already rejects "2.7" for one).</summary>
    public static bool Integral => Code is TypeCode.Int32 or TypeCode.Int64 or TypeCode.Int16;

    /// <summary><typeparamref name="TValue"/>'s own representable range (as far as decimal can express it), folded into clamping so a step or a typed value can never overflow the conversion back.</summary>
    public static readonly decimal RangeMin;

    /// <inheritdoc cref="RangeMin"/>
    public static readonly decimal RangeMax;

    private static readonly TypeCode Code;

    static InputNumberValue()
    {
        var target = System.Nullable.GetUnderlyingType(typeof(TValue));
        Nullable = target is not null;
        target ??= typeof(TValue);
        Code = Type.GetTypeCode(target);

        (RangeMin, RangeMax) = Code switch
        {
            TypeCode.Int32 => (int.MinValue, (decimal)int.MaxValue),
            TypeCode.Int64 => (long.MinValue, (decimal)long.MaxValue),
            TypeCode.Int16 => (short.MinValue, (decimal)short.MaxValue),
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => (decimal.MinValue, decimal.MaxValue),
            // The same contract (and message) as Blazor's InputNumber<TValue>.
            _ => throw new InvalidOperationException($"The type '{typeof(TValue)}' is not a supported numeric type."),
        };
    }

    public static decimal? ToDecimal(TValue value) => value switch
    {
        null => null,
        int i => i,
        long l => l,
        short s => s,
        float f => Saturate(f),
        double d => Saturate(d),
        decimal m => m,
        _ => null, // unreachable - the static ctor rejected every other TValue
    };

    /// <summary>Converts back. <see langword="null"/> only flows for a nullable <typeparamref name="TValue"/> (the root never emits null otherwise).</summary>
    public static TValue FromDecimal(decimal? value)
    {
        if (value is null) return default!;
        var v = Math.Clamp(value.Value, RangeMin, RangeMax);
        object boxed = Code switch
        {
            TypeCode.Int32 => (int)decimal.Round(v),
            TypeCode.Int64 => (long)decimal.Round(v),
            TypeCode.Int16 => (short)decimal.Round(v),
            TypeCode.Single => (float)v,
            TypeCode.Double => (double)v,
            _ => v,
        };
        return (TValue)boxed; // unboxes into both TValue and Nullable<TValue>
    }

    /// <summary>Parses in <typeparamref name="TValue"/>'s own terms: integral types reject fractions outright, like Blazor's converter - a typed "2.7" is invalid for an int field, not a 2.</summary>
    /// <remarks>
    /// The text is the field's edit text, which never carries grouping: the script guard admits
    /// digits, a sign and ONE separator, and that separator may be "." in any culture because
    /// numeric keypads emit it. So a "." here always means the decimal point. Parsed with the
    /// culture as-is, "1.5" would read as 15 wherever "." is the group separator (de-DE, es-ES,
    /// and others), hence the normalisation and the styles without thousands.
    /// </remarks>
    public static bool TryParse(string text, CultureInfo culture, out decimal value)
    {
        var format = EditFormat(culture);
        var separator = format.NumberDecimalSeparator;
        if (separator != "." && !text.Contains(separator, StringComparison.Ordinal))
            text = text.Replace(".", separator, StringComparison.Ordinal);

        switch (Code)
        {
            case TypeCode.Int32 when int.TryParse(text, IntegerStyles, format, out var i):
                value = i;
                return true;
            case TypeCode.Int64 when long.TryParse(text, IntegerStyles, format, out var l):
                value = l;
                return true;
            case TypeCode.Int16 when short.TryParse(text, IntegerStyles, format, out var s):
                value = s;
                return true;
            case TypeCode.Single or TypeCode.Double
                when double.TryParse(text, DecimalStyles, format, out var d) && double.IsFinite(d):
                value = Saturate(d);
                return true;
            case TypeCode.Decimal when decimal.TryParse(text, DecimalStyles, format, out var m):
                value = m;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    /// <summary>
    /// The culture's number format as the edit text uses it: with a plain "-" for the minus sign.
    /// The script guard admits that one character, and it is what a keyboard types. Several
    /// cultures write the sign differently (U+2212 in sv-SE, a direction mark before it in he-IL
    /// and ar-SA): there a typed "-5" did not parse, and the edit text of a negative value began
    /// with a character the guard refused, so nothing could be typed into it.
    /// </summary>
    internal static NumberFormatInfo EditFormat(CultureInfo culture)
    {
        var format = culture.NumberFormat;
        if (format.NegativeSign == "-") return format;
        format = (NumberFormatInfo)format.Clone();
        format.NegativeSign = "-";
        return format;
    }

    private const NumberStyles IntegerStyles = NumberStyles.Integer;
    private const NumberStyles DecimalStyles = NumberStyles.Integer | NumberStyles.AllowDecimalPoint;

    /// <summary>decimal's range is smaller than double's - clamp instead of throwing on conversion (and NaN, which decimal cannot express, lands on 0).</summary>
    public static decimal Saturate(double value) => value switch
    {
        double.NaN => 0,
        >= (double)decimal.MaxValue => decimal.MaxValue,
        <= (double)decimal.MinValue => decimal.MinValue,
        _ => (decimal)value,
    };

    /// <summary>
    /// The same for a <see langword="float"/>, through its shortest text that reads back as the same
    /// value. Widened to <see langword="double"/> first, a float shows its binary tail (0.1f becomes
    /// 0.100000001490116), and that is what the field would put in front of the user on focus.
    /// </summary>
    public static decimal Saturate(float value)
    {
        if (float.IsNaN(value)) return 0;
        if (value >= (float)decimal.MaxValue) return decimal.MaxValue;
        if (value <= (float)decimal.MinValue) return decimal.MinValue;
        return decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
