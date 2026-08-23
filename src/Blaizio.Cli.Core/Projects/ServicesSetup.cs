using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Projects;

/// <summary>The outcome of wiring the service registration, for reporting.</summary>
public sealed class ServicesResult
{
    /// <summary>The program file looked at (project-relative), or null when the project has none
    /// (a class library, a Startup-era app).</summary>
    public string? Path { get; init; }

    /// <summary>True when the file registers Blaizio's services - whether it already did or this
    /// run made it so. False with a non-null <see cref="Path"/> means the CLI found the file but
    /// could not place the call: the caller says what to add by hand.</summary>
    public bool Registered { get; init; }

    /// <summary>Human-readable changes applied this run. Empty when nothing needed doing.</summary>
    public IReadOnlyList<string> Changes { get; init; } = [];
}

/// <summary>
/// Wires <c>builder.Services.AddBlaizio()</c> into <c>Program.cs</c>. The components reach the
/// browser-side <c>ICore</c>, the dialog and toast services and the theme store through DI, and
/// nothing else in the install pipeline registers them: a project that skips this line compiles,
/// runs, and falls over the first time a component that injects a service renders ("No registered
/// service of type 'Blaizio.ICore'"). One idempotent patch: the call goes just above the line that
/// builds the host, where every other registration already sits. A call the app wrote itself -
/// with an options lambda, on its own line, anywhere - counts as present and is never touched.
/// </summary>
public sealed partial class ServicesSetup
{
    /// <summary>The registration every app needs.</summary>
    public const string Call = "AddBlaizio(";

    /// <summary>The comment the CLI writes above its call, so <c>uninstall</c> strips exactly the
    /// line it wrote and never a registration the app authored.</summary>
    public const string Marker = "// Blaizio services - wired by the CLI";

    private const string ProgramFile = "Program.cs";

    /// <summary>
    /// Ensure <c>Program.cs</c> registers Blaizio's services. Nothing to do when it already does,
    /// wherever and however it does it; otherwise the call lands just above the line that builds
    /// the host. A file with no recognisable build line is left alone and reported as not
    /// registered - the caller tells the user what to add.
    /// </summary>
    public async Task<ServicesResult> EnsureAsync(string projectDir, CancellationToken ct = default)
    {
        var content = Read(projectDir);
        if (content is null)
            return new ServicesResult();

        if (content.Contains(Call, StringComparison.Ordinal))
            return new ServicesResult { Path = ProgramFile, Registered = true };

        var build = BuildLineRegex().Match(content);
        if (!build.Success)
            return new ServicesResult { Path = ProgramFile };

        var indent = build.Groups["indent"].Value;
        var builder = build.Groups["builder"].Value;
        var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var insert = $"{indent}{Marker}{newline}{indent}{builder}.Services.AddBlaizio();{newline}{newline}";

        content = content.Insert(build.Index, insert);
        await File.WriteAllTextAsync(System.IO.Path.Combine(projectDir, ProgramFile), content, ct);

        return new ServicesResult
        {
            Path = ProgramFile,
            Registered = true,
            Changes = [$"{builder}.Services.AddBlaizio() registered"],
        };
    }

    /// <summary>True when the project's <c>Program.cs</c> registers Blaizio's services - or when
    /// there is no <c>Program.cs</c> to check, which is not a missing registration.</summary>
    public bool IsRegistered(string projectDir)
        => Read(projectDir) is not { } content || content.Contains(Call, StringComparison.Ordinal);

    /// <summary>True when the project has a <c>Program.cs</c> at all.</summary>
    public static bool HasProgram(string projectDir)
        => File.Exists(System.IO.Path.Combine(projectDir, ProgramFile));

    /// <summary>
    /// Reverse of <see cref="EnsureAsync"/> for <c>uninstall</c>: strip the call the CLI wrote -
    /// recognised by its <see cref="Marker"/> comment - and nothing else. A registration the app
    /// wrote itself stays; it is the app's.
    /// </summary>
    public async Task<ServicesResult> RemoveAsync(string projectDir, bool dryRun = false, CancellationToken ct = default)
    {
        var content = Read(projectDir);
        if (content is null)
            return new ServicesResult();

        var written = WrittenBlockRegex().Match(content);
        if (!written.Success)
            return new ServicesResult { Path = ProgramFile, Registered = content.Contains(Call, StringComparison.Ordinal) };

        content = content.Remove(written.Index, written.Length);
        if (!dryRun)
            await File.WriteAllTextAsync(System.IO.Path.Combine(projectDir, ProgramFile), content, ct);

        return new ServicesResult
        {
            Path = ProgramFile,
            Registered = content.Contains(Call, StringComparison.Ordinal),
            Changes = ["AddBlaizio() registration removed"],
        };
    }

    private static string? Read(string projectDir)
    {
        var abs = System.IO.Path.Combine(projectDir, ProgramFile);
        return File.Exists(abs) ? File.ReadAllText(abs) : null;
    }

    // The line that builds the host: `var app = builder.Build();`, `await builder.Build().RunAsync();`,
    // `using var host = builder.Build();` - whatever the variable is called, it is the one every
    // registration must precede.
    [GeneratedRegex(@"^(?<indent>[ \t]*)[^\r\n]*?\b(?<builder>\w+)\.Build\(\)", RegexOptions.Multiline)]
    private static partial Regex BuildLineRegex();

    // Exactly what EnsureAsync writes: the marker line, the call line, and the blank line that
    // follows - any indentation, either newline convention.
    [GeneratedRegex(@"^[ \t]*// Blaizio services - wired by the CLI\r?\n[ \t]*\w+\.Services\.AddBlaizio\(\);\r?\n(\r?\n)?", RegexOptions.Multiline)]
    private static partial Regex WrittenBlockRegex();
}
