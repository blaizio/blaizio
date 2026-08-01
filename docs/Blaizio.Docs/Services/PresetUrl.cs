using Blaizio.Cli.Core.Styling;
using Microsoft.AspNetCore.Components;

namespace Blaizio.Docs.Services;

/// <summary>
/// The composer's URL contract: a shareable <c>?preset=</c> code on <c>/themes</c>. Reading
/// decodes a deep link; syncing rewrites the current URL (replace, no history spam) so the
/// address bar always carries the live selection.
/// </summary>
public static class PresetUrl
{
    /// <summary>Decode a <c>?preset=</c> deep link, if the current URL carries a valid one.</summary>
    public static bool TryReadCode(NavigationManager nav, out PresetSelection selection)
    {
        selection = default!;
        var query = new Uri(nav.Uri).Query;
        if (string.IsNullOrEmpty(query)) return false;
        foreach (var pair in query.TrimStart('?').Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0 || !pair[..eq].Equals("preset", StringComparison.OrdinalIgnoreCase)) continue;
            return PresetCode.TryDecode(Uri.UnescapeDataString(pair[(eq + 1)..]), out selection);
        }
        return false;
    }

    /// <summary>Reflect the current selection into <c>?preset=</c> so the URL is always shareable.</summary>
    public static void Sync(NavigationManager nav, string code) =>
        nav.NavigateTo($"themes?preset={code}", replace: true);
}
