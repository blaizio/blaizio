# Blaizio.Icons.Tabler

[Tabler icons](https://tabler.io/icons) for Blazor, rendered by `BzIcon` from
[Blaizio.Icons](https://www.nuget.org/packages/Blaizio.Icons). The set the Blaizio components
draw from, so the `blaizio` CLI installs it with every project.

```razor
<BzIcon Icon="Tabler.Outline.Settings" Class="size-4" />
<BzIcon Icon="Tabler.Filled.Star" />
```

- Two families: `Tabler.Outline.*` (stroked, width 2) and `Tabler.Filled.*` (solid), 24px grid.
- Every icon is a typed `Icon` value that carries its own paint, grid and stroke, so Tabler mixes
  freely with Lucide, Phosphor, Remix and Hugeicons in one app.
- Tree-shakeable: each icon is a self-contained property, and a trimmed WebAssembly publish keeps
  only the ones you reference.
- Sizing and colouring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from the Tabler source by `scripts/Update-BlaizioIcons.ps1 -Set Tabler` in
the Blaizio repository. The icon data keeps Tabler's MIT licence (THIRD-PARTY-LICENSE.txt); the
package code is MIT.

Part of the [Blaizio](https://blaiz.io) component framework. Browse every icon at
https://blaiz.io/docs/components/icons.
