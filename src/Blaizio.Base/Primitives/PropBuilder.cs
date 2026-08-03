using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blaizio;

/// <summary>
/// Assembles the single attribute dictionary a headless primitive splats onto its rendered
/// element (via <c>@attributes</c> / <see cref="BasePrimitive"/>).
/// </summary>
/// <remarks>
/// Stored values are only ever <see cref="string"/> (HTML / ARIA / <c>data-*</c> attributes) or
/// <see cref="EventCallback"/> (event handlers). Keeping values to those two shapes avoids the
/// classic Blazor splat foot-gun where a boxed <c>bool</c> renders as <c>"True"</c> - boolean HTML
/// attributes go through <see cref="Flag"/> (present-or-omitted) and ARIA booleans through
/// <see cref="Aria(string,bool)"/> (the literal strings <c>"true"</c>/<c>"false"</c>).
///
/// <para><b>Merge precedence</b>: <see cref="Merge"/> folds the
/// consumer's <c>class</c>/<c>style</c> and splatted attributes <i>over</i> the primitive's props -
/// <c>class</c> and <c>style</c> concatenate, matching event handlers compose (the primitive's runs
/// first, then the consumer's), and any other clashing key lets the consumer win.</para>
/// </remarks>
public sealed class PropBuilder
{
    private readonly Dictionary<string, object> _props = new(StringComparer.Ordinal);
    private readonly object? _owner;

    /// <summary>
    /// Creates a builder. Pass the owning component as <paramref name="owner"/> when the primitive
    /// wires event handlers, so a handler composed with a consumer's splatted one still drives the
    /// component's re-render. Components that emit no events can omit it.
    /// </summary>
    public PropBuilder(object? owner = null) => _owner = owner;

    /// <summary>Set a string attribute. A <c>null</c> value removes the key.</summary>
    public PropBuilder Set(string name, string? value)
    {
        if (value is null) _props.Remove(name);
        else _props[name] = value;
        return this;
    }

    /// <summary>
    /// A boolean HTML attribute (e.g. <c>disabled</c>, <c>hidden</c>): rendered as a bare attribute
    /// when <paramref name="on"/> is <see langword="true"/>, omitted entirely otherwise.
    /// </summary>
    public PropBuilder Flag(string name, bool on)
    {
        if (on) _props[name] = "";
        else _props.Remove(name);
        return this;
    }

    /// <summary>Set a <c>data-{name}</c> attribute.</summary>
    public PropBuilder Data(string name, string? value) => Set($"data-{name}", value);

    /// <summary>Set an <c>aria-{name}</c> string attribute.</summary>
    public PropBuilder Aria(string name, string? value) => Set($"aria-{name}", value);

    /// <summary>Set an <c>aria-{name}</c> boolean attribute as the literal <c>"true"</c>/<c>"false"</c> ARIA wants.</summary>
    public PropBuilder Aria(string name, bool value) => Set($"aria-{name}", value ? "true" : "false");

    /// <summary>Wire an event handler (e.g. <c>onclick</c>). No-op when the callback has no delegate.</summary>
    public PropBuilder On(string eventName, EventCallback callback)
    {
        if (callback.HasDelegate) _props[eventName] = callback;
        return this;
    }

    /// <inheritdoc cref="On(string,EventCallback)"/>
    public PropBuilder On<T>(string eventName, EventCallback<T> callback)
    {
        if (callback.HasDelegate) _props[eventName] = callback;
        return this;
    }

    /// <summary>
    /// Folds the consumer's <paramref name="userClass"/>/<paramref name="userStyle"/> and splatted
    /// <paramref name="userAttributes"/> over the assembled props and returns the final dictionary
    /// to splat. Safe to call once, at render time.
    /// </summary>
    public IReadOnlyDictionary<string, object> Merge(
        IReadOnlyDictionary<string, object>? userAttributes,
        string? userClass = null,
        string? userStyle = null)
    {
        if (!string.IsNullOrEmpty(userClass)) Append("class", userClass, " ");
        if (!string.IsNullOrEmpty(userStyle)) Append("style", userStyle, "; ");

        if (userAttributes is not null)
        {
            foreach (var (key, value) in userAttributes)
            {
                switch (key)
                {
                    case "class" when value is string c: Append("class", c, " "); break;
                    case "style" when value is string s: Append("style", s, "; "); break;
                    default:
                        _props[key] = TryCompose(key, value, out var composed) ? composed : value;
                        break;
                }
            }
        }

        return _props;
    }

    private void Append(string key, string addition, string separator)
    {
        _props[key] = _props.TryGetValue(key, out var existing) && existing is string { Length: > 0 } s
            ? $"{s}{separator}{addition}"
            : addition;
    }

    /// <summary>
    /// Compose the primitive's handler with the consumer's for the same event - primitive first,
    /// then consumer. Callbacks of the same shape compose; anything else (a non-callback value, or
    /// two callbacks with different argument types) lets the consumer's value win.
    /// </summary>
    /// <remarks>
    /// The composed callback keeps the incoming shape, which matters beyond the element splat: a
    /// props dictionary is also splatted onto <i>components</i> (the RenderAs idiom), and travels
    /// from there onto whatever element they render, where the DOM event expects exactly that
    /// <see cref="EventCallback{TValue}"/>. No Blaizio parameter is named after a DOM event (they
    /// are <c>Click</c>, <c>Select</c>, <c>Focus</c>, …), so an <c>onclick</c> key is never
    /// swallowed by a parameter on the way - it stays an attribute, and this composition is what
    /// keeps the target's own handler running alongside it.
    /// </remarks>
    private bool TryCompose(string key, object userValue, out object composed)
    {
        composed = userValue;
        if (!_props.TryGetValue(key, out var mine)) return false;

        var owner = _owner ?? this;
        switch (mine, userValue)
        {
            case (EventCallback a, EventCallback b):
                composed = EventCallback.Factory.Create(owner, () => Both(a.InvokeAsync(), b.InvokeAsync));
                return true;
            case (EventCallback<MouseEventArgs> a, EventCallback<MouseEventArgs> b):
                composed = EventCallback.Factory.Create<MouseEventArgs>(owner, e => Both(a.InvokeAsync(e), () => b.InvokeAsync(e)));
                return true;
            case (EventCallback<KeyboardEventArgs> a, EventCallback<KeyboardEventArgs> b):
                composed = EventCallback.Factory.Create<KeyboardEventArgs>(owner, e => Both(a.InvokeAsync(e), () => b.InvokeAsync(e)));
                return true;
            case (EventCallback<FocusEventArgs> a, EventCallback<FocusEventArgs> b):
                composed = EventCallback.Factory.Create<FocusEventArgs>(owner, e => Both(a.InvokeAsync(e), () => b.InvokeAsync(e)));
                return true;
            case (EventCallback<ChangeEventArgs> a, EventCallback<ChangeEventArgs> b):
                composed = EventCallback.Factory.Create<ChangeEventArgs>(owner, e => Both(a.InvokeAsync(e), () => b.InvokeAsync(e)));
                return true;
            default:
                return false;
        }
    }

    private static async Task Both(Task first, Func<Task> second)
    {
        await first;
        await second();
    }
}
