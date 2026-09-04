# Blaizio.Cli.Contracts

The dependency-free contracts shared by the `blaizio` CLI and the Blaizio docs site:

- The preset code codec (`PresetCode`) and the canonical option tables it encodes: skins,
  palettes, fonts (`FontCatalog`), chart colors and radius.
- The registry wire model (`RegistryIndex`, `RegistryItem`, `RegistryFile`) that every
  `registry.json` and item file conforms to.

Types keep their `Blaizio.Cli.Core.*` namespaces, so code written against the full
[`Blaizio.Cli.Core`](https://www.nuget.org/packages/Blaizio.Cli.Core) engine compiles unchanged
against this package. Reference this one when you only need to read or produce preset codes and
registry manifests without the CLI machinery, for example from a Blazor WebAssembly app or a
registry host.

## Documentation

Visit https://blaiz.io/docs/registry to view the documentation.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
