# Manual screen-reader smoke matrix

Composite widgets whose semantics cannot be verified by bUnit (attribute assertions) or Playwright
(DOM/axe checks): what a real screen reader ANNOUNCES depends on the browser's accessibility-tree
mapping and the reader's heuristics, so these runs stay manual. Run the matrix before each release
tag, against the docs site's component pages (the demos are the fixtures - every demo carries real
labels since audit batch 9).

## Configurations

| # | Reader | Browser | Platform | Notes |
|---|--------|---------|----------|-------|
| 1 | NVDA (latest stable) | Chrome | Windows | primary Windows pairing |
| 2 | NVDA (latest stable) | Firefox | Windows | NVDA's reference pairing; catches Chromium-only assumptions |
| 3 | VoiceOver | Safari | macOS | primary macOS pairing |

Run each widget below in configurations 1 and 3 always; add 2 when a check fails or behaves oddly
in 1 (Firefox often disambiguates whether the bug is ours or Chromium's).

Global checks for every widget:

- Reaching the widget by Tab announces role + accessible name (no unnamed "group"/"region").
- Reduced motion (OS setting) does not break any announced state.
- Light/dark and skin switches never change what is announced (visual layers only).
- RTL pages (the RTL demos): arrow-key direction follows reading direction where the pattern
  requires it (menus, tabs, carousel, tree expand/collapse) and announcements stay correct.

## Dialog (BzDialog, BzAlertDialog, BzSheet, BzDrawer)

1. Open via trigger: reader announces "dialog" + the title (aria-labelledby wiring), then the
   description when present.
2. Focus lands inside the dialog; Tab cycles inside only (focus trap); Shift+Tab from the first
   element wraps to the last.
3. Escape closes (unless PreventDismiss demo); focus returns to the trigger and the trigger is
   re-announced.
4. Modal: content behind the overlay is NOT reachable by the reader's browse/next-item commands
   (aria-hidden / inert backdrop).
5. Unnamed-dialog console warning never fires on any docs demo.

## Select (BzSelect)

1. Closed trigger announces role (combobox), the current value, and expanded=false.
2. Open with Enter/Space/ArrowDown: listbox announced, active option follows arrows
   (aria-activedescendant or focus - what matters is each arrow press announces the new option).
3. Typeahead: typing a letter announces the jumped-to option.
4. Selecting announces the new value on the collapsed trigger.
5. Multiple mode: selected state announced per option ("selected" / "not selected"); token removal
   via Backspace on the closed trigger announces the removal (batch 5 contract: the token X is
   pointer-only and silent to AT).
6. Group labels are announced when arrowing into a new group (batch 5 auto-wired aria-labelledby).

## Combobox (BzCombobox)

1. Input announces role combobox + expanded state + its label.
2. Typing filters; the reader announces the result count change or the active option (listbox
   updates must not be silent - aria-live or activedescendant movement).
3. Arrow keys move the active option with announcements; Enter selects and announces.
4. No-results state is announced (the empty message is reachable/announced, not visual-only).
5. Multiple mode: same selected-state announcements as Select.

## Menu (BzDropdownMenu, BzContextMenu, BzMenubar)

1. Menubar: single tab stop; arrows move between top-level items with announcements; the bar's
   AriaLabel is announced on entry.
2. Open menu: "menu" announced; items announce role menuitem (or menuitemcheckbox/radio with
   checked state).
3. Submenus: expanding announces the submenu; Escape steps back one level only.
4. Checkbox/radio items announce state changes on toggle.
5. Shortcut hints (BzMenubarShortcut) are announced with the item, not as separate noise.
6. Escape closes all; focus returns to the trigger.

## Tree (BzTree)

1. Entry announces "tree" + AriaLabel + the focused item with level/position ("level 2, 3 of 5").
2. Arrows move focus with per-node announcements (text + expanded/collapsed + selected state).
3. Expand/collapse announces the state change; * (expand siblings) announces sensibly (no flood).
4. Checkable: Space announces checked/unchecked/mixed (tri-state on branches).
5. F2 rename: entering edit announces the input; Enter commits and the live region announces
   "Renamed to ...".
6. Keyboard drag: Ctrl+Space announces the grab instructions (live region); each arrow move
   announces; drop/cancel announced ("Dropped." / "Move cancelled.").
7. Typeahead jumps announce the matched node.
8. Lazy loading: expanding announces the loading state or at least the loaded children afterwards;
   a load failure announces "Could not load ...".

## Carousel (BzCarousel)

1. Region announced with its AriaLabel + "carousel" roledescription on entry.
2. Slides announce "slide, N of M" (batch 9 contract) when browsed.
3. Prev/next buttons have names and announce disabled state at the edges (non-loop demo).
4. Dots: "Go to slide N" per button, current one announced as current (aria-current).
5. Autoplay demo: rotation stops on keyboard focus entering the carousel and does NOT restart by
   itself; the play/pause control announces its state; with OS reduced motion set, autoplay never
   starts.

## Recording results

Copy the table below into the release issue; one row per widget x configuration. "Pass" only when
every numbered check passed. Any failure gets a linked issue and the release blocks on dialog,
select, combobox and menu failures (tree/carousel judged case by case).

| Widget | NVDA+Chrome | VoiceOver+Safari | NVDA+Firefox (if run) | Issues |
|--------|-------------|------------------|------------------------|--------|
| Dialog | | | | |
| Select | | | | |
| Combobox | | | | |
| Menu | | | | |
| Tree | | | | |
| Carousel | | | | |
