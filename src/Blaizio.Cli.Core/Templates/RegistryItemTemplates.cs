using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core.Templates;

/// <summary>
/// <see cref="ITemplateProvider"/> backed by a fetched <c>registry:template</c> item, so a
/// registry-hosted template scaffolds through the exact engine the built-in ones use - same
/// token substitution (<c>{{RootNamespace}}</c>, <c>{{ComponentNamespace}}</c>,
/// <c>{{ProjectName}}</c>, <c>{{Skin}}</c>), same skip-existing semantics, same containment
/// (<see cref="TemplateScaffolder"/> resolves every path strictly inside the project).
/// </summary>
public sealed class RegistryItemTemplates(RegistryItem item) : ITemplateProvider
{
    /// <inheritdoc />
    /// <remarks>The template id is implicit - this provider carries exactly one item.</remarks>
    public bool Has(string templateId) => item.Files.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<TemplateFile> GetFiles(string templateId) =>
        [.. item.Files.Select(file => new TemplateFile(
            Destination(file),
            file.Content ?? throw new InvalidOperationException(
                $"Template '{item.Name}' file '{file.Path}' has no content; the registry item is not resolved.")))];

    /// <summary>
    /// Where a template file lands, relative to the new project's root: its <c>target</c> when
    /// given, else its <c>path</c> as-is - a template's layout IS the project layout. A <c>~/</c>
    /// prefix is accepted and redundant here.
    /// </summary>
    private static string Destination(RegistryFile file)
    {
        var destination = string.IsNullOrEmpty(file.Target) ? file.Path : file.Target;
        return destination.StartsWith("~/", StringComparison.Ordinal)
            ? destination[2..]
            : destination;
    }
}
