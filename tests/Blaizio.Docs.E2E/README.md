# Blaizio.Docs.E2E

Playwright end-to-end suite for the docs app: routing, keyboard smoke, dialogs, copy controls,
mobile rail, axe-core accessibility passes, and the per-skin visual-regression matrix
(8 skins x LTR/RTL x light/dark on component pages whose defects bUnit cannot see - the class
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

## CI

The functional and axe tests run as a required job on every push and pull request
(`docs-e2e` in `.github/workflows/ci.yml`). The visual matrix is excluded there because its
baselines are machine-local; run it on your own machine.

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
