# Floating-surface portal — execution plan

> Status: **approved, not started**. Written 2026-07-16 for the next session to execute cold.
> Decision record: user approved the JS-layer portal (no outlet, no CLI wiring, no user-visible
> setup) with an `Inline` opt-out parameter. Param name **`Inline`** is settled - do not rename.

## The bug that forced this

The sidebar nav's grouping-toggle tooltip clips at the sidebar's right edge
(`/docs/components/*`, hover the Category toggle next to the "Components" header). Verified
mechanism, not speculation:

- `BzTooltipContent` renders **inline** as a DOM descendant of the trigger's location - inside
  `[data-slot=sidebar-container]`, which is `fixed z-10` and therefore a **stacking context**.
- The tooltip's `z-50` competes only *inside* that context; the sibling `sidebar-inset`
  (opaque `bg-background`) paints over everything past the sidebar edge (x=256). Measured:
  tooltip rect 156→273, painted only to 256.
- `position: fixed` on the tooltip does NOT escape this - z-order is decided by the ancestor
  stacking context, not by the fixed positioning.

This is a *class* of bug: any floating surface declared inside any ancestor that creates a
stacking context (fixed/sticky+z, transform, filter, backdrop-blur, container-type, opacity<1)
or an overflow clip can be painted over or cut. The fix is to physically move floating content
to `document.body`.

## Design (approved)

**Portal inside Base's JS layer.** No `PortalOutlet` component, no registry service, no root
provider, no host-page or CLI wiring, nothing in `Program.cs`. `AddBlaizio()` stays the single
DI entry point, untouched. A consumer upgrades `Blaizio.Base` + re-pulls components and the
clipping stops.

Mechanics:

1. **Attach**: the JS module that already manages each floating element moves it to
   `document.body` right before the first `computePosition` (anchored surfaces) or on mount
   (overlay surfaces). Leave a placeholder anchor (empty comment node) at the original DOM spot.
2. **Detach**: the `destroy`/dispose call the components ALREADY make moves the node back to its
   placeholder **before** Blazor unmounts it. Blazor removes nodes by reference, so teardown is
   safe as long as the node is back home first. This is the AntDesign-Blazor-proven pattern
   (their overlay JS-moves to body in production).
3. **`Inline` parameter** on every floating content component, default `false`:
   - `false` (default) → portal to body.
   - `true` → render in place, today's behavior; JS simply skips the move.
4. **Direction**: a body-level node escapes a subtree-scoped `BzDirectionProvider`'s CSS
   ancestry. The floating element must stamp its own `dir` attribute from the Direction cascade
   it already receives (only when the cascade is RTL, or always - implementer's call; never from
   config, see the rtl-flag rule). Theme/skin/dark classes live on `<html>` - unaffected.
5. **Positioning math unchanged**: floating-ui anchors by element refs
   (`computePosition(anchor, floating, { strategy: 'fixed' })` in `positioning.ts`), indifferent
   to where `floating` lives in the DOM.

## Inventory (verified against source 2026-07-16)

**Anchored surfaces** - all go through `src/Blaizio.Base/lib/ts/positioning.ts`
(`createPositioning`); portal belongs in that one module, every consumer inherits it:

| Base component | Styled wrapper (Blaizio.Ui) |
|---|---|
| `BaseTooltipContent` | `BzTooltipContent` |
| `BasePopoverContent` | `BzPopoverContent` |
| `BaseHoverCardContent` | `BzHoverCardContent` |
| `BaseDropdownMenuContent` + `BaseDropdownMenuSubContent` (`MenuContentBase.cs`) | `BzDropdownMenuContent` / `BzDropdownMenuSubContent` |
| `BaseContextMenuContent` | `BzContextMenuContent` |
| `BaseMenubarContent` | `BzMenubarContent` |
| `BaseSelectContent` | `BzSelectContent` |
| `BaseComboboxContent` | `BzComboboxContent` |

Sub-menus: portal the ROOT content; verify whether sub-content anchored to its parent item
still positions correctly when the parent is at body (it should - refs again), and whether
sub-content itself should stay inline relative to its (already portaled) parent. Decide by
testing the dropdown-submenu docs example.

**Overlay surfaces** (no anchor, fixed inset): declarative `BaseDialogContent` (+ its
`BaseDialogOverlay`), and whatever AlertDialog/Sheet/Drawer/CommandDialog compose from it.
Same placeholder/move-back treatment, applied on mount instead of on first position. The
IMPERATIVE dialog/toast paths already render at the app root via `BaseDialogProvider` /
`BaseToastProvider` - no change there.

Not floating, out of scope: NavigationMenu viewport (verify - it may position inline by
design), Toast (provider-owned), everything else.

## Execution order

1. **Read first**: `src/Blaizio.Base/lib/ts/positioning.ts` (whole file, ~130 lines),
   `presence.ts` (unmount timing - the move-back must precede presence-driven removal),
   one consumer end-to-end (`BaseTooltip.razor` + `BaseTooltipContent.razor`).
2. **Portal in `positioning.ts`**: on attach - insert placeholder comment before `floating`,
   `document.body.appendChild(floating)`; on `destroy` - `placeholder.replaceWith(floating)`
   (guard: placeholder may be gone if the ancestor unmounted first → just remove `floating`).
   Accept an `inline: boolean` option that skips all of it.
3. **Thread `Inline`** through the 8 Base content components → their JS attach options → the 8
   styled wrappers (param + xmldoc + forward). Overlay surfaces get the same param wired to
   their own module (check `dismissableLayer.ts` / dialog JS for the right attach point).
4. **Base TS rebuild**: `pnpm build` in `src/Blaizio.Base/lib` (dist is Touch-stamp-gated;
   see worktree-nuget-cache-clash memory for the pack/refresh flow).
5. **Focus/dismiss audit** per surface: open-focus, Escape, click-outside (dismissableLayer uses
   refs/document listeners - expected fine), focus restore on close, Tab order out of a
   portaled popover/select. The docs pages of each component are the test bench.
6. **Tests**:
   - Base.Tests (bUnit): render tooltip/popover open → dispose the component → no exception;
     `Inline` renders without the placeholder path. JS is stubbed in bUnit, so the real
     teardown proof is manual/browser (step 7) - the bUnit tests guard the parameter plumbing.
   - Manual browser matrix (docs site): open each surface, close, reopen, navigate away while
     open, dispose mid-animation. Watch the console for "node to be removed" renderer errors.
7. **The regression check** (must pass): docs sidebar → hover the grouping toggle → tooltip
   fully visible past the sidebar edge. Also: select inside dialog, dropdown in a table row.
8. **Ship the chain**: bump all packages to `0.1.0-alpha.5` (LOCKSTEP: Base/Icons/Cli/Cli.Core
   csprojs + `PackageVersions.Blaizio` + docs csproj PackageReferences + `_BlaizioPkgVersion`
   + README install line + CHANGELOG section). Then: pack all four → purge
   `artifacts/pkg-cache/blaizio.*` (STOP the docs server first - it serves `_content` from that
   extraction; order: stop → purge → `dotnet restore --force` → build → start) → registry
   rebuild (`scripts/build-registry.ps1`) → `dotnet tool` reinstall → docs
   `-p:BlaizioRefresh=true` build → restart docs server (say so).
9. **Docs**: component pages mention `Inline` where relevant (API tables regenerate from
   source); a short "Portaling" note on the Tooltip/Popover pages: default body-portal, `Inline`
   opts out, when to use it (CSS-containment parents, print, testing).
10. **Goldens**: styled wrapper source changes → `SkinInlinerGoldenTests` snapshots for touched
    components regenerate. Update deliberately, diff-review the golden changes.

## Gotchas / constraints

- **No em dashes** in any user-facing text (docs, xmldoc that reaches API pages, CLI output).
- **No window globals** in JS - stay inside the ES-module + typed-service pattern.
- **Never `dir` from config** - the rtl flag means support only; the portal's dir stamp comes
  from the Direction CASCADE, not `blaizio.json`.
- The move-back guard matters: if the whole page section unmounts (navigation), the placeholder
  is already gone - `destroy` must handle a detached placeholder without throwing.
- `scrollLock.ts` / `dismissableLayer.ts` may hold assumptions about content ancestry - grep
  them for `closest(` / `contains(` before assuming they survive the move.
- Docs consumer note: after Base changes, plain `dotnet build docs/Blaizio.Docs` does NOT
  refresh copied components - `-p:BlaizioRefresh=true` does (and `-p:BlaizioRepack=true` for
  Base/Icons repack + cache purge).
- Keep the docs server running between steps except during C# rebuilds; announce restarts.
- DMSign.Web dogfood (v3 step 10) is still pending and owned by the user - after this ships,
  the packages it will pull are alpha.5.

## Definition of done

- Sidebar grouping tooltip renders past the sidebar edge, unclipped.
- All 8 anchored surfaces + declarative dialogs portal by default; `Inline` restores in-place
  rendering on each.
- No renderer teardown errors across the manual matrix; all test suites green.
- alpha.5 packed everywhere, registry rebuilt, tool reinstalled, docs refreshed + server up.
- CHANGELOG documents the behavior change + the `Inline` escape hatch (this IS a behavior
  change for anyone styling floating content via ancestor selectors - call that out).
