# Changelog

All notable changes to the Blaizio packages (`Blaizio.Base`, `Blaizio.Icons`, `Blaizio.Cli`) and
the registry-distributed `Blaizio.Ui` source. Format loosely follows
[Keep a Changelog](https://keepachangelog.com); versions are lockstep across packages while
pre-release.

## [Unreleased]

## 0.1.0-alpha.6 — 2026-07-18

### Fixed
- **Base**: the `.blaizio/` contract now materializes **before** the CLI-wired Tailwind compile.
  Both hook `BeforeBuild`, but the Tailwind target lives in the project file and package targets
  import after it, so on a fresh clone or worktree the Tailwind compile ran first and failed on
  the missing `.blaizio/` imports before the contract could materialize. The materialize target
  now also declares itself before `BlaizioTailwindFetch`/`BlaizioTailwindBuild` - a fresh checkout
  heals on its first `dotnet build`, no manual copying.
- **Ui**: `BzPieChart`, `BzRadarChart`, and `BzRadialBarChart` tooltips gained the edge-aware
  placement `BzChart` already had - near the chart's start/end the panel hugs that edge instead
  of centring, and with no room above it flips below the anchor - so cards that clip overflow no
  longer cut the tooltip. The single-row pie/radial panels also dropped their forced minimum
  width so they fit beside small charts.

### Added
- **Base**: opt-in theme-scope portal frames. An ancestor with
  `data-bz-portal-frame="some classes"` makes its portaled surfaces re-home into a shared
  `<body>`-level `display: contents` container carrying those classes instead of bare `<body>`,
  so ancestry-scoped CSS (theme pins, scoped skins) survives the portal move. The frame is
  created on demand and removed when its last surface leaves; nested portals resolve to the same
  frame.

### Changed — floating surfaces portal to the body
- **Base**: every floating surface — tooltip, popover, hover card, dropdown menu, context menu,
  menubar menu, select, combobox, and the declarative dialog/alert-dialog/sheet/drawer content and
  overlay — now **moves itself to `document.body` while open** and returns to its place in the DOM
  before unmounting. A surface declared inside an ancestor that creates a stacking context
  (`position: fixed`/`sticky` with a z-index, `transform`, `filter`, `backdrop-blur`,
  `container-type`, `opacity` below 1) or an overflow clip can no longer be painted over or cut at
  that ancestor's edge. Positioning, focus, dismissal, and animations are unchanged; submenus stay
  inside their (portaled) parent surface. No setup: upgrade `Blaizio.Base` and re-pull components.
- **Breaking (styling)**: because the open surface is a `<body>`-level node, **ancestor CSS
  selectors no longer reach it** (e.g. `.my-panel .bz-tooltip-content { ... }` or a parent's
  `[data-theme]` scope). Style floating content through its own classes/attributes, target
  `:root`, or set `Inline` (below) to restore in-place rendering. Theme, skin, and dark classes on
  `<html>` are unaffected.

### Added
- **Base/Ui**: `Inline` parameter (default `false`) on every floating content component —
  `BzTooltipContent`, `BzPopoverContent`, `BzHoverCardContent`, `BzDropdownMenuContent`,
  `BzContextMenuContent`, `BzMenubarContent`, `BzSelectContent`, `BzComboboxContent`,
  `BzDialogContent`, `BzDialogOverlay`, `BzAlertDialogContent`, `BzSheetContent`,
  `BzDrawerContent` (and their `Base*` counterparts). `Inline="true"` renders in place (today's
  pre-alpha.5 behavior) — for CSS-containment parents, print, or tests that assert on local markup.
- **Base**: a portaled surface stamps its own `dir` attribute from the cascaded
  `BzDirectionProvider` direction, so a subtree-scoped RTL keeps applying to body-level surfaces.

## 0.1.0-alpha.4 — 2026-07-16

### Changed — CSS layout v3
- **Registry/Ui**: component classes are now **inlined per skin** at registry build — `blaizio
  build` merges the shared + skin `@apply` lists (TailwindMerge semantics) and substitutes every
  `bz-*` token in the shipped `.razor`/`.cs` source, emitting per-skin variants under
  `r/{style}/`. No skin stylesheet ships to consumers; `bz-*` classes are gone from output.
- **Base**: ships the static contract (`blaizio.css` — `data-*` variants, keyframes,
  chart/toast machinery — plus the vendored `animate.css`) and `buildTransitive` MSBuild targets
  that **materialize them into the consumer's gitignored `.blaizio/`** before every build.
  Opt out with `<BlaizioMaterializeContract>false</...>`, redirect with `<BlaizioContractDir>`.
- **CLI**: v3 layout throughout — `init` scaffolds ONE user-owned tokens file
  (`Styles/app.css`: Tailwind input, `:root`/`.dark` values with preset/chart/radius/fonts baked
  as plain editable values, `@theme inline` map) and only ever patches it surgically afterwards.
  `add` pulls the recorded skin's inlined variants; a full `apply` (skin swap) re-installs the
  ledgered components (confirm-gated); `update` has no styling leg left; `uninstall` strips by
  record. New config keys: `css`, `cssCreated`, `ejected`.
- **CLI**: `update` runs the confirm-gated **v1 → v3 migration** when it sees the old
  `Styles/blaizio/` layout: components re-install from the skin's inlined variants first, the
  tokens file is composed from the project's v1 sheets (user values survive; preset/fonts/pointer
  folded in), then `Styles/blaizio/` is deleted.
- **Ui**: `--primary-button` retired — the default button's dark fill derives from `--primary`
  via a `color-mix` formula (WCAG AA-verified across presets).
- **Docs**: v3 story (Installation/Theming/CLI rewritten), per-skin Source view on component
  pages, Get Code dialog emits the merged comment-free token block.

### Added
- **CLI**: `blaizio eject` — copies the materialized contract into the tokens file, drops the
  `.blaizio/` imports and sets `"ejected": true`; the styling plumbing is frozen and yours.
  Confirm-gated, irreversible, clean no-op on a second run.
- **CLI**: `blaizio contrast` — WCAG AA audit of the tokens file's color pairs (light + dark,
  focus ring at 3:1, the derived dark button fill); exits `1` on failures, `--json` for CI.

### Changed
- **CLI**: a `/create` code's chart palette and radius now bake directly into the tokens file's
  `:root` (exact-declaration rewrite). The selection is recorded in `blaizio.json` (`chart`,
  `radius`) so `update`/`apply` re-runs keep it. `apply --only tokens` patches values in place.
- **CLI**: `preset resolve` round-trips the full `/create` code, including fonts, chart and
  radius.

### Fixed
- **Ui**: accordion/collapsible close blink — the `@theme` animation shorthands in `blaizio.css`
  dropped the `--tw-animation-fill-mode` hook, so the exit animation ran with `fill-mode: none`
  and the closed panel snapped back to full height until the unmount landed. The shorthands now
  carry `var(--tw-animation-fill-mode, none)`, letting the `[data-state='closed']` forwards pin
  hold the final frame.

## 0.1.0-alpha.3 — 2026-07-14

### Fixed
- **Base**: collapsible/accordion panels re-measure their content height while open
  (`MutationObserver` + window-resize, rAF-throttled) — content that grew after opening
  (async loads, expanding composers) no longer causes the close animation to start from a stale
  height. The measurement also lifts the inner wrapper's height pin first so fresh content can't
  report the old value back.

### Changed
- **CLI**: tool bumped in lockstep; installs pin `Blaizio.Base`/`Blaizio.Icons` `0.1.0-alpha.3`.

## 0.1.0-alpha.2 — 2026-07-11

### Fixed
- **Ui/Base**: floating-surface close blink (dialogs, dropdowns, popovers) — exit animations now
  hold their final frame via `[data-state='closed'] { --tw-animation-fill-mode: forwards; }` in
  `shared.css`, so a slow unmount round-trip can no longer flash the surface back to visible.

### Added
- **CLI**: `apply [preset]` (preset name or `/create` code, `--only theme,fonts,tokens`);
  `preset decode/resolve/url/open`; `registry add/validate` with `@namespace/component`
  resolution; `docs <components...>`; commander-style help (`BlaizioHelpProvider`); `add`
  absorbed `--update/--upgrade/--diff/--view`; `search` absorbed `list`.
- **CLI**: `uninstall` (formerly `deinit`) is undo-by-record — components, NuGet packages,
  `@using`s and config are removed exactly as tracked in `blaizio.json`; user files survive.

## 0.1.0-alpha.1 — 2026-07

Initial pre-release.

- **Blaizio.Base**: headless primitives (behavior, ARIA, keyboard, `data-state` contract) with
  TypeScript interop shipped as `_content/Blaizio.Base/dist/*` — consumers copy no JS.
- **Blaizio.Icons**: Tabler icons as a tree-shakeable SVG component.
- **Blaizio.Ui**: 61 styled Tailwind v4 components, 8 skins, 9 color palettes, RTL via logical
  properties, light/dark token model — distributed as source through the registry.
- **Blaizio.Cli**: `init` (templates incl. Showcase, Tailwind v4 wiring with standalone-binary
  auto-fetch or bundler detection, host-page patching), `add` (transitive registry dependencies,
  namespace rewrite, install ledger), `search/view/info/docs`, `build` (registry compiler),
  sha256-verified Tailwind standalone downloads, per-user binary cache, `--json` contract on
  every command.
- **Docs**: component documentation site hosting the registry (`/r`) and the `/create`
  configurator with shareable preset codes.
