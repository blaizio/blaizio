# Blaizio CLI — Plan

> Working design doc for `Blaizio.Cli` and the IDE plugin story.
> Domain: **https://blaiz.io** · Registry: **https://blaiz.io/r**

## Model

Components ship as **source**, copied into the consumer project by the CLI — not referenced as a package.

- **`Blaizio.Base`** — headless behavior + ARIA + `data-*` contract + JS assets. Distributed as **NuGet**. Namespace `Blaizio.*`, never rewritten. JS served from `_content/blaizio.base/dist/`.
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
| component JS | ships via `_content/blaizio.base/dist/` from NuGet — `add` copies **no** JS |
| theming | light/dark only (`:root` + `.dark` token sets; 'system' resolves at runtime) |

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
    --registry <url>  registry base URL or local path (overrides blaizio.json)
-h, --help
    --version
```

`--json` on every command is the seam IDE plugins and the MCP server ride on. Output contract:
stdout in `--json` mode is exactly one JSON document; human text is muted by `--silent`;
warnings/diagnostics/errors go to stderr. Exit codes: 0 ok, 1 error (or `diff` drift),
2 registry error, 130 Ctrl+C.

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
    --style <name>    component style (skin): ash/aura/ember/flint/forge/glow/spark/wisp (default ember)
    --reinstall       re-copy existing components
-p, --preset [name]   PLACEHOLDER — parsed, prints "coming soon", no-op
```

Steps:
1. detect/scaffold `.csproj` (net10, `Microsoft.NET.Sdk.Razor`).
2. `dotnet add package Blaizio.Base Blaizio.Icons TailwindMerge.NET`.
3. write `blaizio.json`.
4. Tailwind v4 — write managed CSS under `Styles/blaizio/` (`theme.css` tokens + `@theme` map, `base.css` contract/`data-*` variants/keyframes, `shared.css` common skin layer, one `style-<skin>.css` with that skin's differences); generate/patch `Styles/app.css` (imports `tailwindcss` + `tw-animate-css` + the managed files, `@source` globs over the output dir, dark variant). Idempotent: a Blaizio-owned input is regenerated (stale skin pruned); a user-authored one is only topped up with missing directives. CSS embedded in the CLI → offline.
5. `_Imports.razor` += `@using <namespace>`.
6. register Base JS/CSS from `_content/blaizio.base/`.
7. if `[components...]` passed, chain into `add`.

## `blaizio add`

```
blaizio add [components...]

-a, --all             add every registry component
    --overwrite       overwrite existing files
-o, --output <dir>    dest override (else config output; same -o as init)
-ns, --namespace <ns> namespace override (else config)
    --dry-run         resolve + print plan, write nothing
    --no-deps         skip NuGet + registryDependencies
    --no-nuget        skip NuGet only, keep registry deps (ProjectReference setups)
```

Every non-dry `add` records the item under `installed` in `blaizio.json` (name → files written,
POSIX paths) — the record `update` (no args) re-pulls and `diff` compares upstream.

## Tailwind pipeline commands

Tailwind v4 config is CSS-first, so the input `init` writes is **universal** — every pipeline compiles the same file. Compilation is a pluggable provider (`ITailwindPipeline`): detect + setup + build-hint. Detect-first, never clobber.

```
blaizio tailwind detect              which pipelines are present + recommendation
blaizio tailwind setup --mode <id>   wire a pipeline (auto|standalone|node|vite|rollup|postcss|none)
blaizio tailwind fetch               fetch the standalone binary (stub)
```

`auto` prefers Present > Partial (in preference order vite > rollup > postcss > node > standalone) > standalone default. A *partial* bundler (config present, Tailwind plugin missing) wins auto and surfaces its manual step — auto never wires standalone over a bundler the project owns. NuGet installs (init/add/upgrade) report per-package progress (`Installing Blaizio.Base (1/3)...`).

Providers: `standalone` (native binary + MSBuild target, zero Node — the auto default when nothing is found; the target **auto-downloads** the binary on first `dotnet build` via MSBuild `DownloadFile`, opt out with `BlaizioTailwindAutoFetch=false`, pin with `BlaizioTailwindVersion`), `node` (`@tailwindcss/cli`, PM by lockfile), `vite`/`rollup`/`postcss` (detect-and-report; add the plugin to the existing bundler), `none` (input only). `init --tailwind <mode>` (default auto) runs setup after writing the CSS. `blaizio tailwind fetch` pre-fetches the binary for CI/offline.

tw-animate-css is **vendored** into `Styles/blaizio/animate.css` (imported locally, not by package name) so every pipeline — including the Node-free standalone — resolves it. `--pointer` writes a cursor rule into `Styles/blaizio/options.css`; `--rtl` records the flag and prints the `dir="rtl"`/`BlazeDirectionProvider` step (skins already mirror via logical properties + `:dir()`).

## Maintainer / query commands

```
blaizio generate [source] -o <path>      scan Blaizio.Ui -> registry.json manifest
blaizio build [registry.json] -o <dir>   compile manifest -> per-item /r/*.json + index
blaizio list [-q query] [-l limit] [--offset n]
blaizio search <query>
blaizio view <name>
blaizio diff [name]                       local vs upstream (exit 1 on drift; default: all installed)
blaizio update [components...]            re-pull, overwriting local copies (default: all installed)
blaizio upgrade                           bump Blaizio.Base/Icons/TailwindMerge to the tool's pinned
                                          versions, then re-pull all installed components
blaizio deinit [--dry-run]                inverse of init: remove blaizio.json, Styles/blaizio,
                                          a Blaizio-owned Styles/app.css, .blaizio targets + csproj
                                          import, host-page wiring. Components/usings/packages stay.
                                          Confirms first (-y skips); --dry-run previews.
blaizio info [--json]                     project + config + versions
```

`update` = source sync only; `upgrade` = package version bump + source sync (prints the
`dotnet tool update -g Blaizio.Cli` hint for the tool itself). A `migrate` codemod command was
considered and dropped — nothing to migrate from pre-1.0; revisit at the first breaking change.

```
```

### The registry (`generate` + `build`)

`generate` scans `src/Blaizio.Ui` into a `registry.json` manifest (committed with the source): one item per component family folder + a shared `utils` lib item. Cross-component dependencies are **inferred** by finding which other families' `Bz*` types each family references (so `sidebar` pulls `button`, `sheet`, `tooltip`, … and `alert-dialog` pulls `button` + `dialog`). `build` then inlines file content into per-item JSON + `index.json` for hosting at `blaiz.io/r`. Current manifest: 56 items, 335 files. Since components use both the styled layer and the headless Base primitives, `add` writes `@using <ns>` **and** `@using Blaizio` into `_Imports.razor`, plus a project-wide `global using Blaizio;` (`Blaizio.GlobalUsings.g.cs`) so copied `.cs` files — which no longer nest under `Blaizio` after the rewrite and don't see `_Imports` — still resolve the Base/Icons types. The app-wide DI registration (`ServiceCollectionExtensions.cs`) is excluded from `utils` (it references component services, so it's optional glue, not a leaf helper).

**Compile-verified:** real components (button/card/alert/… and the JS-interop dialog + alert-dialog set) copied via the CLI compile in a Blazor consumer that ProjectReferences Base/Icons — namespace rewrite, global using, and transitive resolve all produce building code. Caveat: copied components assume the consumer has the standard Blazor `_Imports` usings (`Microsoft.AspNetCore.Components.Web`, etc.), which `dotnet new blazor` apps ship but bare class libraries don't.

### The docs site is a consumer

The docs project carries **no ProjectReference** to Blaizio.Ui/Base/Icons. It consumes the product
the way every user does: Blaizio.Base + Blaizio.Icons as NuGet packages (from `artifacts/local-nuget`
via the repo `nuget.config` until published), and the styled components as CLI-copied source under
`docs/Blaizio.Docs/Components/Ui` (gitignored, namespace pinned to `Blaizio.Ui` by the committed
`docs/Blaizio.Docs/blaizio.json` so no docs page changes). The `BlaizioConsumerPrepare` MSBuild
target automates the chain on every build — pack (if missing) → CLI → registry into `wwwroot/r` →
`add --all`. After editing component source: rebuild with `-p:BlaizioRefresh=true` (re-copy +
registry); `-p:BlaizioRepack=true` additionally repacks Base/Icons and purges their NuGet cache.

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

Templates ship as files embedded in the CLI (flat names encode the destination path with `__` for `/` and `~` for the extension dot, so no template file carries a real `.cs`/`.razor` extension the SDK would claim and drop). `TemplateScaffolder` writes them with `{{RootNamespace}}`/`{{ComponentNamespace}}`/`{{ProjectName}}`/`{{Skin}}` substituted, skipping existing files unless `--force`. `init -t showcase` scaffolds a runnable Blazor WASM app (writes the csproj when absent), wires Tailwind, and adds the demo's component set. `init --registry <url>` points the config at a custom/local registry. **Verified:** the scaffolded app compiles (WASM SDK + local Base/Icons) and its Tailwind CSS builds.

## Showcase (`-t showcase`) — the flagship

Not an empty starter. A full, practical, `dotnet`-runnable Blazor WASM app proving Blaizio's range. Every showcased component is **copied in via `add`** (the template exercises the CLI's own pipeline), not referenced. **Implemented + compile-verified** (app builds with 0 errors against local Base/Icons refs; Tailwind CSS compiles over all pages).

- **Shell** — responsive sidebar (mobile `Sheet`), topbar, dark-mode + RTL toggles, `BzCommandDialog` command palette (mod+k via `BzKbd`), `BzToastProvider` root.
- **Dashboard** (`/`) — 4 stat cards, `Tabs` (Overview/Activity/Team), `Table` with mock orders + footer total, `Avatar`+`Progress` activity list, `Skeleton` loading demo.
- **Forms** (`/forms`) — EditForm + DataAnnotations over every input: InputText/Number/Date, Select, Combobox, Checkbox, RadioGroup, Switch, Slider, all in `Field` wrappers.
- **Overlays** (`/overlays`) — Dialog, AlertDialog, Sheet, Popover, Tooltip, DropdownMenu (checkbox items + PreventDefault), Toast (service-driven; `AddBlaizio()` in Program registers the imperative services - the toast/dialog services live in Blaizio.Base, so no extra registration ships through the registry).
- **Data** (`/data`) — Accordion, Collapsible, Tree (selector-based), Carousel with dots.
- **Auth** (`/login`, `/register`) — card forms with validation, fake static `AuthState`.
- `Data/DemoData.cs` mock records → runs immediately, no backend.

Tailwind note: the generated `Styles/app.css` uses `@import "tailwindcss" source(none)` + explicit `@source` globs (components dir + project-wide `../**/*.razor`) — auto-detection would walk `bin`/`obj` binaries and crash the v4 scanner.

## Other templates

- **Blazor Web App** — Server / WASM / Auto interactivity.
- **WASM standalone**.
- **Class library** — components only, no host. `init -t library` scaffolds a Razor-SDK csproj
  (FrameworkReference Microsoft.AspNetCore.App, ImplicitUsings, pinned Blaizio packages) and seeds
  the standard Blazor `_Imports`. A pre-existing bare `Microsoft.NET.Sdk` csproj is hardened in
  place by `init` (SDK swap + framework ref + implicit usings, format-preserving); `add` warns when
  it detects an un-hardened bare lib. **Compile-verified** with copied components + local project refs.

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
