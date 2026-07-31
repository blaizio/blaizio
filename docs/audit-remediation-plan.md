# Audit Remediation Plan

Source: `Blaizio-Codebase-Audit.docx` (consolidated audit, baseline 9ce5642, 2026-07-30).
Every finding re-verified against current main (d34c4f2) on 2026-07-30. All 19 findings are still present; none were fixed by audit batches 1-4 (those covered API freeze, not these areas). This plan sequences the work as audit batches 5+, in the same style as the earlier freeze batches.

## Verification deltas vs the doc

The doc is accurate. Re-verification found four things it understates or misses:

1. **A11Y-04 is worse than written.** The token spans sit inside a `role="combobox"` trigger, so they also pollute the combobox accessible-name computation, and `@onclick:stopPropagation` means remove has no keyboard path at all ([BzSelectValue.razor:31](../src/Blaizio.Ui/Components/Select/BzSelectValue.razor)).
2. **Select and DropdownMenu group labels are outliers, not a new pattern.** Combobox (`BaseComboboxGroup.razor:39`) and Command (`BaseCommandGroup.razor:16`) already auto-wire `aria-labelledby` to a generated label id. Select and DropdownMenu never name their groups. Fix is copy-paste of an in-repo pattern, so it moves from "consolidation candidate" to a cheap a11y fix.
3. **PERF-02 is bigger than the four cited files.** ~80 `"import"` call sites across `src`; `positioning.js` imported in 9+ components, `presence.js` 8+, `dismissableLayer.js` 6+. Only `ThemeService` memoizes.
4. **SUPPLY-01 nuance.** AngleSharp is transitive via bunit 1.38.5, but `CalendarRenderTests.cs` takes a direct `using AngleSharp.Dom` compile dependency. Fix is a direct pinned PackageReference, not a bunit bump.

## User-reported additions (2026-07-30, verified)

**RTL-01 - HIGH - Switch thumb renders wrong in RTL when checked.** All 8 skins slide the thumb with a physical transform, e.g. `data-checked:translate-x-[calc(100%-4px)]` ([style-wisp.css:662](../src/Blaizio.Ui/Styles/style-wisp.css)), same pattern in spark/glow/flint/forge/ember/ash/aura. In `dir="rtl"` the checked thumb must travel left (negative X), so the positive translate pushes it outside the track (reproduced: Arabic airplane-mode toggle, thumb overflows the right edge). There is not a single `rtl:` variant anywhere in `src/Blaizio.Ui/Styles`; the rest of the system is RTL-safe only because it uses logical properties (`start-`/`end-`), and the switch thumb is the one physical-transform outlier (the only other `translate-x` is the radio indicator's symmetric centering, which is fine). Fix: add an `rtl:data-checked:-translate-x-[...]` counterpart next to every checked translate in all 8 skins. Inliner note: the v3 dispositions already cover RTL ancestor compilation, so the new `rtl:` variants must be expressible there too; add a golden test. Scheduled as 5.6.

**TEST-01 - CONFIRMED - No graphical UI tests exist.** The user's assumption is correct. All 700 tests are code-level: bUnit markup/behavior assertions (Base, 380) and CLI unit/command tests (320). There is no rendered-browser suite, no visual-regression/screenshot suite, no per-skin RTL or dark-mode rendering check. RTL-01 is exactly the defect class this gap hides: bUnit sees the correct class string and passes; only a rendered pixel check catches the thumb leaving the track. Batch 10's Playwright work is extended to include per-skin visual regression (LTR + RTL, light + dark).

---

One doc claim is environment-suspect: the BUILD-01 "solution build fails at Touch" repro came from the auditor's sandbox (their environment also blocked browser startup). Local builds work (long-standing). The finding is still valid as a hardening gap (no `ContinueOnError`, hard-fails on a locked `app.css`), but it is not a live breakage. Downgraded from High to Medium here. Note: the `BlaizioPackPackages` Touch stamps at csproj:215+ are the deliberate worktree NuGet-cache scheme and must NOT be touched by this fix.

## Decisions adopted from the doc

- **No blanket subcomponent reduction.** The 152-component Base surface is mostly semantic alternatives. Trigger/Content, Content/List, Tabs triad, Slider triad, Tree/Sortable boundaries, and menu item variants all stay.
- **Consolidations are perf-driven and additive only**: fold repeated visual children (indicators, thumbs) into parent render paths behind optional fragments; keep the low-level components working.
- **Measure before and after every consolidation** (instance count, render duration, allocations, interop calls). No merge lands on file-count arguments alone.
- **Calendar day and DataTable row/cell internal-frame rewrites wait for profiling.**

## Decisions that deviate from the doc

- **A11Y-02 (ToggleGroup): take the toggle-button branch**, not the radiogroup branch. Keep `role="group"`, use `aria-pressed` in both single and multiple modes, keep deselect-allowed. Radiogroup would force new keyboard contract + forbid deselection, which changes shipped behavior. Consequence: docs nav segmented switch styles its active state via `aria-checked` today; that selector must migrate to `aria-pressed` in the same change.
- **A11Y-05 (carousel dots): simple-buttons option**, not full Tabs pattern. Dots become plain `<button>`s in a labelled group with `aria-label="Go to slide N"`; drop `tablist`/`tab`/`aria-selected`. Full Tabs (tabpanel slides, roving focus, aria-controls) is heavy machinery for a dot strip and conflicts with slides staying `role="group"` + `aria-roledescription="slide"` per APG carousel pattern.
- **A11Y-09 (ColorArea): document + supplement, do not restructure.** Single-thumb 2D surface with dual-axis `aria-valuetext` matches what mainstream libraries ship. Action reduces to: ensure BzColorPicker docs point at the numeric channel inputs as the accessible path, and add per-axis value/valuetext detail. Low priority.
- **CLI-01: staged-commit design, no journal/recovery command.** Full transaction journal + `blaizio recover` is overkill for a file-copy CLI. Sufficient: resolve/validate everything first, stage writes to temp, snapshot overwritten files, atomic-move commit, packages installed only after files commit, config saved last, best-effort rollback (restore snapshots, `dotnet remove package`) on failure. This also matches the existing undo-by-record uninstall model.
- **CLI-02: use MSBuild's built-in `VerifyFileHash` task** in the generated targets instead of vendoring a verifier. Requires pinning a concrete version (kill the mutable `releases/latest` default) and embedding the expected SHA-256 per RID at generation time. `blaizio tailwind update` refreshes pin + hashes.

---

## Batch 5 - a11y contract fixes (release blockers)

**Status: DONE 2026-07-31** (344aa04 switch RTL, faf70c6 group labels, 09a3390 popover naming, cb87ed3 toggle group, 96af34b select tokens, 3abaa3a carousel rotation). Solution suite 736 green.

All in Blaizio.Base / Blaizio.Ui. Breaking-change window is open (pre-beta), so semantics changes land now or never.

| # | Finding | Change |
|---|---------|--------|
| 5.1 | A11Y-01 | Popover adopts Dialog's title/description registration: `PopoverContext` gets TitleId/DescriptionId, `BzPopoverTitle`/`BzPopoverDescription` register, content auto-emits `aria-labelledby`/`aria-describedby`, explicit override wins, debug warning when dialog unnamed. |
| 5.2 | A11Y-02 | ToggleGroup: `aria-pressed` both modes, drop `role="radio"`/`aria-checked`, root stays `role="group"`. Migrate docs nav segmented-switch CSS from `aria-checked` to `aria-pressed`. Update tests. |
| 5.3 | A11Y-04 | BzSelectValue: token X loses role/tabindex and goes aria-hidden (pointer-only shortcut, no phantom AT node, no interactive nesting). Accessible removal = Backspace/Delete on the closed trigger (removes last token) + deselect in the listbox. Docs updated. |
| 5.4 | A11Y-03 | Carousel: `Playing`/`PlayingChanged` parameters, `PauseAsync`/`ResumeAsync`, new `BzCarouselPlayPause` control (first tab stop). User-initiated stop (focus, hover, control) never auto-resumes; only explicit restart. `prefers-reduced-motion` disables autoplay in carousel.ts. |
| 5.5 | bonus | Select + DropdownMenu group label auto-wiring, copied from `BaseComboboxGroup`: generated label id, `aria-labelledby` on group root. |
| 5.6 | RTL-01 | Switch RTL fix: `rtl:data-checked:-translate-x-[...]` counterpart in all 8 skins; verify rendered in `dir="rtl"` docs page; inliner golden test for the rtl variant. |

Acceptance: no unnamed dialog reachable through shipped defaults; ToggleGroup tree valid; token removal fully keyboard-operable; carousel rotation user-controllable and reduced-motion honored; bUnit tests updated per contract.

## Batch 6 - CLI integrity + supply chain

**Status: DONE 2026-07-31** (ac7aa96 AngleSharp/bunit2/audit/global.json, 6637894 verified fetch, ab1893f offline remove guard, c1644d1 transactional add). Solution suite 747 green under pinned SDK 10.0.302. Note: the AngleSharp patch forced bunit 1.38.5 to 2.8.6 (every bunit 1.x is binary-incompatible with patched AngleSharp); the migration was mechanical.

| # | Finding | Change |
|---|---------|--------|
| 6.1 | CLI-02 | StandalonePipeline generates pinned version + per-RID SHA-256; `DownloadFile` followed by `VerifyFileHash` (build fails on mismatch, file deleted); drop `latest` as default; `blaizio tailwind update` re-pins. |
| 6.2 | CLI-01 | AddService reorder + staging: validate all items first; stage component writes in temp dir; snapshot files to be overwritten; commit via atomic moves; NuGet install after file commit; imports/global-usings; `blaizio.json` save last; on failure restore snapshots and remove installed packages, report clearly. `Prune` deletes only after successful save. |
| 6.3 | CLI-03 | `InstalledItem` gains `Dependencies` (recorded at install time). RemoveService uses persisted graph when registry unavailable; if neither available, block destructive remove without `--force` and say why. |
| 6.4 | SUPPLY-01 | Direct `AngleSharp` PackageReference pinned to patched version in Blaizio.Base.Tests; enable `NuGetAudit` in Directory.Build.props so the next advisory fails loudly. |
| 6.5 | BUILD-02 | `global.json` pinning intended SDK; CI covers it. |

Acceptance: injected failure after each Add step leaves original project or clean rollback (new failure-injection tests); no unverified executable runs in default first build; offline remove is guarded.

## Batch 7 - Docs performance + build hardening

| # | Finding | Change |
|---|---------|--------|
| 7.1 | PERF-01 | Theme-composer preview state moves to a scoped page-level model; inactive preview page unmounts (lazy-mount on first visit, retention configurable). Record before/after DOM node count, memory, switch latency. Guard: /create control-surface theme pin must stay intact. |
| 7.2 | BUILD-01 | Tailwind/esbuild Touch stamps replaced with dedicated stamp files (obj/) as target outputs; `wwwroot/app.css` no longer the freshness marker. Do not touch the pack-scheme stamps (csproj:215+). Clean + incremental build test. |
| 7.3 | DOCS-02 | CreatePage: `IAsyncDisposable` with awaited JS cleanup; selection/history/URL sync extracted to scoped `ThemeComposerState`. |
| 7.4 | DOCS-01 | Demo.razor split: `CodePanel`, `DemoInspector`, `AccessibilityXray`; debounce hover, cache slot metadata reflection. |
| 7.5 | DOCS-03 | CodeHighlighter: cap cache (or key by snippet identity); tokenizer fixtures. Low. |

## Batch 8 - rendering efficiency (measure-first)

| # | Finding | Change |
|---|---------|--------|
| 8.1 | PERF-02 | Scoped JS module registry: caches `Task<IJSObjectReference>` per module path per circuit scope, disposes once. Components resolve through it; per-widget instances unchanged. Measure interop call/proxy reduction on a menu-heavy page. |
| 8.2 | consolidation | ComboboxItem: parent-owned `SelectedIndicator` fragment replaces per-item `CascadingValue` + `BaseComboboxItemIndicator` in the default path; legacy child kept working. Benchmark 100/1000 options. |
| 8.3 | consolidation | Checkbox indicator, Switch thumb, Progress indicator: parent renders default visual, optional override fragment; child components remain for composition. |
| 8.4 | benchmarks | BenchmarkDotNet or timed bUnit harness: DataTable 100/1k/10k rows, Tree 1k nodes, Calendar month nav, 1k-option Combobox, Chart first render. Results checked into docs. Gates any Calendar-day or DataTable-frame rewrite. |

## Batch 9 - API safety + configurability (a11y mediums)

| # | Finding | Change |
|---|---------|--------|
| 9.1 | A11Y-08 | Typed `AriaLabel`/`AriaLabelledBy` (and `AriaControls` where relevant) on BaseResizeHandle, BaseProgress, BaseTabsList, BaseRadioGroup, BaseMenubar, BaseCarousel. Debug diagnostic for unnamed focusable composites (reuse Dialog's warning pattern). |
| 9.2 | A11Y-06 | Accordion `HeadingLevel` (default 3) + `Region` opt-out (default on, documented). |
| 9.3 | A11Y-07 | Tabs panel `PanelFocusable` parameter (default true, matches APG; documented when to disable). |
| 9.4 | A11Y-05 | Carousel dots to plain buttons in labelled group; slide labels "N of M". |
| 9.5 | A11Y-09 | ColorArea: docs for accessible alternative path; per-axis valuetext polish. |

## Batch 10 - decomposition + test infrastructure (ongoing)

- Extract from BaseTree (1367 ln): selection controller, drag controller, interop session. From TailwindSetup (821 ln): scaffolder, token patcher, migration service. InitCommand/AddCommand stay adapters over Core services.
- Playwright suite for Docs: routing, theme composer, mobile rail, copy controls, dialogs, static-asset build. axe-core pass + keyboard smoke tests on published docs.
- Visual-regression (screenshot) suite per skin: key components in LTR + RTL, light + dark (TEST-01). This is the only test class that catches rendering defects like RTL-01; bUnit cannot.
- Manual NVDA + VoiceOver smoke matrix for composite widgets (Dialog, Combobox, Tree, Menu, Carousel, Select) - cannot be automated away.
- CLI failure-injection tests kept green as the transaction suite.
- CSS v3/inliner: no new work; keep golden tests as regression gate (dispositions already merged).

## Out of scope / rejected

- Blanket component-count reduction (doc and this plan agree: counterproductive).
- ToggleGroup radiogroup semantics (behavior-breaking, no consumer demand).
- Full Tabs pattern for carousel dots.
- Transaction journal + recovery command for CLI (staged commit + rollback suffices).
- Runtime highlighter replacement (embedded trusted snippets; cap the cache instead).
