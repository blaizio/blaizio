<p align="center">
  <a href="https://blaiz.io">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset=".github/assets/wordmark-dark.svg">
      <img src=".github/assets/wordmark-light.svg" alt="Blaizio" width="360">
    </picture>
  </a>
</p>

<h3 align="center">Blazor UI components you own</h3>

<p align="center">
  <a href="https://github.com/blaizio/blaizio/actions/workflows/ci.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/blaizio/blaizio/ci.yml?branch=main&label=build"></a>
  <a href="https://github.com/blaizio/blaizio/actions/workflows/pages.yml"><img alt="Docs site" src="https://img.shields.io/github/actions/workflow/status/blaizio/blaizio/pages.yml?branch=main&label=docs"></a>
  <a href="LICENSE.md"><img alt="License MIT" src="https://img.shields.io/github/license/blaizio/blaizio"></a>
  <a href="https://www.nuget.org/packages/Blaizio.Base"><img alt="NuGet version" src="https://img.shields.io/nuget/v/Blaizio.Base?label=nuget"></a>
  <a href="https://www.nuget.org/packages/Blaizio.Base"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/Blaizio.Base?label=downloads"></a>
  <a href="https://github.com/blaizio/blaizio/stargazers"><img alt="Stars" src="https://img.shields.io/github/stars/blaizio/blaizio"></a>
  <a href="https://github.com/blaizio/blaizio/discussions"><img alt="Discussions" src="https://img.shields.io/github/discussions/blaizio/blaizio"></a>
</p>

A Blazor UI component framework built on headless primitives and Tailwind CSS v4. Components are
distributed as **source** - the CLI copies them into your project, rewrites the namespace, and from
then on the code is yours to edit.

Documentation, component gallery and the theme composer live at **[blaiz.io](https://blaiz.io/docs)**.

## Why Blaizio

- **You own the components.** Every styled component is `.razor` source in your repository, under
  your namespace. Read it, change it, delete it. There is no black box to work around.
- **Behavior stays a package.** `Blaizio.Base` ships the headless primitives: ARIA, keyboard
  interaction, focus management, positioning, a `data-state` contract. Styling never lives there,
  so a fix to behavior reaches you as a version bump and never overwrites your edits.
- **Tailwind CSS v4, no Node required.** The CLI wires the standalone Tailwind binary, or your own
  bundler if you have one.
- **73 components, 8 skins, 16 palettes.** Compose skin, palette, fonts, chart colors and radius on
  [blaiz.io/themes](https://blaiz.io/themes), then reproduce the exact look with one preset code.
- **RTL and accessibility built in.** Logical properties throughout, WCAG AA contrast on every
  palette, and an axe gate in CI over every component family in light and dark.
- **A registry you can host too.** The docs site is also the component registry; publish your own
  components the same way and install them with `@your-namespace/component`.

## Layers

| Project | Ships as | What it is |
|---|---|---|
| [`Blaizio.Base`](src/Blaizio.Base) | NuGet | Headless, unstyled primitives: behavior, ARIA, keyboard interaction and a `data-state` attribute contract. Zero CSS. JS interop served from `_content/Blaizio.Base/dist/`. |
| [`Blaizio.Icons`](src/Blaizio.Icons) | NuGet | `BzIcon`, one tree-shakeable Blazor SVG component, and the typed `Icon` value every set is made of. |
| `Blaizio.Icons.Tabler`, `.Lucide`, `.Phosphor`, `.Remix`, `.HugeIcons` | NuGet | Five icon sets as generated `Icon` members ([`Tabler.Outline.*`](src/Blaizio.Icons.Tabler), [`Lucide.Outline.*`](src/Blaizio.Icons.Lucide), [`Phosphor.Regular.*`](src/Blaizio.Icons.Phosphor) and five more weights, [`Remix.Line.*`](src/Blaizio.Icons.Remix), [`HugeIcons.StrokeRounded.*`](src/Blaizio.Icons.HugeIcons)). Mix freely; a trimmed publish keeps only the icons you reference. Tabler is what the styled components use, so the CLI installs it; the rest are one `dotnet add package` away. Browse all of them at [blaiz.io/docs/components/icons](https://blaiz.io/docs/components/icons). |
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

# Everything, including the docs site (which packs Base, Icons and the icon sets into artifacts/local-nuget,
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
src/Blaizio.Icons.*        icon sets: Tabler, Lucide, Phosphor, Remix, HugeIcons (NuGet, generated by scripts/Update-BlaizioIcons.ps1)
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

## License

Licensed under the [MIT license](LICENSE.md).
