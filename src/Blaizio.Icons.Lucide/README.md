# Blaizio.Icons.Lucide

[Lucide](https://lucide.dev) icons for Blazor, rendered by `BzIcon` from
[Blaizio.Icons](https://www.nuget.org/packages/Blaizio.Icons).

```razor
<BzIcon Icon="Lucide.Outline.House" Class="size-4" />
```

- One style, stroked: `Lucide.Outline.*`, 24px grid, stroke width 2.
- Every icon is a typed `Icon` value that carries its own grid and stroke, so Lucide mixes
  freely with Tabler (`Tabler.Outline.*`), Phosphor, Remix and Hugeicons in one app.
- Tree-shakeable: each icon is a self-contained property, and a trimmed WebAssembly publish keeps
  only the ones you reference.
- Sizing and colouring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from the Lucide source by `scripts/Update-BlaizioIcons.ps1 -Set Lucide` in
the Blaizio repository. The icon data keeps Lucide's ISC licence (THIRD-PARTY-LICENSE.txt); the
package code is MIT.

Part of the [Blaizio](https://blaiz.io) component framework. Browse every icon at
https://blaiz.io/docs/components/icons.
