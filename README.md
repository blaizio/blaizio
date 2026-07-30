# Blaizio

A Blazor UI component framework built on headless primitives and Tailwind CSS v4. Components are
distributed as **source** - the CLI copies them into your project, rewrites the namespace, and from
then on the code is yours to edit.

> **Status: pre-release.** Packages are versioned `0.1.0-alpha.x` and not yet published to
> nuget.org. Everything below works today against a local build of this repository.

## Layers

| Project | Ships as | What it is |
|---|---|---|
| [`Blaizio.Base`](src/Blaizio.Base) | NuGet | Headless, unstyled primitives: behavior, ARIA, keyboard interaction and a `data-state` attribute contract. Zero CSS. JS interop served from `_content/Blaizio.Base/dist/`. |
| [`Blaizio.Icons`](src/Blaizio.Icons) | NuGet | Tabler icons as a tree-shakeable Blazor SVG component. |
| [`Blaizio.Ui`](src/Blaizio.Ui) | Source via registry | 70 styled components over the primitives. Copied into your app by the CLI - never referenced as a package. |
| [`Blaizio.Cli`](src/Blaizio.Cli) | dotnet tool (`blaizio`) | `init`, `add`, `remove`, `apply`, registry queries, Tailwind pipeline wiring, uninstall. |
| [`Blaizio.Docs`](docs/Blaizio.Docs) | - | Documentation site. Also hosts the component registry (`/r`) and the `/create` theme configurator. |

## Quick start

```sh
blaizio new showcase              # scaffold a new demo app: config, packages, CSS, components
# or, in an existing Blazor app:
blaizio init
blaizio add button card dialog
```

`init` writes `blaizio.json`, installs the NuGet layers, wires Tailwind v4 (managed CSS +
input file, standalone binary or your own bundler) and patches the host page. `add` copies
component source into `Components/Ui/` under your root namespace and records it in `blaizio.json`
so `update`, `add --diff` and `uninstall` know exactly what the CLI owns.

## Styling

- **Tokens**: `:root` holds the light values, `.dark` overrides them - that's the whole model.
- **8 skins** (structure): ash, aura, ember (default), flint, forge, glow, spark, wisp.
- **15 palettes**: nova (default), aurora, comet, corona, eclipse, equinox, magnetar, meteor, nebula, polaris, pulsar, quasar, solstice, umbra, zenith.
- **/create** on the docs site composes skin + palette + fonts + chart colors + radius into a
  compact preset code: `blaizio init --preset <code>` or `blaizio apply <code>` reproduces the
  exact look. `blaizio preset resolve` turns a project back into a shareable code.
- RTL-ready (logical properties throughout) and WCAG AA-minded out of the box.

## Building from source

```sh
dotnet build Blaizio.slnx

# Base JS assets (needed once before packing Blaizio.Base):
cd src/Blaizio.Base/lib && pnpm install && pnpm build

# Pack the NuGet layers into the local feed the docs/consumers use:
dotnet pack src/Blaizio.Base  -o artifacts/local-nuget
dotnet pack src/Blaizio.Icons -o artifacts/local-nuget

# CLI as a global tool from a local pack:
dotnet pack src/Blaizio.Cli -o artifacts/cli-pack
dotnet tool install -g blaizio.cli --add-source artifacts/cli-pack --version 0.1.0-alpha.5

# Docs site (serves the registry at /r):
dotnet run --project docs/Blaizio.Docs

# Tests:
dotnet test
```

## Repository layout

```
src/Blaizio.Base       headless primitives (NuGet) + TypeScript interop (lib/)
src/Blaizio.Icons      icon component (NuGet)
src/Blaizio.Ui         styled components + skins/presets (registry source)
src/Blaizio.Cli        the dotnet tool
src/Blaizio.Cli.Core   CLI engine (registry client, resolver, rewriter, config)
docs/Blaizio.Docs      docs site, registry host, /create configurator
tests/                 Core + CLI test suites
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
