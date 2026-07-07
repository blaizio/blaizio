using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Templates;

namespace Blaizio.Cli.Core.Tests;

/// <summary>A throwaway directory that deletes itself on dispose. Used for filesystem-touching tests.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        // Random name via Guid isn't available (Guid.NewGuid is fine — only the workflow runtime
        // bans Date/Random). Use a per-instance unique folder under the OS temp root.
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "blaizio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Absolute path to the directory.</summary>
    public string Path { get; }

    /// <summary>Combine a relative path under the temp dir.</summary>
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Read a file under the temp dir.</summary>
    public string Read(string relative) => File.ReadAllText(Combine(relative));

    /// <summary>True when a file exists under the temp dir.</summary>
    public bool Exists(string relative) => File.Exists(Combine(relative));

    /// <summary>Write a file under the temp dir, creating parent folders.</summary>
    public void Write(string relative, string content)
    {
        var full = Combine(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }
}

/// <summary>An in-memory <see cref="IRegistryClient"/> for resolver/add tests. No network or disk.</summary>
public sealed class FakeRegistryClient : IRegistryClient
{
    private readonly Dictionary<string, RegistryItem> _items = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Register an item, returning this for chaining.</summary>
    public FakeRegistryClient Add(RegistryItem item)
    {
        _items[item.Name] = item;
        return this;
    }

    /// <summary>How many times <see cref="GetItemAsync"/> was called (to assert caching).</summary>
    public int FetchCount { get; private set; }

    public Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default)
        => Task.FromResult(new RegistryIndex { Name = "test", Items = [.. _items.Values] });

    public Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default)
    {
        FetchCount++;
        return _items.TryGetValue(nameOrUrlOrPath, out var item)
            ? Task.FromResult(item)
            : throw new RegistryException($"no such item '{nameOrUrlOrPath}'");
    }
}

/// <summary>A stub <see cref="ICssAssetProvider"/> returning marker content for TailwindSetup tests.</summary>
public sealed class FakeCssAssets : ICssAssetProvider
{
    public string GetThemeCss() => "/* theme */";
    public string GetAnimateCss() => "/* animate */";
    public string GetBaseCss() => "/* base */";
    public string GetSkinCss(string skin) => $"/* skin:{skin} */";
    public IReadOnlyList<string> AvailableSkins { get; } = ["ember", "spark"];
}

/// <summary>An in-memory <see cref="ITemplateProvider"/> for scaffolder tests.</summary>
public sealed class FakeTemplateProvider(params TemplateFile[] files) : ITemplateProvider
{
    public bool Has(string templateId) => files.Length > 0;
    public IReadOnlyList<TemplateFile> GetFiles(string templateId) => files;
}
