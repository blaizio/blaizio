#!/usr/bin/env bash
# Regenerate the component registry and stage it for hosting.
#
# Scans src/Blaizio.Ui into a manifest, compiles per-item JSON + index.json, and writes them into
# the docs site's wwwroot/r so they are served at <docs-origin>/r/*.json (blaiz.io/r in production).
# Run from the repo root; CI runs this before publishing the docs.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CLI_PROJ="src/Blaizio.Cli/Blaizio.Cli.csproj"
SOURCE="src/Blaizio.Ui"
OUT="docs/Blaizio.Docs/wwwroot/r"

echo "Building the CLI (Release)..."
dotnet build "$CLI_PROJ" -c Release -v q
DLL="src/Blaizio.Cli/bin/Release/net10.0/blaizio.dll"

echo "Generating the manifest ($SOURCE/registry.json)..."
dotnet "$DLL" generate "./$SOURCE" --fonts

echo "Compiling resolved item JSON -> $OUT ..."
rm -rf "$OUT"
dotnet "$DLL" build ./registry.json -o "$ROOT/$OUT" --cwd "./$SOURCE"

echo "Done. Registry hosted at $OUT (served as /r/*.json)."
