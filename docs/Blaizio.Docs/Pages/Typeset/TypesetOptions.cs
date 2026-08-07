using Blaizio.Cli.Core.Styling;

namespace Blaizio.Docs.Pages.Typeset;

/// <summary>
/// One full designer selection - every knob of the /typeset page. Font knobs hold FontCatalog
/// names ("default" = leave the site's face alone); the rest hold the raw CSS values the
/// wrapper's custom properties get.
/// </summary>
public sealed record TypesetSelection(
    string Heading = "default",
    string Body = "default",
    string Mono = "default",
    string Size = TypesetOptions.DefaultSize,
    string Leading = TypesetOptions.DefaultLeading,
    string Flow = TypesetOptions.DefaultFlow,
    string Measure = TypesetOptions.DefaultMeasure);

/// <summary>
/// The /typeset designer's option lists and the CSS each selection produces. Values are the raw
/// CSS the stylesheet knobs take, so the preview wrapper and the Get Code output can never drift.
/// </summary>
public static class TypesetOptions
{
    public const string DefaultSize = "15px";
    public const string DefaultLeading = "1.75";
    public const string DefaultFlow = "1.25em";
    public const string DefaultMeasure = "70ch";

    /// <summary>Line length. "full" = no cap, the wrapper fills its container.</summary>
    public static readonly (string Value, string Label)[] Measures =
    [
        ("60ch", "Narrow"),
        ("70ch", "Regular"),
        ("80ch", "Wide"),
        ("90ch", "Extra wide"),
        ("full", "Full width"),
    ];

    /// <summary>Base text size - everything else derives from it in em.</summary>
    public static readonly (string Value, string Label)[] Sizes =
    [
        ("13px", "13px"),
        ("14px", "14px"),
        ("15px", "15px"),
        ("16px", "16px"),
        ("17px", "17px"),
        ("18px", "18px"),
    ];

    /// <summary>Body line-height.</summary>
    public static readonly (string Value, string Label)[] Leadings =
    [
        ("1.5", "Tight"),
        ("1.625", "Snug"),
        ("1.75", "Regular"),
        ("1.9", "Relaxed"),
        ("2.1", "Loose"),
    ];

    /// <summary>Space between blocks - headings, lists and rules scale their own gaps from it.</summary>
    public static readonly (string Value, string Label)[] Flows =
    [
        ("0.75em", "Compact"),
        ("1em", "Snug"),
        ("1.25em", "Regular"),
        ("1.5em", "Relaxed"),
        ("2em", "Loose"),
    ];

    /// <summary>Heading and body share one pool: "Default" (the site's face) plus every offered
    /// sans and serif. Mono keeps its own pool below.</summary>
    public static readonly FontDefinition[] TextFonts =
    [
        FontCatalog.All[0],
        .. FontCatalog.All.Where(f => f is { Offered: true, Kind: FontKind.Sans }),
        .. FontCatalog.All.Where(f => f is { Offered: true, Kind: FontKind.Serif }),
    ];

    public static readonly FontDefinition[] MonoFonts =
    [
        FontCatalog.All[0],
        .. FontCatalog.All.Where(f => f is { Offered: true, Kind: FontKind.Mono }),
    ];

    public static FontDefinition Font(string name) =>
        FontCatalog.All.FirstOrDefault(f => f.Name == name) ?? FontCatalog.All[0];

    public static string Label((string Value, string Label)[] options, string value) =>
        options.FirstOrDefault(o => o.Value == value).Label ?? value;

    /// <summary>"Regular (1.75)" - the rail's value line for the scale knobs.</summary>
    public static string ValueLabel((string Value, string Label)[] options, string value)
    {
        var label = options.FirstOrDefault(o => o.Value == value).Label;
        if (label is null) return value;
        // Sizes label IS the value - don't render "15px (15px)".
        return label == value ? value : $"{label} ({value})";
    }

    /// <summary>
    /// The custom property declarations this selection needs on top of the stylesheet defaults.
    /// Font knobs at "default" emit nothing: the stylesheet already inherits the site's faces.
    /// The preview wrapper inlines raw stacks (the docs define no per-face variables); the Get
    /// Code output references the <c>--font-*</c> variables step 2 tells the consumer to create.
    /// </summary>
    private static List<(string Name, string Value)> Declarations(TypesetSelection s, bool asVars)
    {
        string Face(string name) => asVars ? $"var({FontVar(name)})" : Font(name).Stack;
        var vars = new List<(string, string)>();
        if (s.Heading != "default") vars.Add(("--typeset-font-heading", Face(s.Heading)));
        if (s.Body != "default") vars.Add(("--typeset-font-body", Face(s.Body)));
        if (s.Mono != "default") vars.Add(("--typeset-font-mono", Face(s.Mono)));
        vars.Add(("--typeset-size", s.Size));
        vars.Add(("--typeset-leading", s.Leading));
        vars.Add(("--typeset-flow", s.Flow));
        return vars;
    }

    /// <summary>The app-level custom property naming a catalogue face ("lora" = --font-lora).</summary>
    public static string FontVar(string name) => $"--font-{name}";

    /// <summary>The preview wrapper's inline style: the declarations plus the measure cap.</summary>
    public static string WrapperStyle(TypesetSelection s)
    {
        var style = string.Join(" ", Declarations(s, asVars: false).Select(v => $"{v.Item1}: {v.Item2};"));
        return s.Measure == "full" ? style : $"{style} max-inline-size: {s.Measure};";
    }

    /// <summary>The Get Code "your typeset" block: a preset class ready to paste after typeset.css.
    /// Faces are referenced through the <c>--font-*</c> variables <see cref="FontVarsCss"/> defines.</summary>
    public static string PresetCss(TypesetSelection s)
    {
        var lines = Declarations(s, asVars: true).Select(v => $"  {v.Item1}: {v.Item2};");
        return $".typeset-custom {{\n{string.Join("\n", lines)}\n}}";
    }

    /// <summary>
    /// The :root block naming each picked catalogue face as a <c>--font-*</c> variable with its
    /// full fallback stack - what the preset class references, so faces are declared once and
    /// reusable outside the typeset too. Null when the selection is all system faces.
    /// </summary>
    public static string? FontVarsCss(TypesetSelection s)
    {
        var names = new[] { s.Heading, s.Body, s.Mono }.Where(n => n != "default").Distinct().ToArray();
        if (names.Length == 0) return null;
        var lines = names.Select(n => $"  {FontVar(n)}: {Font(n).Stack};");
        return $":root {{\n{string.Join("\n", lines)}\n}}";
    }
}

/// <summary>
/// Serves the raw text of Styles/typeset.css (embedded by the csproj) - the Get Code dialog's
/// copy is byte-for-byte the stylesheet the docs themselves run.
/// </summary>
public static class TypesetCss
{
    private static string? _css;

    public static string Get()
    {
        if (_css is not null) return _css;
        using var stream = typeof(TypesetCss).Assembly.GetManifestResourceStream("Blaizio.Docs.Typeset.typeset.css")
            ?? throw new InvalidOperationException("Embedded typeset.css not found.");
        using var reader = new StreamReader(stream);
        return _css = reader.ReadToEnd();
    }
}
