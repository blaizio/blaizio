# Contributing to Blaizio

Thanks for considering a contribution. This page covers the local setup, the layout, and the
conventions pull requests are expected to follow.

## Prerequisites

- .NET SDK 10 (`global.json` pins the exact patch and rolls forward within it)
- Node 22 and pnpm 10 (the Base TypeScript sources and the docs site's Tailwind/esbuild pipeline;
  CI uses the same versions)

## Getting started

```sh
git clone https://github.com/blaizio/blaizio
cd blaizio
(cd src/Blaizio.Base/lib  && pnpm install && pnpm build)
(cd docs/Blaizio.Docs/lib && pnpm install)
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
| `src/Blaizio.Cli.Contracts` | Dependency-free contracts shared by the CLI and the docs site (preset codec, option tables, registry wire model) |
| `docs/Blaizio.Docs` | Docs site, registry host, `/themes` configurator |
| `docs/*.md` | Engineering notes and historical plans; see `docs/README.md` before treating one as current |
| `tests/` | Base, Core and CLI suites, the Base allocation benchmarks, and the docs Playwright E2E suite (opt-in via `BLAIZIO_E2E=1`) |

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
- `dotnet test` green for every suite (`dotnet test` at the repository root).
- User-facing text (docs pages, CLI output, READMEs): plain hyphens, no em dashes.
- One concern per PR; note breaking changes in the description and `CHANGELOG.md`.
- Add a line under `Unreleased` in `CHANGELOG.md` for anything a user of the packages, the CLI
  or the docs site would notice.

## Licensing

Blaizio is MIT licensed. By submitting a contribution you agree that it is licensed under the
same [MIT license](LICENSE.md) as the rest of the project. There is no CLA to sign.

## Releasing (maintainers)

Packages publish from `.github/workflows/publish.yml` on a `v*` tag, through NuGet trusted
publishing (OIDC, no API key). All five packages release in lockstep under one version; the tag
must equal `BlaizioVersionBase`. A version nuget.org already holds is skipped, so re-running a
release is safe.

1. Bump the version in four places to the same value: `BlaizioVersionBase` in
   `Directory.Build.props` (Base + Icons, and the version the docs site displays) and `Version` in
   `src/Blaizio.Cli/Blaizio.Cli.csproj`, `src/Blaizio.Cli.Core/Blaizio.Cli.Core.csproj` and
   `src/Blaizio.Cli.Contracts/Blaizio.Cli.Contracts.csproj`. A packed version is immutable: never
   republish different bytes under one version.
2. Move the `Unreleased` entries in `CHANGELOG.md` under a heading for the new version.
3. Commit, then tag and push: `git tag v0.1.1 && git push origin main --tags`.

The docs site deploys from `.github/workflows/pages.yml` on every push to `main`, independently
of package releases.

One-time setup: a trusted publishing policy on nuget.org (owner `blaizio`, repository `blaizio`,
workflow file `publish.yml`, environment `release`) and a `release` environment on the repository
holding the `NUGET_USER` secret (the nuget.org profile name). Before the first publish, run
`dotnet pack -c Release -o ./artifacts/publish` for each project and inspect the packages.

## Reporting bugs

Open a GitHub issue with the smallest reproduction you can manage - a failing test or a minimal
`blaizio` command sequence is ideal. For security issues see [SECURITY.md](SECURITY.md).
