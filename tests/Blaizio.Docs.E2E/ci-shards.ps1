#!/usr/bin/env pwsh
<#
.SYNOPSIS
    How CI splits the functional and axe tests across parallel jobs.

.DESCRIPTION
    The one place that says which test class runs in which CI job (`docs-e2e-shard` in
    .github/workflows/ci.yml builds its matrix from `-Matrix`). Tests stay serial inside a job:
    several are timing-sensitive, so the suite is split across runners instead of threads.

    Every shard but the last names its classes. The last one is the catch-all: its filter is
    built here from the other shards' lists, so a class is never in two jobs and a class added
    later runs in CI without an edit to this file. Move a class into a named shard (or add a
    shard) only to rebalance the run times.

.EXAMPLE
    pwsh tests/Blaizio.Docs.E2E/ci-shards.ps1
    Lists every shard with its `dotnet test --filter` expression.

.EXAMPLE
    pwsh tests/Blaizio.Docs.E2E/ci-shards.ps1 -Shard axe
    Prints one shard's filter, to run that shard locally.

.EXAMPLE
    pwsh tests/Blaizio.Docs.E2E/ci-shards.ps1 -Matrix
    Prints the job matrix as JSON, for the workflow.
#>
[CmdletBinding(DefaultParameterSetName = 'List')]
param(
    [Parameter(ParameterSetName = 'Matrix')]
    [switch] $Matrix,

    [Parameter(ParameterSetName = 'Shard', Mandatory)]
    [string] $Shard
)

$ErrorActionPreference = 'Stop'

# Shard name -> the test classes it runs. Balanced on measured run times (2026-11-23, seconds on a
# local run): DataTableEditingTests 259, AxeTests 250, OverlayLifecycleTests 134 +
# DataTableColumnsTests 122, and 279 for everything else (ScriptModuleTests 197 plus the small
# classes).
$named = [ordered]@{
    'data-table-editing'   = @('DataTableEditingTests')
    'axe'                  = @('AxeTests')
    'overlays-and-columns' = @('OverlayLifecycleTests', 'DataTableColumnsTests')
}

# Runs every class no shard above names.
$catchAll = 'everything-else'

# Never in CI: the visual baselines are machine-local (see README.md).
$excluded = 'VisualRegression'

# A stale name would leave its shard with no tests and a green result, so every listed class must
# still be declared in this folder, and in one shard only.
$source = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter *.cs | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$seen = @{}
foreach ($name in $named.Keys) {
    if ($name -eq $catchAll) { throw "ci-shards.ps1: '$catchAll' is the catch-all shard and takes no class list." }
    foreach ($class in $named[$name]) {
        if ($class -notmatch '^\w+$') { throw "ci-shards.ps1: '$class' (shard '$name') is not a class name." }
        if ($class -like "*$excluded*") { throw "ci-shards.ps1: '$class' (shard '$name') is a visual regression class, which CI does not run." }
        if ($seen.ContainsKey($class)) { throw "ci-shards.ps1: '$class' is listed in two shards ('$($seen[$class])' and '$name')." }
        if ($source -notmatch "\bclass\s+$class\b") { throw "ci-shards.ps1: '$class' (shard '$name') is not a class in tests/Blaizio.Docs.E2E. Rename or remove it." }
        $seen[$class] = $name
    }
}

# The dots pin the whole class name: "~.AxeTests." matches Blaizio.Docs.E2E.AxeTests.<method> and
# not a class whose name merely contains it.
$shards = [ordered]@{}
foreach ($name in $named.Keys) {
    $shards[$name] = ($named[$name] | ForEach-Object { "FullyQualifiedName~.$_." }) -join '|'
}
$shards[$catchAll] = (@("FullyQualifiedName!~$excluded") + ($seen.Keys | Sort-Object | ForEach-Object { "FullyQualifiedName!~.$_." })) -join '&'

switch ($PSCmdlet.ParameterSetName) {
    'Matrix' {
        @{ include = @($shards.Keys | ForEach-Object { [ordered]@{ shard = $_; filter = $shards[$_] } }) } | ConvertTo-Json -Compress -Depth 3
    }
    'Shard' {
        if (-not $shards.Contains($Shard)) { throw "ci-shards.ps1: no shard '$Shard'. Shards: $($shards.Keys -join ', ')." }
        $shards[$Shard]
    }
    default {
        foreach ($name in $shards.Keys) { '{0,-22} {1}' -f $name, $shards[$name] }
    }
}
