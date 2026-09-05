# Blaizio.Icons

`BzIcon`, a single tree-shakeable SVG icon component for Blazor, with the
[Tabler icons](https://tabler.io/icons) set built in.

```razor
<BzIcon Icon="Icons.Outline.Settings" Class="size-4" />
<BzIcon Icon="Icons.Filled.Star" />
```

- Each icon is a typed `Icon` value carrying its paint (outline or filled), grid and stroke width,
  so one component renders every set at its true weight.
- Tree-shakeable: each icon is a self-contained property; a trimmed WebAssembly publish keeps only
  the icons you reference.
- Tabler outline and filled variants: `Icons.Outline.*`, `Icons.Filled.*`.
- Sizing and coloring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

## More sets

Four more sets ship as packages on top of this one, versioned together, and mix freely in one app:

| Package | Members | Icons |
| --- | --- | --- |
| `Blaizio.Icons.Lucide` | `Lucide.Outline.*` | [Lucide](https://lucide.dev), ISC |
| `Blaizio.Icons.Phosphor` | `Phosphor.Thin/Light/Regular/Bold/Fill/Duotone.*` | [Phosphor](https://phosphoricons.com), MIT |
| `Blaizio.Icons.Remix` | `Remix.Line.*`, `Remix.Fill.*` | [Remix Icon](https://remixicon.com), Apache-2.0 |
| `Blaizio.Icons.HugeIcons` | `HugeIcons.StrokeRounded.*` | [Hugeicons](https://hugeicons.com) free set, MIT |

Path data is generated from each set's source by `scripts/Update-BlaizioIcons.ps1` in the Blaizio
repository; the icon data keeps its own licence (THIRD-PARTY-LICENSE.txt in each package).

Part of the [Blaizio](https://blaiz.io) component framework; used by the styled components the
`blaizio` CLI copies into your app, and just as usable on its own. Browse every icon at
https://blaiz.io/docs/components/icons.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
