using System.Text;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The one-shot tokens-file transitions: the v1 → v3 CSS migration (<c>blaizio update</c>) and the
/// contract eject (<c>blaizio eject</c>). Both transform the file once and never run as part of
/// steady-state syncing. <see cref="TailwindSetup"/> stays the public face; commands never
/// construct this.
/// </summary>
internal sealed class TailwindMigration(ICssAssetProvider assets)
{
    /// <summary>See <see cref="TailwindSetup.MigrateAsync"/> - this is its implementation.</summary>
    public async Task<MigrationResult> MigrateAsync(
        string projectDir, string componentOutput, string preset, string? cssInput, CancellationToken ct)
    {
        var managedAbs = Path.Combine(projectDir, TailwindSetup.StylesDir, TailwindSetup.LegacyManagedDir);
        var block = ComposeMigratedTokenBlock(managedAbs, preset);

        var (inputRel, inputAbs) = TokensFileScaffolder.InputPath(projectDir, cssInput);
        var inputDirAbs = Path.GetDirectoryName(inputAbs)!;
        var required = TokensFileScaffolder.BuildRequiredLines(projectDir, componentOutput, cssInput, inputDirAbs);

        var wasManaged = false;
        if (File.Exists(inputAbs))
        {
            var text = await File.ReadAllTextAsync(inputAbs, ct);
            wasManaged = text.StartsWith(TailwindSetup.Marker, StringComparison.Ordinal);
            if (wasManaged && cssInput is null)
            {
                // Fully CLI-written v1 input: regenerate as the v3 scaffold around the composed block.
                var sb = new StringBuilder();
                sb.AppendLine(TokensFileScaffolder.ScaffoldHeader);
                foreach (var line in required)
                    sb.AppendLine(line);
                sb.AppendLine();
                sb.Append(block);
                await File.WriteAllTextAsync(inputAbs, sb.ToString(), ct);
            }
            else
            {
                // A user/bundler input: strip the v1 lines, wire the v3 imports, inject the
                // composed block when the file doesn't carry a token map of its own.
                text = TokensFileScaffolder.SyncInput(text, required);
                if (!TokensFileScaffolder.HasTokenMap(text))
                    text = $"{text.TrimEnd('\n')}\n\n{block}";
                await File.WriteAllTextAsync(inputAbs, text, ct);
            }
        }
        else
        {
            Directory.CreateDirectory(inputDirAbs);
            var sb = new StringBuilder();
            foreach (var line in required)
                sb.AppendLine(line);
            sb.AppendLine();
            sb.Append(block);
            await File.WriteAllTextAsync(inputAbs, sb.ToString(), ct);
            wasManaged = true; // this run created it - ours to delete on uninstall
        }

        // The v1 managed sheets are dead now - the values live in the tokens file, the contract
        // materializes into .blaizio/ at build, and the skin ships inlined in the components.
        var removed = new List<string>();
        if (Directory.Exists(managedAbs))
        {
            foreach (var file in Directory.EnumerateFiles(managedAbs, "*", SearchOption.AllDirectories))
                removed.Add(TokensFileScaffolder.ToPosix(Path.GetRelativePath(projectDir, file)));
            Directory.Delete(managedAbs, recursive: true);
        }

        await TokensFileScaffolder.EnsureGitignoreAsync(projectDir, ct);

        return new MigrationResult(TokensFileScaffolder.ToPosix(inputRel), wasManaged, removed);
    }

    /// <summary>
    /// The v3 token block composed from the project's v1 sheets (embedded-asset fallback when a
    /// sheet is missing): theme.css values as the user left them, preset merged, fonts/pointer
    /// folded in, v1-only rules and the retired <c>--primary-button</c> dropped.
    /// </summary>
    public string ComposeMigratedTokenBlock(string managedAbs, string preset)
    {
        var themePath = Path.Combine(managedAbs, "theme.css");
        var css = File.Exists(themePath) ? File.ReadAllText(themePath) : assets.GetThemeCss();
        css = TokensFileScaffolder.CollapseBlankRuns(CssBlocks.StripComments(css)).TrimStart('\n');

        // v1-only pieces with no v3 home: the heading hook now inlines from shared.css, and the
        // dark button repaint derives from --primary (no extra token).
        css = CssBlocks.RemoveNestedBlock(css, "@layer base", ".bz-font-heading");
        css = CssBlocks.RemoveTopLevelBlock(css, ".dark .bz-button-variant-default");
        css = CssBlocks.RemoveTopLevelBlock(css, ".dark .bz-button-variant-default:hover");
        css = CssBlocks.RemoveDeclaration(css, ".dark", "--primary-button");

        if (!string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase))
        {
            var presetPath = Path.Combine(managedAbs, $"preset-{preset}.css");
            var presetCss = File.Exists(presetPath) ? File.ReadAllText(presetPath) : assets.GetPresetCss(preset);
            presetCss = CssBlocks.StripComments(presetCss);
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}"))
            {
                if (name != "--primary-button")
                    css = CssBlocks.SetDeclaration(css, ":root", name, value);
            }
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}.dark"))
            {
                if (name != "--primary-button")
                    css = CssBlocks.SetDeclaration(css, ".dark", name, value);
            }
        }

        // fonts.css: ":root { --font-heading: …; }" and/or a top-level "html { font-family: …; }".
        var fontsPath = Path.Combine(managedAbs, "fonts.css");
        if (File.Exists(fontsPath))
        {
            var fonts = CssBlocks.StripComments(File.ReadAllText(fontsPath));
            foreach (var (name, value) in CssBlocks.Declarations(fonts, ":root"))
            {
                if (name == "--font-heading")
                    css = CssBlocks.SetDeclaration(css, ":root", name, value);
            }
            var htmlLine = fonts.Split('\n').Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("html {", StringComparison.Ordinal) && l.EndsWith("}", StringComparison.Ordinal));
            if (htmlLine is not null)
                css = CssBlocks.SetNestedRule(css, "@layer base", "html", htmlLine);
        }

        // options.css: the only flag it ever carried is the pointer cursor.
        var optionsPath = Path.Combine(managedAbs, "options.css");
        if (File.Exists(optionsPath) && File.ReadAllText(optionsPath).Contains("cursor: pointer", StringComparison.Ordinal))
            css = CssBlocks.SetNestedRule(css, "@layer base", TailwindSetup.PointerPrelude, TailwindSetup.PointerRule);

        return css;
    }

    /// <summary>See <see cref="TailwindSetup.EjectAsync"/> - this is its implementation.</summary>
    public async Task<EjectResult> EjectAsync(string projectDir, string? cssInput, CancellationToken ct)
    {
        var (inputRel, inputAbs) = TokensFileScaffolder.InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            throw new InvalidOperationException(
                $"No tokens file at '{TokensFileScaffolder.ToPosix(inputRel)}'. Run 'blaizio add' first.");

        // An actual @import line, not a raw Contains: the ejected file itself mentions the path
        // in its provenance comments.
        var text = await File.ReadAllTextAsync(inputAbs, ct);
        var hasImport = text.Split('\n').Any(line =>
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("@import", StringComparison.Ordinal)
                && trimmed.Contains(TailwindSetup.ContractImportMarker, StringComparison.Ordinal);
        });
        if (!hasImport)
            throw new InvalidOperationException(
                $"'{TokensFileScaffolder.ToPosix(inputRel)}' does not import the contract ({TailwindSetup.ContractImportMarker}) - " +
                "nothing to eject. The project is either not initialized or already ejected.");

        // Prefer the materialized sheets: they version-track the installed Blaizio.Base package.
        // Mixing sources could pair sheets from different versions, so it's both-or-embedded.
        var contractAbs = Path.Combine(projectDir, TailwindSetup.ContractDir, TailwindSetup.ContractSheet);
        var animateAbs = Path.Combine(projectDir, TailwindSetup.ContractDir, TailwindSetup.AnimateSheet);
        var materialized = File.Exists(contractAbs) && File.Exists(animateAbs);
        var contractCss = materialized ? await File.ReadAllTextAsync(contractAbs, ct) : assets.GetBaseCss();
        var animateCss = materialized ? await File.ReadAllTextAsync(animateAbs, ct) : assets.GetAnimateCss();

        // Drop the two contract imports wherever they sit (the ./-vs-../ prefix varies with the
        // input's location); everything else in the file stays.
        var lines = text.Split('\n').Where(line =>
        {
            var trimmed = line.TrimStart();
            return !(trimmed.StartsWith("@import", StringComparison.Ordinal)
                && (trimmed.Contains(TailwindSetup.ContractImportMarker, StringComparison.Ordinal)
                    || trimmed.Contains($"{TailwindSetup.ContractDir}/{TailwindSetup.AnimateSheet}", StringComparison.Ordinal)));
        });

        // Append the sheets in their old import order (animate before contract). Plain comments,
        // not the "/* blaizio:" marker — this content is the user's now, no sync may reclaim it.
        var sb = new StringBuilder(string.Join('\n', lines).TrimEnd('\n'));
        sb.Append("\n\n/* Ejected Blaizio contract (was ")
            .Append(TailwindSetup.ContractDir).Append('/').Append(TailwindSetup.AnimateSheet).Append(") - yours to edit. */\n");
        sb.Append(animateCss.Replace("\r\n", "\n").Trim('\n'));
        sb.Append("\n\n/* Ejected Blaizio contract (was ")
            .Append(TailwindSetup.ContractDir).Append('/').Append(TailwindSetup.ContractSheet).Append(") - yours to edit. */\n");
        sb.Append(contractCss.Replace("\r\n", "\n").Trim('\n'));
        sb.Append('\n');

        await File.WriteAllTextAsync(inputAbs, sb.ToString(), ct);
        return new EjectResult(TokensFileScaffolder.ToPosix(inputRel), materialized);
    }
}
