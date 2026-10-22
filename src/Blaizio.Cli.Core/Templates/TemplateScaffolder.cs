namespace Blaizio.Cli.Core.Templates;

/// <summary>Tokens substituted into template files as they are written.</summary>
/// <param name="RootNamespace">The app's root namespace.</param>
/// <param name="ComponentNamespace">The namespace copied components live in.</param>
/// <param name="ProjectName">The project/assembly name.</param>
/// <param name="Style">The active component style.</param>
public sealed record TemplateTokens(
    string RootNamespace,
    string ComponentNamespace,
    string ProjectName,
    string Style)
{
    /// <summary>Obsolete: use <see cref="Style"/>.</summary>
    [Obsolete("Use Style. \"Skin\" was renamed to \"style\".")]
    public string Skin
    {
        get => Style;
        init => Style = value;
    }
}

/// <summary>The outcome of scaffolding a template.</summary>
public sealed record ScaffoldResult(IReadOnlyList<string> Written, IReadOnlyList<string> Skipped);

/// <summary>
/// Writes a template's files into a project, substituting tokens (<c>{{RootNamespace}}</c>,
/// <c>{{ComponentNamespace}}</c>, <c>{{ProjectName}}</c>, <c>{{Style}}</c>; the older <c>{{Skin}}</c> spelling still works). Existing files are left
/// untouched unless overwrite is requested, so re-running never clobbers user edits.
/// </summary>
public sealed class TemplateScaffolder(ITemplateProvider provider)
{
    /// <summary>Scaffold <paramref name="templateId"/> into <paramref name="projectDir"/>.</summary>
    public async Task<ScaffoldResult> ScaffoldAsync(
        string projectDir,
        string templateId,
        TemplateTokens tokens,
        bool overwrite = false,
        CancellationToken ct = default)
    {
        var written = new List<string>();
        var skipped = new List<string>();

        foreach (var file in provider.GetFiles(templateId))
        {
            var relative = file.RelativePath.Replace('\\', '/');
            var absolute = SafePath.Resolve(projectDir, relative);

            if (File.Exists(absolute) && !overwrite)
            {
                skipped.Add(relative);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllTextAsync(absolute, Substitute(file.Content, tokens), ct);
            written.Add(relative);
        }

        return new ScaffoldResult(written, skipped);
    }

    private static string Substitute(string content, TemplateTokens t) => content
        .Replace("{{RootNamespace}}", t.RootNamespace)
        .Replace("{{ComponentNamespace}}", t.ComponentNamespace)
        .Replace("{{ProjectName}}", t.ProjectName)
        .Replace("{{Style}}", t.Style)
        // The pre-rename spelling: templates published before "skin" became "style" keep working.
        .Replace("{{Skin}}", t.Style);
}
