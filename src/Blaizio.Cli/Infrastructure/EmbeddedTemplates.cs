using System.Reflection;
using Blaizio.Cli.Core.Templates;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// <see cref="ITemplateProvider"/> backed by template files embedded in the CLI. Each resource is
/// named <c>tpl.&lt;template&gt;.&lt;flat-name&gt;.tmpl</c>, where the flat name encodes the
/// destination path with <c>__</c> for directory separators and <c>~</c> for the file extension's
/// dot (e.g. <c>wwwroot__index~html</c> → <c>wwwroot/index.html</c>). The extension is encoded so no
/// template file carries a real code extension (<c>.cs</c>/<c>.razor</c>) that the SDK would claim.
/// </summary>
public sealed class EmbeddedTemplates : ITemplateProvider
{
    private static readonly Assembly Owner = typeof(EmbeddedTemplates).Assembly;

    /// <inheritdoc />
    public bool Has(string templateId) => ResourceNames(templateId).Any();

    /// <inheritdoc />
    public IReadOnlyList<TemplateFile> GetFiles(string templateId)
    {
        var prefix = Prefix(templateId);
        var files = new List<TemplateFile>();

        foreach (var resource in ResourceNames(templateId))
        {
            var flat = resource[prefix.Length..];                 // e.g. wwwroot__index~html.tmpl
            if (flat.EndsWith(".tmpl", StringComparison.Ordinal))
                flat = flat[..^".tmpl".Length];
            var relative = flat.Replace("__", "/").Replace('~', '.'); // wwwroot/index.html
            files.Add(new TemplateFile(relative, Read(resource)));
        }

        return files;
    }

    private static string Prefix(string templateId) => $"tpl.{templateId}.";

    private static IEnumerable<string> ResourceNames(string templateId)
    {
        var prefix = Prefix(templateId);
        return Owner.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string Read(string resource)
    {
        using var stream = Owner.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded template resource '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
