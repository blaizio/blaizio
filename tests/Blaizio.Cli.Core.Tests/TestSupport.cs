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
    // Mirrors the real theme.css shape enough for the v3 token block: the dark variant, the
    // @theme inline map (whose --font-heading/--radius-lg entries must never be rewritten by a
    // :root-scoped patch), exact-name declarations in :root/.dark, and the base layer.
    public string GetThemeCss() =>
        """
        /* maintainer comment - stripped from the composed block */
        @custom-variant dark (&:is(.dark *));

        @theme inline {
          --font-heading: var(--font-heading);
          --radius-lg: var(--radius);
        }

        :root {
          --font-heading: var(--font-sans, ui-sans-serif, system-ui, sans-serif);
          --radius: 0.75rem;
          --background: oklch(1 0 0);
          --primary: oklch(0.55 0.22 304);
          --chart-1: oklch(0.63 0.23 304);
          --chart-2: oklch(0.62 0.19 275);
          --chart-3: oklch(0.65 0.2 350);
          --chart-4: oklch(0.65 0.15 245);
          --chart-5: oklch(0.7 0.13 195);
        }

        .dark {
          --background: oklch(0.17 0 0);
          --primary: oklch(0.61 0.2 304);
          --primary-button: oklch(0.55 0.21 304);
        }

        @layer base {
          * {
            border-color: var(--color-border);
          }
          body {
            background-color: var(--color-background);
            color: var(--color-foreground);
          }
        }
        """;
    public string GetAnimateCss() => "/* animate */";
    public string GetBaseCss() => "/* base */";
    public string GetSharedSkinCss() => "/* shared */";
    public string GetSkinCss(string skin) => $"/* skin:{skin} */";
    public IReadOnlyList<string> AvailableSkins { get; } = ["ember", "spark"];
    public string GetPresetCss(string preset) => preset switch
    {
        "comet" =>
            """
            .preset-comet {
              --background: oklch(0.99 0.004 195);
              --primary: oklch(0.5 0.11 195);
            }

            .preset-comet.dark {
              --background: oklch(0.176 0.015 215);
              --primary: oklch(0.61 0.1 195);
              --primary-button: oklch(0.5 0.1 195);
            }
            """,
        "nebula" =>
            """
            .preset-nebula {
              --primary: oklch(0.55 0.18 275);
            }

            .preset-nebula.dark {
              --primary: oklch(0.62 0.16 275);
              --primary-button: oklch(0.53 0.18 275);
            }
            """,
        _ => throw new ArgumentException($"Unknown preset '{preset}'.", nameof(preset)),
    };
    public IReadOnlyList<string> AvailablePresets { get; } = ["comet", "nebula"];
}

/// <summary>An in-memory <see cref="ITemplateProvider"/> for scaffolder tests.</summary>
public sealed class FakeTemplateProvider(params TemplateFile[] files) : ITemplateProvider
{
    public bool Has(string templateId) => files.Length > 0;
    public IReadOnlyList<TemplateFile> GetFiles(string templateId) => files;
}
