using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Blaizio;

/// <summary>How an editable table cell takes its input.</summary>
public enum CellEditorKind
{
    /// <summary>The value's type has no built-in editor; the column needs an editor template.</summary>
    None,

    /// <summary>A text box whose text parses back into the value: text, numbers, dates, times, ids.</summary>
    Text,

    /// <summary>A checkbox that flips in place (<see cref="bool"/>).</summary>
    Toggle,

    /// <summary>A pick from a fixed list (an <see cref="Enum"/>).</summary>
    Choice,
}

/// <summary>One option of a <see cref="CellEditorKind.Choice"/> editor: the submitted value and its label.</summary>
/// <param name="Value">The text the editor submits for this option.</param>
/// <param name="Label">What the option shows.</param>
public readonly record struct CellChoice(string Value, string Label);

/// <summary>
/// Two-way, text-based access to one member of a row: the read, parse, validate and write half of an
/// editable table cell, built once from a member expression such as <c>p =&gt; p.Age</c>.
/// <para>
/// Reading uses the compiled getter. Writing assigns through a setter compiled from the same
/// expression, available when the member is a settable property or field on a class (an
/// <c>init</c> accessor, a read-only field, a computed property or a struct row has none, so the
/// caller applies the change itself). Text round-trips through <see cref="Format"/> and
/// <see cref="TryParse"/> in the current culture, the raw value without any display format (an
/// editor shows <c>1234.5</c>, not <c>$1,234.50</c>), and <see cref="Validate"/> runs the
/// member's DataAnnotations (<c>[Required]</c>, <c>[Range]</c>, <c>[StringLength]</c>, ...).
/// </para>
/// </summary>
/// <typeparam name="TItem">The row type.</typeparam>
/// <typeparam name="TValue">The member's type.</typeparam>
public sealed class CellBinding<TItem, TValue>
{
    private static readonly Type s_type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
    private static readonly bool s_nullableValue = Nullable.GetUnderlyingType(typeof(TValue)) is not null;

    private readonly Func<TItem, TValue> _get;
    private readonly Action<TItem, TValue>? _set;
    private readonly ValidationAttribute[] _rules;

    /// <summary>Builds the binding for the member <paramref name="member"/> selects, e.g. <c>p =&gt; p.Name</c>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    public CellBinding(Expression<Func<TItem, TValue>> member)
    {
        ArgumentNullException.ThrowIfNull(member);

        _get = member.Compile();

        var access = member.Body as MemberExpression;
        var info = access?.Member;
        MemberName = info?.Name;
        _rules = info is null ? [] : [.. info.GetCustomAttributes(typeof(ValidationAttribute), inherit: true).Cast<ValidationAttribute>()];
        DisplayName = info is null ? null : ReadDisplayName(info);
        _set = access is not null && IsAssignable(access) ? CompileSetter(member, access) : null;

        Kind = KindOf(s_type);
        InputMode = IsUnsignedInteger(s_type) ? "numeric" : null;
        Choices = Kind == CellEditorKind.Choice ? BuildChoices() : [];
    }

    /// <summary>The bound member's name (<c>Age</c> for <c>p =&gt; p.Age</c>), or <see langword="null"/> when the expression is not a plain member access.</summary>
    public string? MemberName { get; }

    /// <summary>The member's <c>[Display(Name = ...)]</c> or <c>[DisplayName]</c>, when it has one.</summary>
    public string? DisplayName { get; }

    /// <summary>The built-in editor the value's type gets.</summary>
    public CellEditorKind Kind { get; }

    /// <summary>The <c>inputmode</c> hint for a text editor (<c>numeric</c> for unsigned whole numbers), or <see langword="null"/>.</summary>
    public string? InputMode { get; }

    /// <summary>The options of a <see cref="CellEditorKind.Choice"/> editor (empty for the other kinds). A nullable enum starts with a blank option.</summary>
    public IReadOnlyList<CellChoice> Choices { get; }

    /// <summary>Whether <see cref="Write"/> can assign the member. False for <c>init</c>, read-only or computed members and for struct rows.</summary>
    public bool CanWrite => _set is not null;

    /// <summary>Whether <see langword="null"/> is a legal value (a <see cref="Nullable{T}"/> member).</summary>
    public bool AllowsNull => s_nullableValue;

    /// <summary>The member's current value on <paramref name="item"/>.</summary>
    public TValue Read(TItem item) => _get(item);

    /// <summary>Assigns <paramref name="value"/> to the member on <paramref name="item"/>.</summary>
    /// <exception cref="InvalidOperationException">The member is not writable (see <see cref="CanWrite"/>).</exception>
    public void Write(TItem item, TValue value)
    {
        if (_set is null)
            throw new InvalidOperationException($"{MemberName ?? "The bound member"} has no setter the binding can call (an init accessor, a read-only member or a struct row). Apply the change yourself.");
        _set(item, value);
    }

    /// <summary>
    /// The value as editing text in <paramref name="culture"/> (the current culture by default): the
    /// raw value with no display format, round-tripping through <see cref="TryParse"/>. Dates show the
    /// short date, plus the time when there is one; doubles use the round-trip form.
    /// </summary>
    public string Format(TValue value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        object? boxed = value;
        return boxed switch
        {
            null => string.Empty,
            string text => text,
            DateTime date => date.ToString(date.TimeOfDay == TimeSpan.Zero ? "d" : "G", culture),
            DateOnly date => date.ToString("d", culture),
            TimeOnly time => time.ToString(time is { Second: 0, Millisecond: 0 } ? "t" : "T", culture),
            double number => number.ToString("R", culture),
            float number => number.ToString("R", culture),
            bool flag => flag ? "TRUE" : "FALSE",
            Enum choice => choice.ToString(),
            IFormattable formattable => formattable.ToString(null, culture),
            _ => boxed.ToString() ?? string.Empty,
        };
    }

    /// <summary>
    /// Parses editing text back into a value in <paramref name="culture"/> (the current culture by
    /// default). Text is taken as typed; blank text is <see langword="null"/> for a nullable member and
    /// an error otherwise. On failure <paramref name="error"/> holds a message naming
    /// <paramref name="displayName"/> (falling back to <see cref="DisplayName"/>, then the member name),
    /// worded by <paramref name="messages"/> from the <see cref="CellParseError"/>, in English without it.
    /// </summary>
    public bool TryParse(string? text, out TValue value, [NotNullWhen(false)] out string? error, string? displayName = null,
        CultureInfo? culture = null, Func<CellParseError, string>? messages = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        text ??= string.Empty;
        var name = displayName ?? DisplayName ?? MemberName ?? "value";

        if (s_type == typeof(string))
        {
            value = (TValue)(object)text;
            error = null;
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            value = default!;
            if (s_nullableValue)
            {
                error = null;
                return true;
            }
            error = new CellParseError(CellParseErrorKind.Required, name, []).Describe(messages);
            return false;
        }

        if (TryParseCore(s_type, trimmed, culture, out var parsed))
        {
            value = (TValue)parsed;
            error = null;
            return true;
        }

        value = default!;
        error = ParseError(s_type, name).Describe(messages);
        return false;
    }

    /// <summary>
    /// Runs the member's DataAnnotations against <paramref name="value"/> as the new value on
    /// <paramref name="item"/>, without assigning it. Returns the first failure's message (each rule
    /// words its own, so an attribute's <c>ErrorMessage</c> or resource localizes it), or
    /// <see langword="null"/> when every rule passes (or the member has none).
    /// </summary>
    public string? Validate(TItem item, TValue value, string? displayName = null)
    {
        if (_rules.Length == 0 || item is null) return null;

        var context = new ValidationContext(item, displayName ?? DisplayName ?? MemberName ?? "value", serviceProvider: null, items: null)
        {
            MemberName = MemberName,
        };
        foreach (var rule in _rules)
        {
            if (rule.GetValidationResult(value, context) is { } failure && failure != ValidationResult.Success)
                return failure.ErrorMessage ?? rule.FormatErrorMessage(context.DisplayName);
        }
        return null;
    }

    /// <summary>
    /// The value a cleared cell takes: an empty string for text, <see langword="null"/> for a nullable
    /// member. Returns <see langword="false"/> when the type has no empty value (a plain number, date or bool).
    /// </summary>
    public bool TryGetClearValue(out TValue value)
    {
        if (s_type == typeof(string))
        {
            value = (TValue)(object)string.Empty;
            return true;
        }
        value = default!;
        return s_nullableValue;
    }

    /// <summary>Value equality for change detection, with a <see langword="null"/> and an empty string counted as the same text.</summary>
    public static bool ValuesEqual(TValue a, TValue b)
    {
        if (a is string || b is string || (a is null && b is null))
            return string.Equals((a as string) ?? string.Empty, (b as string) ?? string.Empty, StringComparison.Ordinal);
        return EqualityComparer<TValue>.Default.Equals(a, b);
    }

    // ---- construction helpers ----

    private static string? ReadDisplayName(MemberInfo member) =>
        member.GetCustomAttribute<DisplayAttribute>(inherit: true)?.GetName()
        ?? member.GetCustomAttribute<DisplayNameAttribute>(inherit: true)?.DisplayName;

    // A member the compiled assignment would actually change on the caller's row: a settable,
    // non-init property or a non-read-only field, reached through reference types only (assigning
    // through a struct copy would update the copy and silently lose the edit).
    private static bool IsAssignable(MemberExpression access)
    {
        if (access.Type != typeof(TValue)) return false;
        if (access.Expression is null || access.Expression.Type.IsValueType) return false;

        return access.Member switch
        {
            PropertyInfo { SetMethod: { IsPublic: true } setter } => !IsInitOnly(setter),
            FieldInfo field => !field.IsInitOnly && !field.IsLiteral,
            _ => false,
        };
    }

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static Action<TItem, TValue> CompileSetter(Expression<Func<TItem, TValue>> member, MemberExpression access)
    {
        var value = Expression.Parameter(typeof(TValue), "value");
        return Expression.Lambda<Action<TItem, TValue>>(Expression.Assign(access, value), member.Parameters[0], value).Compile();
    }

    private static CellEditorKind KindOf(Type type)
    {
        if (type == typeof(bool)) return CellEditorKind.Toggle;
        if (type.IsEnum) return type.IsDefined(typeof(FlagsAttribute), inherit: false) ? CellEditorKind.Text : CellEditorKind.Choice;
        if (Type.GetTypeCode(type) is >= TypeCode.Char and <= TypeCode.Decimal or TypeCode.DateTime or TypeCode.String)
            return CellEditorKind.Text;
        if (type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) || type == typeof(Guid))
            return CellEditorKind.Text;
        return CellEditorKind.None;
    }

    private static bool IsUnsignedInteger(Type type) =>
        Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64;

    private static CellChoice[] BuildChoices()
    {
        var names = Enum.GetNames(s_type);
        var choices = new List<CellChoice>(names.Length + 1);
        if (s_nullableValue) choices.Add(new CellChoice(string.Empty, string.Empty));
        foreach (var name in names) choices.Add(new CellChoice(name, name));
        return [.. choices];
    }

    // ---- parsing ----

    private static bool TryParseCore(Type type, string text, CultureInfo culture, [NotNullWhen(true)] out object? value)
    {
        const NumberStyles Whole = NumberStyles.Integer | NumberStyles.AllowThousands;
        const NumberStyles Real = NumberStyles.Float | NumberStyles.AllowThousands;
        const DateTimeStyles Dates = DateTimeStyles.AllowWhiteSpaces;

        value = null;

        // Before the type-code switch: GetTypeCode reports an enum as its underlying integer type.
        if (type.IsEnum)
        {
            if (Enum.TryParse(type, text, ignoreCase: true, out var choice) && IsKnownEnumValue(type, choice!, text))
                value = choice;
            return value is not null;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                if (bool.TryParse(text, out var flag)) value = flag;
                else if (text is "1") value = true;
                else if (text is "0") value = false;
                break;
            case TypeCode.Char:
                if (text.Length == 1) value = text[0];
                break;
            case TypeCode.SByte when sbyte.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.Byte when byte.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.Int16 when short.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.UInt16 when ushort.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.Int32 when int.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.UInt32 when uint.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.Int64 when long.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.UInt64 when ulong.TryParse(text, Whole, culture, out var n): value = n; break;
            case TypeCode.Single when float.TryParse(text, Real, culture, out var n): value = n; break;
            case TypeCode.Double when double.TryParse(text, Real, culture, out var n): value = n; break;
            case TypeCode.Decimal when decimal.TryParse(text, Real, culture, out var n): value = n; break;
            case TypeCode.DateTime when DateTime.TryParse(text, culture, Dates, out var d): value = d; break;
            default:
                if (type == typeof(DateOnly) && DateOnly.TryParse(text, culture, Dates, out var date)) value = date;
                else if (type == typeof(TimeOnly) && TimeOnly.TryParse(text, culture, Dates, out var time)) value = time;
                else if (type == typeof(DateTimeOffset) && DateTimeOffset.TryParse(text, culture, Dates, out var offset)) value = offset;
                else if (type == typeof(TimeSpan) && TimeSpan.TryParse(text, culture, out var span)) value = span;
                else if (type == typeof(Guid) && Guid.TryParse(text, out var id)) value = id;
                break;
        }
        return value is not null;
    }

    // Enum.TryParse accepts any number ("42"); a choice editor should only take a declared name or
    // value. A flags enum takes combinations, so it only needs to parse.
    private static bool IsKnownEnumValue(Type type, object value, string text) =>
        type.IsDefined(typeof(FlagsAttribute), inherit: false) || Enum.IsDefined(type, value) ||
        !text.All(c => char.IsDigit(c) || c is '-' or '+');

    private static CellParseError ParseError(Type type, string name)
    {
        if (type.IsEnum)
            return new(CellParseErrorKind.Choice, name, Enum.GetNames(type));
        var kind = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => CellParseErrorKind.Boolean,
            TypeCode.Char => CellParseErrorKind.Character,
            >= TypeCode.SByte and <= TypeCode.Decimal => CellParseErrorKind.Number,
            TypeCode.DateTime => CellParseErrorKind.Date,
            _ when type == typeof(DateOnly) || type == typeof(DateTimeOffset) => CellParseErrorKind.Date,
            _ when type == typeof(TimeOnly) || type == typeof(TimeSpan) => CellParseErrorKind.Time,
            _ => CellParseErrorKind.Invalid,
        };
        return new(kind, name, []);
    }
}
