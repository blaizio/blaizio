using System.Globalization;
using System.Text;

namespace Blaizio.Ui;

/// <summary>
/// The image fill a <see cref="BzColorPicker"/> edits in <see cref="ColorPickerMode.Image"/>: a
/// source, how it lays into its box, and seven tone adjustments. <see cref="ToCss"/> renders the
/// paint (what the picker's <c>Value</c> carries in image mode) and <see cref="ToFilterCss"/> the
/// matching <c>filter</c> - two properties, because CSS has no way to fold a filter into a paint.
/// </summary>
/// <remarks>
/// Every adjustment runs <c>-1</c> to <c>1</c>, <c>0</c> being the untouched image. Exposure,
/// contrast and saturation map onto the native filter functions; temperature, tint, highlights and
/// shadows have no CSS function of their own, so they are carried by an SVG filter the picker
/// renders alongside - <see cref="ToFilterCss"/> takes the id of that filter and references it.
/// Rotation is baked into the bitmap when the user rotates, so <see cref="Src"/> always points at
/// the image as it looks.
/// </remarks>
/// <param name="Src">Where the bitmap lives. The picker sets an object URL for a chosen file, which lives only as long as the page does.</param>
/// <param name="Fit">How the bitmap lays into its box.</param>
/// <param name="Exposure">Overall lightness.</param>
/// <param name="Contrast">Distance between lights and darks.</param>
/// <param name="Saturation">Color intensity.</param>
/// <param name="Temperature">Warm (positive) to cool (negative).</param>
/// <param name="Tint">Magenta (positive) to green (negative).</param>
/// <param name="Highlights">Lifts or pulls down the bright end.</param>
/// <param name="Shadows">Lifts or pulls down the dark end.</param>
public sealed record ImageFillValue(
    string? Src = null,
    ImageFillFit Fit = ImageFillFit.Fill,
    double Exposure = 0,
    double Contrast = 0,
    double Saturation = 0,
    double Temperature = 0,
    double Tint = 0,
    double Highlights = 0,
    double Shadows = 0)
{
    /// <summary>An empty fill - no image, everything neutral.</summary>
    public static ImageFillValue Empty { get; } = new();

    /// <summary>
    /// The id of the SVG filter carrying temperature, tint, highlights and shadows. The picker
    /// stamps this on the model and renders the matching <c>&lt;filter&gt;</c>, so
    /// <see cref="ToFilterCss"/> resolves for a consumer too - as long as the picker is on the
    /// page, since a filter reference points at a live element.
    /// </summary>
    public string? FilterId { get; init; }

    /// <summary>Whether there is a bitmap to paint.</summary>
    public bool HasImage => !string.IsNullOrWhiteSpace(Src);

    /// <summary>The adjustments back at neutral, keeping the image and its fit.</summary>
    public ImageFillValue Reset() => new(Src, Fit) { FilterId = FilterId };

    /// <summary>The CSS paint for this fill - what the picker's <c>Value</c> holds in image mode.</summary>
    public string ToCss()
    {
        if (!HasImage) return "none";
        // Tile scales to half the box rather than painting at the bitmap's own size: a photo is
        // usually far larger than what it fills, so a natural-size tile would never repeat.
        var (size, repeat, position) = Fit switch
        {
            ImageFillFit.Fit => ("contain", "no-repeat", "center"),
            ImageFillFit.Crop => ("auto", "no-repeat", "center"),
            ImageFillFit.Tile => ("50%", "repeat", "left top"),
            _ => ("cover", "no-repeat", "center"),
        };
        return $"url({Src}) {position}/{size} {repeat}";
    }

    /// <summary>
    /// The <c>filter</c> that goes with the paint. Exposure, contrast and saturation become the
    /// native functions; temperature, tint, highlights and shadows are referenced through
    /// <see cref="FilterId"/>, the SVG filter the picker renders for them.
    /// </summary>
    public string ToFilterCss()
    {
        var parts = new List<string>();
        if (Exposure != 0) parts.Add($"brightness({Number(1 + Exposure)})");
        if (Contrast != 0) parts.Add($"contrast({Number(1 + Contrast)})");
        if (Saturation != 0) parts.Add($"saturate({Number(1 + Saturation)})");
        if (NeedsSvgFilter && FilterId is not null) parts.Add($"url(#{FilterId})");
        return parts.Count > 0 ? string.Join(' ', parts) : "none";
    }

    /// <summary>Whether any adjustment needs the SVG filter (the other three are native functions).</summary>
    public bool NeedsSvgFilter => Temperature != 0 || Tint != 0 || Highlights != 0 || Shadows != 0;

    /// <summary>
    /// The <c>feColorMatrix</c> values for temperature and tint: a per-channel gain, warming by
    /// lifting red and dropping blue (and the reverse going cool), tinting by trading green
    /// against magenta.
    /// </summary>
    public string ColorMatrix()
    {
        var r = 1 + 0.25 * Temperature;
        var b = 1 - 0.25 * Temperature;
        var g = 1 - 0.2 * Tint;
        return string.Join(' ', new[]
        {
            Number(r), "0", "0", "0", "0",
            "0", Number(g), "0", "0", "0",
            "0", "0", Number(b), "0", "0",
            "0", "0", "0", "1", "0",
        });
    }

    /// <summary>
    /// The tone curve for highlights and shadows as an <c>feComponentTransfer</c> table: five
    /// samples of a curve that lifts (or drops) each end while leaving the middle alone.
    /// </summary>
    public string ToneTable()
    {
        var samples = new StringBuilder();
        for (var i = 0; i <= 4; i++)
        {
            var x = i / 4.0;
            // Weighted to the ends: shadows own the dark quarter, highlights the bright one.
            var shifted = x + Shadows * 0.35 * (1 - x) * (1 - x) + Highlights * 0.35 * x * x;
            if (i > 0) samples.Append(' ');
            samples.Append(Number(Math.Clamp(shifted, 0, 1)));
        }
        return samples.ToString();
    }

    /// <summary>
    /// Read a paint this type wrote back into a source and a fit. Adjustments do not live in the
    /// paint (they are a filter, a separate property), so a parsed fill comes back neutral - bind
    /// <c>Image</c> rather than the string to keep a grade.
    /// </summary>
    public static bool TryParse(string? text, out ImageFillValue image)
    {
        image = Empty;
        var trimmed = text?.Trim();
        if (trimmed is null || !trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase)) return false;

        var close = trimmed.IndexOf(')');
        if (close < 0) return false;

        var src = trimmed[4..close].Trim().Trim('\'', '"');
        if (src.Length == 0) return false;

        var rest = trimmed[(close + 1)..];
        var fit = rest switch
        {
            _ when rest.Contains("repeat", StringComparison.OrdinalIgnoreCase)
                && !rest.Contains("no-repeat", StringComparison.OrdinalIgnoreCase) => ImageFillFit.Tile,
            _ when rest.Contains("contain", StringComparison.OrdinalIgnoreCase) => ImageFillFit.Fit,
            _ when rest.Contains("cover", StringComparison.OrdinalIgnoreCase) => ImageFillFit.Fill,
            _ => ImageFillFit.Crop,
        };

        image = new(src, fit);
        return true;
    }

    private static string Number(double value) =>
        Math.Round(value, 4).ToString(CultureInfo.InvariantCulture);
}
