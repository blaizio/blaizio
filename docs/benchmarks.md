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
