# Blaizio.Icons.HugeIcons

[Hugeicons](https://hugeicons.com) (the free stroke-rounded set) for Blazor, rendered by `BzIcon`
from [Blaizio.Icons](https://www.nuget.org/packages/Blaizio.Icons).

```razor
<BzIcon Icon="HugeIcons.StrokeRounded.Home01" Class="size-4" />
```

- One family: `HugeIcons.StrokeRounded.*`, 24px grid, stroke width 1.5. The `Icon` value carries
  that stroke, so it renders at Hugeicons' true weight next to Tabler's 2.
- Mixes freely with Tabler (`Tabler.Outline.*`), Lucide, Phosphor and Remix in one app.
- Tree-shakeable: each icon is a self-contained property, and a trimmed WebAssembly publish keeps
  only the ones you reference.
- Sizing and colouring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from the `@hugeicons/core-free-icons` npm package by
`scripts/Update-BlaizioIcons.ps1 -Set HugeIcons` in the Blaizio repository. The icon data keeps
Hugeicons' MIT licence (THIRD-PARTY-LICENSE.txt); the package code is MIT.

Part of the [Blaizio](https://blaiz.io) component framework. Browse every icon at
https://blaiz.io/docs/components/icons.
