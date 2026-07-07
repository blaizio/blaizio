namespace Blaizio.Cli.Core.Templates;

/// <summary>One file belonging to a project template.</summary>
/// <param name="RelativePath">Destination path relative to the project root (POSIX-style).</param>
/// <param name="Content">File contents, with template tokens still to be substituted.</param>
public sealed record TemplateFile(string RelativePath, string Content);

/// <summary>Supplies the files for a named project template (e.g. the Showcase app).</summary>
public interface ITemplateProvider
{
    /// <summary>Whether a template with this id exists.</summary>
    bool Has(string templateId);

    /// <summary>The files for a template. Empty when the template has no scaffold.</summary>
    IReadOnlyList<TemplateFile> GetFiles(string templateId);
}
