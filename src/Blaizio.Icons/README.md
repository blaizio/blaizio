# Blaizio.Icons

[Tabler icons](https://tabler.io/icons) for Blazor as a single, tree-shakeable SVG component.

```razor
<BzIcon Icon="Icons.Outline.Settings" Class="size-4" />
<BzIcon Icon="Icons.Filled.Star" />
```

- Each icon is a static field of path data - only the icons you reference end up in your app
  (the IL trimmer removes the rest).
- Outline and filled variants.
- Sizing and coloring via CSS classes (`size-*`, `text-*`); the SVG inherits `currentColor`.

Path data is generated from the Tabler source by `scripts/Update-BlaizioIcons.ps1` in the
Blaizio repository.

Part of the [Blaizio](https://blaiz.io) component framework; used by the styled components the
`blaizio` CLI copies into your app, and just as usable on its own.

## Documentation

Visit https://blaiz.io/docs to view the documentation.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
