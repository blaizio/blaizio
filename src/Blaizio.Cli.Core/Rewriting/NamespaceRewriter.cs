using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Rewriting;

/// <summary>
/// Rewrites the source root namespace of copied component files to the consumer's configured
/// namespace. A plain token-boundary swap of the root prefix: <c>Blaizio.Ui.Button</c> becomes
/// <c>MyApp.Components.Ui.Button</c>, while <c>Blaizio.Base</c>/<c>Blaizio.Icons</c> (the NuGet
/// layers) are left untouched. Covers C# <c>namespace</c> declarations, Razor <c>@using</c>/
/// <c>@namespace</c> directives, and any fully-qualified references in between.
/// </summary>
public sealed partial class NamespaceRewriter
{
    /// <summary>The root namespace the registry ships components under.</summary>
    public const string SourceRoot = "Blaizio.Ui";

    private readonly Regex _rootToken;
    private readonly string _target;

    /// <param name="targetNamespace">Consumer namespace to write, e.g. <c>MyApp.Components.Ui</c>.</param>
    /// <param name="sourceRoot">Registry root prefix to replace; defaults to <see cref="SourceRoot"/>.</param>
    public NamespaceRewriter(string targetNamespace, string sourceRoot = SourceRoot)
    {
        // Escape '$' — the target is used as a Regex replacement string, where '$' has
        // substitution semantics.
        _target = targetNamespace.Replace("$", "$$");
        // \b ... (?=\.|\b) — match the whole root prefix at a token boundary, so a longer
        // identifier that merely starts with it (Blaizio.UiKit) is never rewritten.
        _rootToken = new Regex($@"\b{Regex.Escape(sourceRoot)}\b", RegexOptions.Compiled);
    }

    /// <summary>Return <paramref name="content"/> with the root namespace prefix rewritten.</summary>
    public string Rewrite(string content) => _rootToken.Replace(content, _target);
}
