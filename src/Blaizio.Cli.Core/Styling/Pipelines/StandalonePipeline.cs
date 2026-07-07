using System.Xml.Linq;
using Blaizio.Cli.Core.Projects;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>
/// Compiles via Tailwind's standalone native binary — no Node. Setup drops an MSBuild target that
/// runs the binary on every <c>dotnet build</c>/<c>dotnet watch</c> and imports it into the project,
/// so CSS just compiles with the app. The binary itself is fetched separately (a one-time download
/// into <c>.blaizio/</c>); until then the target warns rather than failing the build.
/// </summary>
public sealed class StandalonePipeline : ITailwindPipeline
{
    /// <summary>Directory the binary + target live in (project-relative).</summary>
    public const string Dir = ".blaizio";

    /// <summary>The MSBuild target file name.</summary>
    public const string TargetsFile = "Blaizio.Tailwind.targets";

    /// <inheritdoc />
    public string Id => "standalone";

    /// <inheritdoc />
    public string Title => "Standalone + MSBuild";

    /// <inheritdoc />
    public string Summary => "Native tailwindcss binary run by an MSBuild target. Zero Node.";

    /// <inheritdoc />
    public bool CanSetup => true;

    /// <inheritdoc />
    public Detection Detect(ProjectContext project)
    {
        if (File.Exists(Path.Combine(project.ProjectDir, Dir, TargetsFile)))
            return Detection.Present($"{Dir}/{TargetsFile}");
        if (BinaryPath(project.ProjectDir) is { } bin && File.Exists(bin))
            return Detection.Partial($"{Dir} binary (no MSBuild target yet)");
        if (OnPath("tailwindcss"))
            return Detection.Partial("tailwindcss on PATH");
        return Detection.Absent;
    }

    /// <inheritdoc />
    public string BuildHint(ProjectContext project, TailwindPaths paths) => "dotnet build   (or dotnet watch)";

    /// <inheritdoc />
    public async Task<PipelineSetupResult> SetupAsync(ProjectContext project, TailwindPaths paths, CancellationToken ct = default)
    {
        var dirAbs = Path.Combine(project.ProjectDir, Dir);
        Directory.CreateDirectory(dirAbs);

        var changed = new List<string>();

        var targetsAbs = Path.Combine(dirAbs, TargetsFile);
        await File.WriteAllTextAsync(targetsAbs, TargetsXml(paths), ct);
        changed.Add($"{Dir}/{TargetsFile}");

        if (project.CsprojPath is not null && EnsureImport(project.CsprojPath))
            changed.Add(Path.GetFileName(project.CsprojPath));

        var exe = OperatingSystem.IsWindows() ? "tailwindcss.exe" : "tailwindcss";
        return new PipelineSetupResult
        {
            PipelineId = Id,
            ChangedFiles = changed,
            BuildHint = BuildHint(project, paths),
            Notes =
            [
                $"Fetch the standalone binary into {Dir}/{exe} (run 'blaizio tailwind fetch').",
                "CSS then compiles automatically on 'dotnet build' / 'dotnet watch'.",
            ],
        };
    }

    /// <summary>Absolute path where the standalone binary is expected, per OS.</summary>
    public static string BinaryPath(string projectDir)
    {
        var exe = OperatingSystem.IsWindows() ? "tailwindcss.exe" : "tailwindcss";
        return Path.Combine(projectDir, Dir, exe);
    }

    private static string TargetsXml(TailwindPaths paths) =>
        $"""
        <Project>
          <!-- Written by 'blaizio tailwind setup --mode standalone'. Runs the standalone Tailwind
               binary on build so CSS compiles with the app - no Node required. -->
          <PropertyGroup>
            <BlaizioTailwindExt Condition="'$(OS)' == 'Windows_NT'">.exe</BlaizioTailwindExt>
            <BlaizioTailwindExe Condition="'$(BlaizioTailwindExe)' == ''">$(MSBuildProjectDirectory)/{Dir}/tailwindcss$(BlaizioTailwindExt)</BlaizioTailwindExe>
            <BlaizioTailwindInput Condition="'$(BlaizioTailwindInput)' == ''">{paths.Input}</BlaizioTailwindInput>
            <BlaizioTailwindOutput Condition="'$(BlaizioTailwindOutput)' == ''">{paths.Output}</BlaizioTailwindOutput>
          </PropertyGroup>

          <Target Name="BlaizioTailwindBuild" BeforeTargets="BeforeBuild" Condition="Exists('$(BlaizioTailwindExe)')">
            <Exec Command="&quot;$(BlaizioTailwindExe)&quot; -i &quot;$(BlaizioTailwindInput)&quot; -o &quot;$(BlaizioTailwindOutput)&quot; --minify" />
          </Target>

          <Target Name="BlaizioTailwindMissing" BeforeTargets="BeforeBuild" Condition="!Exists('$(BlaizioTailwindExe)')">
            <Warning Text="Blaizio: standalone tailwindcss not found at $(BlaizioTailwindExe). Run 'blaizio tailwind fetch'." />
          </Target>
        </Project>

        """;

    /// <summary>Add an <c>&lt;Import&gt;</c> of the targets file to the csproj if absent. Returns true when changed.</summary>
    private static bool EnsureImport(string csprojPath)
    {
        var importPath = $"{Dir}/{TargetsFile}";
        var doc = XDocument.Load(csprojPath);
        var project = doc.Root;
        if (project is null)
            return false;

        var already = project.Elements("Import")
            .Any(e => string.Equals(
                (e.Attribute("Project")?.Value ?? string.Empty).Replace('\\', '/'),
                importPath, StringComparison.OrdinalIgnoreCase));
        if (already)
            return false;

        project.Add(new XElement("Import", new XAttribute("Project", importPath)));
        doc.Save(csprojPath);
        return true;
    }

    private static bool OnPath(string command)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return false;

        var exts = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat" } : [""];
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in exts)
            {
                try
                {
                    if (File.Exists(Path.Combine(dir, command + ext)))
                        return true;
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry; skip.
                }
            }
        }
        return false;
    }
}
