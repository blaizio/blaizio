# docs/

`Blaizio.Docs/` is the documentation site (also the registry host and the `/themes`
configurator). Everything else in this folder is engineering notes: the plans and audits behind
decisions that are now in the code. They are kept because source comments cite them, not because
they describe the current state - when a note and the code disagree, the code is right.

| File | What it was | Status |
|---|---|---|
| `benchmarks.md` | Render/allocation baselines behind the CI allocation gate | Reference, refreshed when the gate moves |
| `a11y-screen-reader-matrix.md` | Manual NVDA + VoiceOver checks per widget, run before a release | Reference |
| `api-freeze-audit.md` | Pre-beta public API consistency sweep and its scoreboard | Historical, complete |
| `audit-remediation-plan.md` | The remediation plan that followed the audit | Historical, complete |
| `v3-audit.md` | Audit that produced the v3 CLI (skin inliner, registry pipeline) | Historical, complete |
| `cli-plan.md` | Original CLI design plan | Historical, complete |
| `portal-plan.md` | Floating surfaces moving to `document.body` | Historical, complete |

Current behavior is documented on the site (https://blaiz.io/docs) and in `CHANGELOG.md`.
