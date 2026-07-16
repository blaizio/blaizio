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

## `blaizio new` (alias `create`)

```
blaizio new [template]           showcase | webapp | wasm | library (prompted when omitted)

-n, --name <name>     new project name (default: the directory name)
-d, --defaults        template=showcase, no prompts
+ every styling/wiring option init takes (--style, -p, --namespace, -o, --tailwind, --rtl, --pointer, -f)
```

Scaffolds the app, then runs the init pipeline over it and adds the template's component set.
The split exists so each verb means one thing: `new` starts an app, `init` wires the app you
already have, `add` grabs components (bootstrapping init when needed). Template/name reach
`init` only programmatically - no `-t`/`-n` flags exist on it.

## `blaizio init`

```
blaizio init [components...]     existing app only - never scaffolds

-ns, --namespace <ns> root namespace for copied components
-o, --output <dir>    component output dir (default: Components/Ui)
-f, --force           overwrite existing blaizio.json
-d, --defaults        no prompts
    --rtl             wire BlazeDirectionProvider RTL
    --pointer         cursor-pointer on buttons
    --style <name>    component style (skin): ash/aura/ember/flint/forge/glow/spark/wisp (default ember)
-p, --preset [name]   color preset name or /create code
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

Templates ship as files embedded in the CLI (flat names encode the destination path with `__` for `/` and `~` for the extension dot, so no template file carries a real `.cs`/`.razor` extension the SDK would claim and drop). `TemplateScaffolder` writes them with `{{RootNamespace}}`/`{{ComponentNamespace}}`/`{{ProjectName}}`/`{{Skin}}` substituted, skipping existing files unless `--force`. `blaizio new showcase` scaffolds a runnable Blazor WASM app (writes the csproj when absent), wires Tailwind, and adds the demo's component set. `init`/`new` `--registry <url>` points the config at a custom/local registry. **Verified:** the scaffolded app compiles (WASM SDK + local Base/Icons) and its Tailwind CSS builds.

## Showcase (`new showcase`) — the flagship

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
- **Class library** — components only, no host. `blaizio new library` scaffolds a Razor-SDK csproj
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

---
# CSS layout v3 — inlined component classes, one tokens file

Supersedes the v2 two-file plan after studying the shadcn CLI source (`apply` = destructive
component re-install from per-style registry sources; install-time AST transformers; plumbing via
npm package + `eject`). v3 keeps v2's tokens-file work and goes further: the skin CSS disappears
from consumer repos entirely — the look ships **inlined into each component's classes**.

## End state (consumer repo)

```
blaizio.json          config
Styles/app.css        Tailwind input + the user's theme values ("the tokens file")
Components/Ui/…       self-contained components - all classes inline
.blaizio/             materialized at build from Blaizio.Base, gitignored (like obj/)
```

Gone vs v1: `theme.css`, `preset-*.css`, `fonts.css`, `tokens.css`, `options.css`, `shared.css`,
`style-*.css`. The one CSS file left is the one no Tailwind project can not have.

## Source of truth

- **Maintainer (this repo): unchanged.** Components authored once with `bz-*` tokens;
  `shared.css` baseline + 8 `style-*.css` skins. `blaizio build` compiles them together.
- **Consumer: their component files + their tokens file.** Skin sheets never exist for them.
  - Theme-wide change (colors/radius/fonts/chart) → tokens file, one place.
  - One component's look → that component's file, one place.
  - Skin swap → CLI re-install from the registry (destructive, warned) — never hand-editing.
  - Trade accepted (same as the ecosystem): cross-cutting STRUCTURAL tweaks that aren't
    token-shaped become per-component edits; shared.css as a consumer surface is gone.

## The three pieces

### 1. Tokens file — user-owned

Path recorded in `blaizio.json "css"` (any name/place; `Styles/app.css` only as the scaffold
default). Contains: `@import "tailwindcss"` + `@source` globs (scaffolded case), one import of the
materialized contract, `@custom-variant dark`, `:root`/`.dark` values (preset palette, chart,
radius, `--font-heading` baked as plain editable values, comment-free), the `@theme inline`
token→utility map, `@layer base` (body colors, `html { font-family }`, pointer rule).

After init the CLI touches it only surgically: keep the import line(s) in sync, patch token
values in place (`SetDeclaration`: replace `--x: …;` or append into the block). Never rewrites.

### 2. Contract sheet — materialized, not committed

`.blaizio/blaizio.css`: the small static remainder — `data-*` `@custom-variant`s, accordion/
collapsible/shimmer keyframes + animation vars (with the `--tw-animation-fill-mode` hook),
vendored tw-animate, reduced-motion gate, `scrollbar-thin`.

Delivery: **content + `buildTransitive` MSBuild targets inside Blaizio.Base** (no new package) —
an incremental copy into `.blaizio/` before the Tailwind step. `.gitignore`d; regenerated every
build; a style-plumbing update is just a Base package bump. Known caveats, doc-grade: fresh clone
must `dotnet build` once before an external `tailwindcss --watch`; node-only CI stages need a
dotnet build first (npm mirror package possible later for bundler-mode parity).

### 3. Components — classes inline

Shipped source carries the merged shared+skin utilities directly; `data-slot`/`data-variant`
attributes stay (cross-component variants like `in-data-[slot=button-group]:rounded-md` depend on
them); `bz-*` classes vanish from output.

## The inliner (`blaizio build`)

Registry-build-time compiler, not an install-time transform:

1. Parse `shared.css` + `style-<skin>.css`; extract each `.bz-*` selector's `@apply` list.
2. Merge baseline + skin per token with TailwindMerge.NET (same semantics as runtime `Tw.Merge`).
3. Substitute every `bz-*` token across ALL shipped source strings (`.razor` markup and `.cs`
   class builders) with the resolved utilities.
4. Emit per-skin artifacts: `r/{skin}/<item>.json` × 8 skins; index carries the skin list.

Prerequisites:
- **Skin audit:** every shared/skin rule must be element-attached-expressible (`.dark …` → `dark:`,
  `[data-state=open]` → `data-open:`, `::before` → `before:`, descendant `.bz-x .bz-y` rules →
  `data-[slot=…]` variants). Anything animation/keyframe-shaped moves to the contract sheet.
- **Authoring refactor:** interpolated class construction
  (`$"bz-button-variant-{Variant.GetDescription()}"`) cannot be substituted — variant-bearing
  components switch to enumerable literals
  (`Variant switch { Default => "bz-button-variant-default", … }`).
- **Golden-file tests:** every component × every skin, snapshot the substituted source, diff on
  registry build. The inliner is a compiler; this is its test suite.

## Per-command (v3)

| Command | Behavior |
|---|---|
| `init` (fresh) | Scaffold tokens file (preset/code overlays baked); host wiring = stylesheet link, boot.js, `.dark` only (`style-*`/`preset-*` classes are dead); record `css`/`cssCreated`/theme/preset/rtl/heading/font/chart/radius; gitignore `.blaizio/`. |
| `init` (adopt/top-up) | Discover or take `--css`; inject imports + token block if absent; presence of the contract import = initialized. |
| `add` (components) | Fetch the recorded skin's variant from `r/{skin}/`; namespace rewrite; ledger; NuGet pins keep Base (contract/JS) in sync with component expectations. |
| `add font-*` | Record heading/body half; patch `--font-heading` / `html { font-family }` in the tokens file; host font link. |
| `add --css <path>` | Re-point config; ensure imports at the new input; warn if no token block detected. |
| `apply --only theme/tokens/fonts` | Non-destructive value patches in the tokens file (palette / chart+radius / fonts). |
| `apply` (full, or a code carrying a style change) | Destructive leg: warn ("overwrites components — commit or stash"), re-install every LEDGERED component from the new skin's registry variant (our ledger beats their filename scan), then patch tokens. `add --diff` previews. |
| `update` | Re-pulls components (current skin) as today; NO styling leg left — plumbing tracks the Base package version. Runs the v1→v3 migration when it sees `Styles/blaizio/`. |
| `uninstall` | By ledger: components, packages, `@using`s, host wiring, imports stripped from the tokens file, the file itself only when `cssCreated: true`; `.blaizio/` + gitignore entry removed. |
| `tailwind build/watch` | `-i` defaults from `blaizio.json "css"`. |
| `preset resolve` | Unchanged (full round-trip already). |
| `eject` | See below. |

## `blaizio eject`

Copies the materialized contract INTO the tokens file, deletes the contract import, sets
`"ejected": true` (update/doctor stop expecting the materialization). Irreversible-warned, `-y`
gated, exactly one job. Motive differs from shadcn's: it removes no dependency (Base stays for
behavior/JS) — it exists to FREEZE and own the plumbing, and to make ".blaizio/ is generated,
don't touch" defensible: don't like generated? Eject and own it. Ship last; one-afternoon command.

## Migration v1 → v3 (in `update`, confirm-gated; `-y` accepts)

1. Compose the tokens file: merge `preset-*.css` values over `theme.css` (scope rewritten to
   `:root`/`.dark`), fold in `fonts.css` (`--font-heading`, `html font-family`), `options.css`
   (pointer), baked chart/radius (already in theme.css). Inject into the recorded input.
2. Re-install all ledgered components from `r/{skin}/` (inlined classes) — same warn+stash gate
   as `apply`.
3. Delete `Styles/blaizio/`; strip old `./blaizio/*` imports; gitignore `.blaizio/`; record any
   missing selections in blaizio.json.

## Docs impact

- Component pages / get-code: source views become skin-aware (source shown per selected style).
- Theme tab already emits the merged comment-free `:root`/`.dark` — pastes straight into the
  tokens file.
- Installation/Theming/CLI pages rewritten to the v3 story ("your components + your tokens file").
- /create donut keeps its docs-side scoped preset sheets (unaffected).

## Build order

1. Skin audit (inline-expressibility inventory; move stragglers to the contract).
2. Authoring refactor: enumerable variant literals across variant-bearing components.
3. Inliner in the registry compiler + golden-file suite.
4. Per-skin registry output (`r/{skin}/`) + index.
5. Blaizio.Base: contract content + `buildTransitive` targets (materialization).
6. CLI: init/add/apply/update/uninstall/tailwind per the table; `cssCreated`/`ejected` in config.
7. v1→v3 migration leg + fixtures.
8. Docs (skin-aware source views, page prose).
9. `eject`.
10. Dogfood: migrate DMSign.Web.
