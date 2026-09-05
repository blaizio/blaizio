using System.Text.Json.Serialization;

namespace Blaizio.Docs.Services;

/// <summary>One family of a set as <c>wwwroot/icons/index.json</c> lists it (the <c>BlaizioIconsJson</c> task).</summary>
/// <param name="Name">The nested class (<c>Outline</c>, <c>Regular</c>, <c>StrokeRounded</c>...).</param>
/// <param name="Kind">The paint model: <c>Outline</c> (stroked) or <c>Filled</c>.</param>
/// <param name="ViewBox">The family's grid.</param>
/// <param name="Stroke">The family's stroke width (outline paint only).</param>
/// <param name="Count">How many icons the family has.</param>
/// <param name="File">The family file under <c>wwwroot/icons/</c>; its bodies are read on the JS side.</param>
public sealed record IconFamilyInfo(string Name, string Kind, string ViewBox, float Stroke, int Count, string File);

/// <summary>One set in <c>index.json</c>: its generated class and its families.</summary>
public sealed record IconSetIndex(string Class, IconFamilyInfo[] Families);

/// <summary>Source-generated deserialization for the icon browser's index (reflection-free, trim-clean).</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IconSetIndex[]))]
internal sealed partial class IconJsonContext : JsonSerializerContext;
