# Render benchmarks

Timed bUnit render harness: `dotnet run -c Release --project tests/Blaizio.Base.Benchmarks`.
Median of 5 iterations after 1 warmup, allocations from `GC.GetAllocatedBytesForCurrentThread`.
A cheap, repeatable before/after signal for consolidation work (audit batch 8), not
BenchmarkDotNet rigor - compare runs on the same machine only.

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
