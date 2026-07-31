# Blaizio.Docs.E2E

Playwright end-to-end suite for the docs app: routing, keyboard smoke, dialogs, copy controls,
mobile rail, axe-core accessibility passes, and the per-skin visual-regression matrix
(8 skins x LTR/RTL x light/dark on component pages whose defects bUnit cannot see - the class
string is right but the pixels are wrong, e.g. the RTL switch-thumb bug).

## Running

The suite is opt-in: without the env var every test no-ops (so a plain `dotnet test` at the
solution root stays green and fast).

```bash
BLAIZIO_E2E=1 dotnet test tests/Blaizio.Docs.E2E
```

On first run the fixture downloads the Playwright Chromium build and boots the docs app on
http://127.0.0.1:5237 (the docs project's own build chain packs Base, rebuilds the CLI and
refreshes Components/Ui - the first boot is slow, later ones are incremental).

## Visual regression

Baselines live in `Screenshots/` next to the project and are machine-local (font rasterization
differs across machines, so they are gitignored). First run writes them and passes; later runs
compare with a per-pixel tolerance and fail on drift, writing a `*.actual.png` next to the
baseline for eyeballing.

```bash
BLAIZIO_E2E=1 BLAIZIO_E2E_UPDATE=1 dotnet test tests/Blaizio.Docs.E2E --filter Visual
```

`BLAIZIO_E2E_UPDATE=1` re-records every baseline (run after an intentional visual change).
