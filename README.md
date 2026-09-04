# Blaizio

A Blazor UI component framework built on headless primitives and Tailwind CSS v4. Components are
distributed as **source** - the CLI copies them into your project, rewrites the namespace, and from
then on the code is yours to edit.

## Layers

| Project | Ships as | What it is |
|---|---|---|
| [`Blaizio.Base`](src/Blaizio.Base) | NuGet | Headless, unstyled primitives: behavior, ARIA, keyboard interaction and a `data-state` attribute contract. Zero CSS. JS interop served from `_content/Blaizio.Base/dist/`. |
| [`Blaizio.Icons`](src/Blaizio.Icons) | NuGet | Tabler icons as a tree-shakeable Blazor SVG component. |
| [`Blaizio.Ui`](src/Blaizio.Ui) | Source via registry | 73 styled components over the primitives. Copied into your app by the CLI - never referenced as a package. |
| [`Blaizio.Cli`](src/Blaizio.Cli) | dotnet tool (`blaizio`) | `new`, `add`, `remove`, `apply`, registry queries, Tailwind pipeline wiring, `uninstall`. |
| [`Blaizio.Docs`](docs/Blaizio.Docs) | - | Documentation site. Also hosts the component registry (`/r`) and the `/themes` theme configurator. |

## Quick start

```sh
dotnet tool install -g Blaizio.Cli
blaizio new showcase              # scaffold a new demo app: config, packages, CSS, components
# or, in an existing Blazor app:
blaizio add button card dialog
```

The first `add` in a project wires it: writes `blaizio.json`, installs the NuGet layers, sets up
Tailwind v4 (managed CSS + input file, standalone binary or your own bundler) and patches the host
page. Every `add` copies component source into `Components/Ui/` under your root namespace and
records it in `blaizio.json`, so `add --update`, `add --diff`, `remove` and `uninstall` know
exactly what the CLI owns.

## Styling

- **Tokens**: `:root` holds the light values, `.dark` overrides them - that's the whole model.
- **8 skins** (structure): ash, aura, ember (default), flint, forge, glow, spark, wisp.
- **16 palettes**: nova (default), aurora, comet, corona, eclipse, equinox, magnetar, meteor, nebula, polaris, pulsar, quasar, solstice, umbra, vesper, zenith.
- **/themes** on the docs site composes skin + palette + fonts + chart colors + radius into a
  compact preset code: `blaizio add --preset <code>` or `blaizio apply <code>` reproduces the
  exact look. `blaizio preset resolve` turns a project back into a shareable code.
- RTL-ready (logical properties throughout) and WCAG AA-minded out of the box.

## Building from source

Prerequisites: .NET SDK 10 (the exact patch is pinned in `global.json`), Node 22 and pnpm 10.

```sh
# Node toolchains, once: the Base interop bundle and the docs site's Tailwind/esbuild pipeline.
cd src/Blaizio.Base/lib   && pnpm install && pnpm build && cd ../../..
cd docs/Blaizio.Docs/lib  && pnpm install              && cd ../../..

# Everything, including the docs site (which packs Base/Icons into artifacts/local-nuget,
# builds the registry and copies the styled components through the CLI):
dotnet build Blaizio.slnx

# CLI as a global tool from a local pack:
dotnet pack src/Blaizio.Cli -o artifacts/cli-pack
dotnet tool install -g Blaizio.Cli --add-source artifacts/cli-pack

# Docs site (serves the registry at /r):
dotnet run --project docs/Blaizio.Docs

# Tests:
dotnet test
```

## Repository layout

```
src/Blaizio.Base           headless primitives (NuGet) + TypeScript interop (lib/)
src/Blaizio.Icons          icon component (NuGet)
src/Blaizio.Ui             styled components + skins/presets (registry source)
src/Blaizio.Cli            the dotnet tool
src/Blaizio.Cli.Core       CLI engine (registry client, resolver, rewriter, config)
src/Blaizio.Cli.Contracts  dependency-free contracts shared by the CLI and the docs site
docs/Blaizio.Docs          docs site, registry host, /themes configurator
docs/*.md                  engineering notes and historical plans (see docs/README.md)
tests/                     Base, Core, CLI, benchmark and docs E2E suites
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the local setup and PR conventions,
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community standards, and
[SECURITY.md](SECURITY.md) for reporting vulnerabilities.

## Documentation

Visit https://blaiz.io/docs to view the documentation.

## License

Licensed under the [MIT license](LICENSE.md).
