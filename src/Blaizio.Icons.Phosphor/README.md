# Blaizio.Icons.Phosphor

[Phosphor](https://phosphoricons.com) icons for Blazor, rendered by `BzIcon` from
[Blaizio.Icons](https://www.nuget.org/packages/Blaizio.Icons).

```razor
<BzIcon Icon="Phosphor.Regular.House" Class="size-4" />
<BzIcon Icon="Phosphor.Duotone.House" Class="size-4 text-primary" />
```

- All six weights, one family each: `Phosphor.Thin`, `Light`, `Regular`, `Bold`, `Fill`,
  `Duotone`. Every weight is drawn as solid paths on a 256px grid; the `Icon` value carries that
  grid, so no `ViewBox` to set.
- Mixes freely with Tabler (`Icons.Outline.*`), Lucide, Remix and Hugeicons in one app.
- Tree-shakeable: each icon is a self-contained property, and a trimmed WebAssembly publish keeps
  only the ones you reference.
- Sizing and colouring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from the Phosphor source by `scripts/Update-BlaizioIcons.ps1 -Set Phosphor`
in the Blaizio repository. The icon data keeps Phosphor's MIT licence (THIRD-PARTY-LICENSE.txt);
the package code is MIT.

Part of the [Blaizio](https://blaiz.io) component framework. Browse every icon at
https://blaiz.io/docs/components/icons.
