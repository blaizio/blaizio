using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// The browser-side services Blazor cannot provide reliably from C#: focus that survives open
/// animations, and the declarative per-key <c>preventDefault</c> guard. Registered by
/// <c>AddBlaizio()</c>; wraps <c>core.js</c>, the module the rest of Blaizio's JS is built on.
/// <para>
/// Backed by JS interop: call these from <c>OnAfterRenderAsync</c> or event handlers, never during
/// prerendering.
/// </para>
/// </summary>
/// <remarks>
/// There is deliberately NO <c>PreventDefaultAsync</c> / <c>StopPropagationAsync</c> here. The
/// browser applies an event's default action synchronously during dispatch, while a C# handler
/// runs after it (on Blazor Server a network round-trip later) - an imperative API would be
/// timing-dependent by construction. Suppress a key's default declaratively instead: put
/// <c>data-bz-prevent-keys="enter, mod+s"</c> on the element (comma-separated combos, exact
/// modifier match, <c>mod</c> = ⌘ on mac / Ctrl elsewhere) and make sure the guard listener is
/// loaded via <see cref="EnsureGuardsAsync"/> - or use a component parameter like
/// <c>BzInputText.PreventKeys</c>, which does both. The event still reaches the Blazor handler;
/// only its default action is suppressed. For the static whole-event case Blazor's own
/// <c>@onkeydown:preventDefault</c> / <c>@onclick:stopPropagation</c> remain the right tool.
/// </remarks>
public interface ICore
{
    /// <summary>
    /// Focuses <paramref name="element"/>, retrying across animation frames while it is still
    /// disconnected or unrendered (e.g. <c>display:none</c> mid-open-animation - the case where
    /// the built-in <c>ElementReference.FocusAsync</c> silently fails). Returns whether the element
    /// ended up focused.
    /// </summary>
    ValueTask<bool> FocusAsync(ElementReference element, FocusOptions? options = null);

    /// <summary>
    /// Ensures the delegated <c>data-bz-prevent-keys</c> guard listener is installed (loads
    /// <c>core.js</c>; installation is idempotent). Call once after first render from any
    /// component that renders the attribute itself.
    /// </summary>
    ValueTask EnsureGuardsAsync();
}

/// <summary>Options for <see cref="ICore.FocusAsync"/>.</summary>
public sealed record FocusOptions
{
    /// <summary>Skip the browser's scroll-into-view on focus.</summary>
    public bool PreventScroll { get; init; }

    /// <summary>Also select the content (text-entry elements only).</summary>
    public bool Select { get; init; }

    /// <summary>Extra animation frames to wait for the element to become focusable. Default 3.</summary>
    public int Retries { get; init; } = 3;
}
