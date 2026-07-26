using System.Text;

namespace Blaizio.Ui;

/// <summary>
/// The picker's drag cursors - the same open/closed hand artwork the rest of the design system
/// uses for a grab gesture, rather than the browser's <c>grab</c>/<c>grabbing</c> defaults.
/// <see cref="BzColorPicker"/> publishes them as custom properties on its root, and the sheets
/// read them (<c>cursor:var(--bz-color-cursor-stop),grab</c>) - keeping the long data URI out of
/// the stylesheet, where the registry inliner would have to carry it through class strings.
/// </summary>
/// <remarks>
/// The art is a 48x48 RGBA bitmap drawn down to <see cref="Size"/> by the SVG wrapper, which is
/// what fixes the hotspot at the middle of the palm. Mersal's stylesheet carries the same pair for
/// its pan tool and reorder handles; keep the two in step. It replaced a pair built from the Tabler
/// outline hands: those
/// were vector glyphs filled white and outlined black at 26px, and at cursor size the doubled
/// stroke closed up the gaps between the fingers, so the hand read as a blob.
/// </remarks>
internal static class ColorCursors
{
    /// <summary>Custom property carrying the at-rest cursor.</summary>
    public const string StopVariable = "--bz-color-cursor-stop";

    /// <summary>Custom property carrying the dragging cursor.</summary>
    public const string GrabVariable = "--bz-color-cursor-grab";

    /// <summary>The open hand image, for a draggable surface at rest.</summary>
    public static string StopImage { get; } = Image(OpenHandPng);

    /// <summary>The closed hand image, for a surface being dragged.</summary>
    public static string GrabImage { get; } = Image(ClosedHandPng);

    /// <summary>The hotspot, in image pixels - the middle of the palm at <see cref="Size"/>.</summary>
    public const int Hotspot = 19;

    /// <summary>
    /// Both cursors as a style declaration, for the picker root to cascade. Chromium ignores an
    /// SVG cursor, so ts/cursors.ts overwrites these with rasterised PNGs on first render; this is
    /// what shows in the meantime, and what engines that DO render SVG cursors keep using if the
    /// script never runs. The rasteriser reads the wrapper's intrinsic size, so both paths draw
    /// the hand at the same size.
    /// </summary>
    public static string Variables { get; } =
        $"{StopVariable}:url({StopImage}) {Hotspot} {Hotspot};" +
        $"{GrabVariable}:url({GrabImage}) {Hotspot} {Hotspot}";

    // On-screen size. The bitmaps below are 48px so they stay crisp on a HiDPI screen. Keep
    // Hotspot at the palm if this changes - it is expressed in these same pixels.
    private const int Size = 40;

    /// <summary>
    /// One bitmap wrapped in an SVG that pins its size, base64'd whole. Base64 (rather than
    /// percent-encoding the markup) keeps the URI free of spaces, quotes and <c>#</c>, so it needs
    /// no quoting inside <c>url()</c> and survives being carried in a style attribute.
    /// </summary>
    private static string Image(string png)
    {
        var svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            $"width=\"{Size}px\" height=\"{Size}px\">" +
            $"<image xlink:href=\"data:image/png;base64,{png}\" width=\"{Size}\" height=\"{Size}\"/>" +
            "</svg>";
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }

    // The hand bitmaps, 48x48 RGBA.
    private const string OpenHandPng =
        "iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAAAAXNSR0IArs4c6QAABXpJREFUaEPtmF1IZGUcxh+3zGzaND+KbdQps7XNsS2vchMGq4v0OLIXKSWIo9gHJlgXXQSBelFQQUgkXQg6liKCEMaZubULNboQTJ3ddlw1R62txjHbdO1js/PY+8JZc+t8zFAL88KLo8455/n9v9+Tght8pdzg+pEE+K89mPRA0gM2LZAMIZsGtH150gNHmJBGOXbo738A2Ldt7us8LF73peibxL5ZB0Hxv4vNz9xxW3ZDSFqbwlMB3CL2OwAUABEAnwF4G8AvAH4F8JuAuBoPCjsAvJbCKToNwDMA7gJAYZ0ej+dcNBo9FgqFHgHwAoASAEEAXwgQwtiGsANA8RTuAPApgMekRdPS0jb29vacCwsL0dLS0pxDlu7TvPE6gF0BYgvCDgAtT/EeAJ8MDw9fLCgocHg8nvTMzMztzc1NF4WnpKQgPz9/MRKJPNDQ0DA7MjKSC+AJADEAV3QQlnLDKgCvSweQwXAB8OL+/l9FJjs7e21/fz8lFovlSQBFUcKqqp4cGhpaaWxsvA/ASyKkxgDMivyQuWEqNawCsOLQ+llaDrwFoEECZGVlbVBBLBZzSoDq6uqlQCBw/+DgYMTn8xXoFP6kVac3AHwkvEEIU+XWKgDj/3YADId3NSuePQRAD9xzBMCaz+fLr6+vPz86OnrK5XItRCKRMICXARBmz2yZtQrAOn9cVJ0eAE8b9MC6z+fL8/v9a01NTfler3deVdVSzQOFIid2RL8wHEZWAZjAdwiADwBU6gC+ESH0Nw8MDAxstLS0OP1+/3pTU1Oe1+sNqarK8vqg5oUfAFwWfSIhAPqmdasAYAj1Aij/J4CqqqrlYDBYKAH6+/s3mpubnV6v97yqqqcAuAF8L8KIDc/wMuqBw02rHgBdvwrgOQCPSoCJiYkon15ZWXlQ/7u7u1c7OzsPSiqXoigHCc3POoCHAXwHYFtUpLgDyKZ1GwA2orO6J/DBd0sAw0++FuC0Vlov6RLZ8G2MeoBzDssm55uh2traL8fHx0+3t7ev9vb2Hlj3/w7AmGfVeZVjwPz8fNTtdh+EyNjYWKyuri7LCkBNTc1XgUCACZxwD7Drsuo8CWC4vLz83PT09EPSz5OTk3sVFRWENLV0AAnPAVl1aHXmwBmXy7W8uLh4b2pq6uHDi2EIXRKzIDCX2MwSUoVkDtwJgBCvcHxwOp1fz87OnsjJyeFUanoVFRWFl5aWaAAWBQnAMdvwMprEsgpxfMgUEBzIGh0Ox7dzc3PHCwsL+T9Ty+FwbOzu7n4u5qGENjLZBxhKEoKDnA/A8zyYzMzMXC4rKyOcoRUKhX50u93MLc5SfgDsHz+bPeQY9QBF8bucgRguhOAoTYhK7Tj5Jr/Q19e33traejBG/9sqLi4Oh8Nh9pUGACsAtsREaupcYAZAQuhPYoTI1rRzdOZsj46OjuWenh4OZ9ddiqJcCAaDxQBeA6AC2BQJzPhP+DitHyvY3KQnyrSO/CFVd3V1rbS1tZ3Izc29prROTU1tKopyZXt7m176WCvN7wnxHCF4xOTbC1PLrAf0N5cHekKwR7BCnQHwrPh5NSMj41JJScnu1tbWsUgkkr6zs0NvXdRC731tkpgRIzTFc4w2bX0ZEqaID31ZQjCW2akJQo88DuApACc5J4kSeQFASIQME5bCOT7T8qz9lg73djwgWeQ7IYYLvSE3f2f/YJ1nYvK4SKG0NjeF81DPv5sOHfnweADwXhTJCsWDDoWzUnHLN3RMTAplmPDYKF9yUbgly8cbQIajBKFwKV4aiULlK0Z+5jZVcY6K9Xh5QH9v3lNu/ZxEsfIlr23hifCAnWJg+dpEeMCyGCsXJgGsWC2e1yQ9EE9rWrlX0gNWrBbPa/4EtK/NQLDpD/IAAAAASUVORK5CYII=";

    private const string ClosedHandPng =
        "iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAAAAXNSR0IArs4c6QAABMpJREFUaEPtmM9rI2UYx7+JxjZpdm2nrGutW0y3SdUSlx6WIiiIiGCbVPDSQ8HGCl3EW5EehFroxfawoOxBKFYaxL/ATC568qCtBa2u6G4nhrLG2NpOYjYkNevWON/1fWWoCDOZpLIwD7ykpJn3fT7P73c8uMvFc5frDxfg//ag6wHXAw4t4IaQQwM6ftz1gGMTOtzA9YBDAzp+3PWAYxM63KDZHuB+94gl9/4TwBEAftYd6vuvx5sBwD28YvkAcPUDeBpACcBnAHQAfwC4LWAI0hQYpwBU/F6x2gBwnQPwpclUXwF4CcDvAGoAbgkIwjj2ihMAKk9rtwN4DcCLAJ4AkPV6vQ+k0+nbuq5XJycnBwBcB9AH4HMDcBXAJwLE7BXC2BYnAFL58wA+HhgYKHd3d5c3NjYuKIryi67rj1Abj8cDRVGy/f39tYODg8OdnZ0ggHcAhI31EQBNeIYwtiGcANDypwBMG15Yymaz+6FQ6ExHR8dee3v7LV3XGUp3AEZGRq6ur69HFxcXdxYWFu6ACdkD8C6A9wBURZ7Yyo1GAfhcwLBkpxHP8wAu1et/n6soSo56FwqFXgkwOjqqqaoaTiaTNxKJRN/09PQ3q6urF8Lh8LVMJnNThF9Z5AkrlmVpFIClkqHQbcTymwBeNQH8zNOPAfyoqur5ZDKZSyQSD6+trf00NTV1Lh6PX02lUlFRtQoAKqJStRSAyXsfgNMAzhiunzMS92UJ0NPTcyMYDFY1TXtUemBubk5bXl4Oa5pWjkQip7LZ7M1QKHQ6Fot9r6rq4wAeA7APgF5glbIsdj0gK48fwDMAEsJ6UQlg+WQA8Xj8h1QqReXpBeYDw4ml1rLYBWDlofIXAXwKYLutrQ21Wi3iEIDllwBsfC0DICwbFSvPFZ/P92SpVAr6/X5lZmZGW1lZYVm0JbFY7Jqqqgy1EwOg9e83IC53dXVdLBQKbFINiymECLDb6hCiBwjA0vkCgPc7OzvzxWLxoUYJBgcHM9vb2xxFYqYcaFkSyxBi9WH5fJ4d1e/371Wr1bONQASDwXylUvkawBsAfhVViB3ZsjSSxLKBEeIpdlKv11s6OjpiaFmWra2t/eHhYRrjsjEnfQDg4CT6gOwBHSKUCPGskdxvs37X63X2B0vS29v7bT6f528vAWD3LgI4tDsP2fUAD2QXpqJmiFHj4Ld8Pp++tLRUmZ2d5eT5nzI2NnY9nU4PAngFwBfiviCb2InMQhKC4wRDRwHwnLBm38TERGZ+fv7s0NAQS+4/srm5WRofHz/c3d19EMCHInx42WH9p/V5R7AljXhAHkAI9gXmBCFYnULGSPC66NIIBAJ70Wi0XCwWkcvlAtVqlSFHha+IOwHnn9/EJMrqY8v6VMQJgDmcCEFvMCm5WGY5IkQMS/O+wC77HTu36OCs+VScYcMBTt7SbFm/GQASQl5uCMLc4OJ9gd8z8WlZlkeOCZz7qTQ/GTb8ztYIbaZ06gG5l7wbM7kZVlSefxOAZxCA8U1LU2F5NyZUw8o3ywMSQr6dkJd88+sVAvC6SAj5ZoKK24754zHWLA8c9yr3NS/+X76BaNorlWZ7wHYCNuOBVnigGXpZ3sMFsGyqFv3Q9UCLDGt5W9cDlk3Voh/+BSxHakBpki17AAAAAElFTkSuQmCC";
}
