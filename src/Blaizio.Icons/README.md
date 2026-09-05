# Blaizio.Icons

`BzIcon`, a single tree-shakeable SVG icon component for Blazor, and the `Icon` value it renders.
The icon sets themselves are separate packages on top of this one, versioned together, and mix
freely in one app:

| Package | Members | Icons |
| --- | --- | --- |
| `Blaizio.Icons.Tabler` | `Tabler.Outline.*`, `Tabler.Filled.*` | [Tabler](https://tabler.io/icons), MIT - the set the Blaizio components use |
| `Blaizio.Icons.Lucide` | `Lucide.Outline.*` | [Lucide](https://lucide.dev), ISC |
| `Blaizio.Icons.Phosphor` | `Phosphor.Thin/Light/Regular/Bold/Fill/Duotone.*` | [Phosphor](https://phosphoricons.com), MIT |
| `Blaizio.Icons.Remix` | `Remix.Line.*`, `Remix.Fill.*` | [Remix Icon](https://remixicon.com), Apache-2.0 |
| `Blaizio.Icons.HugeIcons` | `HugeIcons.StrokeRounded.*` | [Hugeicons](https://hugeicons.com) free set, MIT |

```razor
<BzIcon Icon="Tabler.Outline.Settings" Class="size-4" />
<BzIcon Icon="Lucide.Outline.House" Class="size-4" />
```

- Each icon is a typed `Icon` value carrying its paint (outline or filled), grid and stroke width,
  so one component renders every set at its true weight.
- Tree-shakeable: each icon is a self-contained property; a trimmed WebAssembly publish keeps only
  the icons you reference.
- Sizing and coloring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from each set's source by `scripts/Update-BlaizioIcons.ps1` in the Blaizio
repository; the icon data keeps its own licence (THIRD-PARTY-LICENSE.txt in each set package).

Part of the [Blaizio](https://blaiz.io) component framework; used by the styled components the
`blaizio` CLI copies into your app, and just as usable on its own. Browse every icon at
https://blaiz.io/docs/components/icons.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
