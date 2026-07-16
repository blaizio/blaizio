# Blaizio.Cli.Core

The engine behind the `blaizio` CLI, packaged separately so IDE plugins and other tools can drive
the same logic without shelling out: registry client, transitive dependency resolver, namespace
rewriter, file writer, NuGet installer, Tailwind/host wiring and `blaizio.json` configuration.

Every result type is JSON-serializable - the CLI's `--json` output is these objects, so a direct
consumer and a `blaizio --json` parser see the same shapes.

## When to use it

- Building an IDE integration (Visual Studio, Rider) that adds Blaizio components in-process.
- Automation that needs the resolver/rewriter as a library instead of a child process.

For everything else, use the [`Blaizio.Cli`](https://www.nuget.org/packages/Blaizio.Cli) dotnet
tool - it is the supported front door and adds the interactive experience on top of this engine.

## Documentation

Visit https://blaiz.io/docs/cli to view the documentation.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
