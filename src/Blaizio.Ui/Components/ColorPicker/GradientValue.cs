using System.Globalization;
using System.Text;

namespace Blaizio.Ui;

/// <summary>One stop of a <see cref="GradientValue"/>.</summary>
/// <param name="Color">The stop color - any string <see cref="ColorMath"/> can parse.</param>
/// <param name="Position">Where it sits along the ramp, <c>0</c>-<c>1</c>.</param>
public sealed record GradientStop(string Color, double Position);

/// <summary>
/// The gradient a <see cref="BzColorPicker"/> edits in <see cref="ColorPickerMode.Gradient"/>: a
/// shape, an angle and an ordered stop list. <see cref="ToCss"/> renders it as a CSS paint (what
/// the picker's <c>Value</c> carries in gradient mode) and <see cref="TryParse"/> reads one back,
/// so <c>@bind-Value</c> round-trips.
/// </summary>
/// <remarks>
/// Assign the string to the <c>background</c> shorthand. <see cref="GradientType.Diamond"/> has no
/// CSS function of its own - it renders as four quadrant ramps, each carrying its own position and
/// size, which only the shorthand accepts. Every other shape is a single gradient function and
/// works anywhere a <c>&lt;image&gt;</c> does, <c>background-image</c> included.
/// </remarks>
/// <param name="Type">The shape. Defaults to <see cref="GradientType.Linear"/>.</param>
/// <param name="Angle">Ramp direction in degrees, for <see cref="GradientType.Linear"/> and <see cref="GradientType.Angular"/>. <c>180</c> is the CSS default (top to bottom).</param>
/// <param name="Stops">The stops, sorted by position. Always at least two.</param>
public sealed record GradientValue(
    GradientType Type,
    double Angle,
    IReadOnlyList<GradientStop> Stops)
{
    /// <summary>The CSS default direction - top to bottom.</summary>
    public const double DefaultAngle = 180;

    /// <summary>A two-stop linear ramp from <paramref name="color"/> to its transparent self.</summary>
    public static GradientValue FromColor(string color)
    {
        var transparent = ColorMath.TryParse(color, out var h, out var s, out var v, out _)
            ? ColorMath.Format(h, s, v, 0, ColorFormat.Hex)
            : "#00000000";
        return new(GradientType.Linear, DefaultAngle, [new(color, 0), new(transparent, 1)]);
    }

    /// <summary>The stops sorted by position - the order every renderer and editor sees.</summary>
    public IReadOnlyList<GradientStop> Sorted => [.. Stops.OrderBy(stop => stop.Position)];

    /// <summary>This gradient with one stop's color replaced.</summary>
    public GradientValue WithStopColor(int index, string color) =>
        index < 0 || index >= Stops.Count
            ? this
            : this with { Stops = [.. Stops.Select((stop, i) => i == index ? stop with { Color = color } : stop)] };

    /// <summary>This gradient with one stop moved along the ramp (clamped to <c>0</c>-<c>1</c>).</summary>
    public GradientValue WithStopPosition(int index, double position) =>
        index < 0 || index >= Stops.Count
            ? this
            : this with
            {
                Stops = [.. Stops.Select((stop, i) => i == index ? stop with { Position = Math.Clamp(position, 0, 1) } : stop)],
            };

    /// <summary>This gradient with a stop inserted at <paramref name="position"/>, colored by sampling the ramp there.</summary>
    public GradientValue WithStopAdded(double position, out int index)
    {
        position = Math.Clamp(position, 0, 1);
        var color = ColorAt(position);
        var stops = new List<GradientStop>(Stops);
        index = stops.FindIndex(stop => stop.Position > position);
        if (index < 0) index = stops.Count;
        stops.Insert(index, new(color, position));
        return this with { Stops = stops };
    }

    /// <summary>This gradient without one stop. Two stops is the floor - removing below it is a no-op.</summary>
    public GradientValue WithStopRemoved(int index) =>
        Stops.Count <= 2 || index < 0 || index >= Stops.Count
            ? this
            : this with { Stops = [.. Stops.Where((_, i) => i != index)] };

    /// <summary>The ramp color at <paramref name="position"/>, interpolated in sRGB like the browser does.</summary>
    public string ColorAt(double position)
    {
        var sorted = Sorted;
        if (sorted.Count == 0) return "#000000";

        var lower = sorted[0];
        var upper = sorted[^1];
        foreach (var stop in sorted)
        {
            if (stop.Position <= position) lower = stop;
            if (stop.Position >= position) { upper = stop; break; }
        }

        if (!ColorMath.TryParse(lower.Color, out var h1, out var s1, out var v1, out var a1)) return lower.Color;
        if (!ColorMath.TryParse(upper.Color, out var h2, out var s2, out var v2, out var a2)) return lower.Color;

        var span = upper.Position - lower.Position;
        var t = span <= 0 ? 0 : Math.Clamp((position - lower.Position) / span, 0, 1);

        // Mix through RGB, not HSV: that is what the browser paints between two stops.
        var (r1, g1, b1) = ColorMath.HsvToRgb(h1, s1, v1);
        var (r2, g2, b2) = ColorMath.HsvToRgb(h2, s2, v2);
        var (h, s, v) = ColorMath.RgbToHsv(
            (byte)Math.Round(r1 + (r2 - r1) * t),
            (byte)Math.Round(g1 + (g2 - g1) * t),
            (byte)Math.Round(b1 + (b2 - b1) * t));
        return ColorMath.Format(h, s, v, a1 + (a2 - a1) * t, ColorFormat.Hex);
    }

    /// <summary>The CSS paint for this gradient - what the picker's <c>Value</c> holds in gradient mode.</summary>
    public string ToCss()
    {
        var stops = string.Join(", ", Sorted.Select(stop => $"{stop.Color} {Percent(stop.Position)}"));
        return Type switch
        {
            GradientType.Radial => $"radial-gradient(circle at center, {stops})",
            GradientType.Angular => $"conic-gradient(from {Degrees(Angle)} at center, {stops})",
            GradientType.Diamond => string.Join(", ", DiamondCorners.Select(corner =>
                $"linear-gradient(to {corner.Direction}, {stops}) {corner.Origin} / 50% 50% no-repeat")),
            _ => $"linear-gradient({Degrees(Angle)}, {stops})",
        };
    }

    /// <summary>A paint that previews the ramp itself, ignoring shape and angle - for the stop bar.</summary>
    public string ToRampCss() =>
        $"linear-gradient(to right, {string.Join(", ", Sorted.Select(stop => $"{stop.Color} {Percent(stop.Position)}"))})";

    /// <inheritdoc cref="ToCss"/>
    public override string ToString() => ToCss();

    // The four quadrants of the diamond: each ramp runs from the element's centre out to its corner.
    private static readonly (string Direction, string Origin)[] DiamondCorners =
    [
        ("top left", "left top"),
        ("top right", "right top"),
        ("bottom left", "left bottom"),
        ("bottom right", "right bottom"),
    ];

    // ---- parsing ------------------------------------------------------------------------------

    /// <summary>
    /// Read a CSS gradient back into the model. Accepts what <see cref="ToCss"/> writes plus the
    /// usual hand-written forms: <c>linear-gradient</c> with an angle or a <c>to &lt;side&gt;</c>
    /// keyword, <c>radial-gradient</c>/<c>conic-gradient</c> with or without a shape/position
    /// prelude, and stops with or without explicit positions.
    /// </summary>
    public static bool TryParse(string? text, out GradientValue gradient)
    {
        gradient = FromColor("#000000");
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();
        var layers = SplitTopLevel(trimmed, ',');

        // The diamond's four quadrant layers all carry the same stop list - read the first one.
        if (layers.Count == 4 && layers.All(layer => layer.Contains("linear-gradient(", StringComparison.OrdinalIgnoreCase))
            && layers.All(layer => layer.Contains("50%", StringComparison.Ordinal)))
        {
            var first = layers[0];
            var open = first.IndexOf('(');
            var close = MatchingParen(first, open);
            if (open < 0 || close < 0 || !TryParseStops(first[(open + 1)..close], skipPrelude: true, out var corners))
                return false;
            gradient = new(GradientType.Diamond, DefaultAngle, corners);
            return true;
        }

        var (type, prefix) = trimmed switch
        {
            _ when trimmed.StartsWith("linear-gradient(", StringComparison.OrdinalIgnoreCase) => (GradientType.Linear, "linear-gradient("),
            _ when trimmed.StartsWith("radial-gradient(", StringComparison.OrdinalIgnoreCase) => (GradientType.Radial, "radial-gradient("),
            _ when trimmed.StartsWith("conic-gradient(", StringComparison.OrdinalIgnoreCase) => (GradientType.Angular, "conic-gradient("),
            _ => (GradientType.Linear, ""),
        };
        if (prefix.Length == 0) return false;

        var body = trimmed[(prefix.Length - 1)..];
        var end = MatchingParen(body, 0);
        if (end < 0) return false;

        var inner = body[1..end];
        var angle = type == GradientType.Radial ? DefaultAngle : AngleOf(inner, type);
        if (!TryParseStops(inner, skipPrelude: true, out var stops)) return false;

        gradient = new(type, angle, stops);
        return true;
    }

    // The prelude is the leading direction/shape argument, when the first comma-separated part
    // carries no color: "45deg", "to right", "from 90deg at center", "circle at center".
    private static bool TryParseStops(string inner, bool skipPrelude, out IReadOnlyList<GradientStop> stops)
    {
        var parts = SplitTopLevel(inner, ',');
        if (skipPrelude && parts.Count > 0 && IsPrelude(parts[0]))
            parts.RemoveAt(0);

        var parsed = new List<GradientStop>();
        foreach (var part in parts)
        {
            if (!TryParseStop(part, parsed.Count, parts.Count, out var stop)) continue;
            parsed.Add(stop);
        }

        // A gradient needs two ends; anything less is not something the editor can drive.
        if (parsed.Count < 2)
        {
            stops = [];
            return false;
        }

        stops = [.. parsed.OrderBy(stop => stop.Position)];
        return true;
    }

    private static bool IsPrelude(string part)
    {
        var text = part.Trim();
        if (text.StartsWith("to ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("from ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("at ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("circle", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("ellipse", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
            return true;
        // A bare angle: "45deg", "0.25turn", ".5rad".
        return TryParseAngle(text, out _);
    }

    private static bool TryParseStop(string part, int index, int count, out GradientStop stop)
    {
        stop = new("#000000", 0);
        var text = part.Trim();
        if (text.Length == 0) return false;

        // The position is the trailing token, when it is a percentage or a bare fraction.
        var space = LastTopLevelSpace(text);
        double? position = null;
        if (space > 0)
        {
            var tail = text[(space + 1)..].Trim();
            if (tail.EndsWith('%') && double.TryParse(tail[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                position = percent / 100;
                text = text[..space].Trim();
            }
            else if (double.TryParse(tail, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction))
            {
                position = fraction;
                text = text[..space].Trim();
            }
        }

        if (!ColorMath.TryParse(text, out _, out _, out _, out _)) return false;

        // No explicit position: spread the stop evenly, the way CSS does.
        var spread = count > 1 ? (double)index / (count - 1) : 0;
        stop = new(text, Math.Clamp(position ?? spread, 0, 1));
        return true;
    }

    private static double AngleOf(string inner, GradientType type)
    {
        var parts = SplitTopLevel(inner, ',');
        if (parts.Count == 0) return DefaultAngle;
        var prelude = parts[0].Trim();

        if (type == GradientType.Angular)
        {
            var from = prelude.IndexOf("from ", StringComparison.OrdinalIgnoreCase);
            if (from < 0) return 0; // conic's own default is 0deg (12 o'clock)
            var rest = prelude[(from + 5)..].Trim();
            var at = rest.IndexOf(" at ", StringComparison.OrdinalIgnoreCase);
            if (at >= 0) rest = rest[..at];
            return TryParseAngle(rest.Trim(), out var conic) ? conic : 0;
        }

        if (TryParseAngle(prelude, out var angle)) return angle;

        // The `to <side>` keywords, as their equivalent angles.
        return prelude.Replace("to ", "", StringComparison.OrdinalIgnoreCase).Trim().ToLowerInvariant() switch
        {
            "top" => 0,
            "right" => 90,
            "bottom" => 180,
            "left" => 270,
            "top right" or "right top" => 45,
            "bottom right" or "right bottom" => 135,
            "bottom left" or "left bottom" => 225,
            "top left" or "left top" => 315,
            _ => DefaultAngle,
        };
    }

    private static bool TryParseAngle(string text, out double degrees)
    {
        degrees = 0;
        var (suffix, scale) = text switch
        {
            _ when text.EndsWith("deg", StringComparison.OrdinalIgnoreCase) => ("deg", 1d),
            _ when text.EndsWith("turn", StringComparison.OrdinalIgnoreCase) => ("turn", 360d),
            _ when text.EndsWith("rad", StringComparison.OrdinalIgnoreCase) => ("rad", 180 / Math.PI),
            _ when text.EndsWith("grad", StringComparison.OrdinalIgnoreCase) => ("grad", 0.9),
            _ => ("", 0d),
        };
        if (suffix.Length == 0) return false;
        if (!double.TryParse(text[..^suffix.Length].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var raw))
            return false;
        degrees = raw * scale;
        return true;
    }

    // ---- text helpers -------------------------------------------------------------------------

    /// <summary>Split on a separator that is not inside parentheses - <c>rgb(1, 2, 3)</c> stays whole.</summary>
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var buffer = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            if (ch == separator && depth == 0)
            {
                parts.Add(buffer.ToString().Trim());
                buffer.Clear();
                continue;
            }
            buffer.Append(ch);
        }
        if (buffer.Length > 0) parts.Add(buffer.ToString().Trim());
        return [.. parts.Where(part => part.Length > 0)];
    }

    private static int LastTopLevelSpace(string text)
    {
        var depth = 0;
        var last = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') depth--;
            else if (text[i] == ' ' && depth == 0) last = i;
        }
        return last;
    }

    private static int MatchingParen(string text, int open)
    {
        if (open < 0 || open >= text.Length || text[open] != '(') return -1;
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static string Percent(double position) =>
        Math.Round(position * 100, 2).ToString(CultureInfo.InvariantCulture) + "%";

    private static string Degrees(double angle) =>
        Math.Round(angle, 2).ToString(CultureInfo.InvariantCulture) + "deg";
}
