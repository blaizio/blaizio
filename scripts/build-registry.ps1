<#
.SYNOPSIS
  Regenerate the component registry and stage it for hosting.
.DESCRIPTION
  Scans src/Blaizio.Ui into a manifest, compiles per-item JSON + index.json, and writes them into
  the docs site's wwwroot/r so they are served at <docs-origin>/r/*.json (blaiz.io/r in production).
  CI runs this before publishing the docs.
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$cliProj = 'src/Blaizio.Cli/Blaizio.Cli.csproj'
$source = 'src/Blaizio.Ui'
$out = 'docs/Blaizio.Docs/wwwroot/r'

Write-Host 'Building the CLI (Release)...'
dotnet build $cliProj -c Release -v q
$dll = 'src/Blaizio.Cli/bin/Release/net10.0/blaizio.dll'

Write-Host "Generating the manifest ($source/registry.json)..."
dotnet $dll generate "./$source"

Write-Host "Compiling resolved item JSON -> $out ..."
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
dotnet $dll build ./registry.json -o (Join-Path $root $out) --cwd "./$source"

Write-Host "Done. Registry hosted at $out (served as /r/*.json)."
