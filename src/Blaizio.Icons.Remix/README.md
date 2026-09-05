# Blaizio.Icons.Remix

[Remix Icon](https://remixicon.com) for Blazor, rendered by `BzIcon` from
[Blaizio.Icons](https://www.nuget.org/packages/Blaizio.Icons).

```razor
<BzIcon Icon="Remix.Line.Home" Class="size-4" />
<BzIcon Icon="Remix.Fill.Home" Class="size-4" />
```

- Two families: `Remix.Line.*` and `Remix.Fill.*`, 24px grid, both drawn as solid paths (Remix
  outlines are filled shapes, not strokes).
- Mixes freely with Tabler (`Icons.Outline.*`), Lucide, Phosphor and Hugeicons in one app.
- Tree-shakeable: each icon is a self-contained property, and a trimmed WebAssembly publish keeps
  only the ones you reference.
- Sizing and colouring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from the Remix Icon source by `scripts/Update-BlaizioIcons.ps1 -Set Remix`
in the Blaizio repository. The icon data keeps Remix Icon's Apache-2.0 licence
(THIRD-PARTY-LICENSE.txt); the package code is MIT.

Part of the [Blaizio](https://blaiz.io) component framework. Browse every icon at
https://blaiz.io/docs/components/icons.
