# Blaizio.Ui

The styled component layer: 61 Tailwind CSS v4 components over the `Blaizio.Base` primitives.

**This project is not a package.** It is the *source of truth* the registry is compiled from —
`blaizio build` walks this project and emits static JSON items; `blaizio add` copies the
component source into consumer apps with the namespace rewritten. Consumers own the copies.

## Layout

```
Components/            one folder per component family (.razor + .cs class builders)
Styles/
  blaizio.css          the Tailwind contract: data-* custom variants, keyframes,
                       reduced-motion gate (embedded into the CLI as base.css)
  shared.css           skin-independent baseline (@apply rules keyed by bz-* classes)
  style-<skin>.css     8 skins: ash, aura, ember, flint, forge, glow, spark, wisp
  preset-<name>.css    8 color palettes over the default Nova palette
```

## Conventions

- Components emit semantic `bz-*` classes plus `data-slot` / `data-variant` / `data-size`
  attributes; the active skin sheet supplies the look. (CSS layout v3 — see
  `docs/cli-plan.md` — will inline the resolved classes into shipped source at registry build.)
- Classes merge through `TailwindMerge.NET` (`Tw.Merge`), so consumer `Class` parameters win.
- RTL through logical properties (`ps-*`, `ms-*`, `:dir()`) — no `dir`-specific sheets.
- Every interactive part exposes `RenderAs` for element polymorphism.

## Editing a component

Change the source here, then refresh consumers of the local registry
(`dotnet build docs/Blaizio.Docs -p:BlaizioRefresh=true` re-copies the docs site's components,
`blaizio add <name> --overwrite` updates any other local consumer).
