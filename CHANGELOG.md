# Changelog

All notable changes to the Blaizio packages (`Blaizio.Base`, `Blaizio.Icons`, `Blaizio.Cli`) and
the registry-distributed `Blaizio.Ui` source. Format loosely follows
[Keep a Changelog](https://keepachangelog.com); versions are lockstep across packages while
pre-release.

## 0.1.0-alpha.13 — 2026-07-25

### Added
- **Cli**: `add` and `update` check the registry before they touch the project. Both wire first and
  fetch after, so an unreachable registry used to install packages, write the tokens file and edit
  the host page and only then fail - leaving a half-applied project behind a late error. One
  request up front turns that into a clean refusal (exit 2, "nothing was changed"). A wiring-only
  run (`add --rtl`, `--tailwind`, `--css`) skips the check, and a registry that answers without an
  `index.json` still passes: items resolve at the base path.

### Fixed
- **Cli**: an item resolves from a registry that ships no `index.json` even when the project has a
  skin recorded. The per-skin variant gate read the index and let a missing one fail the whole
  lookup, so v1 (raw sources) and third-party registries were unusable on any initialized project;
  no index now means no skin variants, as documented, rather than an error. Registry failures also
  carry a reason (unreachable / not found / malformed) instead of only a message.

## 0.1.0-alpha.12 — 2026-07-25

### Added
- **Cli**: `blaizio remove <components...>` (alias `rm`) takes individual components back out -
  previously only `uninstall` could undo an add, and it removed everything. Removal is by record
  like uninstall: exactly the files `add` wrote for each named item plus its `blaizio.json` entry,
  so files you authored under the output directory are never swept up and a file two items share
  survives while either is installed. Names resolve however they are typed. It refuses to break
  the project - an item another installed component depends on is reported and skipped (exit 1)
  unless `--force` - and never uninstalls NuGet packages or touches the wiring; components and
  packages nothing needs anymore are listed instead. `--dry-run` previews, `-y` skips the prompt.

## 0.1.0-alpha.11 — 2026-07-25

### Added
- **Ui**: `BzColorPicker` edits gradients. `ShowGradient` adds a Solid / Gradient switch, a stop bar
  and the shape controls (Linear, Radial, Angular, Diamond); the area, sliders and text input then
  edit whichever stop is selected. In gradient mode `Value` carries the CSS paint instead of a color
  string, and the mode follows the shape of whatever `Value` is handed - so a gradient string round
  trips through `@bind-Value`. `@bind-Gradient` exposes the same thing as a model (`GradientValue`:
  type, angle, stops), and `Mode` is bindable too. New parts: `BzColorModeTabs`,
  `BzColorGradientBar`, `BzColorGradientType`.

  Diamond has no CSS gradient function, so it serializes as four quadrant ramps carrying their own
  position and size: assign gradient values to the `background` shorthand, not `background-image`,
  which drops the whole declaration.
- **Ui**: `BzColorPalette` and `BzColorPaletteGroup` - a labelled swatch grid of named ramps, the way
  a design system lists its colors. Rows of equal length line up column by column, and every shade is
  an ordinary `BzColorSwatch`, so clicking applies it and the matching one marks itself selected.
- **Base/Ui**: an Image surface - `BzColorImage` - fills with a picture and grades it. Everything
  happens in the browser (`imageFill.ts`, nothing is uploaded): choose a file, pick how it lays into
  its box (Fill / Fit / Crop / Tile), rotate it a quarter turn at a time, and grade it with
  exposure, contrast, saturation, temperature, tint, highlights and shadows. `Value` becomes the
  CSS paint; the grade rides on `@bind-Image` as an `ImageFillValue`, whose `ToFilterCss()` gives
  the matching `filter` - a separate CSS property, so it cannot travel in the paint. Rotation is
  baked into a new bitmap rather than left as a transform, so the paint always matches what you
  see. Like Gradient it is one of the picker's surfaces, so Solid, Gradient and Image share a
  single switch; each is optional (`ShowSolid`, `ShowGradient`, `ShowImage`), the switch appears
  only once more than one is on, and a picker with all three off still picks a solid color.
- **Ui**: `BzColorPicker` has a `Bordered` card frame (on by default) - turn it off inside a surface
  that already has one, such as a popover - and `ShowOpacityInput` to drop the percentage field
  beside the alpha slider.
- **Ui**: the picker's draggable surfaces use the Tabler hand cursors (`HandStop` at rest,
  `HandGrab` while dragging) in place of the browser's `grab`/`grabbing`, and the gradient stop
  add/remove buttons carry tooltips. Chromium ignores SVG cursors, so `ts/cursors.ts` rasterises
  the pair to PNG on first render and writes them back over the custom properties the sheets read;
  `grab`/`grabbing` stay behind them as the fallback.
- **Ui**: `BzSelectTrigger` takes a `Disabled` of its own, for disabling one trigger inside a
  composite without disabling the whole select.
- **Ui/Base**: the Image surface can save its picture back out. `BzColorImage`'s `Download` (and
  the picker's `ImageDownload`) adds a download button beside rotate that writes the picture as it
  looks in the preview - rotation and the tone grade baked in, by redrawing through the same
  filter string the preview renders with. An untouched image keeps its original file bytes (and
  encoding); a graded one re-encodes as PNG. `DownloadFileName` names the file.

### Fixed
- **Base**: marquee labels (`BzTree`'s `MarqueeLabels`) reveal on hover as intended. Two timing
  faults made them look dead: the one measurement pass ran before webfonts swapped in, so labels
  that only overflow in the real face were never marked truncated (nothing re-measured on font
  load, and no observer fires for a font swap) - now re-measured on `document.fonts.ready` and
  `loadingdone`, and each label is watched by the ResizeObserver, not just the root. The slide was
  also far too slow at 15ms per overflowing pixel: a 190px tail took 2.85s of linear travel, so a
  normal hover moved the text a few imperceptible pixels and slid back. Now 5ms/px capped at
  1.6s, which puts a typical reveal just under a second.
- **Base/Ui**: the marquee slide is driven by the module, not a CSS transition. Chromium does not
  repaint glyphs for interpolated `text-indent` transition frames on an ellipsised line box - the
  computed value and layout advance while the pixels stay frozen, so the reveal looked dead even
  with everything wired correctly. `ts/marquee.ts` now steps the inline `text-indent` in a rAF
  loop (every frame a discrete style write, which does repaint), reverses interrupted slides at
  the same speed, honors reduced motion with an instant reveal, and starts the slide for a cursor
  already parked on a label when it becomes armed. The stylesheet keeps only the hover
  ellipsis-to-clip swap.
- **Cli**: component names resolve regardless of case and separators - `blaizio add inputnumber`,
  `INPUTNUMBER` and `Input_Number` all land on `input-number` (previously only `InputNumber` or
  `input-number` matched). Plain names resolve through the registry index; registries without one
  keep the literal kebab-case path.
- **Cli**: registry dependency inference reads code only. Mentioning another component in a
  comment ("like `BzInputText`") counted as a dependency, so `add input-number` also installed
  `input-text` - 19 phantom dependencies across 17 components. Comments (razor, C# line and
  block, HTML) are stripped before the scan; the shipped registry is regenerated without them.
- **Base**: `BaseSelectContent` no longer throws an unhandled `ObjectDisposedException` when the
  component is torn down mid close-animation (its surrounding surface swapped out, say) - the
  `onClosing` callback is guarded like the dispose paths already were.

## 0.1.0-alpha.10 — 2026-07-25

### Changed
- **Cli**: stack updates are a top-level command: `blaizio update [components...]`. Same lockstep
  operation as before - bump the Blaizio NuGet packages to the tool's pinned versions, then
  re-pull installed components (all, or just the ones named), with the v1-to-v3 migration gate
  intact - but under the verb every package manager uses, instead of hiding a refresh-everything
  mode behind `add`. It does not update the tool itself; that stays
  `dotnet tool update -g Blaizio.Cli`.

### Removed
- **Cli**: `add --update` and its hidden `--upgrade` alias - use `blaizio update`. Strict parsing
  rejects the old spellings loudly, so a stale script fails with a usage error rather than doing
  something else.
- **Cli**: the legacy `deinit` spelling of `uninstall` (the `un` alias stays).

## 0.1.0-alpha.9 — 2026-07-25

### Changed
- **Base/Ui**: `BaseInputNumber` and `BzInputNumber` are generic - `BzInputNumber<TValue>` - over the
  numeric types Blazor's own `InputNumber<TValue>` supports: `int`, `long`, `short`, `float`,
  `double`, `decimal`, bare or nullable. `TValue` is inferred from the binding, so
  `@bind-Value="_days"` on an `int` now just works - no `(double?)` cast, no transforming setter.
  Semantics follow the type:
  - Parsing is per-type, like Blazor's converter: an integral `TValue` rejects `"2.7"` outright
    instead of truncating it.
  - Only a nullable `TValue` can rest empty (empty commits `null`); a non-nullable one keeps its
    last good value and reverts on blur, matching Blazor's failed-parse behavior.
  - Values clamp to the type's own range on top of `Min`/`Max`, so stepping can never overflow the
    conversion back.

  Internally every computation now runs in `decimal`, so integer and money math is exact - three
  `0.1` steps make `0.3`, not `0.30000000000000004` - and the cascaded `InputNumberContext` carries
  `decimal?`, keeping the Group / Input / Increment / Decrement parts non-generic. Migration:
  existing `double?` bindings are unaffected (`TValue` infers as `double?`); code that referenced
  the component type by name needs the type argument (`BzInputNumber<double?>`). `Min` / `Max` /
  `Step` / `LargeStep` stay `double`-typed parameters. A float/double magnitude beyond decimal's
  ±7.9e28 saturates.

## 0.1.0-alpha.8 — 2026-07-25

### Fixed
- **Base**: `BaseInputNumber` reconciles the displayed text correctly in controlled mode. alpha.7 kept
  the text in step with the value by comparing against the last value the component emitted, which
  holds only when the parent stores exactly what it is handed. Two common cases break that, and both
  showed up as the field displaying one number while the binding held another:
  - A `@bind-Value:set` that **transforms** the value (clamp, round, `?? fallback`) sends something
    else back. Clearing a field bound with `v => _days = (int)Math.Clamp(v ?? 1, 1, 365)` emitted
    `null`, the parent turned it into `1`, and that `1` was read as an external push - refilling the
    box the user had just emptied.
  - On a Blazor Server circuit a render batch can be built before a later keystroke and delivered
    after it, so the value arriving is sometimes an **earlier** one of the component's own. That was
    also read as external, rewinding the display to a number the user had already typed past.

  Reconciliation is now driven by whether the component committed since the last parameter sync,
  and an unflagged change only rewrites the text when the text does not already spell that value -
  so a partial decimal (`"40."` while `40` lands) survives too. A genuine parent push while the
  field has focus still reaches the display, which is what alpha.7 set out to fix.
- **Base**: a stepper hold now continues from where its own first press landed. The repeat loop
  seeded its running total by re-reading the controlled value, which in controlled mode still holds
  the pre-press number until the parent's render arrives, so the first repeat tick recomputed the
  step the press had already made and the hold lost a step.

## 0.1.0-alpha.7 — 2026-07-24

### Added
- **Ui**: `BzColorPicker` - a composable color picker family: saturation/value area
  (`BzColorArea` over the new headless `BaseColorArea`), hue and alpha sliders, eye dropper,
  format select (HEX/RGB/HSL/HSB), per-channel inputs riding `BzInputNumber`, opacity input,
  preview, swatches and saved colors.
- **Ui**: `BzQrCode` - SVG QR codes with module/eye styling, colors and gradients, quiet zone,
  error-correction levels, and center content (logo) support.
- **Ui**: `BzTree` marquee (drag-to-select) support.
- **Base**: select-level `Disabled` on `BaseSelect`.

### Changed
- **Cli**: `add --update` and `add --upgrade` merged into `add --update`, which now does both:
  bump the Blaizio NuGet packages to the tool's pinned versions, then re-pull the installed
  components (all of them, or just the ones you name). The split shipped a foot-gun: components
  lean on the package's JS/CSS assets, so a source-only sync could pair new components with an
  older package (a component importing a `dist/` module its package doesn't carry fails at
  runtime). `--upgrade` remains as a hidden alias so existing scripts keep working. The v1 → v3
  migration now bumps the packages too.

### Fixed
- **Base**: `BaseInputNumber` survives controlled lag on Blazor Server. Two defects: (1) while
  the input had focus, a parent-pushed `Value` never reached the displayed text - the field
  showed one number while the bound value held another. The text now resyncs whenever the
  incoming value is not the echo of the component's own last emit, focused or not. (2) the
  press-and-hold repeat loop re-read the controlled value every 60ms tick, but that value only
  updates with a render - which on a Server circuit can lag well behind the cadence - so every
  tick redid the same stale math and a whole hold netted a single step. The loop now accumulates
  a local pending value and stops at the bounds itself.
- **Base**: JS-to-.NET callbacks no longer surface "There is no tracked object with id ..." as an
  uncaught promise rejection when they race component teardown - e.g. a dialog button that
  navigates: the focus scope's unmount round-trip (and any late animationend / dismiss / scroll
  report) can arrive after the component disposed its `DotNetObjectReference`. Every interop
  module now routes callbacks through a guard (`ts/interop.ts`) that swallows exactly the
  disposed-reference rejection - the component is gone, there is nobody left to notify - and
  keeps every other failure as loud as before.
- **Base**: closing a floating surface no longer throws `ObjectDisposedException` when the close
  races the component's own disposal - e.g. a dropdown-menu item that navigates: the menu's exit
  animation reports back while navigation is already tearing the component down, and both paths
  disposed the same JS positioning reference. Every floating surface's dispose helpers (dropdown
  menu, popover, tooltip, combobox, select, dialog content + overlay, hover card, collapsible,
  table of contents) now claim their reference synchronously before awaiting - the second caller
  no-ops - and swallow `ObjectDisposedException` alongside the existing circuit-gone guard.
- **Ui**: `--input` gained contrast against its surface in every palette - the light value dropped
  from lightness `0.91` to `0.88` and the dark value rose from `0.325` to `0.36`. Input borders,
  and everything else deriving from the token, now read as an edge rather than a suggestion.
- **Ui**: interactive states that leaned on `--muted` over `--background` (barely two percent
  apart in light mode) moved to `--accent`, so they are actually visible: pagination link hover,
  outline-toggle hover, and the outline/ghost bubble action hovers. Data-table column-header
  hover moved to the existing `color-mix(..., var(--foreground) 5%)` hover step in the skins that
  had no visible treatment (ember, spark, forge, wisp, aura, flint).
- **Ui**: the pagination link's active page shows its border again. The shared
  `[data-active="true"]` rule and each skin's base `.bz-pagination-link` rule carry the same
  specificity, so the skin's `border-transparent` - imported later - silently won and the current
  page lost its outline in every skin. The active treatment now lives in the skin rules.
- **Ui**: `BzDataTable` row clicks are delegated in JS, so a click can be attributed to its
  target. Clicking a row now toggles that row's selection when `Selectable`, while clicks that
  originate inside an interactive cell control (the selection checkbox, a row-actions trigger, a
  link) stay that control's own. `OnRowClick` gains the same treatment, finally honouring its
  documented "anywhere outside an interactive cell control" contract - as a Blazor `@onclick` on
  the `tr` it had fired for every click that bubbled out of the row.
- **Base**: a tooltip trigger opens on focus only when the focus is *keyboard* focus
  (`:focus-visible`). Focus also lands on a trigger programmatically - a dropdown or dialog
  sharing that trigger restores focus there when it closes - which popped the tooltip open under
  a pointer nowhere near it. Keyboard users still get the immediate, no-delay open, including
  when Escape closes an overlay back onto the trigger.

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
