using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// A controlled/uncontrolled state helper: lets a component be either <i>controlled</i>
/// (the consumer owns the value via a bound parameter + change callback) or <i>uncontrolled</i> (the
/// component keeps its own state, seeded once from a default), behind a single <see cref="Value"/> and
/// one <see cref="SetAsync"/> setter - so the component body never branches on which mode it's in.
/// </summary>
/// <remarks>
/// Drive it from the lifecycle: call <see cref="Sync"/> in <c>OnParametersSet</c> to reconcile with the
/// latest parameters, and <see cref="SetAsync"/> from event handlers to commit a new value.
///
/// <para><b>Controlled vs uncontrolled</b> is decided by one rule,
/// <see cref="ControllableState.IsControlled(bool, bool, bool)"/>, and passed to <see cref="Sync"/> as
/// <c>controlled</c>. When controlled, <see cref="SetAsync"/> leaves <see cref="Value"/> alone - it
/// fires the change callback and lets the parent flow a new value back in on the next render.</para>
/// </remarks>
/// <typeparam name="T">The state value type (e.g. <see cref="bool"/> for a toggle's pressed state).</typeparam>
public sealed class ControllableState<T>
{
    private T _uncontrolled = default!;
    private bool _seeded;

    /// <summary><see langword="true"/> when the consumer owns the value (a controlled parameter was supplied).</summary>
    public bool IsControlled { get; private set; }

    /// <summary>The effective current value - the controlled parameter when controlled, else the internal state.</summary>
    public T Value { get; private set; } = default!;

    /// <summary>
    /// Reconcile with the current render's parameters. Call from <c>OnParametersSet</c>.
    /// </summary>
    /// <param name="controlled">Whether the consumer is driving the value this render: the result of
    /// <see cref="ControllableState.IsControlled(bool, bool, bool)"/>.</param>
    /// <param name="controlledValue">The controlled value (ignored when <paramref name="controlled"/> is false).</param>
    /// <param name="defaultValue">The uncontrolled seed - applied once, on the first sync only.</param>
    public void Sync(bool controlled, T controlledValue, T defaultValue)
    {
        if (!_seeded)
        {
            _uncontrolled = defaultValue;
            _seeded = true;
        }

        IsControlled = controlled;
        Value = controlled ? controlledValue : _uncontrolled;
    }

    /// <summary>
    /// Commit <paramref name="next"/>: when uncontrolled, reflects it in <see cref="Value"/>
    /// immediately; always raises <paramref name="onChange"/> so a controlled parent (or a
    /// <c>@bind</c>) can react. The parent's new value, if any, lands on the next sync.
    /// </summary>
    /// <remarks>
    /// The committed value is remembered even while controlled. It is what the component falls back
    /// to if the parent stops controlling on a later render, which is what a binding next to a default
    /// does whenever the bound value goes back to unset (<c>@bind-Value</c> with <c>DefaultValue</c>,
    /// and the user closes the last open item): the fallback is then the value just committed, not
    /// the one from before the parent took over.
    /// </remarks>
    public async Task SetAsync(T next, EventCallback<T> onChange)
    {
        _uncontrolled = next;
        if (!IsControlled) Value = next;

        await onChange.InvokeAsync(next);
    }
}

/// <summary>
/// The one rule for "is this value controlled", shared by every stateful component.
/// </summary>
/// <remarks>
/// <para>A value is controlled when it is <b>set</b>, or when its change listener is <b>bound</b> and
/// no <b>default</b> was supplied. Everything else is uncontrolled: the component keeps the state,
/// seeded from the default.</para>
///
/// <para>The rule reads the values, not "was the parameter written in markup": a styled wrapper
/// always forwards <c>Value="@Value" ValueChanged="@ValueChanged" DefaultValue="@DefaultValue"</c>,
/// so the inner component cannot see what the consumer wrote, only what arrived.</para>
///
/// <para>The three cases the rule exists for, each of which one of the two older rules ("a listener
/// is bound" and "the value is not null") got wrong:</para>
/// <list type="number">
/// <item><description><c>Value="x"</c> with no listener is controlled. Under "a listener is bound" it
/// was ignored and the component showed nothing.</description></item>
/// <item><description><c>DefaultValue="a"</c> with <c>ValueChanged="Log"</c> (listening only) is
/// uncontrolled. Under "a listener is bound" it was controlled with no value, so the selection never
/// moved.</description></item>
/// <item><description><c>@bind-Value</c> on a field that holds nothing yet is controlled, and a parent
/// can set the value back to nothing. Under "the value is not null" the binding started uncontrolled
/// and a later null fell back to the component's own state.</description></item>
/// </list>
///
/// <para>A listener with neither a value nor a default cannot be told from a binding that holds
/// nothing, so it reads as controlled. To only listen, supply the default
/// (<c>DefaultValue="a" ValueChanged="Log"</c>).</para>
///
/// <para><b>What "set" and "default supplied" mean per kind of parameter.</b></para>
/// <list type="bullet">
/// <item><description><b>Nullable value and nullable default</b> (<c>string? Value</c>,
/// <c>DateOnly? Value</c>, <c>IReadOnlyList&lt;string&gt;? Values</c>): each is set when it is not
/// null. This is most components: Tabs, Select, Combobox, Accordion, Toggle Group, Radio Group,
/// Menubar, Navigation Menu, Calendar, Color Area, Slider, Input OTP, Input Tags, Input Color,
/// Input Date, Input Time, Tree, and the Data Table's sort, search, widths and order.</description></item>
/// <item><description><b>Nullable value, default that cannot be null</b> (<c>bool? Open</c> with
/// <c>bool DefaultOpen</c>, <c>Checked</c>, <c>Pressed</c>, <c>Expanded</c>, <c>Wrap</c>,
/// <c>CalendarOpen</c>, <c>int? SelectedIndex</c>): the default always exists, so it always counts
/// as supplied and the listener alone never controls. Nothing is lost: the change callback carries
/// a plain <c>bool</c> or <c>int</c>, so <c>@bind-Open</c> always supplies a value, and a listener
/// that arrives with no value can only be listening. The Color Picker's color is in this group too:
/// it has a built-in default color, so its state is never empty.</description></item>
/// <item><description><b>Search text</b> (<c>string? Search</c> with <c>string DefaultSearch = ""</c>):
/// an empty default is no default, so <c>@bind-Search</c> on a null string stays controlled.
/// </description></item>
/// <item><description><b>Values that cannot be null</b> (<c>TValue Value</c> on the number input when
/// <c>TValue</c> is not nullable, the Calendar's <c>DateRange</c>, the Data Table's <c>int Page</c>):
/// "set" and "supplied" mean "differs from the type's default" (<see cref="IsSet{T}"/>; for
/// <c>DefaultPage</c>, differs from 1). A one-way value that equals the type's default
/// (<c>Value="0"</c> with no listener) cannot be told from an unset one and is treated as unset:
/// bind it, or use a nullable type.</description></item>
/// <item><description><b>A value with no default twin</b> (<c>Format</c> and <c>SavedColors</c> on
/// the color components): the parameter seeds the state when its listener is not bound and controls
/// it when it is. Pass <c>valueSet: false, defaultSupplied: false</c>.</description></item>
/// </list>
///
/// <para>Avoid a binding next to a default on one value: the default wins while the bound value is
/// unset, so the parent cannot clear it (the component falls back to the last value it committed).
/// </para>
/// </remarks>
public static class ControllableState
{
    /// <summary>
    /// Whether a value is controlled this render: it is set, or its listener is bound and no default
    /// was supplied. See the remarks on <see cref="ControllableState"/> for what each argument means
    /// for each kind of parameter.
    /// </summary>
    /// <param name="valueSet">The value parameter holds a value.</param>
    /// <param name="listenerBound">The change callback has a delegate (<c>XChanged.HasDelegate</c>).</param>
    /// <param name="defaultSupplied">The default parameter holds a value, or the default always exists.</param>
    public static bool IsControlled(bool valueSet, bool listenerBound, bool defaultSupplied) =>
        valueSet || (listenerBound && !defaultSupplied);

    /// <summary>
    /// Whether <paramref name="value"/> differs from its type's default: not null for a reference or
    /// nullable type, not zero (or the empty struct) for a value type. The meaning of "set" for a
    /// parameter whose type is generic or cannot be null.
    /// </summary>
    public static bool IsSet<T>(T? value) => !EqualityComparer<T>.Default.Equals(value, default);
}
