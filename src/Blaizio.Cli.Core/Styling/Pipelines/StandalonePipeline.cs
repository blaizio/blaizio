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

    // Detection consults machine-global state (the per-user shared cache and PATH). These pins let
    // tests replace both so Detect is hermetic; the public ctor uses the real machine.
    private readonly string? _cacheRoot;
    private readonly Func<string, bool> _commandOnPath;

    /// <summary>Detects against the real machine (per-user shared cache + PATH).</summary>
    public StandalonePipeline() : this(cacheRoot: null, commandOnPath: OnPath) { }

    /// <summary>Test seam: pin the shared-cache root and the PATH probe.</summary>
    internal StandalonePipeline(string? cacheRoot, Func<string, bool> commandOnPath)
    {
        _cacheRoot = cacheRoot;
        _commandOnPath = commandOnPath;
    }

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
        if (File.Exists(TailwindBinary.CachedBinaryPath(TailwindBinary.DefaultVersion, TailwindBinary.IsMusl(), _cacheRoot)))
            return Detection.Partial("binary in the shared cache (no MSBuild target yet)");
        if (_commandOnPath("tailwindcss"))
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

        var notes = new List<string>
        {
            "The binary auto-downloads into a per-user shared cache on first build, sha256-verified against the pinned release (or run 'blaizio tailwind fetch' now).",
            "CSS then compiles automatically on 'dotnet build' / 'dotnet watch'.",
        };

        if (project.IsMaui)
            notes.Add($"MAUI project: the target also registers {paths.Output} as a MauiAsset, so the compiled stylesheet ships in the app package from the first build instead of the second.");

        return new PipelineSetupResult
        {
            PipelineId = Id,
            ChangedFiles = changed,
            BuildHint = BuildHint(project, paths),
            Notes = notes,
        };
    }

    /// <summary>Absolute path where the standalone binary is expected, per OS.</summary>
    public static string BinaryPath(string projectDir)
    {
        var exe = OperatingSystem.IsWindows() ? "tailwindcss.exe" : "tailwindcss";
        return Path.Combine(projectDir, Dir, exe);
    }

    private static string TargetsXml(TailwindPaths paths) =>
        $$"""
        <Project>
          <!-- Written by 'blaizio tailwind setup' (standalone mode). Runs the standalone Tailwind
               binary on build so CSS compiles with the app, no Node required. The binary lives in a
               per-user shared cache (one download serves every project); a project-local
               {{Dir}}/tailwindcss[.exe] overrides it. On first build it is auto-downloaded into the
               cache and its SHA-256 is verified against the pinned release checksums below; the
               build fails (and the download is deleted) rather than run an unverified executable.
               Disable auto-fetch with BlaizioTailwindAutoFetch=false. Overriding the pinned release
               via BlaizioTailwindVersion requires a matching BlaizioTailwindSha256 (or use the
               verified 'blaizio tailwind fetch'). -->
          <PropertyGroup>
            <BlaizioTailwindExt Condition="'$(OS)' == 'Windows_NT'">.exe</BlaizioTailwindExt>
            <BlaizioTailwindInput Condition="'$(BlaizioTailwindInput)' == ''">{{paths.Input}}</BlaizioTailwindInput>
            <BlaizioTailwindOutput Condition="'$(BlaizioTailwindOutput)' == ''">{{paths.Output}}</BlaizioTailwindOutput>
            <BlaizioTailwindAutoFetch Condition="'$(BlaizioTailwindAutoFetch)' == ''">true</BlaizioTailwindAutoFetch>

            <!-- The name a MAUI app loads the stylesheet by: the output path minus wwwroot/, which
                 is what the template's own MauiAsset glob produces (LogicalName = RecursiveDir +
                 file name), so index.html's href="app.css" resolves either way. -->
            <_BlaizioTwOutputSlashes>$(BlaizioTailwindOutput.Replace('\','/'))</_BlaizioTwOutputSlashes>
            <BlaizioTailwindAssetName Condition="'$(BlaizioTailwindAssetName)' == ''">$(_BlaizioTwOutputSlashes.Replace('wwwroot/',''))</BlaizioTailwindAssetName>
            <BlaizioTailwindVersion Condition="'$(BlaizioTailwindVersion)' == ''">{{TailwindBinary.DefaultVersion}}</BlaizioTailwindVersion>

            <!-- Resolve the release asset for this OS/architecture. Windows stays on x64: no
                 windows-arm64 asset is published (x64 runs under emulation). -->
            <_BlaizioTwOs Condition="'$(OS)' == 'Windows_NT'">windows</_BlaizioTwOs>
            <_BlaizioTwOs Condition="'$(_BlaizioTwOs)' == '' and $([MSBuild]::IsOSPlatform('OSX'))">macos</_BlaizioTwOs>
            <_BlaizioTwOs Condition="'$(_BlaizioTwOs)' == ''">linux</_BlaizioTwOs>
            <_BlaizioTwArch>$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant())</_BlaizioTwArch>
            <_BlaizioTwArch Condition="'$(_BlaizioTwArch)' == 'arm64'">arm64</_BlaizioTwArch>
            <_BlaizioTwArch Condition="'$(_BlaizioTwArch)' != 'arm64'">x64</_BlaizioTwArch>
            <_BlaizioTwArch Condition="'$(_BlaizioTwOs)' == 'windows'">x64</_BlaizioTwArch>
            <_BlaizioTwAsset>tailwindcss-$(_BlaizioTwOs)-$(_BlaizioTwArch)$(BlaizioTailwindExt)</_BlaizioTwAsset>
            <_BlaizioTwUrl>https://github.com/tailwindlabs/tailwindcss/releases/download/$(BlaizioTailwindVersion)/$(_BlaizioTwAsset)</_BlaizioTwUrl>

            <!-- Expected SHA-256 for the download: an explicit BlaizioTailwindSha256 wins, else the
                 checksum pinned at setup time for {{TailwindBinary.DefaultVersion}}. Empty means the
                 fetch target refuses to download. -->
            <_BlaizioTwSha Condition="'$(BlaizioTailwindSha256)' != ''">$(BlaizioTailwindSha256)</_BlaizioTwSha>
        {{PinnedShaProperties()}}

            <!-- Per-user shared cache root, matching the CLI (BLAIZIO_CACHE_DIR overrides). -->
            <_BlaizioTwCacheRoot Condition="'$(BLAIZIO_CACHE_DIR)' != ''">$(BLAIZIO_CACHE_DIR)</_BlaizioTwCacheRoot>
            <_BlaizioTwCacheRoot Condition="'$(_BlaizioTwCacheRoot)' == '' and '$(OS)' == 'Windows_NT'">$(LOCALAPPDATA)/blaizio/cache</_BlaizioTwCacheRoot>
            <_BlaizioTwCacheRoot Condition="'$(_BlaizioTwCacheRoot)' == '' and $([MSBuild]::IsOSPlatform('OSX'))">$(HOME)/Library/Caches/blaizio</_BlaizioTwCacheRoot>
            <_BlaizioTwCacheRoot Condition="'$(_BlaizioTwCacheRoot)' == '' and '$(XDG_CACHE_HOME)' != ''">$(XDG_CACHE_HOME)/blaizio</_BlaizioTwCacheRoot>
            <_BlaizioTwCacheRoot Condition="'$(_BlaizioTwCacheRoot)' == ''">$(HOME)/.cache/blaizio</_BlaizioTwCacheRoot>
            <_BlaizioTwCacheDir>$(_BlaizioTwCacheRoot)/tailwind/$(BlaizioTailwindVersion)</_BlaizioTwCacheDir>

            <!-- Binary resolution: explicit > project-local override > shared cache. -->
            <_BlaizioTwLocal>$(MSBuildProjectDirectory)/{{Dir}}/tailwindcss$(BlaizioTailwindExt)</_BlaizioTwLocal>
            <BlaizioTailwindExe Condition="'$(BlaizioTailwindExe)' == '' and Exists('$(_BlaizioTwLocal)')">$(_BlaizioTwLocal)</BlaizioTailwindExe>
            <BlaizioTailwindExe Condition="'$(BlaizioTailwindExe)' == ''">$(_BlaizioTwCacheDir)/$(_BlaizioTwAsset)</BlaizioTailwindExe>
          </PropertyGroup>

          <Target Name="BlaizioTailwindFetch" BeforeTargets="BeforeBuild"
                  Condition="!Exists('$(BlaizioTailwindExe)') and '$(BlaizioTailwindAutoFetch)' == 'true'">
            <Error Condition="'$(_BlaizioTwSha)' == ''"
                   Text="Blaizio: no pinned SHA-256 for $(_BlaizioTwAsset) at $(BlaizioTailwindVersion), so the build will not download it unverified. Run 'blaizio tailwind fetch' (verified against the release manifest) or set BlaizioTailwindSha256." />
            <Message Importance="high" Text="Blaizio: downloading $(_BlaizioTwAsset) into the shared cache ..." />
            <DownloadFile SourceUrl="$(_BlaizioTwUrl)"
                          DestinationFolder="$(_BlaizioTwCacheDir)"
                          DestinationFileName="$(_BlaizioTwAsset)"
                          Retries="2" />
            <VerifyFileHash File="$(_BlaizioTwCacheDir)/$(_BlaizioTwAsset)" Hash="$(_BlaizioTwSha)" Algorithm="SHA256" />
            <Exec Condition="'$(OS)' != 'Windows_NT'" Command="chmod +x &quot;$(BlaizioTailwindExe)&quot;" />
            <OnError ExecuteTargets="BlaizioTailwindDiscard" />
          </Target>

          <!-- A download that fails verification must not survive to the next build, where
               Exists() would happily execute it. -->
          <Target Name="BlaizioTailwindDiscard">
            <Delete Files="$(_BlaizioTwCacheDir)/$(_BlaizioTwAsset)" />
            <Message Importance="high" Text="Blaizio: removed $(_BlaizioTwAsset) from the cache after a failed download or SHA-256 verification." />
          </Target>

          <Target Name="BlaizioTailwindBuild" BeforeTargets="BeforeBuild" DependsOnTargets="BlaizioTailwindFetch"
                  Condition="Exists('$(BlaizioTailwindExe)')">
            <Exec Command="&quot;$(BlaizioTailwindExe)&quot; -i &quot;$(BlaizioTailwindInput)&quot; -o &quot;$(BlaizioTailwindOutput)&quot; --minify" />
          </Target>

          <!-- .NET MAUI packages wwwroot through a MauiAsset glob that is evaluated before any
               target runs, so a stylesheet this build just compiled would be missing from the app
               package until the NEXT build. Re-register it once it exists. Remove first: on a
               rebuild the glob already caught the file, and a duplicate MauiAsset is a build error. -->
          <Target Name="BlaizioTailwindMauiAsset" AfterTargets="BlaizioTailwindBuild"
                  Condition="'$(UseMaui)' == 'true' and Exists('$(BlaizioTailwindOutput)')">
            <ItemGroup>
              <MauiAsset Remove="$(BlaizioTailwindOutput)" />
              <MauiAsset Include="$(BlaizioTailwindOutput)" LogicalName="$(BlaizioTailwindAssetName)" />
            </ItemGroup>
          </Target>

          <Target Name="BlaizioTailwindMissing" BeforeTargets="BeforeBuild"
                  Condition="!Exists('$(BlaizioTailwindExe)') and '$(BlaizioTailwindAutoFetch)' != 'true'">
            <Warning Text="Blaizio: standalone tailwindcss not found at $(BlaizioTailwindExe). Run 'blaizio tailwind fetch' or set BlaizioTailwindAutoFetch=true." />
          </Target>
        </Project>

        """;

    // One <_BlaizioTwSha> property per release asset, so the fetch target can verify whichever
    // asset this OS/architecture resolves to. Conditions gate on the setup-time pinned version:
    // a different BlaizioTailwindVersion leaves the hash empty and the fetch target refuses.
    private static string PinnedShaProperties() =>
        string.Join('\n', TailwindChecksums.Pinned
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
                $"    <_BlaizioTwSha Condition=\"'$(_BlaizioTwSha)' == '' and '$(BlaizioTailwindVersion)' == '{TailwindBinary.DefaultVersion}' " +
                $"and '$(_BlaizioTwAsset)' == '{pair.Key}'\">{pair.Value}</_BlaizioTwSha>"));

    /// <summary>Remove the <c>&lt;Import&gt;</c> of the targets file from the csproj. Returns true when changed.</summary>
    public static bool RemoveImport(string csprojPath)
    {
        var importPath = $"{Dir}/{TargetsFile}";
        var doc = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var project = doc.Root;
        if (project is null)
            return false;

        var imports = project.Elements("Import")
            .Where(e => string.Equals(
                (e.Attribute("Project")?.Value ?? string.Empty).Replace('\\', '/'),
                importPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (imports.Count == 0)
            return false;

        foreach (var import in imports)
        {
            // Also drop the indentation text node the element sits on, so removal is clean.
            if (import.PreviousNode is XText ws && string.IsNullOrWhiteSpace(ws.Value))
                ws.Remove();
            import.Remove();
        }
        doc.Save(csprojPath);
        return true;
    }

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
