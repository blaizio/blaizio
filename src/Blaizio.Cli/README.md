# Blaizio.Cli

The `blaizio` dotnet tool: initializes Blazor projects for the Blaizio component framework and
copies components from the registry into your app **as source you own**.

```sh
dotnet tool install -g Blaizio.Cli
blaizio new showcase            # a new app from a template
blaizio init                    # wire Blaizio into your existing app
blaizio add button card dialog
```

## Commands

| Command | Purpose |
|---|---|
| `new` | Scaffold a new app from a template (showcase, webapp, wasm, library), then run the init pipeline over it. `create` is an alias. |
| `init` | Wire Blaizio into an existing app: write `blaizio.json`, install the NuGet layers, wire Tailwind v4 (managed CSS + input, standalone binary auto-fetch or your bundler), patch the host page. |
| `add` | Copy components (with transitive registry dependencies) into your output dir, rewrite namespaces, record the install. Also `--update`, `--upgrade`, `--diff`, `--view`, and `font-*` items. |
| `apply` | Re-style an existing project from a preset name or a `/create` code (`--only theme,fonts,tokens`). |
| `search` / `view` / `info` / `docs` | Query the registry. |
| `preset decode/resolve/url/open` | Work with `/create` preset codes. |
| `registry add/validate` | Extra registries, `@namespace/component` resolution. |
| `tailwind detect/setup/fetch` | Tailwind pipeline wiring (standalone binary is sha256-verified and cached per user). |
| `uninstall` | Undo-by-record: removes exactly the components, packages and wiring tracked in `blaizio.json`; user files survive. |
| `eject` | Copy the materialized contract sheets into your tokens file and own the styling plumbing from then on. |
| `build` | Compile a registry from component source (maintainers). |

Every command supports `--json` (single JSON document on stdout) for IDE plugins and automation,
plus `-c/--cwd`, `-y`, `-s/--silent`, `--registry`.

## Configuration

`blaizio.json` records the component namespace and output dir, the Tailwind input (`css`), the
chosen skin/preset and font/chart/radius selections, extra registries, and the install ledger
(components + CLI-installed packages) that powers `add --update`, `add --diff` and `uninstall`.


## Documentation

Visit https://blaiz.io/docs/cli to view the documentation.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
