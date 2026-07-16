# Blaizio.Cli

The `blaizio` dotnet tool: initializes Blazor projects for the Blaizio component framework and
copies components from the registry into your app **as source you own**.

```sh
dotnet tool install -g Blaizio.Cli
blaizio init -t showcase        # or `blaizio init` in an existing app
blaizio add button card dialog
```

## Commands

| Command | Purpose |
|---|---|
| `init` | Write `blaizio.json`, install the NuGet layers, wire Tailwind v4 (managed CSS + input, standalone binary auto-fetch or your bundler), patch the host page. Templates: showcase, webapp, wasm, library. |
| `add` | Copy components (with transitive registry dependencies) into your output dir, rewrite namespaces, record the install. Also `--update`, `--upgrade`, `--diff`, `--view`, and `font-*` items. |
| `apply` | Re-style an existing project from a preset name or a `/create` code (`--only theme,fonts,tokens`). |
| `search` / `view` / `info` / `docs` | Query the registry. |
| `preset decode/resolve/url/open` | Work with `/create` preset codes. |
| `registry add/validate` | Extra registries, `@namespace/component` resolution. |
| `tailwind detect/setup/fetch` | Tailwind pipeline wiring (standalone binary is sha256-verified and cached per user). |
| `uninstall` | Undo-by-record: removes exactly the components, packages and wiring tracked in `blaizio.json`; user files survive. |
| `build` | Compile a registry from component source (maintainers). |

Every command supports `--json` (single JSON document on stdout) for IDE plugins and automation,
plus `-c/--cwd`, `-y`, `-s/--silent`, `--registry`.

## Configuration

`blaizio.json` records the component namespace and output dir, the Tailwind input (`css`), the
chosen skin/preset and font/chart/radius selections, extra registries, and the install ledger
(components + CLI-installed packages) that powers `add --update`, `add --diff` and `uninstall`.

Design doc: `docs/cli-plan.md` in the repository.
