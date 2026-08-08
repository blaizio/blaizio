namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// A decoded preset code: the full /create selection. <see cref="Chart"/>, <see cref="Heading"/>,
/// <see cref="Font"/> and <see cref="Radius"/> are token overlays (docs-side CSS classes);
/// <c>"default"</c> means "not customized". <see cref="Overrides"/> are the directly-edited theme
/// tokens layered over the preset (empty = none).
/// </summary>
public sealed record PresetSelection(
    string Style,
    string Preset,
    bool Rtl,
    string Chart = "default",
    string Heading = "default",
    string Font = "default",
    string Radius = "default")
{
    /// <summary>Directly-edited theme tokens (see <see cref="ThemeTokens"/>), applied over the
    /// preset. Init-only so the positional shape (and every existing call site) stays intact.</summary>
    public IReadOnlyList<TokenOverride> Overrides { get; init; } = [];

    // Sequence equality for the list member - the record default would compare references, which
    // would break every "decode equals what was encoded" comparison (history, tests, dialogs).
    public bool Equals(PresetSelection? other) =>
        other is not null && Style == other.Style && Preset == other.Preset && Rtl == other.Rtl
        && Chart == other.Chart && Heading == other.Heading && Font == other.Font
        && Radius == other.Radius && Overrides.SequenceEqual(other.Overrides);

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(Style, Preset, Rtl, Chart, Heading, Font, Radius);
        foreach (var o in Overrides) hash = HashCode.Combine(hash, o);
        return hash;
    }
}

/// <summary>
/// The compact shareable code behind the docs /create page and <c>blaizio add --preset</c>
/// (code-as-state): nothing is stored anywhere - the code IS the state. Each knob's
/// option index is packed into one base-36 character.
///
/// Three generations:
///   v1 (2-3 chars): <c>[style][preset][r?]</c>
///   v2 (6-7 chars): <c>[style][preset][chart][heading][font][radius][r?]</c>
///   v3 (v1/v2 + "-" + 7-char chunks): each chunk is one edited theme token -
///   <c>[mode+token][LL][CC][HH]</c>, all base-36 (mode folds into the first char: dark adds 16
///   to the ThemeTokens index; L and C quantized to thousandths, H to whole degrees).
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

    /// <summary>Canonical chart palette order. Append-only. The preset-named entries are the
    /// series their namesake color presets pair with by default.</summary>
    public static readonly string[] Charts =
    [
        "default", "ocean", "sunset", "forest", "mono",
        "polaris", "umbra", "corona", "magnetar", "aurora", "equinox",
    ];

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
    /// common style+color share stays 2-3 chars), the v2 form otherwise, and appends the v3
    /// override segment only when tokens were edited.
    /// </summary>
    public static string Encode(PresetSelection selection)
    {
        var s = Digits[Math.Max(0, Array.IndexOf(Styles, selection.Style))];
        var p = Digits[Math.Max(0, Array.IndexOf(Presets, selection.Preset))];
        var rtl = selection.Rtl ? "r" : "";

        var body = selection is { Chart: "default", Heading: "default", Font: "default", Radius: "default" }
            ? $"{s}{p}{rtl}"
            : $"{s}{p}{Digits[Math.Max(0, Array.IndexOf(Charts, selection.Chart))]}" +
              $"{Digits[Math.Max(0, Array.IndexOf(Fonts, selection.Heading))]}" +
              $"{Digits[Math.Max(0, Array.IndexOf(Fonts, selection.Font))]}" +
              $"{Digits[Math.Max(0, Array.IndexOf(Radii, selection.Radius))]}{rtl}";

        if (selection.Overrides.Count == 0) return body;

        var sb = new System.Text.StringBuilder(body).Append('-');
        foreach (var o in selection.Overrides)
        {
            var token = Array.IndexOf(ThemeTokens.All, o.Token);
            if (token < 0) continue;
            sb.Append(Digits[token + (o.Dark ? ThemeTokens.All.Length : 0)]);
            AppendPacked(sb, (int)Math.Clamp(Math.Round(o.Color.L * 1000), 0, 1000));
            AppendPacked(sb, (int)Math.Clamp(Math.Round(o.Color.C * 1000), 0, 1000));
            AppendPacked(sb, (int)Math.Clamp(Math.Round(o.Color.H), 0, 359) % 360);
        }
        return sb.ToString();

        // Two base-36 chars: 0..1295, enough for every quantized component.
        static void AppendPacked(System.Text.StringBuilder sb, int value) =>
            sb.Append(Digits[value / 36]).Append(Digits[value % 36]);
    }

    /// <summary>Decode a v1, v2 or v3 code. False when malformed or any index is out of range.</summary>
    public static bool TryDecode(string? code, out PresetSelection selection)
    {
        selection = default!;
        code = code?.Trim().ToLowerInvariant();
        if (code is null) return false;

        // v3: the base code, then "-", then 7-char override chunks.
        List<TokenOverride> overrides = [];
        var dash = code.IndexOf('-');
        if (dash >= 0)
        {
            var tail = code[(dash + 1)..];
            code = code[..dash];
            if (tail.Length == 0 || tail.Length % 7 != 0) return false;
            for (var i = 0; i < tail.Length; i += 7)
            {
                var slot = Digits.IndexOf(tail[i]);
                if (slot < 0 || slot >= ThemeTokens.All.Length * 2) return false;
                if (!TryPacked(tail, i + 1, out var l) || !TryPacked(tail, i + 3, out var c)
                    || !TryPacked(tail, i + 5, out var h))
                    return false;
                if (l > 1000 || c > 1000 || h > 359) return false;
                overrides.Add(new TokenOverride(
                    ThemeTokens.All[slot % ThemeTokens.All.Length],
                    slot >= ThemeTokens.All.Length,
                    new OklchColor(l / 1000.0, c / 1000.0, h)));
            }
        }

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
            selection = new PresetSelection(style, preset, rtl) { Overrides = overrides };
            return true;
        }

        if (!TryIndex(code[2], Charts, out var chart)
            || !TryIndex(code[3], Fonts, out var heading)
            || !TryIndex(code[4], Fonts, out var font)
            || !TryIndex(code[5], Radii, out var radius))
            return false;

        selection = new PresetSelection(style, preset, rtl, chart, heading, font, radius) { Overrides = overrides };
        return true;

        static bool TryPacked(string s, int at, out int value)
        {
            value = 0;
            var hi = Digits.IndexOf(s[at]);
            var lo = Digits.IndexOf(s[at + 1]);
            if (hi < 0 || lo < 0) return false;
            value = hi * 36 + lo;
            return true;
        }
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
