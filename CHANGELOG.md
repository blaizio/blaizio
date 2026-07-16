# Changelog

All notable changes to the Blaizio packages (`Blaizio.Base`, `Blaizio.Icons`, `Blaizio.Cli`) and
the registry-distributed `Blaizio.Ui` source. Format loosely follows
[Keep a Changelog](https://keepachangelog.com); versions are lockstep across packages while
pre-release.

## [Unreleased]

### Changed
- **CLI**: a `/create` code's chart palette and radius now bake directly into the managed
  `theme.css` `:root` (exact-declaration rewrite) instead of a separate `tokens.css` overlay.
  The selection is recorded in `blaizio.json` (`chart`, `radius`) so `update`/`apply` re-runs
  keep it; re-runs delete a legacy `tokens.css` and its import. `apply --only tokens` patches
  `theme.css` in place.
- **CLI**: `preset resolve` round-trips the full `/create` code, including fonts, chart and
  radius.
- **Docs**: the Get Code dialog's Theme tab emits a single merged, comment-free `:root`/`.dark`
  block — customized chart/heading/radius values are folded into the preset's own declarations.

### Fixed
- **Ui**: accordion/collapsible close blink — the `@theme` animation shorthands in `blaizio.css`
  dropped the `--tw-animation-fill-mode` hook, so the exit animation ran with `fill-mode: none`
  and the closed panel snapped back to full height until the unmount landed. The shorthands now
  carry `var(--tw-animation-fill-mode, none)`, letting the `[data-state='closed']` forwards pin
  hold the final frame.

### Planned
- CSS layout v3: one user-owned tokens file, component classes inlined per skin at registry
  build, contract sheet materialized from `Blaizio.Base` at build time, `eject` command.
  See *CSS layout v3* in `docs/cli-plan.md`.

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
