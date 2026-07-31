using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// Dev-aid console warnings for accessibility contracts the markup cannot enforce (the same
/// pattern as the Dialog's unnamed-dialog warning). A focusable composite (carousel region,
/// radiogroup, menubar, tablist, resize handle) needs an accessible name; when neither the typed
/// AriaLabel/AriaLabelledBy parameters nor a splatted aria-label/aria-labelledby supply one,
/// the component calls this once after its first render.
/// </summary>
internal static class AriaDiagnostics
{
    /// <summary>Whether the component received an accessible name through either channel.</summary>
    public static bool HasName(string? ariaLabel, string? ariaLabelledBy, IReadOnlyDictionary<string, object>? attributes) =>
        !string.IsNullOrEmpty(ariaLabel)
        || !string.IsNullOrEmpty(ariaLabelledBy)
        || (attributes is not null && (attributes.ContainsKey("aria-label") || attributes.ContainsKey("aria-labelledby")));

    /// <summary>
    /// Emit the unnamed-composite warning for <paramref name="component"/>. Call only when
    /// <see cref="HasName"/> is <see langword="false"/>, once, after the first render.
    /// </summary>
    public static async Task WarnUnnamedAsync(IJSRuntime js, string component)
    {
        try
        {
            await js.InvokeVoidAsync("console.warn",
                $"Blaizio {component}: set AriaLabel (or AriaLabelledBy pointing at a visible heading) " +
                "so screen readers can announce what this control operates on.");
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone.
        }
    }
}
