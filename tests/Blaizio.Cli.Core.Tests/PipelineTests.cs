using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Styling.Pipelines;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class PackageManagerTests
{
    [Theory]
    [InlineData("pnpm-lock.yaml", PackageManager.Pnpm)]
    [InlineData("yarn.lock", PackageManager.Yarn)]
    [InlineData("bun.lockb", PackageManager.Bun)]
    public void Detects_from_lockfile(string lockfile, PackageManager expected)
    {
        using var dir = new TempDir();
        dir.Write(lockfile, "");
        Assert.Equal(expected, PackageManagers.Detect(dir.Path));
    }

    [Fact]
    public void Defaults_to_npm_without_a_lockfile()
    {
        using var dir = new TempDir();
        Assert.Equal(PackageManager.Npm, PackageManagers.Detect(dir.Path));
    }
}

public class TailwindBinaryTests
{
    [Fact]
    public void Asset_name_matches_the_current_platform_shape()
    {
        var asset = TailwindBinary.AssetName();
        Assert.StartsWith("tailwindcss-", asset);
        Assert.Equal(OperatingSystem.IsWindows(), asset.EndsWith(".exe"));
    }

    [Fact]
    public void Latest_url_points_at_the_releases_latest_download()
    {
        var url = TailwindBinary.DownloadUrl("latest", musl: false).ToString();
        Assert.Contains("/releases/latest/download/tailwindcss-", url);
    }

    [Fact]
    public void Pinned_version_is_normalized_with_a_v_prefix()
    {
        var url = TailwindBinary.DownloadUrl("4.1.11", musl: false).ToString();
        Assert.Contains("/releases/download/v4.1.11/", url);
    }
}

public class StandalonePipelineTests
{
    private static ProjectContext ProjectWithCsproj(TempDir dir)
    {
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"></Project>");
        return ProjectContext.Discover(dir.Path);
    }

    private static readonly TailwindPaths Paths = new("Styles/app.css", "wwwroot/app.css");

    [Fact]
    public async Task Writes_the_targets_file_and_imports_it_into_the_csproj()
    {
        using var dir = new TempDir();
        var project = ProjectWithCsproj(dir);

        var result = await new StandalonePipeline().SetupAsync(project, Paths);

        Assert.True(dir.Exists(".blaizio/Blaizio.Tailwind.targets"));
        Assert.Contains(".blaizio/Blaizio.Tailwind.targets", dir.Read("App.csproj"));
        Assert.Contains(".blaizio/Blaizio.Tailwind.targets", result.ChangedFiles);
    }

    [Fact]
    public async Task Import_is_added_only_once()
    {
        using var dir = new TempDir();
        var project = ProjectWithCsproj(dir);

        await new StandalonePipeline().SetupAsync(project, Paths);
        await new StandalonePipeline().SetupAsync(project, Paths);

        var imports = dir.Read("App.csproj").Split("Blaizio.Tailwind.targets").Length - 1;
        Assert.Equal(1, imports);
    }

    [Fact]
    public async Task Detects_present_after_setup()
    {
        using var dir = new TempDir();
        using var cache = new TempDir();
        var project = ProjectWithCsproj(dir);
        // Hermetic: an empty shared cache and a no-hit PATH probe, so a tailwindcss binary already
        // on THIS machine (real cache / PATH) can't turn the pre-setup assertion into Partial.
        var pipeline = new StandalonePipeline(cacheRoot: cache.Path, commandOnPath: _ => false);

        Assert.Equal(PipelinePresence.Absent, pipeline.Detect(project).Presence);
        await pipeline.SetupAsync(project, Paths);
        Assert.Equal(PipelinePresence.Present, pipeline.Detect(project).Presence);
    }

    [Fact]
    public async Task Targets_xml_has_no_double_hyphen_in_comments()
    {
        // XML comments cannot contain '--'; a regression here breaks every consumer build.
        using var dir = new TempDir();
        var project = ProjectWithCsproj(dir);
        await new StandalonePipeline().SetupAsync(project, Paths);

        var targets = dir.Read(".blaizio/Blaizio.Tailwind.targets");
        foreach (var comment in ExtractComments(targets))
            Assert.DoesNotContain("--", comment);
    }

    private static IEnumerable<string> ExtractComments(string xml)
    {
        var i = 0;
        while ((i = xml.IndexOf("<!--", i, StringComparison.Ordinal)) >= 0)
        {
            var end = xml.IndexOf("-->", i + 4, StringComparison.Ordinal);
            if (end < 0) yield break;
            yield return xml[(i + 4)..end];
            i = end + 3;
        }
    }
}

public class NodePipelineTests
{
    private static readonly TailwindPaths Paths = new("Styles/app.css", "wwwroot/app.css");

    [Fact]
    public async Task Adds_dev_dependency_and_scripts_to_package_json()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var project = ProjectContext.Discover(dir.Path);

        await new NodePipeline().SetupAsync(project, Paths);
        var json = dir.Read("package.json");

        Assert.Contains("@tailwindcss/cli", json);
        Assert.Contains("\"css\"", json);
        Assert.Contains("\"css:watch\"", json);
    }

    [Fact]
    public void Detect_reports_present_when_tailwind_is_in_package_json()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        dir.Write("package.json", "{ \"devDependencies\": { \"tailwindcss\": \"^4\" } }");
        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal(PipelinePresence.Present, new NodePipeline().Detect(project).Presence);
    }

    [Fact]
    public void Detect_reports_partial_when_package_json_lacks_tailwind()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        dir.Write("package.json", "{ \"name\": \"x\" }");
        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal(PipelinePresence.Partial, new NodePipeline().Detect(project).Presence);
    }
}

public class TailwindPipelineRegistryTests
{
    private readonly TailwindPipelineRegistry _registry = new();

    [Fact]
    public void Recommends_standalone_for_an_empty_project()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal("standalone", _registry.Recommend(project).Id);
    }

    [Fact]
    public void Recommends_an_existing_node_pipeline_over_standalone()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        dir.Write("package.json", "{ \"devDependencies\": { \"tailwindcss\": \"^4\" } }");
        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal("node", _registry.Recommend(project).Id);
    }

    [Fact]
    public void Prefers_a_present_bundler_over_node()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        dir.Write("vite.config.ts", "export default {}");
        dir.Write("package.json", "{ \"devDependencies\": { \"@tailwindcss/vite\": \"^4\", \"tailwindcss\": \"^4\" } }");
        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal("vite", _registry.Recommend(project).Id);
    }

    [Fact]
    public void Recommends_a_present_rollup_setup()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        dir.Write("rollup.config.mjs", "export default {}");
        dir.Write("package.json", "{ \"devDependencies\": { \"@tailwindcss/postcss\": \"^4\", \"tailwindcss\": \"^4\" } }");
        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal("rollup", _registry.Recommend(project).Id);
    }

    [Fact]
    public void Rollup_config_without_the_plugin_detects_partial()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        dir.Write("rollup.config.js", "export default {}");
        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal(PipelinePresence.Partial, new RollupPipeline().Detect(project).Presence);
    }

    [Fact]
    public void Recommends_a_partial_bundler_over_standalone()
    {
        // A rollup config without its Tailwind plugin yet: the project owns that bundler, so auto
        // must surface it (with its manual step) instead of silently wiring standalone over it.
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        dir.Write("rollup.config.js", "export default {}");
        var project = ProjectContext.Discover(dir.Path);

        var recommended = _registry.Recommend(project);
        Assert.Equal("rollup", recommended.Id);
        Assert.False(recommended.CanSetup);
    }

    [Fact]
    public void Resolves_by_id_case_insensitively_and_returns_null_for_unknown()
    {
        Assert.Equal("none", _registry.Resolve("NONE")!.Id);
        Assert.Null(_registry.Resolve("does-not-exist"));
    }
}
