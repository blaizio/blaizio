# Blaizio.Cli

The `blaizio` dotnet tool: initializes Blazor projects for the Blaizio component framework and
copies components from the registry into your app **as source you own**.

```sh
dotnet tool install -g Blaizio.Cli
blaizio new showcase            # a new app from a template
blaizio add button card dialog  # wires Blaizio into your existing app first when needed
```

## Commands

| Command | Purpose |
|---|---|
| `new` | Scaffold a new app from a template (showcase, webapp, wasm, library), then run the wiring pipeline over it. `create` is an alias. |
| `add` | Copy components (with transitive registry dependencies) into your output dir, rewrite namespaces, record the install. Wires Blaizio into the project first when needed: `blaizio.json`, NuGet layers, Tailwind v4 (managed CSS + input, standalone binary auto-fetch or your bundler), host page - the whole flow the hidden legacy `init` command used to own. Also `--update` (packages + components in lockstep), `--diff`, `--view`, and `font-*` items. |
| `apply` | Re-style an existing project from a preset name or a `/themes` code (`--only theme,fonts,tokens`). |
| `search` / `view` / `info` / `docs` | Query the registry. |
| `preset decode/resolve/url/open` | Work with `/themes` preset codes. |
| `registry add/validate` | Extra registries, `@namespace/component` resolution. |
| `tailwind detect/setup/fetch` | Tailwind pipeline wiring (standalone binary is sha256-verified and cached per user). |
| `remove` | Take individual components back: deletes exactly the files `add` recorded for them and drops their entry; refuses items another component still needs (`--force` overrides). |
| `uninstall` | Undo-by-record: removes exactly the components, packages and wiring tracked in `blaizio.json`; user files survive. |
| `eject` | Copy the materialized contract sheets into your tokens file and own the styling plumbing from then on. |
| `build` | Compile a registry from component source (maintainers). |

Every command supports `--json` (single JSON document on stdout) for IDE plugins and automation,
plus `-c/--cwd`, `-y`, `-s/--silent`, `--registry`.

## Your local edits

Components are copied into your project, so editing them is expected. `update` (and
`add --overwrite`) re-pull those files, and the content hash recorded for every write lets the CLI
tell a file you changed apart from one that merely has a newer version upstream. Untouched files
are replaced silently; changed ones are offered as a checkbox list.

| Run | Components you changed |
|---|---|
| interactive | picked from a checkbox list - unticked keeps yours |
| `-y`, `--json`, `--silent`, no TTY | **kept** (everything else still updates) |
| `update --force`, `add --overwrite --force-overwrite` | replaced - the only way to discard edits unattended |

`-y` means "don't ask me", never "overwrite everything". Inspect before deciding with
`blaizio add --diff <component>`. (`add -f/--force` is unrelated: it re-writes `blaizio.json`.)

An overwriting run also removes files a component **stopped shipping** (an upstream rename or
split), so the old copy can't linger and shadow its replacement - by record only, and untouched
files only. One you changed, or one with no baseline, is reported and left for `--force`.

## Configuration

`blaizio.json` records the component namespace and output dir, the Tailwind input (`css`), the
chosen skin/preset and font/chart/radius selections, extra registries, and the install ledger
(each component's files with their baseline hashes, plus CLI-installed packages) that powers
`update`, `add --diff` and `uninstall`.


## Documentation

Visit https://blaiz.io/docs/cli to view the documentation.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
