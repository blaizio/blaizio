using System.Text.Json;

namespace Blaizio.Cli.Core.Configuration;

/// <summary>Reads and writes <c>blaizio.json</c> at a project root.</summary>
public static class ConfigStore
{
    /// <summary>Absolute path where the config lives for a given project directory.</summary>
    public static string PathFor(string projectDir) => Path.Combine(projectDir, BlaizioConfig.FileName);

    /// <summary>True when a project has already been initialized.</summary>
    public static bool Exists(string projectDir) => File.Exists(PathFor(projectDir));

    /// <summary>Load the config, or null when the project is not initialized.</summary>
    public static async Task<BlaizioConfig?> LoadAsync(string projectDir, CancellationToken ct = default)
    {
        var path = PathFor(projectDir);
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, CoreJson.Default.BlaizioConfig, ct)
                ?? throw new InvalidDataException($"{BlaizioConfig.FileName} is empty or malformed.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"{BlaizioConfig.FileName} is invalid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>Load the config or throw a clear error when the project is not initialized.</summary>
    public static async Task<BlaizioConfig> RequireAsync(string projectDir, CancellationToken ct = default)
        => await LoadAsync(projectDir, ct)
            ?? throw new InvalidOperationException(
                $"No {BlaizioConfig.FileName} found in '{projectDir}'. Run 'blaizio add' first.");

    /// <summary>Write the config to the project root (atomically — write a temp file, then swap).</summary>
    public static async Task SaveAsync(string projectDir, BlaizioConfig config, CancellationToken ct = default)
    {
        var path = PathFor(projectDir);
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, config, CoreJson.Default.BlaizioConfig, ct);
        }
        File.Move(tmp, path, overwrite: true);
    }
}
