using System.Text.Json;

namespace Blaizio.Cli.Tests;

/// <summary>A throwaway directory that deletes itself on dispose.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "blaizio-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Absolute path to the directory.</summary>
    public string Path { get; }

    /// <summary>Combine a relative path under the temp dir.</summary>
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

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

/// <summary>
/// Writes a minimal local registry directory (<c>index.json</c> + per-item JSON) for commands to
/// resolve against, mirroring what <c>blaizio build</c> emits.
/// </summary>
public static class LocalRegistry
{
    /// <summary>Create the fixture registry under <paramref name="dir"/>/r and return its path.</summary>
    public static string Create(TempDir dir)
    {
        dir.Write("r/index.json",
            """
            {
              "name": "test",
              "items": [
                { "name": "button", "type": "registry:ui", "description": "A button." },
                { "name": "card", "type": "registry:ui", "description": "A card." }
              ]
            }
            """);
        dir.Write("r/button.json",
            """
            {
              "name": "button",
              "type": "registry:ui",
              "files": [
                { "path": "Ui/Button/BzButton.razor", "type": "registry:ui", "content": "@* Blaizio.Ui *@\n<button>x</button>\n" }
              ]
            }
            """);
        dir.Write("r/card.json",
            """
            {
              "name": "card",
              "type": "registry:ui",
              "registryDependencies": ["button"],
              "files": [
                { "path": "Ui/Card/BzCard.razor", "type": "registry:ui", "content": "<div>card</div>\n" }
              ]
            }
            """);
        return dir.Combine("r");
    }
}

/// <summary>Captures everything a command writes to <see cref="Console.Out"/> (the --json stream).</summary>
public sealed class StdoutCapture : IDisposable
{
    private readonly TextWriter _original = Console.Out;
    private readonly StringWriter _buffer = new();

    public StdoutCapture() => Console.SetOut(_buffer);

    /// <summary>The captured stdout text.</summary>
    public string Text => _buffer.ToString();

    /// <summary>Parse the captured stdout as one JSON document.</summary>
    public JsonDocument Json() => JsonDocument.Parse(Text);

    public void Dispose() => Console.SetOut(_original);
}
