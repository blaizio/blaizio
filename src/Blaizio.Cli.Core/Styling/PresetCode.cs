namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// A decoded preset code: the full /create selection. <see cref="Chart"/>, <see cref="Heading"/>,
/// <see cref="Font"/> and <see cref="Radius"/> are token overlays (docs-side CSS classes);
/// <c>"default"</c> means "not customized".
/// </summary>
public sealed record PresetSelection(
    string Style,
    string Preset,
    bool Rtl,
    string Chart = "default",
    string Heading = "default",
    string Font = "default",
    string Radius = "default");

/// <summary>
/// The compact shareable code behind the docs /create page and <c>blaizio add --preset</c>
/// (code-as-state): nothing is stored anywhere - the code IS the state. Each knob's
/// option index is packed into one base-36 character.
///
/// Two generations, told apart by length:
///   v1 (2-3 chars): <c>[style][preset][r?]</c>
///   v2 (6-7 chars): <c>[style][preset][chart][heading][font][radius][r?]</c>
///
/// The option tables below are the single canonical order, shared by the CLI and the docs
/// (Blaizio.Docs delegates here). They are APPEND-ONLY: reordering or removing entries breaks
/// every previously shared code.
/// </summary>
public static class PresetCode
{
    /// <summary>Canonical style (skin) order. Append-only.</summary>
    public static readonly string[] Styles =
        ["ember", "spark", "glow", "forge", "flint", "aura", "ash", "wisp"];

    /// <summary>Canonical color preset order ("nova" = the built-in default). Append-only.</summary>
    public static readonly string[] Presets =
    [
        "nova", "nebula", "quasar", "comet", "zenith", "solstice", "meteor", "pulsar", "eclipse",
        "polaris", "umbra", "corona", "magnetar", "aurora", "equinox",
    ];

    /// <summary>Canonical chart palette order. Append-only.</summary>
    public static readonly string[] Charts =
        ["default", "ocean", "sunset", "forest", "mono"];

    /// <summary>Canonical font order (shared by the heading and body knobs). The order lives in
    /// <see cref="FontCatalog.All"/>, which is append-only for the same reason these tables are.</summary>
    public static readonly string[] Fonts =
        [.. FontCatalog.All.Select(f => f.Name)];

    /// <summary>Canonical radius scale order. Append-only.</summary>
    public static readonly string[] Radii =
        ["default", "none", "sm", "lg", "xl"];

    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Encode a selection. Emits the short v1 form when every overlay is still "default" (so the
    /// common style+color share stays 2-3 chars), the v2 form otherwise.
    /// </summary>
    public static string Encode(PresetSelection selection)
    {
        var s = Digits[Math.Max(0, Array.IndexOf(Styles, selection.Style))];
        var p = Digits[Math.Max(0, Array.IndexOf(Presets, selection.Preset))];
        var rtl = selection.Rtl ? "r" : "";

        if (selection is { Chart: "default", Heading: "default", Font: "default", Radius: "default" })
            return $"{s}{p}{rtl}";

        var c = Digits[Math.Max(0, Array.IndexOf(Charts, selection.Chart))];
        var h = Digits[Math.Max(0, Array.IndexOf(Fonts, selection.Heading))];
        var f = Digits[Math.Max(0, Array.IndexOf(Fonts, selection.Font))];
        var r = Digits[Math.Max(0, Array.IndexOf(Radii, selection.Radius))];
        return $"{s}{p}{c}{h}{f}{r}{rtl}";
    }

    /// <summary>Decode a v1 or v2 code. False when malformed or any index is out of range.</summary>
    public static bool TryDecode(string? code, out PresetSelection selection)
    {
        selection = default!;
        code = code?.Trim().ToLowerInvariant();
        if (code is not { Length: 2 or 3 or 6 or 7 }) return false;

        var rtl = code.Length is 3 or 7;
        if (rtl)
        {
            if (code[^1] != 'r') return false;
            code = code[..^1];
        }

        if (!TryIndex(code[0], Styles, out var style) || !TryIndex(code[1], Presets, out var preset))
            return false;

        if (code.Length == 2)
        {
            selection = new PresetSelection(style, preset, rtl);
            return true;
        }

        if (!TryIndex(code[2], Charts, out var chart)
            || !TryIndex(code[3], Fonts, out var heading)
            || !TryIndex(code[4], Fonts, out var font)
            || !TryIndex(code[5], Radii, out var radius))
            return false;

        selection = new PresetSelection(style, preset, rtl, chart, heading, font, radius);
        return true;
    }

    private static bool TryIndex(char digit, string[] options, out string value)
    {
        value = default!;
        var i = Digits.IndexOf(digit);
        if (i < 0 || i >= options.Length) return false;
        value = options[i];
        return true;
    }
}
