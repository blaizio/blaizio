# Changelog

All notable changes to the Blaizio packages (`Blaizio.Base`, `Blaizio.Icons`, `Blaizio.Cli`) and
the registry-distributed `Blaizio.Ui` source. Format loosely follows
[Keep a Changelog](https://keepachangelog.com); versions are lockstep across packages while
pre-release.

## Unreleased

### Added
- **Cli**: `minBase` on registry items - the lowest `Blaizio.Base` an item's sources work against,
  set per family in the generator when a component calls into a Base capability (typically a JS
  module) that shipped in a specific release. `add` now fails BEFORE installing anything when the
  project pins an older `Blaizio.Base`, naming the item, both versions and the upgrade path
  (`dotnet tool update --global Blaizio.Cli`, then `blaizio update`) - previously the unpinned
  package was skipped without a version look and the component's interop 404'd at runtime. A
  missing, floating (`0.1.0-alpha.*`) or unpinned reference skips the check; `panel` is the first
  item to carry one (its drag-resize module shipped in Base alpha.24). Item schema and the
  registry-item reference document the field.
- **Ui**: the Panel family (`BzPanel` + Header/Title/Close/Content/Footer) - an in-flow side panel
  that PUSHES its siblings instead of overlaying them: no portal, no backdrop, no focus trap, so
  the rest of the page stays interactive. Because it sits in normal flow it is bounded by whatever
  parent it lives in - the full page or any div - and docks to any edge (`Side`: Start/End push
  along the inline axis and mirror under RTL; Top/Bottom push along the block axis). Open/close
  animates the panel's size between 0 and `Size` (any CSS length, exposed as `--panel-size`), with
  the inner surface pinned to the content-facing edge so it slides in from the docked edge like a
  sheet while the siblings are pushed; while closed the panel is `inert`, so nothing inside it can
  be tabbed to or read. `Variant` picks the look: `Attached` (flat, one shared border - reads as
  part of the content) or `Floating` (an inset card with a full border, rounding and a shadow -
  its own surface). `Resizable` adds a
  window-splitter handle on the content-facing edge: pointer drag resizes live with the gesture
  applied browser-side (no per-move interop), arrow keys nudge, Home/End jump to `MinSize`/`MaxSize`
  (any CSS length, a bare number meaning px, clamped), and the settled px size lands in
  `SizeChanged` for persistence. The
  handle sits centered on the panel's edge, window-splitter style, and takes the Resizable
  component's options: `Handle` picks the affordance - None (bare strip), Line (a thin centered
  line revealed on hover and while dragging), or a grip on top of that feedback
  (Grip/Dots/Knob/Pill) - `RevealOnHover` hides the grip until the handle is hovered, focused or
  dragging, `HandleContent` replaces the grip with custom content, `HandleClass` restyles the
  strip, and `Cursor`/`DragCursor` swap the OS resize arrows.
- **Ui**: `ResizableHandleVariant.Line` and `RevealOnHover` on the Resizable handle - the same
  affordances as the panel's: every variant except None thickens the hairline into a thin centered
  line on hover and while dragging, and `RevealOnHover` hides the grip until the handle is
  hovered, focused or dragging. None stays exactly as before.
- **Ui**: sheets through the dialog service - `BzDialogOptions.SheetSide` dresses an imperatively
  shown instance in the Sheet skin sliding from that edge, and `ShowSheetAsync` (component and
  template overloads) is the sugar that sets it. Same contract as `ShowAsync`: the content closes
  itself through the cascaded `DialogInstance`, stacking and z-layering included.
- **Base**: `ts/panel.js`, the push panel's resize module - resolves `MinSize`/`MaxSize` CSS
  lengths to px by probing, owns the pointer/keyboard gesture, and reports only the committed size
  to C#.
- **Ui**: `AutoFocus` and `AutoSelect` across the input family - `InputText`, `InputGroupInput` and
  `InputNumber` take both; `InputTags`, `InputOtp`, `InputDate` and `InputTime` take `AutoFocus`
  (their segments and slots already select themselves). `AutoFocus` is the one the platform cannot
  give you: browsers honor the native attribute only while the document loads, so a field inside a
  dialog, a wizard step or anything Blazor renders later never focuses. This focuses on MOUNT and
  retries across animation frames, which is what carries a field that renders while its dialog is
  still opening. On a container - an OTP's slots, a date field's segment row - it descends to the
  first focusable control. `AutoSelect` selects the value on every focus, the replace-on-type
  pattern for a field the user overwrites rather than edits. Each of the seven also exposes a
  `FocusAsync(FocusOptions?)` method - hold the component with `@ref` and call it, no id to
  invent; it rides `ICore`'s retrying focus and reports whether focus landed.
- **Base**: `ICore` / `Core`, the browser-side services Blazor cannot provide from C#, registered by
  `AddBlaizio()`. `FocusAsync` retries across animation frames, so focusing a target that is still
  `display:none` mid-open-animation lands instead of silently doing nothing, and it reports whether
  it did. It takes an `ElementReference` or an element id - the id form is how you reach the styled
  inputs, which never expose a reference: give the field an `id` through its forwarded attributes
  and focus it with that. The id is re-resolved on every retry (a call racing the render that
  creates the element still lands), and either form descends from a wrapper to the first focusable
  control inside, so a container id - an OTP's, a date field's - focuses the right thing. `EnsureGuardsAsync` installs the key guard below. There is deliberately no imperative
  `PreventDefaultAsync`: the browser applies a default action synchronously during dispatch and a
  C# handler runs after it, so such an API would be timing-dependent by construction. The module
  behind it, `ts/core.ts`, is the renamed `ts/interop.ts` and now also holds the combo parsing the
  hotkeys module used to own.
- **Ui**: `PreventKeys` on `InputText` and `InputGroupInput` - the combos whose browser default is
  suppressed, comma-separated and matched exactly on modifiers (`"enter, mod+s"`). A send-on-Enter
  composer can now handle Enter in `OnKeyDown` without the newline landing first, while Shift+Enter
  still breaks the line: the granularity Blazor's own all-or-nothing
  `@onkeydown:preventDefault` cannot express. The suppression is one delegated capture listener
  reading a `data-bz-prevent-keys` attribute, so it costs no interop per keystroke and the event
  still reaches the C# handler.
- **Ui**: `Cursor` / `GrabbingCursor` on `ColorPicker` and `Cursor` / `DragCursor` on
  `ResizableHandle`, matching the pair `Sortable` already had. `BzCursor.From` gained `halo`, a
  contrasting backing drawn behind the glyph the way the OS hands pair a white body with a dark
  line - without one an outline icon is bare 2px strokes with a see-through interior, legible over
  a plain page and invisible over busy content.

- **Ui**: infinite scroll on `Virtualizer` - `OnLoadMore`, `LoadMoreOffset` and `LoadMoreContent`.
  A batch-fetching source becomes one continuous list: when the rendered window comes within
  `LoadMoreOffset` items of the end (10 by default) the callback fires once, item-sized skeleton
  rows hold the tail while the batch is on the way, and appending to `Items` re-arms it for the
  next batch. There is deliberately no `HasMore` flag to maintain: the callback only re-arms when
  `Items` grows, so a drained source stops itself - the fetch that returns without appending is
  the last one ever made.
- **Ui**: `ItemsProvider` on `Virtualizer` - the constant-memory source for large server-backed
  lists. The provider receives a start index + count (plus a cancellation token that fires when a
  faster scroll supersedes the window) and returns that slice with the set's total count; the
  virtualizer holds only the current window, discards what scrolls away, sizes the scrollbar from
  the total up front so the user can jump anywhere, and renders item-sized skeleton placeholders
  (`Placeholder` overrides) while a window is on the way. `RefreshDataAsync` re-queries after the
  underlying data changes. The two sources never combine and the split is enforced loudly:
  `Items` (+`OnLoadMore`) is the feed model where batches accumulate in a list you own;
  `ItemsProvider` is the seeking model where the virtualizer fetches windows you don't keep -
  setting both, or `OnLoadMore` with a provider, throws with a message saying which to drop.
- **Base, Ui**: `ScrollToIndexAsync` and `InitialItemIndex` on the window virtualizer and
  `Virtualizer`. `InitialItemIndex` opens a long list at an item on the first interactive render
  (one-shot, clamped); `ScrollToIndexAsync` jumps after that - instant, top-aligned, index math in
  fixed mode, measured offsets in dynamic mode (a jump into unmeasured territory lands on the
  estimate and settles as rows measure). Called before the JS attaches, the target is remembered
  and applied on attach.
- **Ui**: `Loading` / `LoadingContent` on `Virtualizer`. A virtualized list whose rows are still on
  their way used to render its empty state - or nothing - until the fetch landed, so a slow source
  read as "no results". Set `Loading` while the request is in flight and an empty list paints
  item-sized skeleton rows instead; `LoadingContent` swaps in your own wait. `DataTable` picked up
  the same default: while an `ItemsProvider` loads and no rows are on screen it shows skeleton rows
  shaped like the table - one bar per visible column, a page's worth when `PageSize` is set -
  instead of the old "Loading..." sentence, and a custom `LoadingContent` still renders in a single
  cell spanning every column.
- **Cli**: `@default/item`, the reserved namespace a third-party registry uses to depend on the
  components its consumers already install. Inside a namespaced item a plain dependency name means
  that item's own registry, which is right for a registry shipping a complete set and wrong for one
  building on the base components: an editor that needs `toolbar` sent the CLI looking for
  `@editor/toolbar`. A `@default/` dependency resolves against the consumer's default registry and
  installs exactly as their own copy would - ordinary folder, ordinary namespace, ordinary install
  record - so nothing is duplicated and an already-installed component is reused. It works on the
  command line too, and `registry add` refuses to record it.
- **Cli**: a missing dependency that the resolver rewrote now says so. A plain name inside a
  namespaced item is claimed by that item's registry, so the failure named an address the author
  never wrote (`https://acme.dev/r/toolbar.json`) with nothing joining it to the `toolbar` they
  did write. It now adds which dependency, of which item, why it resolved there, and that
  `@default/toolbar` reaches the consumer's own registry instead.
- **Cli**: `registry add` refuses `@blaizio`. Its installs would land in a `Blaizio` folder, and
  every `Blaizio.Base` reference inside those files would then bind to that nested namespace
  segment instead of the package, so the components would install and fail to compile.
- **Docs**: the /themes composer redesigned around direct token editing. The left rail is now a
  bottom dock on every viewport (dropdowns open upward; the meta actions live in a three-dot
  menu at the end; Shuffle/Undo/Redo/Get Code are stacked beside it), the canvas takes the full
  width with the page switcher at the top, and page 01 leads with a theme identity card (theme +
  body face, live token swatches). Every semantic token (primary, surfaces, statuses, border,
  chart series) is a swatch in the dock: its popover holds a live color picker with an AA/AAA
  contrast readout, edits apply per mode (light/dark edited separately), persist like every other
  knob, and text-bearing surfaces auto-derive an AA-guaranteed foreground. Preset codes gained a
  v3 form carrying the edits (`<code>-<chunks>`), so customized themes share by URL and decode in
  the CLI - `blaizio apply` patches them over the preset. Shuffle no longer draws from the theme
  gallery: themes are curated complete looks, Shuffle explores raw combinations (a random
  primary pair plus charts, faces and radius); picking a theme clears the edits and applies its
  pairings.

### Changed
- **Ui**: `BzBubble` gained `TailShape` - `Triangle` (the flat pointer it always had, still the
  default) or `Curved` (a taller wedge with a rounded tip that sweeps away from the corner). Both
  shapes are the same `::after` pseudo-element, so the tail keeps taking the variant's fill from
  the sheet and the ghost rule still hides it; each shape is drawn once for the top-start corner
  and `Align="End"`, `Tail="Bottom"` and RTL flip it instead of carrying geometry per corner.
  `BubbleReactionsSide` is now `BubbleReactionAnchor` (the `Side` parameter is unchanged).
- **Cli**: `update` sets its closing line apart - a yellow `Tool itself:` label and a blank line
  above it. It was the only line in the summary with no label at all, so the one step the command
  cannot perform for itself read as a footnote under the results and was missed.

### Removed
- **Ui**: the color picker's built-in hand cursors, and `ts/cursors.ts` with them. The picker
  forced its own artwork on every draggable surface and rasterised it through a canvas on first
  render (Chromium refuses SVG cursors without intrinsic dimensions). The OS grab and grabbing
  hands are the default now, and custom artwork is opt-in through the parameters above - the same
  contract as the scrollbar utility: the library ships behaviour, not taste.

### Fixed
- **Ui**: a `Virtualizer` with `MaxHeight` is keyboard-scrollable. Chromium refuses to scroll a
  scroll box that cannot take focus, so the viewport now carries `tabindex="0"` (with the
  library's inset focus ring), the same treatment the data table's scroll container already had -
  and the axe `scrollable-region-focusable` rule stops flagging it.
- **Ui**: the sidebar trigger's default glyph mirrors under RTL. The icon draws its panel on the
  left, which is where the sidebar sits in LTR; right-to-left flips the sidebar to the right, and
  now the glyph follows.
- **Ui**: keyboard focus on a tab trigger (and a focusable badge) wore the browser's own white
  outline instead of the library's focus ring. Both components paint the two-layer ring but had
  slipped past the `outline-none` every other focusable component carries, so the UA outline drew
  on top - the "white border" on the active tab. They were the only two; the whole styled layer
  was swept.
- **Ui**: an Expand toolbar no longer unfolds when a click lands inside it. The clipped row lifted
  on any focus within the bar, so opening a dropdown menu sitting in the visible row yanked the
  whole bar open under the pointer. The reveal now keys on `:focus-visible` inside the viewport:
  keyboard travel into the hidden rows still holds the bar open on its own, pointer focus leaves
  it clipped, and the trailing toggle remains the pointer's way in.
- **Docs**: a collapsed example-code panel no longer shows a vertical scrollbar. The preview clips
  to four lines, but the code area inside kept its own expanded-height scroll box, so long
  examples scrolled inside the clip; the code area now scrolls only while expanded.
- **Docs**: a wide code block's horizontal scrollbar is reachable from wherever you are reading.
  Blocks now cap at 70vh and scroll on themselves, so the bar stays on screen instead of parking
  at the foot of a 2000px block - the sidebar page had nine of those. Each block also gained a
  wrap toggle beside Copy, which soft-wraps long lines (and breaks a long unbroken token rather
  than restoring the scroll); wrapped lines hang under the code, not under their own line number,
  since the numbers now sit on block line boxes with a hanging indent. Both controls carry
  tooltips.
- **Docs**: the RTL demos' language tabs read right to left, like the demo under them - Arabic
  first from the right, arrow keys following suit.
- **Docs**: the API pages read as reference again. Their table of contents lists the family
  (`BzInputGroup`, `BzInputGroupAddon`, ...) instead of a single "API Reference" entry that
  restated the page title; long parameter descriptions wrap instead of stretching the table into a
  3000px horizontal scroll (the skin's table cells are `nowrap`); and a summary that documents a
  grammar renders its examples as a list, since the build's XML flattener now keeps `<para>` and
  `<item>` breaks. The summaries themselves are boiled down to a small JSON file at build time -
  the first API page used to parse the raw 600KB compiler XML with `XDocument` on the WebAssembly
  interpreter, a visible stall with empty tables underneath it.
- **Docs**: the sidebar variants demo and every RTL demo switch with tabs rather than a stack and
  a dropdown - three sidebar shells stacked was a lot of scrolling for a difference that only
  reads when you can flip between them, and the language switch is a two-way choice, which is what
  a tab pair is for.
- **Base, Icons**: a packed version is never packed twice. Both packages take their version from
  one property (`BlaizioVersionBase`), and any pack that would overwrite an existing `.nupkg` now
  fails with a message naming the fix. NuGet caches by id+version and never re-extracts a version
  it already holds, so republishing different bytes under one version was invisible: a consumer
  compiled against the new package, loaded the old assembly, and hit a `TypeLoadException` at
  runtime that only a cache eviction plus a clean `obj`/`bin` cleared. The docs dogfood loop, which
  repacks on every edit to either package, now mints a private revision per pack and floats its own
  references onto it, so consumer-facing versions stay immutable.
- **Cli**: the version of `Blaizio.Base`/`Blaizio.Icons` that `init` and `upgrade` write into a
  project is generated from the build, so a release cannot ship a tool naming a version the
  packages never packed.
- **Docs**: the site header no longer pushes the page into a horizontal scrollbar between
  roughly 800 and 1100px: the alpha badge and the GitHub link hide below lg, the search control
  falls back to its icon button there, and `html` clips horizontal overflow outright (wide
  content scrolls inside its own container, never the page).
- **Ui**: `BzColorPicker` now decides controlled vs uncontrolled by whether `Value` is supplied
  (the null sentinel every other component uses) instead of by `ValueChanged` having a delegate -
  an uncontrolled picker with a change listener rendered black because it was treated as
  controlled with a null value.
- **Ui**: the Zenith preset's dark block now inverts its primary pairing like the newer palettes -
  bright spring-green primary (`oklch(0.74 0.14 155)`) under a near-black label - instead of white
  on mid emerald. The old pairing was the worst in the audit (3.29:1 raw, 4.86:1 after the button
  deepen); the new one measures 8.58:1 raw, 5.61:1 deepened, and 8.67:1 for primary used as link
  text on the dark background. Light mode is unchanged.
- **Ui**: the remaining primary-filled text surfaces - default badge (seven skins; ash's
  text-only badge was already fine), primary tooltip, toast action and avatar badge - now deepen
  their dark-mode fill 15% toward black in oklab, exactly like the default button. A contrast
  audit of all 15 palettes found seven (comet, meteor, nebula, pulsar, quasar, zenith and the
  default Nova) whose dark `--primary` sits at 3.3-4.0:1 under `--primary-foreground`, below
  WCAG AA for text; the deepened fill clears 4.5:1 in every palette. Non-text primary fills
  (checkbox, radio, switch, slider, progress) stay raw - the worst pairing is 3.29:1, above the
  3:1 graphical minimum.

### Changed
- **Base, Ui** (breaking): tabs default to manual keyboard activation. Arrow keys move focus along
  the tablist and Enter or Space activates the focused tab; a tab no longer loads its panel just
  because focus passed over it on the way somewhere else. `ActivationMode="TabsActivationMode.Automatic"`
  restores selection-follows-focus per instance.
- **Ui**: an Expand toolbar opens and closes with a 150ms height tween instead of snapping. The
  clip now interpolates to the content's own height (`interpolate-size` - Chromium animates,
  other engines keep the instant swap), reduced-motion users keep the instant swap too, and the
  viewport carries 2px of inner breathing room so a control's focus ring is no longer shaved by
  the clip while the bar is collapsed.
- **Ui, Cli** (breaking, 0.1.0-alpha.20): `scrollbar-thin` is a project-level opt-in. The
  components still mark their scroll areas with it (select and command lists, menus, the
  virtualizer, scrollable tables, the sidebar), but `blaizio.css` now ships the utility as an
  inert stub, so an app that never asked keeps the browser's own scrollbars everywhere instead of
  inheriting a look it did not choose. Opting in redefines the utility in the app's own Tailwind
  input - `--scrollbar` on `new`, `add` or `apply` appends the block, or paste it from the
  Scrollbar page - and every mark lights up at once. The block lands in your file: restyling the
  bar is an edit, opting back out is a delete. Projects that want the previous behavior need the
  flag once. `scrollbar-hover`, `scrollbar-activity`, `scrollbar-none` and `scrollbar-auto` are
  unchanged - they are only ever applied by hand.
- **Cli**: `apply` gained `--pointer` and `--scrollbar`, the same wiring toggles `new` and `add`
  take, so a Themes selection reaches an existing app in one command. A full `apply` of a code
  carrying RTL now also records the flag and installs `direction-provider` once, matching what
  `add --rtl` has always done - the skins mirror through logical properties on their own, but a
  layout flips direction through the provider.
- **Ui** (breaking): `TooltipVariant.Default` is now the inverted foreground surface
  (`bg-foreground` / `text-background`), the conventional tooltip look, and the solid primary
  surface moves to its own `TooltipVariant.Primary`. `BzTooltipContent.Variant` previously
  defaulted to `Accent` while the enum's `Default` member painted primary; both now agree on
  `Default`. A tooltip that relied on `TooltipVariant.Default` for the primary fill needs
  `TooltipVariant.Primary`; one that relied on the accent default needs an explicit
  `TooltipVariant.Accent`.
- **Base, Ui** (0.1.0-alpha.18): event callbacks keep their `On` prefix - `OnClick`, `OnSelect`,
  `OnFocus`, `OnBlur`, `OnKeyDown`, `OnDragStart`, `OnDragEnd`. This reverts the alpha.17 rename:
  the ecosystem-standard names win over attribute pass-through. The documented consequence stands -
  a raw DOM attribute of the same name on a component (e.g. `onfocus="..."` as a JS string) binds
  to the parameter instead of reaching the element; use the C# callback, or put the raw attribute
  on the element via `RenderAs`.

### Added
- **Cli**: `registry:theme` items install. A theme item carries a `cssVars` payload split into
  `light` and `dark` maps (token names with or without the `--` prefix); `add` patches the values
  into the tokens file's `:root` / `.dark` blocks declaration by declaration, leaving everything
  else in the file alone. `registry validate` accepts file-less theme/font items but requires
  their payloads.
- **Cli**: namespaced installs nest. `add @acme/button` now writes under its own registry folder
  (`Components/Ui/Acme/`), one namespace segment down (`MyApp.Components.Ui.Acme`), and is
  recorded as `@acme/button` - so two registries can both ship a `button` without colliding on
  disk, in C#, or in `blaizio.json`. `diff`, `remove` and whole-registry `--prune` are all
  namespace-aware (a prune never touches other registries' files or records).
- **Docs**: the /community page - a searchable, paginated directory of community registries (with
  per-entry add-command dialogs) and a community theme gallery whose entries can be applied to the
  whole docs site live or copied as CSS. Driven by two committed JSON files under
  `wwwroot/community/`; listing is a pull request. A new "Registry" guide (docs/registry) covers
  the manifest format, authoring components and themes, hosting, consumption, trust, and the
  get-listed flow.

- **Cli**: a trust gate in front of third-party sources. `registry add` states what recording a
  registry means (installed items are source code compiled into your app, plus their NuGet
  packages) and asks for confirmation when a terminal is attached (`-y` skips; scripts proceed
  with the note printed). `add` with a direct URL from a host that is neither the configured
  registry nor recorded under `registry add` warns and confirms the same way.

### Changed
- **Base, Ui** (breaking, audit batch 3 waves 1+2): one enum per axis and one name per concept
  across the component surface. Shared SelectionMode replaces AccordionType/ToggleGroupType/
  TreeSelectionMode and Select/Combobox bool Multiple; CarouselOrientation/ResizeDirection fold
  into Orientation; TreeCheckState into CheckedState; size enums say Default (not Md) and
  TooltipVariant leads with Default. Renames: Active (was IsActive/Current/Selected), the
  Search/DefaultSearch/SearchChanged triad (was Query/SearchTerm/Filter), FilterPredicate,
  DismissOnOutsideClick (positive, was PreventDismiss/DismissOnClick), Virtualize/ItemSize/
  Overscan, Toast provider Duration/CloseButton/RichColors. Carousel gains bindable
  SelectedIndex; InputOtp gains DefaultValue; BzInputTags/BzSlider surface their base bindings.
  Every emitted class, data-* attribute and JS string is unchanged - only C# names moved.
- **Base, Ui** (breaking, audit batch 3 wave 3): the last rename pass. Table's public types
  drop the phantom "DataGrid" name (DataTableRequest/Result/ItemsProvider/Sort/Column, was
  DataGrid*/GridSort/ColumnDef). Sheet and Drawer share one PanelSide enum (both params `Side`;
  SheetSide/DrawerDirection deleted). The element-tag parameter is `Element` everywhere (was
  `As`, one letter from the unrelated RenderAs). Placeholder splits into string `Placeholder` +
  fragment `PlaceholderContent` on Select and Combobox values. Disabled predicates are
  `DisabledSelector` (Calendar, Sortable). ScrollArea `Type`->`Behavior`, PieChart
  `Color`->`ColorSelector`, Slider `Tooltip`->`TooltipMode`, Sidebar `Collapsible`->`CollapseMode`,
  DirectionProvider `Direction`->`Dir`, InputNumber step `Direction`->`StepDirection`.
  AvatarStatus folds into ImageStatus; UiDialogOptions is BzDialogOptions; OtpSlotState says
  Active/ShowCaret; `*Template` fragments are `*Content`; CarouselDots/SelectValue/CommandDialog/
  ToastProvider gain the attribute splat; ColumnBase `Hidden` inverts to `Visible`; ellipsis
  `MoreLabel` is `AriaLabel`. Wire strings all unchanged.
- **Cli** (breaking, audit batch 1 - blaizio.json + registry schema): the config field that
  stored the skin is now `style` (it shipped as `theme`, colliding with registry theme items),
  and the font pair is `headingFont`/`bodyFont` (was `heading`/`font`). Old files keep working:
  the legacy names are accepted on load and rewritten on the next save. The dead `$schema`
  default (a 404 URL) is no longer written. Registry file entries type as `registry:ui`/
  `registry:lib` only (a new `FileType`, same wire strings - theme/font/template are item kinds,
  not file kinds), and inlined `content` is immutable after parse.
- **Cli** (breaking, audit batch 2): `--json` is output-format only - a machine-driven add/init
  now installs packages and wires the Tailwind pipeline exactly like a human run, and the JSON
  document reports what actually happened. `add -o|--output` is gone (files written outside the
  configured output directory were recorded on paths remove/uninstall/diff resolve against the
  wrong root). The add trust gate records accepted origins in blaizio.json `trustedHosts`, so a
  host confirms once, ever. `update --dry-run` reports the full plan without touching anything.
  New `registry list` and `registry remove` (alias `rm`); `search`'s `list` alias now shows in
  help; `build`/`registry validate` positionals read `[manifest]`. Internal-only Core helpers
  (CssBlocks, TokenOverlays, ProcessRunner, GlobalUsingsWriter, ImportsUpdater) are internal now,
  and the never-read `aliases.ui` config entry is no longer written.
- **Cli** (breaking): registry item JSON no longer carries the dead `tailwind` field (it was read
  by nothing), and `search -o` is gone - `--offset` is long-only, since `-o` means `--output` on
  every other command. Declining the Tailwind download confirm now exits 0 like every other
  declined confirm. The full pre-beta consistency sweep lives in `docs/api-freeze-audit.md`.
- **Cli** (breaking, audit batch 4 - the shared flags say what they mean): every command now
  carries exactly the shared flags it honors. The common surface is tiered - all commands take
  `-c`/`-s`/`--json`; only commands that confirm take `-y`; only commands that read a registry
  take `--registry` - so `info`, `contrast`, `eject`, `generate`, `build` and the `tailwind`
  subcommands no longer accept-and-ignore `--registry`, and `search`/`view`/`info` no longer
  accept a `-y` they never prompt for. `docs` gains `-s` and its `--registry` is documented
  (it was hidden and a hard parse error). The `preset` subcommands share one surface (`url`/
  `open` took no flags at all; every leaf now takes `-c`/`-s`/`--json`). `registry add`/`list`/
  `remove`/`validate` all emit `--json` (`validate` reports a findings array on every outcome -
  missing, unparseable, invalid, valid - for CI). `apply --dry-run` reports the full plan
  (re-installs, theme, fonts, tokens) without touching anything, same as `update`. `--json`
  stdout is uniformly compact (one line per document; on-disk files stay indented), the last
  hardcoded JSON string literals are serialized for real, and `add --json`'s file entries carry
  one `path` (output-relative, POSIX) instead of `relativePath`+`absolutePath`. `init`'s
  never-registered CLI surface is gone - the wiring pipeline is programmatic-only behind
  `new`/`add`, whose flags forward to it.

### Fixed
- **Base**: the incremental-build stamp moved from `wwwroot/dist/.stamp` into `obj/`. As a static
  web asset it was listed in the packaged manifest but stripped from the NuGet payload (default
  dotfile excludes), so every consumer build failed with MSB3030 trying to copy a file the
  package never shipped.
- **Base**: the packaged contract sheet (`css/blaizio.css`) now includes the shimmer utilities
  (`shimmer`, `shimmer-block`, and their knobs) - registry components that emit them
  (`BzSkeleton`) rendered without a sweep against the alpha.16 sheet.
- **Cli**: `CssBlocks.FindBlock` treated top-level statements (`@import ...;`,
  `@custom-variant ...;`) as part of the next block's selector, so a tokens file whose first
  braced block followed such statements was invisible to every scoped patch (fonts, presets,
  chart/radius overlays). Statements now terminate the selector prelude.

## 0.1.0-alpha.16 — 2026-07-26

### Fixed
- **Base**: the select listbox keeps its focus inside a dialog - opening one from a dialog left the
  keyboard dead (arrows and typeahead did nothing) and made Escape close the dialog instead of the
  listbox. Both were the same defect: the listbox portals to the document body, so focus landing on
  an option is outside the dialog's subtree, and the dialog's focus trap - which tests containment
  by DOM alone - yanked it straight back to the trigger; with focus stranded there, Escape bubbled
  into the dialog's own handler. Menus and popovers were never affected because each wraps a
  `BaseFocusScope`, and scopes stack, which is what pauses the trap beneath. The select's surface
  stays mounted while closed (its options register the trigger's display value), so it now creates
  a scope from its JS attach path instead - `passive`, a new option that claims the scope stack and
  nothing else, since `ts/menu.js` already owns focus placement and restoration for a listbox.
  Stacked dialogs compose for free: the stack is LIFO, so a passive scope pauses whatever is
  beneath it at any depth.

## 0.1.0-alpha.15 — 2026-07-26

### Fixed
- **Ui**: a popup opened inside a dialog no longer paints behind it. Popups portal to the document
  body, so one opened from a dialog is a stacking *sibling* of that dialog rather than a
  descendant - the z-indices compete instead of composing. Every floating surface sat at the base
  layer (`z-50`) while `BzDialogProvider` stacks each imperatively shown dialog above it (overlay
  60, window 61, then 70/71...), so a select, dropdown menu, combobox, context menu, menubar,
  popover, hover card or tooltip opened inside a service dialog was simply invisible. Raising the
  constant would not fix it - against dialogs at 60/61, 70/71, 80/81 any fixed value is either
  under a later dialog or above one it should sit beneath - so the modal surfaces (dialog, alert
  dialog, sheet, drawer) now cascade the layer they occupy and each popup lifts itself to
  `layer + 5`: clear of that window, short of the next stacked surface. A popup in the first of two
  stacked dialogs still sits correctly under the second, and outside a modal nothing is emitted, so
  the stylesheet keeps governing. The new `OverlayLayer` ships with the `utils` item.

## 0.1.0-alpha.14 — 2026-07-26

### Changed
- **Base**: ⚠️ `BaseInputNumberIncrement` and `BaseInputNumberDecrement` are replaced by one
  `BaseInputNumberStep` with a `Direction` (`InputNumberStepDirection.Increment` / `Decrement`).
  The parts were 46 lines each and differed in six - an aria-label, a data attribute, the `Can*`
  flag gating them and the sign they press with. The markup changes with them: one
  `data-bz-input-number-step` part carrying `data-direction="increment|decrement"`, so a skin can
  target the pair or either half. An audit found this was the only such split in the library.
- **Ui/Base**: ⚠️ the Image surface hands the edited picture back as a parameter instead of
  offering a download button. `BzColorImage`'s `Download` / `DownloadAriaLabel` /
  `DownloadFileName` and the picker's `ImageDownload` are gone; bind `Export` (or the picker's
  `ImageExport`) for the picture as a `data:` URL - rotation and the tone grade baked into the
  pixels - and save, upload or paint it yourself (an `<a download>` is the whole download). It is
  produced only when something is bound, since baking re-encodes the bitmap, and settles after
  edits stop (`ExportDelayMs`, default `300`) rather than firing on every slider tick. An
  untouched image keeps its original encoding; a graded one comes back as PNG. `imageFill.ts`
  exports `exportImage` in place of `downloadImage`.

### Fixed
- **Base**: `BaseInputNumber` refuses characters that could never be part of a number - a typed
  letter used to sit in the field until blur reverted it. The field stays `type="text"` on purpose
  (`role="spinbutton"` owns the semantics, and a native number input brings a spinner that
  duplicates the stepper buttons, a scroll wheel that changes the value by accident, and a `value`
  that reads back empty for anything the browser dislikes), so `ts/inputNumber.ts` does the one
  thing `type="number"` gave for free: a `beforeinput` guard refuses the character before it is
  inserted, keeping the caret and undo stack intact and covering paste, drops and IME commits.
  What passes is loose (anything that could still grow into a number) and context-aware: no
  decimal separator for an integral `TValue`, no minus when `Min` rules out negatives, and the
  culture's own separator alongside the keypad's dot.

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
