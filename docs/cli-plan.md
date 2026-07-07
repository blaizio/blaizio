# Blaizio CLI — Plan

> Working design doc for `Blaizio.Cli` and the IDE plugin story.
> Domain: **https://blaiz.io** · Registry: **https://blaiz.io/r**

## Model

Components ship as **source**, copied into the consumer project by the CLI — not referenced as a package.

- **`Blaizio.Base`** — headless behavior + ARIA + `data-*` contract + JS assets. Distributed as **NuGet**. Namespace `Blaizio.*`, never rewritten. JS served from `_content/Blaizio.Base/dist/`.
- **`Blaizio.Icons`** — NuGet.
- **`Blaizio.Ui`** — styled Tailwind v4 components. Distributed as **source via the registry**, namespace rewritten into the consumer project.
- **`Blaizio.Cli`** — `dotnet tool`. `init` + `add` + query/build commands.
- **Registry** — static JSON hosted at `https://blaiz.io/r/*.json`. CLI ships zero components; the registry is dumb static data.

## Architecture

Logic lives in a shared core so the CLI and all IDE plugins reuse one resolver.

1. **`Blaizio.Cli.Core`** — registry client, dependency resolver (transitive `registryDependencies`), NuGet installer, file writer, **namespace rewriter**, config read/write. Everything JSON-serializable.
2. **`Blaizio.Cli`** — thin front-end over Core using **Spectre.Console.Cli** (typed commands + auto `-h` help) and **Spectre.Console** (colored markup, radio/checkbox prompts, spinners).
3. IDE plugins call Core directly (.NET IDEs) or shell out to `blaizio --json` (VSCode, Rider).

### Dependency mapping

| Concept | Blaizio |
|---|---|
| package deps | NuGet — `dotnet add package` (Blaizio.Base, Blaizio.Icons, TailwindMerge.NET) |
| registryDependencies | other registry components, resolved transitively |
| import-alias rewrite | **C# namespace rewrite** |
| component files | `.razor` + `.cs`, per-family folders |
| component JS | ships via `_content/Blaizio.Base/dist/` from NuGet — `add` copies **no** JS |
| theming | multi-theme token model (`Dictionary<string, ThemeConfigBase>` + default/system) |

## Registry item shape

```json
{
  "name": "button",
  "type": "registry:ui",
  "nugetDependencies": ["Blaizio.Base", "TailwindMerge.NET"],
  "registryDependencies": ["utils"],
  "files": [{ "path": "Ui/Button/Button.razor", "type": "registry:ui" }],
  "cssVars": { "primary": "..." },
  "tailwind": { "content": ["Components/Ui/**/*.razor"] }
}
```

Item types: `registry:ui`, `registry:lib`, `registry:theme`, `registry:template`.
`blaizio build` walks `src/Blaizio.Ui` and emits one JSON per item to the output dir.

## `blaizio.json` (consumer config)

```json
{
  "$schema": "https://blaiz.io/schema.json",
  "namespace": "MyApp.Components.Ui",
  "output": "Components/Ui",
  "theme": "default",
  "rtl": false,
  "registry": "https://blaiz.io/r",
  "aliases": { "ui": "MyApp.Components.Ui", "base": "Blaizio" }
}
```

## Namespace resolution order

`-ns, --namespace` flag > `blaizio.json` `namespace` > infer from `.csproj` `RootNamespace`/dir > interactive prompt (default `<AssemblyName>.Components.Ui`).

Rewriter sets every copied file's `namespace` line and adds one `@using <namespace>` to `_Imports.razor`. Single flat root namespace — no per-folder namespace math. Base stays `Blaizio.*`, untouched.

---

# Command surface

## Global options (all commands)

```
-c, --cwd <dir>       working dir (default: current)
-y, --yes             skip prompts, take defaults (non-interactive)
-s, --silent          mute output
    --json            machine output (IDE plugins / MCP)
-h, --help
    --version
```

`--json` on every command is the seam IDE plugins and the MCP server ride on.

## `blaizio init`

```
blaizio init [components...]

-t, --template <t>    project template to scaffold (see Templates)
-n, --name <name>     new project name
-ns, --namespace <ns> root namespace for copied components
-o, --output <dir>    component output dir (default: Components/Ui)
-f, --force           overwrite existing blaizio.json
-d, --defaults        template=showcase, no prompts
    --rtl             wire BlazeDirectionProvider RTL
    --pointer         cursor-pointer on buttons
    --theme <name>    component skin: ash/aura/ember/flint/forge/glow/spark/wisp (default ember)
    --reinstall       re-copy existing components
-p, --preset [name]   PLACEHOLDER — parsed, prints "coming soon", no-op
```

Steps:
1. detect/scaffold `.csproj` (net10, `Microsoft.NET.Sdk.Razor`).
2. `dotnet add package Blaizio.Base Blaizio.Icons TailwindMerge.NET`.
3. write `blaizio.json`.
4. Tailwind v4 — write managed CSS under `Styles/blaizio/` (`theme.css` tokens + `@theme` map, `base.css` contract/`data-*` variants/keyframes, one `style-<skin>.css`); generate/patch `Styles/app.css` (imports `tailwindcss` + `tw-animate-css` + the managed files, `@source` globs over the output dir, dark variant). Idempotent: a Blaizio-owned input is regenerated (stale skin pruned); a user-authored one is only topped up with missing directives. CSS embedded in the CLI → offline.
5. `_Imports.razor` += `@using <namespace>`.
6. register Base JS/CSS from `_content/Blaizio.Base/`.
7. if `[components...]` passed, chain into `add`.

## `blaizio add`

```
blaizio add [components...]

-a, --all             add every registry component
-o, --overwrite       overwrite existing files
    --path <dir>      dest override (else config output)
-ns, --namespace <ns> namespace override (else config)
    --dry-run         resolve + print plan, write nothing
    --diff [name]     upstream vs local diff
    --view [name]     print file contents, no write
    --no-deps         skip NuGet + registryDependencies
```

## Tailwind pipeline commands

Tailwind v4 config is CSS-first, so the input `init` writes is **universal** — every pipeline compiles the same file. Compilation is a pluggable provider (`ITailwindPipeline`): detect + setup + build-hint. Detect-first, never clobber.

```
blaizio tailwind detect              which pipelines are present + recommendation
blaizio tailwind setup --mode <id>   wire a pipeline (auto|standalone|node|vite|postcss|none)
blaizio tailwind fetch               fetch the standalone binary (stub)
```

Providers: `standalone` (native binary + MSBuild target, zero Node — the auto default when nothing is found; the target **auto-downloads** the binary on first `dotnet build` via MSBuild `DownloadFile`, opt out with `BlaizioTailwindAutoFetch=false`, pin with `BlaizioTailwindVersion`), `node` (`@tailwindcss/cli`, PM by lockfile), `vite`/`postcss` (detect-and-report; add the plugin to the existing bundler), `none` (input only). `init --tailwind <mode>` (default auto) runs setup after writing the CSS. `blaizio tailwind fetch` pre-fetches the binary for CI/offline.

tw-animate-css is **vendored** into `Styles/blaizio/animate.css` (imported locally, not by package name) so every pipeline — including the Node-free standalone — resolves it. `--pointer` writes a cursor rule into `Styles/blaizio/options.css`; `--rtl` records the flag and prints the `dir="rtl"`/`BlazeDirectionProvider` step (skins already mirror via logical properties + `:dir()`).

## Maintainer / query commands

```
blaizio generate [source] -o <path>      scan Blaizio.Ui -> registry.json manifest
blaizio build [registry.json] -o <dir>   compile manifest -> per-item /r/*.json + index
blaizio list [-q query] [-l limit] [-o offset]
blaizio search <query>
blaizio view <name>
blaizio diff [name]                       local vs upstream
blaizio update [components...]            re-pull newer registry versions
blaizio info [--json]                     project + config + versions
blaizio migrate <rtl|icons> [path]
```

### The registry (`generate` + `build`)

`generate` scans `src/Blaizio.Ui` into a `registry.json` manifest (committed with the source): one item per component family folder + a shared `utils` lib item. Cross-component dependencies are **inferred** by finding which other families' `Bz*` types each family references (so `sidebar` pulls `button`, `sheet`, `tooltip`, … and `alert-dialog` pulls `button` + `dialog`). `build` then inlines file content into per-item JSON + `index.json` for hosting at `blaiz.io/r`. Current manifest: 56 items, 335 files. Since components use both the styled layer and the headless Base primitives, `add` writes `@using <ns>` **and** `@using Blaizio` into `_Imports.razor`, plus a project-wide `global using Blaizio;` (`Blaizio.GlobalUsings.g.cs`) so copied `.cs` files — which no longer nest under `Blaizio` after the rewrite and don't see `_Imports` — still resolve the Base/Icons types. The app-wide DI registration (`ServiceCollectionExtensions.cs`) is excluded from `utils` (it references component services, so it's optional glue, not a leaf helper).

**Compile-verified:** real components (button/card/alert/… and the JS-interop dialog + alert-dialog set) copied via the CLI compile in a Blazor consumer that ProjectReferences Base/Icons — namespace rewrite, global using, and transitive resolve all produce building code. Caveat: copied components assume the consumer has the standard Blazor `_Imports` usings (`Microsoft.AspNetCore.Components.Web`, etc.), which `dotnet new blazor` apps ship but bare class libraries don't.

### Hosting

The built per-item JSON is served as static files from the docs site's `wwwroot/r`, so the docs origin answers `/r/index.json` and `/r/<name>.json` — that's `https://blaiz.io/r` in production, which is already the default `registry` in `blaizio.json` (zero-config once deployed). `wwwroot/r` is generated (gitignored). Regenerate with `scripts/build-registry.{sh,ps1}` (CLI → `generate` → `build` → `wwwroot/r`); CI runs it, or `dotnet build docs/Blaizio.Docs -p:BuildBlaizioRegistry=true` regenerates via an opt-in MSBuild target before publish. HTTP resolution verified end-to-end (`list`, transitive `add` over an HTTP-served `/r`).

---

# Interactive experience

`blaizio` or `blaizio init` with no flags → full TUI. Any flag, or `--yes`/`--json`, → non-interactive (CI, scripts, plugins safe).

Library: **Spectre.Console** — `SelectionPrompt` (radio), `MultiSelectionPrompt` (checkbox), arrow/enter/esc keyboard, `Status`/`Progress` spinners, colored markup.

`init` flow:

```
◆ Blaizio  v1.0
│
◇ Project template?            SelectionPrompt (radio, arrows/enter)
│  ● Showcase — full demo app (dashboard, auth, forms, data table)
│  ○ Blazor Web App (Server/WASM/Auto)
│  ○ WASM standalone
│  ○ Class library (components only)
│
◇ Root namespace?              TextPrompt, default MyApp.Components.Ui
◇ Output directory?            TextPrompt, default Components/Ui
◇ Starting theme?              SelectionPrompt
◇ Add components now?          MultiSelectionPrompt (checkbox, space toggle)
│  ◻ button  ◻ dialog  ◻ table ...
◇ RTL support?                 ConfirmationPrompt (y/n)
│
◆ Plan (review before write)
```

- `esc` = back / cancel.
- Colors: cyan headings, green done, yellow warn, red error.
- Progress spinners for slow steps:

```
⠋ Restoring NuGet packages...
⠙ Fetching button, dialog, table from registry...
✔ Copied 7 files, rewrote namespace, updated _Imports.razor
```

- `add` shows a per-component progress bar while resolving transitive deps.

---

# Templates

## Showcase (`-t showcase`) — the flagship

Not an empty starter. A full, practical, `dotnet`-runnable Blazor Web App proving Blaizio's range. Every showcased component is **copied in via `add`** (the template dogfoods the CLI), not referenced.

- **Shell** — responsive sidebar + topbar, theme switcher, RTL toggle, command palette (`Command`).
- **Dashboard** — cards, charts area, `Table` (sort/filter/paginate), `Tabs`.
- **Forms** — `Form` + validation with every input (Input, Textarea, Select, Combobox, Checkbox, RadioGroup, Switch, Slider, Calendar/DatePicker).
- **Overlays** — Dialog, AlertDialog, Sheet, Popover, Tooltip, DropdownMenu, Toast.
- **Data** — Tree (drag/drop), Sortable, Accordion, Carousel.
- **Auth** — login/register pages (Form + layout), fake state.
- Seeded mock data + service layer → runs immediately, no backend.

Registry item `type: registry:template`. `init` scaffolds the shell; `add` fills components.

## Other templates

- **Blazor Web App** — Server / WASM / Auto interactivity.
- **WASM standalone**.
- **Class library** — components only, no host.

---

# Installation

`dotnet tool`, three entry points:

**No-install one-shot** (lead with this — net10 `dnx`):
```
dnx Blaizio.Cli -- init
```

**Global:**
```
dotnet tool install -g Blaizio.Cli
blaizio init
blaizio add button
```

**Local manifest** (team, committed):
```
dotnet new tool-manifest
dotnet tool install Blaizio.Cli
dotnet blaizio add button
```

Prereq: .NET 10 SDK.

---

# IDE plugins

Principle: don't reimplement the resolver 3×. Two tiers:

- **Tier A** — plugin shells out to `blaizio --json`, parses, renders UI. Works everywhere.
- **Tier B** — .NET IDEs reference `Blaizio.Cli.Core` directly; richer UX.

| IDE | Tech | Tier | Publish |
|---|---|---|---|
| **VSCode** | TS extension | A (shell out `dotnet blaizio --json`) | VS Marketplace + OpenVSX |
| **Visual Studio** | VSIX (C#) | B (ref Core directly) | VS Marketplace |
| **Rider / JetBrains** | IntelliJ plugin (Kotlin) | A (shell out JSON) | JetBrains Marketplace |

Each plugin: registry tree/gallery view, project + `blaizio.json` detection, "Add Blaizio Component" action, `init` command.

**VSCode ships first** — biggest Blazor audience, lowest effort.

## Bonus — MCP server

`blaizio mcp` exposing `list` / `add` over the existing JSON lets Claude Code / Copilot add components conversationally. Cheap once Core exists.

---

# Build order

1. `Blaizio.Cli.Core` — resolver, rewriter, registry client, config (all `--json`-serializable).
2. `Blaizio.Cli` — Spectre front-end.
3. `blaizio build` — registry compiler.
4. Showcase template.
5. IDE plugins — VSCode → Visual Studio → Rider.
6. MCP server.
