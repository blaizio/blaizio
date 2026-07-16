# v3 skin audit — inline-expressibility inventory

Step 1 of the CSS layout v3 build order (see cli-plan.md). Every rule in `shared.css`, the 8
`style-*.css` skins and `blaizio.css` classified for the inliner. Verdict up front: **no
blockers** — every non-simple rule falls into one of six mechanical patterns below.

## Counts (rules)

| File | SIMPLE¹ | MULTI² | COMPLEX-SEL | RAW-DECL | AT-RULE |
|---|---|---|---|---|---|
| shared.css | 193 | 2 | 65 | 12 | 6 |
| style-\*.css (each, ±2) | ~161 | ~5 | ~8 | ~3 | 0 |
| blaizio.css (contract) | 0 | — | 48 | 3 | 56 |

¹ single `.bz-*` selector, `@apply`-only body — inlines as-is.
² comma list of simple `.bz-*` names, `@apply`-only — the inliner applies the list to every
listed token. Inliner feature, not a rewrite.

## The line

After the rewrites below, the invariant is: **every `.bz-*` rule in shared/skins is SIMPLE or
MULTI; everything else lives in the contract sheet.** The inliner never parses selectors beyond
`.bz-token` — complexity is expressed as Tailwind variants inside the `@apply` lists instead.

## Dispositions

### A. Self-attribute rules → `data-[…]:` variants on the same token (rewrite in place)

`.bz-attachment[data-size='sm']`, `.bz-attachment-dropzone[data-dragging]`,
`.bz-pagination-link[data-active="true"]` (shared + every skin),
`.bz-progress[data-orientation='vertical']` (every skin), `.bz-toast:focus-visible`,
`.bz-input-otp-slot:first-child/:last-child` (every skin),
`.bz-breadcrumb-separator:empty::after` (every skin) →
fold into the token's own rule as `data-[size=sm]:…`, `data-dragging:…`, `first:…`,
`empty:after:content-[…]`, `rtl:…` (for the `[dir="rtl"]` doubles in ember/glow/spark) variants.
The `data-vertical`/`data-horizontal` custom variants already exist in the contract.

### B. Parent-state → child rules → named `group` variants (rewrite in place)

The big shared.css bucket (~40 rules): attachment state → media/title/description, toast type →
icon (`.bz-toast[data-bz-toast-type="success"] .bz-toast-icon`), progress/circular-progress/
slider `data-color` → indicator/range/thumb, rich-colors toast → icon/description/close.
Rewrite: parent token's list gains `group/<name>` (e.g. `group/attachment`), child token's list
gains `group-data-[state=error]/attachment:…`. Components already emit the data attributes; only
the sheets change. Groups needed: `attachment`, `progress`, `circular-progress`, `slider`,
`toast`.

### C. Specificity hacks → delete, rely on merge order

`.bz-input-group-input.bz-input-group-input(.bz-input-group-input)` (shared + skins),
`.bz-checkbox[data-color='success'][data-color]`, `.bz-switch[data-color='…'][data-color]`.
These exist only to beat other sheet rules in the cascade. Inline classes have no cascade —
`Tw.Merge(base, colorOverride, Class)` argument order decides. Delete the hacks; **audit the
components so the winning token is merged later** (input-group input after input-text; color
token after base). Golden tests catch regressions.

### D. Descendant element rules → arbitrary variants

`.bz-table-sticky th` → `[&_th]:…` on the table-sticky token.
`[dir="rtl"] .bz-pagination-prev svg` (+next/first/last) → `rtl:[&_svg]:…` on each token.

### E. Chart block → contract sheet (move; classes stay in markup)

`.bz-chart*`: 12 RAW-DECL rules (stroke/fill/transform-origin raw props), 7 animate rules
(`.bz-chart[data-animate='true'] .bz-chart-bar` …), 5 `bz-chart-*` keyframes and the
reduced-motion gate. This is animation-coupled component infrastructure — it moves wholesale to
the contract **keeping its `.bz-chart-*` selectors**, and the chart tokens are deliberately
absent from the inliner map: a token with no map entry passes through substitution verbatim, so
the classes stay in the markup for the contract to target (same mechanism as `data-bz-toast`).
The skins' tiny chart overrides (`.bz-chart-bar` rx, `.bz-chart-grid-line` dasharray — 2-4 raw
decls per skin) are skin-VARYING values, which in v3 means tokens: the contract rules read CSS
vars (`rx: var(--bz-chart-bar-radius, 0)`) and the per-skin values bake into the tokens file's
`:root` at init/apply.

### F. Global pin → contract sheet (move)

`[data-state='closed'] { --tw-animation-fill-mode: forwards; }` in shared.css is not tied to a
`bz-*` token — it belongs next to the animation vars it modifies. Move to the contract sheet.

### G. Skin RAW-DECL → arbitrary-property utilities

`.bz-calendar` (custom-prop assignments like cell sizing) → `[--cell-size:2rem]`-style entries
in the token's `@apply` list. `.bz-slider` raw decls likewise. All arbitrary-property-expressible.

### Already contract (no action)

Everything in `blaizio.css` today: toast positioning/swipe machinery (`[data-bz-toast*]`, 40+
rules, swipe keyframes, spinner `nth-child` delays), virtualizer/scroll-fade, scrollbar
utilities, `@custom-variant`s, accordion/collapsible/shimmer keyframes + animation vars.
None of it is skin-varying; none of it is per-component look. It IS the v3 contract sheet.

## Inliner requirements confirmed by this audit

1. Token → merged `@apply` list (shared baseline, then skin, via TailwindMerge.NET).
2. MULTI selector lists: apply the body to each listed token.
3. Substitution across `.razor` **and** `.cs` shipped sources (variant literals — step 2).
4. No selector parsing beyond `.bz-token` — patterns A–D are expressed as variants inside the
   lists once rewritten.

## Execution note

The physical rewrites (A–D, G) and moves (E, F) land **with step 3**, where the golden-file
suite can prove the substituted output — rewriting variants blind, before the inliner exists to
verify them, would be change without a net.
