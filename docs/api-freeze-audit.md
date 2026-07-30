# API freeze audit - the pre-beta punch list

Findings from a full-surface consistency sweep (2026-07-30) of the component API
(Blaizio.Base + Blaizio.Ui), the CLI surface, and the serialized schemas
(blaizio.json + registry JSON). Beta freezes whatever is left unfixed here, so each item is a
decision: fix before beta, or accept forever. Ranked most freeze-critical first within each
section. Status: FIXED = landed, OPEN = needs a decision/work.

## Components (Base + Ui)

| # | Status | Finding | Fix |
|---|---|---|---|
| C1 | OPEN | Toast provider renames the same params between layers and overloads `Default*`: `BaseToastProvider.DefaultDuration/DefaultCloseButton/DefaultRichColors` vs `BzToastProvider.Duration/CloseButton/RichColors`. `DefaultX` elsewhere = uncontrolled initial value. | Use `Duration/CloseButton/RichColors` in both layers. |
| C2 | OPEN | Four names for "this item is active": `Active` (NavigationMenuLink, PaginationLink), `IsActive` (SidebarMenuButton/SubButton), `Current` (BreadcrumbItem), `Selected` (TableRow). | Standardize on `Active`. |
| C3 | OPEN | Search text triad named 4 ways: `Query` (Combobox, InputTags), `Search` (Command), `SearchTerm` (Tree), `Filter` (DataTable). | One triad: `Search/DefaultSearch/SearchChanged`. |
| C4 | OPEN | `Filter` = predicate delegate on BaseCombobox but = search string on BzDataTable. | `FilterPredicate` for the delegate, `Search` for text. |
| C5 | OPEN | Carousel `OnSelectedIndexChanged` is On-prefixed with no backing `SelectedIndex` param - `@bind-SelectedIndex` impossible. | Add `SelectedIndex`/`DefaultSelectedIndex`, rename callback `SelectedIndexChanged`. |
| C6 | FIXED | `ColorPickerSize.Md`, `QrCodeSize.Md`, `SidebarMenuSubButtonSize.Md` vs `Default` in the other ten Size enums; SidebarMenuButton vs SubButton use different scales. | Rename `Md` to `Default`; align the sidebar pair. |
| C7 | FIXED | `TooltipVariant` is the only variant enum without `Default` and the only one with `Primary` (default is `Accent`). | `Primary` becomes `Default`, reorder. |
| C8 | FIXED | `CarouselOrientation` and `ResizeDirection` duplicate shared `Orientation` member-for-member; Sortable/ScrollArea/Field add one member each. | Use `Orientation` where identical. |
| C9 | FIXED | `TreeCheckState` is a member-for-member clone of `CheckedState`. | Delete `TreeCheckState`. |
| C10 | PARTIAL | `Direction` param name covers four unrelated meanings; RTL is `Dir` everywhere except `BzDirectionProvider.Direction`. | Resizable slice done (`Orientation`); RTL `Dir`, drawer `Side`, InputNumber `StepDirection` still open. |
| C11 | OPEN | Table public types named after a nonexistent "DataGrid": `DataGridRequest/Result`, `DataItemsProvider`, `GridSort`, `ColumnDef`. | `DataTableRequest/Result/ItemsProvider/Sort/Column`. |
| C12 | OPEN | Edge axis has three enums: `SheetSide`, `DrawerDirection` (identical members), `SidebarSide`, plus shared `Side`. | One logical `Side {Start,End,Top,Bottom}` for Sheet/Drawer/Sidebar. |
| C13 | OPEN | Dismissal polarity mixed: `PreventDismiss` (roots) vs `DismissOnOutsideClick` (contents) vs `DismissOnClick` (overlay). | Single positive `DismissOnOutsideClick` at every level. |
| C14 | OPEN | `Placeholder` is `RenderFragment` on BaseSelectValue but `string` on BzSelectValue (Combobox keeps fragment on both). | `Placeholder` = string, `PlaceholderContent` = fragment, uniformly. |
| C15 | OPEN | Disabled-predicate conventions: `IsDateDisabled`, `DisabledSelector`, `IsItemDisabled`. | `*Selector` suffix throughout. |
| C16 | OPEN | Virtualization params disagree: `Virtualized/Virtualize/Dynamic`, `RowHeightPx/RowHeight/ItemSize`, `VirtualOverscan/Overscan`. | `Virtualize`, `ItemSize`, `Overscan` everywhere. |
| C17 | OPEN | `As` (element tag string) vs `RenderAs` (render delegate) - unrelated features one letter apart. | Rename `As` to `Element`. |
| C18 | OPEN | `Type`, `Color`, `Tooltip`, `Collapsible` each carry 2-3 incompatible types under one name across components. | Reserve `Type` for the HTML attribute; `SelectionMode`/`ScrollBehavior`/`ColorSelector`/`TooltipMode`/`CollapseMode`. |
| C19 | FIXED | Single/multiple axis encoded three ways: `AccordionType`/`ToggleGroupType` enums, `TreeSelectionMode`, `bool Multiple` (Select, Combobox). | Shared `SelectionMode {None,Single,Multiple}`. |
| C20 | OPEN | `BaseInputOtp` is the only controllable Base component missing `DefaultValue`. | Add it. |
| C21 | OPEN | Styled wrappers drop base binding surface inconsistently: BzInputTags omits the Query triad, BzSlider omits `OnValueCommit` (BzColorPicker keeps it). | Mirror the full set or none, per family. |
| C22 | OPEN | `AvatarStatus` subset-duplicates `ImageStatus`; `ToastType` vs `StatusColor` = two vocabularies for the semantic-color axis. | Keep `ImageStatus`; reconcile toast/status naming. |
| C23 | OPEN | `OtpSlotState.HasFakeCaret` leaks a rendering hack; `Is*` prefixes there contradict the rest; `UiDialogOptions` is the lone `Ui*` public type; unprefixed public helpers (`Tw`, `Identifier`, `PropBuilder`, `ControllableState`) sit beside `Bz*` ones. | Rename per convention (`BzDialogOptions`, `ShowCaret`, ...). |
| C24 | OPEN | Slot-fragment suffixes mixed (`*Template` vs `*Content` vs bare noun); label slot named `Heading`/`Label`/`Title` across families. | `*Content` for fragments; `Label`+`LabelContent` for label roles. |
| C25 | OPEN | Four styled components declare `Class` without the attribute splat (CarouselDots, SelectValue, CommandDialog, ToastProvider); `Hidden` is the lone negative visibility bool; `MoreLabel` vs `AriaLabel`; Pagination-only explicit `OnClick`s. | Add splats; `Visible`; `AriaLabel`; drop the `OnClick`s. |

Consistent already (locked in happily): `Open/DefaultOpen/OpenChanged` across all 27 open-state
components, `Disabled` (89 uses, zero variants), `Inline` across all 23 popup surfaces,
`Class` + `Attributes` splat on 327/372 styled components.

## CLI + schemas

| # | Status | Finding | Fix |
|---|---|---|---|
| S1 | FIXED | `--json` changes behavior, not just format: init leg skips NuGet install + Tailwind setup under `--json`, so IDE/MCP-driven adds wire projects differently. | Make `--json` output-only. |
| S2 | FIXED | `add -o <dir>` writes where `remove`/`uninstall`/`diff` will not look (record is output-dir-relative, resolved against `config.Output`). | Drop `-o` from `add`, or record per-item output dir. |
| S3 | FIXED | One concept, three names: config field `theme` stores the SKIN set by `--style`; registry calls the list `styles`; `ItemType.Theme` means token set. Also `heading`/`font` where `font` means body. | Renamed: `style`, `headingFont`, `bodyFont`; legacy names accepted on load, never written. |
| S4 | FIXED | `-o` meant `--offset` on `search` but `--output` everywhere else. | `--offset` is long-only now. |
| S5 | FIXED | `RegistryFile.Type` reuses item-level `ItemType` (theme/font/template meaningless per-file); `Content` has a public setter while siblings are init-only. | `FileType {Ui,Lib}` (same wire strings); `Content` is init-only. |
| S6 | FIXED | `tailwind.content` was dead schema - parsed, copied by `build`, read by nothing. | Removed `TailwindConfig` and `RegistryItem.Tailwind`. |
| S7 | OPEN | `installed` records only file paths - no source registry, skin, or hash, so `update`/`diff` cannot detect provenance or drift offline. | Downgraded: additive fields are non-breaking post-freeze (old CLIs ignore them). Provenance is already the qualified `@ns/name` key; add `style`/`hash` whenever the need is real. |
| S8 | OPEN | `registry add`/`registry validate` are standalone settings missing `--json` (validate is CI-shaped and can only emit markup). | Derive from GlobalSettings; `--json` findings array. |
| S9 | OPEN | `docs` hides `--registry` and lacks `-s` (hard parse error under strict parsing). | Derive from GlobalSettings. |
| S10 | OPEN | `preset url`/`open` take no flags at all; `decode`/`resolve` lack `-s`. | One shared `PresetSettings`. |
| S11 | FIXED | Trust is prompted but never recorded - direct-URL installs re-prompt forever; declines leave no trace. | Add `trustedHosts` to blaizio.json (additive, cheap). |
| S12 | OPEN | `InitSettings` carries a full `[CommandArgument]`/`[CommandOption]` surface for a command that is not registered (`init` absent from CliApp). | Strip the attributes or register `init`. |
| S13 | OPEN | `--registry` accepted-and-ignored on info/contrast/eject/generate/build/tailwind; `-y` on search/view/info with no prompt. | Split GlobalSettings into Core/Registry/Confirm tiers. |
| S14 | PARTIAL | `update` and `apply` overwrite every component file with no `--dry-run` (less destructive commands have it). | `update --dry-run` landed; `apply --dry-run` still open (needs dry-run plumbing through the TailwindSetup patches). |
| S15 | OPEN | `--json` output inconsistent: pretty vs compact per command; `relativePath`+`absolutePath` vs `path` vs bare strings; `add --diff` empty doc is a hardcoded string literal. | `WriteIndented=false`, one `path` convention, serialize from real types. |
| S16 | FIXED | Generated projects bake non-live URLs: `$schema` https://blaiz.io/schema.json (does not exist), registry default 404s. | `$schema` dropped; reintroduce only once a real schema is published. |
| S17 | FIXED | `[registry]` positional on `build`/`registry validate` actually binds a local MANIFEST path, colliding with `--registry <url>`. | Rename positional to `[manifest]`. |
| S18 | FIXED | No `registry list`/`registry remove` (hand-edit only); `search`'s `list` alias unadvertised; `validate` reads oddly under "Manage registries". | Add list/rm; advertise alias. |
| S19 | FIXED | Declining the Tailwind download confirm exited 1; every other declined confirm exits 0. | Returns 0 now. |
| S20 | FIXED | Accidentally-public on packable Core: `ProcessRunner`, `CssBlocks`, `TokenOverlays`, `GlobalUsingsWriter`, `ImportsUpdater`; `aliases.ui` written but never read. | Internalize; drop `aliases.ui`. |

Consistent already: shared flag naming/descriptions byte-identical wherever present, blaizio.json
uniformly camelCase with explicit names, `registry:*` values uniform, error exit codes coherent
(2 registry / 130 Ctrl+C / 1 other).

## Suggested batching

1. **Schema breaks that touch every consumer file** (S3 rename + migration, S5, S7, S16) - one
   release, loud CHANGELOG, `update` migrates.
2. **CLI surface** (S1, S2, S8-S15, S17, S18, S20) - mostly mechanical, no consumer-file impact.
3. **Component renames** (C1-C25) - the big one; per-family sweeps with docs + registry + goldens
   regenerated together. Do the shared-enum consolidations (C6-C9, C12, C19) first - they touch
   the most call sites per decision.

Everything OPEN after beta ships is API forever. Delete rows as they land.
