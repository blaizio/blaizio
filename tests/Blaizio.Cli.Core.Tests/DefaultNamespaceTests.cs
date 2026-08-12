using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Blaizio.Cli.Core.Writing;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// <c>@default/item</c> - the escape hatch a third-party registry uses to depend on components
/// the consumer installs from THEIR registry. Without it a plain dependency name inside a
/// namespaced item is claimed by that item's own registry, which is right for a self-contained
/// registry and wrong for one whose components build on the base set (an editor that needs
/// <c>toolbar</c> means the project's toolbar, not a copy it would have to ship).
/// </summary>
public class DefaultNamespaceTests
{
    private static RegistryItem Item(string name, params string[] deps) =>
        new() { Name = name, RegistryDependencies = deps, NugetDependencies = [] };

    /// <summary>A project registry with the base components, plus one third-party registry.</summary>
    private static IRegistryClient Composite(params string[] editorDeps)
    {
        var fallback = new FakeRegistryClient()
            .Add(Item("toolbar", "utils"))
            .Add(Item("select"))
            .Add(Item("utils"));
        var editor = new FakeRegistryClient().Add(Item("editor", editorDeps));
        return new NamespacedRegistryClient(fallback, new Dictionary<string, IRegistryClient> { ["@editor"] = editor });
    }

    [Fact]
    public async Task A_default_dependency_resolves_against_the_project_registry()
    {
        var graph = await new DependencyResolver(Composite("@default/toolbar", "@default/select"))
            .ResolveAsync(["@editor/editor"]);

        // Dependencies land as ordinary items of the default registry - and their own plain deps
        // (toolbar -> utils) keep resolving there too.
        Assert.Equal(["utils", "toolbar", "select", "@editor/editor"], graph.Items.Select(i => i.QualifiedName));
    }

    [Fact]
    public async Task A_default_dependency_is_not_stamped_with_a_namespace()
    {
        var graph = await new DependencyResolver(Composite("@default/toolbar")).ResolveAsync(["@editor/editor"]);

        // No SourceNamespace means the ordinary output folder, namespace and install record: the
        // component is the project's own copy, not the editor registry's.
        var toolbar = graph.Items.Single(i => i.Name == "toolbar");
        Assert.Null(toolbar.SourceNamespace);
        Assert.Null(ComponentWriter.FolderFor(toolbar.SourceNamespace));
        Assert.Equal("@editor", graph.Items.Single(i => i.Name == "editor").SourceNamespace);
    }

    [Fact]
    public async Task A_plain_dependency_still_belongs_to_the_items_own_registry()
    {
        // The behaviour @default exists to opt out of: unqualified names stay namespace-local.
        var client = Composite("toolbar");

        var failure = await Assert.ThrowsAsync<RegistryException>(
            () => new DependencyResolver(client).ResolveAsync(["@editor/editor"]));

        Assert.Contains("toolbar", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_rewritten_dependency_names_the_rewrite_and_the_way_out()
    {
        // The address that failed is the author's OWN registry, for a file they never wrote. The
        // failure has to carry what the address cannot: which dependency, of which item, and the
        // spelling that reaches the consumer's registry instead.
        var failure = await Assert.ThrowsAsync<RegistryException>(
            () => new DependencyResolver(Composite("toolbar")).ResolveAsync(["@editor/editor"]));

        Assert.Equal(RegistryFailure.NotFound, failure.Reason);
        Assert.Contains("'toolbar' is a dependency of '@editor/editor'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("@editor", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'@default/toolbar'", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_item_the_resolver_did_not_rewrite_is_left_alone()
    {
        // Nothing was rewritten, so there is nothing to explain: the plain failure stands.
        var failure = await Assert.ThrowsAsync<RegistryException>(
            () => new DependencyResolver(Composite()).ResolveAsync(["missing"]));

        Assert.DoesNotContain("@default", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_reserved_namespace_works_from_the_command_line_too()
    {
        var graph = await new DependencyResolver(Composite()).ResolveAsync(["@default/select"]);

        Assert.Equal(["select"], graph.Items.Select(i => i.QualifiedName));
    }

    [Fact]
    public void The_reserved_namespace_is_matched_case_insensitively()
    {
        Assert.True(NamespacedRegistryClient.IsDefaultNamespace("@default"));
        Assert.True(NamespacedRegistryClient.IsDefaultNamespace("@Default"));
        Assert.False(NamespacedRegistryClient.IsDefaultNamespace("@defaults"));
        Assert.False(NamespacedRegistryClient.IsDefaultNamespace("@acme"));
    }

    [Fact]
    public void For_returns_the_default_registry_without_a_recorded_entry()
    {
        var fallback = new FakeRegistryClient().Add(Item("toolbar"));
        var client = new NamespacedRegistryClient(fallback, new Dictionary<string, IRegistryClient>());

        Assert.Same(fallback, client.For(NamespacedRegistryClient.DefaultNamespace));
    }
}
