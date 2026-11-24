# Blaizio.Docs.E2E

Playwright end-to-end suite for the docs app: routing, keyboard smoke, dialogs, copy controls,
mobile rail, axe-core accessibility passes, and the per-style visual-regression matrix
(8 styles x LTR/RTL x light/dark on component pages whose defects bUnit cannot see - the class
string is right but the pixels are wrong, e.g. the RTL switch-thumb bug).

## Running

The suite is opt-in: without the env var every test reports as SKIPPED (so a plain `dotnet test`
at the solution root stays green and fast, and the report shows nothing ran).

```bash
BLAIZIO_E2E=1 dotnet test tests/Blaizio.Docs.E2E
```

On first run the fixture downloads the Playwright Chromium build and boots the docs app on
http://127.0.0.1:5237 (the docs project's own build chain packs Base, rebuilds the CLI and
refreshes Components/Ui - the first boot is slow, later ones are incremental).

The suite's docs build goes to its own output (`artifacts/e2e-docs/`), so a dev server you have
running from the project's normal `bin` keeps serving while the tests run - without the split,
the test build deletes the fingerprinted `_framework` files the live server is handing out and
every open tab dies with a blank body.

Two checkouts (or two sessions) can run the suite at the same time: set `BLAIZIO_E2E_PORT` to a free
port in one of them. A port that already answers stops the run before any test, because every test
would otherwise exercise whatever build that server holds.

```bash
BLAIZIO_E2E=1 BLAIZIO_E2E_PORT=5241 dotnet test tests/Blaizio.Docs.E2E
```

While writing a test, the build and the server start of every run can be skipped: start the docs app
once with the fixture's two properties, then attach to it. Rebuild and restart it yourself after a
change to the docs or to `src`; an attached run tests whatever that server holds.

```bash
dotnet run --project docs/Blaizio.Docs/Blaizio.Docs.csproj --no-launch-profile --urls http://127.0.0.1:5249 --property:BlaizioIsolatedOutput="<repo>/artifacts/e2e-docs" --property:BlaizioFetchStars=false
```

```bash
BLAIZIO_E2E=1 BLAIZIO_E2E_ATTACH=1 BLAIZIO_E2E_PORT=5249 dotnet test tests/Blaizio.Docs.E2E --filter "FullyQualifiedName~ScriptModuleTests"
```

## Script coverage

`BLAIZIO_E2E_SCRIPT_COVERAGE=1` records which functions of each Blaizio.Base script module the run
called, and in which test, using the browser's own profiler (nothing is instrumented). The result is
written to `ScriptCoverage/modules.json` (gitignored): per module the number of functions, how many
were called, and the tests that reach it: the ones that called a function which merely opening a page
(the routing and axe tests) does not call. The profiler slows the page down, so this is a measuring
run, not a gate: a test that depends on timing can fail in it (the context menu reopen test does).

```bash
BLAIZIO_E2E=1 BLAIZIO_E2E_SCRIPT_COVERAGE=1 dotnet test tests/Blaizio.Docs.E2E --filter "FullyQualifiedName!~VisualRegressionTests"
```

## Axe results

Every axe run writes the full violation list (all impacts, moderate and minor included) to
`AxeResults/*.json`, one file per route/theme, pass or fail. Only serious/critical violations
fail the gate; the rest are recorded evidence. CI uploads the directory as a build artifact named
`axe-results-<shard>`, one per shard that ran an axe test (today that is `axe-results-axe`).

## CI

The functional and axe tests run on every push and pull request, split by test class across
parallel jobs (`docs-e2e-shard` in `.github/workflows/ci.yml`). Tests still run one at a time inside
a job; several depend on timing, so the split is across runners, not threads. The job
`Docs E2E (functional + axe)` (`docs-e2e`) passes only when every shard passed, and is the one to
list as a required status check. The visual matrix is excluded because its baselines are
machine-local; run it on your own machine.

`ci-shards.ps1` in this folder is the only place that says which class runs in which shard. Its last
shard, `everything-else`, takes every class the others do not name, so a new test class runs in CI
with no edit. Name a class in a shard only to rebalance the run times; the script fails the run when
a listed class no longer exists or sits in two shards.

```bash
pwsh tests/Blaizio.Docs.E2E/ci-shards.ps1
```

lists each shard with its filter. To run one shard the way CI does:

```bash
BLAIZIO_E2E=1 dotnet test tests/Blaizio.Docs.E2E --filter "$(pwsh tests/Blaizio.Docs.E2E/ci-shards.ps1 -Shard axe)"
```

## Visual regression

Baselines live in `Screenshots/` next to the project and are machine-local (font rasterization
differs across machines, so they are gitignored). Record them explicitly first; comparison runs
use a per-pixel tolerance and fail on drift, writing a `*.actual.png` next to the baseline for
eyeballing. A combo with no baseline FAILS - a fresh checkout never silently passes a comparison
it did not make.

```bash
BLAIZIO_E2E=1 BLAIZIO_E2E_UPDATE=1 dotnet test tests/Blaizio.Docs.E2E --filter Visual
```

`BLAIZIO_E2E_UPDATE=1` records every baseline (first run on a machine, and again after an
intentional visual change).
