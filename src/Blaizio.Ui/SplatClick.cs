using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blaizio.Ui;

/// <summary>
/// Helpers for a component that wires its own <c>onclick</c> yet must still honour a consumer's
/// captured <c>@onclick</c>. Blazor captures <c>@onclick</c> as an <see cref="EventCallback{T}"/>
/// of <see cref="MouseEventArgs"/>, which can't be composed through the splat with the component's
/// own handler - so the component invokes the consumer's explicitly and keeps it out of the
/// splatted attributes (so the DOM element isn't wired twice).
/// </summary>
internal static class SplatClick
{
    /// <summary>The attributes minus any <c>onclick</c> (which the component invokes itself).</summary>
    public static IReadOnlyDictionary<string, object>? WithoutOnClick(IReadOnlyDictionary<string, object>? attributes)
    {
        if (attributes is null || !attributes.ContainsKey("onclick")) return attributes;
        var copy = new Dictionary<string, object>(attributes, StringComparer.Ordinal);
        copy.Remove("onclick");
        return copy;
    }

    /// <summary>Invokes the consumer's captured <c>@onclick</c>, if there is one.</summary>
    public static Task InvokeConsumerClickAsync(IReadOnlyDictionary<string, object>? attributes, MouseEventArgs e) =>
        attributes is not null
        && attributes.TryGetValue("onclick", out var raw)
        && raw is EventCallback<MouseEventArgs> callback
            ? callback.InvokeAsync(e)
            : Task.CompletedTask;
}
