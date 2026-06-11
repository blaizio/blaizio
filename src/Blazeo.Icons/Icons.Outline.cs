namespace Blazeo;

/// <summary>
/// Tabler icon path data. Each value is the inner SVG content plus a trailing family marker
/// (<c>#0</c> outline, <c>#1</c> filled) consumed by <see cref="BlazeIcon"/>.
/// </summary>
/// <remarks>
/// This is a curated starter set covering the common icons. Run
/// <c>scripts/Update-BlazeIcons.ps1</c> to (re)generate the full Tabler collection into this file.
/// </remarks>
public partial class Icons
{
    /// <summary>Outline (stroked) Tabler icons.</summary>
    public static class Outline
    {
        public const string AlertTriangle = """<path d="M12 9v4" /><path d="M10.363 3.591l-8.106 13.534a1.914 1.914 0 0 0 1.636 2.871h16.214a1.914 1.914 0 0 0 1.636 -2.87l-8.106 -13.536a1.914 1.914 0 0 0 -3.274 0" /><path d="M12 16h.01" />#0""";
        public const string ArrowLeft = """<path d="M5 12l14 0" /><path d="M5 12l6 6" /><path d="M5 12l6 -6" />#0""";
        public const string ArrowRight = """<path d="M5 12l14 0" /><path d="M13 18l6 -6" /><path d="M13 6l6 6" />#0""";
        public const string Bell = """<path d="M10 5a2 2 0 1 1 4 0a7 7 0 0 1 4 6v3a4 4 0 0 0 2 3h-16a4 4 0 0 0 2 -3v-3a7 7 0 0 1 4 -6" /><path d="M9 17v1a3 3 0 0 0 6 0v-1" />#0""";
        public const string Calendar = """<path d="M4 7a2 2 0 0 1 2 -2h12a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-12" /><path d="M16 3v4" /><path d="M8 3v4" /><path d="M4 11h16" /><path d="M11 15h1" /><path d="M12 15v3" />#0""";
        public const string Check = """<path d="M5 12l5 5l10 -10" />#0""";
        public const string ChevronDown = """<path d="M6 9l6 6l6 -6" />#0""";
        public const string ChevronRight = """<path d="M9 6l6 6l-6 6" />#0""";
        public const string CircleCheck = """<path d="M3 12a9 9 0 1 0 18 0a9 9 0 1 0 -18 0" /><path d="M9 12l2 2l4 -4" />#0""";
        public const string Copy = """<path d="M7 9.667a2.667 2.667 0 0 1 2.667 -2.667h8.666a2.667 2.667 0 0 1 2.667 2.667v8.666a2.667 2.667 0 0 1 -2.667 2.667h-8.666a2.667 2.667 0 0 1 -2.667 -2.667l0 -8.666" /><path d="M4.012 16.737a2.005 2.005 0 0 1 -1.012 -1.737v-10c0 -1.1 .9 -2 2 -2h10c.75 0 1.158 .385 1.5 1" />#0""";
        public const string Download = """<path d="M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2v-2" /><path d="M7 11l5 5l5 -5" /><path d="M12 4l0 12" />#0""";
        public const string Heart = """<path d="M19.5 12.572l-7.5 7.428l-7.5 -7.428a5 5 0 1 1 7.5 -6.566a5 5 0 1 1 7.5 6.572" />#0""";
        public const string InfoCircle = """<path d="M3 12a9 9 0 1 0 18 0a9 9 0 0 0 -18 0" /><path d="M12 9h.01" /><path d="M11 12h1v4h1" />#0""";
        public const string Loader2 = """<path d="M12 3a9 9 0 1 0 9 9" />#0""";
        public const string Mail = """<path d="M3 7a2 2 0 0 1 2 -2h14a2 2 0 0 1 2 2v10a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-10" /><path d="M3 7l9 6l9 -6" />#0""";
        public const string Moon = """<path d="M12 3c.132 0 .263 0 .393 0a7.5 7.5 0 0 0 7.92 12.446a9 9 0 1 1 -8.313 -12.454l0 .008" />#0""";
        public const string Plus = """<path d="M12 5l0 14" /><path d="M5 12l14 0" />#0""";
        public const string Search = """<path d="M3 10a7 7 0 1 0 14 0a7 7 0 1 0 -14 0" /><path d="M21 21l-6 -6" />#0""";
        public const string Star = """<path d="M12 17.75l-6.172 3.245l1.179 -6.873l-5 -4.867l6.9 -1l3.086 -6.253l3.086 6.253l6.9 1l-5 4.867l1.179 6.873l-6.158 -3.245" />#0""";
        public const string Sun = """<path d="M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0" /><path d="M3 12h1m8 -9v1m8 8h1m-9 8v1m-6.4 -15.4l.7 .7m12.1 -.7l-.7 .7m0 11.4l.7 .7m-12.1 -.7l-.7 .7" />#0""";
        public const string Trash = """<path d="M4 7l16 0" /><path d="M10 11l0 6" /><path d="M14 11l0 6" /><path d="M5 7l1 12a2 2 0 0 0 2 2h8a2 2 0 0 0 2 -2l1 -12" /><path d="M9 7v-3a1 1 0 0 1 1 -1h4a1 1 0 0 1 1 1v3" />#0""";
        public const string User = """<path d="M8 7a4 4 0 1 0 8 0a4 4 0 0 0 -8 0" /><path d="M6 21v-2a4 4 0 0 1 4 -4h4a4 4 0 0 1 4 4v2" />#0""";
        public const string X = """<path d="M18 6l-12 12" /><path d="M6 6l12 12" />#0""";
    }
}
