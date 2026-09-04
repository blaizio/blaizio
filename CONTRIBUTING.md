# Contributing to Blaizio

Thanks for considering a contribution. This page covers the local setup, the layout, and the
conventions pull requests are expected to follow.

## Prerequisites

- .NET 10 SDK
- pnpm (only for the docs site's JS bundle and the Base TypeScript sources)

## Getting started

```sh
git clone https://github.com/blaizio/blaizio
cd blaizio
dotnet build Blaizio.slnx
dotnet test
```

The build is self-contained: the docs project packs `Blaizio.Base`/`Blaizio.Icons` into
`artifacts/local-nuget`, builds the component registry into its `wwwroot/r`, and copies the styled
components through the CLI - the same pipeline consumers use.

## Where things live

| Path | What |
|---|---|
| `src/Blaizio.Base` | Headless primitives (NuGet): behavior, ARIA, `data-*` contract, JS assets |
| `src/Blaizio.Icons` | Icon components (NuGet) |
| `src/Blaizio.Ui` | Styled components + skins/presets - the registry's source of truth |
| `src/Blaizio.Cli` / `src/Blaizio.Cli.Core` | The `blaizio` dotnet tool and its engine |
| `docs/Blaizio.Docs` | Docs site, registry host, `/create` configurator |
| `tests/` | Base, Core and CLI test suites |

## Working on components

- Behavior belongs in `Blaizio.Base`; styling in `Blaizio.Ui`. Keep the split strict - Base ships
  no CSS, Ui ships no behavior.
- After editing `Blaizio.Ui` source, refresh the docs site's copies:
  `dotnet build docs/Blaizio.Docs -p:BlaizioRefresh=true`.
- After editing Base TypeScript (`src/Blaizio.Base/lib`): `pnpm build` there, then rebuild.
- The docs site keeps its Node toolchain in `docs/Blaizio.Docs/lib` the same way: `pnpm install` there once; the build runs Tailwind and esbuild itself.
- The skin inliner has a golden-file suite (`tests/Blaizio.Cli.Core.Tests/Goldens`) - update the
  goldens deliberately, never to silence a diff you don't understand.

## Pull requests

- Conventional Commits (`feat(cli): ...`, `fix(ui): ...`, `docs: ...`).
- `dotnet test` green across all three suites.
- User-facing text (docs pages, CLI output, READMEs): plain hyphens, no em dashes.
- One concern per PR; note breaking changes in the description and `CHANGELOG.md`.

## Reporting bugs

Open a GitHub issue with the smallest reproduction you can manage - a failing test or a minimal
`blaizio` command sequence is ideal. For security issues see [SECURITY.md](SECURITY.md).
