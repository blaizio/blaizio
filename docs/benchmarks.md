# Render benchmarks

Timed bUnit render harness: `dotnet run -c Release --project tests/Blaizio.Base.Benchmarks`.
Median of 5 iterations after 1 warmup, allocations from `GC.GetAllocatedBytesForCurrentThread`.
A cheap, repeatable before/after signal for consolidation work (audit batch 8), not
BenchmarkDotNet rigor - compare wall-clock on the same machine only.

Regression gate: every scenario carries an ALLOCATION ceiling (in Program.cs, ~50-100% above the
medians below). CI runs the harness with `BLAIZIO_BENCH_ASSERT=1`, which fails the build when a
ceiling is exceeded - allocations are deterministic per runtime version, so this catches a new
per-item component, dictionary or class merge without the flakiness of timing thresholds. Update
a ceiling deliberately, in the same commit as the change that moves it, and re-record the table.

## 2026-11-06 - DataTable column sizing, pinning and moving

Column widths live in a `colgroup` the script fills in, so the body rows carry no width at all; a
pinned cell carries its pin, its inset (a constant string per column) and, on the group's inner
edge, the divider mark. Resizing and moving cost nothing until used: the script runs every drag and
key, and a finished resize or move is one call. The read-only scenarios did not move (1000 rows:
3.6 MB, 10,000 rows: 34.3 MB).

| Scenario | Median render (ms) | Allocated (MB) |
|----------|-------------------:|---------------:|
| DataTable 1000 x 3 (pinned, sized, movable columns) | 12.8 | 5.2 |

Readings:

- **+1.6 MB over the plain 1000 x 3 table**, about 1.6 KB a row: the pinned cell's three
  attributes and the clipping class on every cell, nearly all of it the harness parsing the longer
  markup. No per-cell component, dictionary or class merge (the cell classes still resolve once per
  column per render).

## 2026-11-04 - DataTable editing

An editable column turns the table into a keyboard grid (`ts/dataTable.ts`). The cursor moves in
the browser with no .NET call and no render; one editor exists at a time; starting and committing
an edit each re-render the body once. The read-only scenarios above did not move (1000 rows: 3.6 MB,
10,000 rows: 34.3 MB), so the editing code costs a read-only table nothing.

| Scenario | Median render (ms) | Allocated (MB) |
|----------|-------------------:|---------------:|
| DataTable 1000 x 3 property columns | 7.3 | 4.1 |
| DataTable 1000 x 3 property columns (editable) | 8.4 | 4.5 |
| DataTable 1000 x 3 (editable): open + commit a cell | 17.2 | 8.5 |

Readings:

- **Idle editable table**: +0.4 MB over the same table read-only, about 400 bytes a row: the row
  address (`data-bz-row-index`) the grid script navigates by, and the harness parsing it. No
  per-cell component, dictionary or class merge.
- **Open + commit**: the first render plus two body passes (the editor in, the editor out with the
  new value). At 1000 rows that is O(rows); with `Virtualize` or `PageSize` it is O(rows in view).
  A save handed to `OnEdit` runs after the commit returns, so a slow save never holds up the cursor.
- **Library controls as editors** (re-measured the same day): the cell editor is now the library's
  own text box, number field, pick list, date or time field, hosted by one small component that
  re-renders itself (never the table) as its value changes. Open + commit stayed under the first
  measurement. The idle table did not move: a bool column's checkbox or switch was already a
  component per cell.

## 2026-10-25 - Code Block syntax highlighting

`SyntaxText` (Blaizio.Syntax, in Blaizio.Base when measured) tokenizes in C#; `BzCodeBlockContent Language="..."` renders one
markup frame per line and re-renders a line only when it changed. The streamed scenario feeds 120
lines one at a time and fails the run if an unchanged line rendered again, or if a line that turned
empty kept its old text. Timings include bUnit's own render and AngleSharp parse, so the
highlighted block's cost over the plain numbered one (~20 ms here) is the fair reading.

| Scenario | Median render (ms) | Allocated (MB) |
|----------|-------------------:|---------------:|
| SyntaxText C# 600 lines | 1.3 | 0.4 |
| CodeBlock C# 600 lines highlighted | 53.5 | 15.8 |
| CodeBlock C# 600 lines numbered, plain | 27.9 | 12.0 |
| CodeBlock C# 120 lines streamed line by line | 139.2 | 54.6 |

Readings:

- **Tokenizer**: 581 lines of C# in 0.84 ms, 612 of TypeScript in 0.79 ms, 538 of Razor in 0.88 ms
  and 2,606 of JSON in 0.71 ms (JIT, warm). For scale, on the same files under Node 22:
  highlight.js 3.2 / 7.4 / n/a / 60 ms, Prism 2.8 / 3.0 / 12.8 / 21 ms, Shiki 39-47 / 156-200 /
  84-149 / 96-117 ms.
- **Streaming**: the 581-line C# file fed one line at a time tokenizes in 5 ms in total, against
  2.9 s (Prism) and 3.6 s (highlight.js), which can only re-highlight everything, and 0.18 s for
  Shiki with its grammar state.

## 2026-08-01 - re-audit R4: DataTable row/cell render path rewrite

The 10k superlinearity bisected: a plain-markup 10k-row table renders in single-digit ms under
the same harness, so the cost was BzDataTable's own body path - two component instances plus an
attribute dictionary and a Tw.Merge per CELL. RenderBodyRow now emits raw tr/td render frames
(identical attributes; the selection checkbox stays a component), and per-column cell classes
resolve once per render instead of once per cell. An unpaged, non-virtualized table over 2,000
rows now logs a one-time console warning pointing at Virtualize/PageSize.

| Scenario | Median render (ms) | Allocated (MB) |
|----------|-------------------:|---------------:|
| Combobox 100 options (legacy indicator child) | 5.5 | 1.6 |
| Combobox 1000 options (legacy indicator child) | 66.4 | 19.6 |
| Combobox 100 options (SelectedIndicator fragment) | 4.5 | 1.4 |
| Combobox 1000 options (SelectedIndicator fragment) | 43.5 | 13.8 |
| Calendar month | 5.3 | 1.8 |
| Tree 1000 nodes (all expanded) | 223.7 | 76.2 |
| Tree 1000 nodes (Virtualize) | 5.5 | 1.8 |
| DataTable 100 rows x 3 cols | 2.0 | 0.6 |
| DataTable 1000 rows x 3 cols | 8.4 | 3.7 |
| DataTable 10000 rows x 3 cols | 254.7 | 34.3 |
| Plain markup table 1000 rows x 3 cols | 9.7 | 0.7 |
| Plain markup table 10000 rows x 3 cols | 59.5 | 5.8 |
| DataTable 10000 x 3 (PageSize 50) | 17.6 | 2.5 |
| DataTable 10000 x 3 (Virtualize) | 9.2 | 2.4 |

Readings:

- **DataTable 10k: 5,618.9 ms / 242.8 MB -> 254.7 ms / 34.3 MB** (22x time, 7x allocation). The
  documented "superlinear; unacceptable accidental default" is gone; scaling is now in line with
  the plain-markup baseline. 1k dropped 66.3 -> 8.4 ms.
- **Virtualize / PageSize** at 10k render in ~10-18 ms - the escape hatches are cheap and now
  benchmarked so they stay that way.
- **Tree 1000 expanded** is unchanged (223.7 ms / 76.2 MB) - per-node cost lives in
  BaseTreeNode's attribute building and is the R6 decomposition's problem, not a quick win; the
  new Virtualize scenario (5.5 ms / 1.8 MB) documents the supported mitigation.

## 2026-07-31 - batch 8 (SDK 10.0.302, Release, Windows 11, bunit 2.8.6)

| Scenario | Median render (ms) | Allocated (MB) |
|----------|-------------------:|---------------:|
| Combobox 100 options (legacy indicator child) | 5.2 | 1.6 |
| Combobox 1000 options (legacy indicator child) | 67.6 | 19.6 |
| Combobox 100 options (SelectedIndicator fragment) | 4.8 | 1.4 |
| Combobox 1000 options (SelectedIndicator fragment) | 42.0 | 13.8 |
| Calendar month | 4.6 | 1.8 |
| Tree 1000 nodes (all expanded) | 212.3 | 76.2 |
| DataTable 100 rows x 3 cols | 6.9 | 1.9 |
| DataTable 1000 rows x 3 cols | 66.3 | 23.7 |
| DataTable 10000 rows x 3 cols | 5618.9 | 242.8 |

Readings:

- **Combobox indicator fold (batch item 8.2)**: the parent-owned `SelectedIndicator` fragment vs
  the legacy per-item `CascadingValue` + `BaseComboboxItemIndicator` child - at 1000 options,
  **-38% render time and -30% allocations**. `BzComboboxItem` ships the fragment path by default;
  the child component remains for composition.
- **DataTable is superlinear**: 10x rows from 1k to 10k costs ~85x the time. This is the gate the
  audit set for the internal row/cell render-frame rewrite (batch 8 conditional item) - the
  hotspot is real, revisit with a profiler before restructuring.
- Tree at 1000 expanded nodes (212 ms, 76 MB) is the next-largest per-node cost; candidate for the
  batch 10 BaseTree decomposition to keep an eye on.

Regenerate this table after any consolidation of repeated subcomponents; keep old sections for
history when numbers shift materially.
