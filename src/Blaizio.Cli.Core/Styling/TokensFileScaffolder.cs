using System.Text;
using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// Creates and syncs the v3 tokens file (see <see cref="TailwindSetup"/> for the layout story) -
/// the init/add leg. Also home to the tokens-file mechanics its siblings
/// (<see cref="TokensFilePatcher"/>, <see cref="TailwindMigration"/>) share: path resolution, the
/// required-directive set, the user-input sync, the token-block composition and the gitignore
/// entry. <see cref="TailwindSetup"/> stays the public face; commands never construct this.
/// </summary>
internal sealed class TokensFileScaffolder(ICssAssetProvider assets)
{
    /// <summary>See <see cref="TailwindSetup.EnsureAsync"/> - this is its implementation.</summary>
    public async Task<TailwindResult> EnsureAsync(
        string projectDir,
        string componentOutput,
        TailwindOptions options,
        string preset,
        bool topUpUserInput,
        string? cssInput,
        string chart,
        string radius,
        bool ejected,
        CancellationToken ct)
    {
        var inputRel = cssInput ?? Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
        var inputAbs = Path.GetFullPath(Path.Combine(projectDir, inputRel));
        var inputDirAbs = Path.GetDirectoryName(inputAbs)!;

        if (ejected)
        {
            return new TailwindResult
            {
                InputPath = ToPosix(inputRel),
                Preset = string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase) ? "nova" : preset,
                InputCreated = false,
                Ejected = true,
            };
        }

        if (cssInput is not null && !File.Exists(inputAbs))
            throw new InvalidOperationException(
                $"The css input '{ToPosix(inputRel)}' (blaizio.json \"css\") does not exist. Create it or fix the path.");

        var hasPreset = !string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase);

        // A project still on the v1 layout (Styles/blaizio/ imports) is never silently rewritten:
        // flipping the imports to v3 would orphan the skin sheets while the components still carry
        // bz-* classes. `blaizio update` runs the migration; here we only report.
        if (TailwindSetup.IsLegacyV1(projectDir, inputAbs))
        {
            return new TailwindResult
            {
                InputPath = ToPosix(inputRel),
                Preset = hasPreset ? preset : "nova",
                InputCreated = false,
                LegacyV1 = true,
            };
        }

        var required = BuildRequiredLines(projectDir, componentOutput, cssInput, inputDirAbs);

        var created = !File.Exists(inputAbs);

        if (created)
        {
            Directory.CreateDirectory(inputDirAbs);
            await File.WriteAllTextAsync(inputAbs, BuildScaffold(required, preset, chart, radius, options), ct);
        }
        else
        {
            var text = await File.ReadAllTextAsync(inputAbs, ct);
            if (text.StartsWith(TailwindSetup.Marker, StringComparison.Ordinal) && cssInput is null)
            {
                // A v1 marker file with no v1 imports left (already-migrated edge) was fully
                // CLI-written — regenerate it as the v3 scaffold.
                await File.WriteAllTextAsync(inputAbs, BuildScaffold(required, preset, chart, radius, options), ct);
            }
            else if (topUpUserInput || cssInput is not null)
            {
                text = SyncInput(text, required);
                // Adopt: a file that never got the token block gains the full block — values, map
                // and base layer — appended after its own content.
                if (!HasTokenMap(text))
                    text = $"{text.TrimEnd('\n')}\n\n{ComposeTokenBlock(preset, chart, radius, options.Pointer)}";
                await File.WriteAllTextAsync(inputAbs, text, ct);
            }
        }

        await EnsureGitignoreAsync(projectDir, ct);

        return new TailwindResult
        {
            InputPath = ToPosix(inputRel),
            Preset = hasPreset ? preset : "nova",
            InputCreated = created,
        };
    }

    /// <summary>The full fresh-init tokens file: directives up top, token block below.</summary>
    public string BuildScaffold(
        IReadOnlyList<string> required, string preset, string chart, string radius, TailwindOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ScaffoldHeader);
        foreach (var line in required)
            sb.AppendLine(line);
        sb.AppendLine();
        sb.Append(ComposeTokenBlock(preset, chart, radius, options.Pointer));
        return sb.ToString();
    }

    internal const string ScaffoldHeader =
        "/* Tailwind input + Blaizio theme tokens. This file is yours: edit the :root/.dark values\n" +
        "   to retheme. The CLI only keeps the imports in sync and patches values on apply. */";

    /// <summary>
    /// The token block of the tokens file: the theme asset (dark variant, <c>@theme inline</c>
    /// map, <c>:root</c>/<c>.dark</c> values, base layer) with the preset palette merged into the
    /// value blocks, the chart/radius selection baked, and the pointer rule when enabled —
    /// comment-free, plain editable values.
    /// </summary>
    public string ComposeTokenBlock(string preset, string chart, string radius, bool pointer)
    {
        var css = CssBlocks.StripComments(assets.GetThemeCss());
        css = CollapseBlankRuns(css).TrimStart('\n');

        if (!string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase))
        {
            var presetCss = CssBlocks.StripComments(assets.GetPresetCss(preset));
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}"))
                css = CssBlocks.SetDeclaration(css, ":root", name, value);
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}.dark"))
                css = CssBlocks.SetDeclaration(css, ".dark", name, value);
        }

        css = TokensFilePatcher.WithTokenOverrides(css, chart, radius);
        if (pointer)
            css = CssBlocks.SetNestedRule(css, "@layer base", TailwindSetup.PointerPrelude, TailwindSetup.PointerRule);
        return css;
    }

    /// <summary>The directives the tokens file must carry: the Tailwind import + app-wide scan
    /// (default flow only — a bundler input owns both), the materialized contract imports, and
    /// the component @source globs.</summary>
    public static List<string> BuildRequiredLines(
        string projectDir, string componentOutput, string? cssInput, string inputDirAbs)
    {
        var contractPrefix = RelativePrefix(inputDirAbs, Path.Combine(projectDir, TailwindSetup.ContractDir));
        var sourceGlob = ToPosix(Path.GetRelativePath(inputDirAbs, Path.Combine(projectDir, componentOutput)));

        var required = new List<string>();
        if (cssInput is null)
        {
            // source(none) disables Tailwind's automatic source detection: without it the scanner
            // walks the whole project — including bin/obj build output, whose binaries crash it
            // ("value that is out of range of code points") — and we list our sources explicitly.
            // A bundler-owned input keeps its own tailwindcss import and scanning setup.
            required.Add("@import \"tailwindcss\" source(none);");
        }
        // The sheets Blaizio.Base materializes into .blaizio/ at build: the vendored tw-animate
        // (Node-free pipelines can't resolve node_modules) and the static contract (data-*
        // variants, keyframes, toast/chart machinery).
        required.Add($"@import \"{contractPrefix}/{TailwindSetup.AnimateSheet}\";");
        required.Add($"@import \"{contractPrefix}/{TailwindSetup.ContractSheet}\";");
        // Copied components (.razor + .cs class builders) so their utilities always generate.
        required.Add($"@source \"{sourceGlob}/**/*.razor\";");
        required.Add($"@source \"{sourceGlob}/**/*.cs\";");
        if (cssInput is null)
        {
            // Every other .razor (pages, layouts) so app markup utilities generate too. A bundler
            // input relies on its own content scanning for app markup.
            required.Add("@source \"../**/*.razor\";");
        }
        return required;
    }

    /// <summary>
    /// True when the input already defines the token map the contract sheets compile against.
    /// Checks for the canonical <c>--color-*</c> mappings, not merely an <c>@theme inline</c>
    /// at-rule: a user file whose only map is a hand-written alias block (say
    /// <c>--color-danger: var(--destructive)</c>) has none of Blaizio's tokens, and treating it
    /// as initialized leaves every <c>@apply</c> in the contract sheets unresolvable.
    /// </summary>
    public static bool HasTokenMap(string text) =>
        text.Contains("--color-background", StringComparison.Ordinal)
        && text.Contains("--color-primary", StringComparison.Ordinal)
        && text.Contains("--color-muted-foreground", StringComparison.Ordinal);

    /// <summary>Append <c>.blaizio/</c> to the project's .gitignore (creating one when absent) —
    /// the materialized contract is build output, like obj/.</summary>
    public static async Task EnsureGitignoreAsync(string projectDir, CancellationToken ct)
    {
        var path = Path.Combine(projectDir, ".gitignore");
        const string entry = $"{TailwindSetup.ContractDir}/";
        if (File.Exists(path))
        {
            var lines = await File.ReadAllLinesAsync(path, ct);
            if (lines.Any(l => l.Trim() is entry or TailwindSetup.ContractDir))
                return;
            var text = await File.ReadAllTextAsync(path, ct);
            await File.WriteAllTextAsync(path, $"{text.TrimEnd('\n')}\n{entry}\n", ct);
        }
        else
        {
            await File.WriteAllTextAsync(path, $"{entry}\n", ct);
        }
    }

    /// <summary>
    /// Sync a user-owned input with the managed directives — owning their content AND their
    /// position: every managed line (current, stale v1, or misplaced, including a hand-written
    /// mirror of the same imports) is removed from wherever it sits and the required set is
    /// reinserted right after the file's last <c>@import</c>. Idempotent; everything the user
    /// authored stays.
    /// </summary>
    public static string SyncInput(string original, IReadOnlyList<string> required)
    {
        var lines = original.Split('\n').ToList();

        bool Ours(string line)
        {
            if (line.Contains(TailwindSetup.MarkerPrefix, StringComparison.Ordinal))
                return true;
            var trimmed = line.TrimStart();
            if ((trimmed.StartsWith("@import", StringComparison.Ordinal) || trimmed.StartsWith("@source", StringComparison.Ordinal))
                && line.Contains($"{TailwindSetup.LegacyManagedDir}/", StringComparison.Ordinal))
                return true;
            // Directives that don't mention blaizio/ (the component @source globs) match exactly.
            return required.Contains(line.Trim());
        }

        lines.RemoveAll(Ours);

        var text = string.Join('\n', lines);
        var toInsert = required.Where(line => !text.Contains(line, StringComparison.Ordinal)).ToArray();
        if (toInsert.Length > 0)
            lines.InsertRange(InsertionIndex(lines), [TailwindSetup.SyncHeader, .. toInsert]);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Where new directives go: right after the file's last <c>@import</c>. CSS ignores an
    /// <c>@import</c> that follows any other rule, so appending at the end would be dead code.
    /// </summary>
    private static int InsertionIndex(List<string> lines)
    {
        var index = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("@import", StringComparison.Ordinal))
                index = i + 1;
        }
        return index;
    }

    /// <summary>The tokens file this project uses: the recorded bundler input, or the default.</summary>
    public static (string Rel, string Abs) InputPath(string projectDir, string? cssInput)
    {
        var rel = cssInput ?? Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
        return (rel, Path.GetFullPath(Path.Combine(projectDir, rel)));
    }

    /// <summary>Collapse runs of blank lines (comment stripping leaves them behind).</summary>
    public static string CollapseBlankRuns(string css)
        => s_blankRun.Replace(css.Replace("\r\n", "\n"), "\n\n");

    private static readonly Regex s_blankRun = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>Import prefix from the input file's directory to a target dir (POSIX, ./-anchored).
    /// Anchors unless the path already climbs (<c>../…</c>) — a dot-DIRECTORY like
    /// <c>.blaizio</c> still needs the <c>./</c>.</summary>
    private static string RelativePrefix(string fromDirAbs, string toDirAbs)
    {
        var rel = ToPosix(Path.GetRelativePath(fromDirAbs, toDirAbs));
        return rel.StartsWith("../", StringComparison.Ordinal) || rel == ".." ? rel : $"./{rel}";
    }

    public static string ToPosix(string path) => path.Replace('\\', '/');
}
