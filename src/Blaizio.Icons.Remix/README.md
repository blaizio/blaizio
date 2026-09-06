# Blaizio.Icons.Remix

[Remix Icon](https://remixicon.com) for Blazor, rendered by `BzIcon` from
[Blaizio.Icons](https://www.nuget.org/packages/Blaizio.Icons).

```razor
<BzIcon Icon="Remix.Line.Home" Class="size-4" />
<BzIcon Icon="Remix.Fill.Home" Class="size-4" />
```

- Two families: `Remix.Line.*` and `Remix.Fill.*`, 24px grid, both drawn as solid paths (Remix
  outlines are filled shapes, not strokes).
- Mixes freely with Tabler (`Tabler.Outline.*`), Lucide, Phosphor and Hugeicons in one app.
- Tree-shakeable: each icon is a self-contained property, and a trimmed WebAssembly publish keeps
  only the ones you reference.
- Sizing and colouring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from the Remix Icon v4.8.0 release by `scripts/Update-BlaizioIcons.ps1 -Set Remix`
in the Blaizio repository. That release is the last one published under Apache-2.0, and the
icon data keeps that licence (THIRD-PARTY-LICENSE.txt); the package code is MIT.

Part of the [Blaizio](https://blaiz.io) component framework. Browse every icon at
https://blaiz.io/docs/components/icons.
