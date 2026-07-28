<!-- One concern per PR. Conventional Commits title: feat(cli): ..., fix(ui): ..., docs: ... -->

## What

<!-- What changes and why - a reviewer should get the story from this alone. -->

## Checklist

- [ ] `dotnet test` green across all three suites
- [ ] Breaking changes flagged with `!` in the title and noted in `CHANGELOG.md`
- [ ] User-facing text (docs pages, CLI output, READMEs) uses plain hyphens - no em dashes
- [ ] Component behavior lives in `Blaizio.Base`, styling in `Blaizio.Ui`; after editing Ui, docs copies refreshed (`dotnet build docs/Blaizio.Docs -p:BlaizioRefresh=true`)
- [ ] Golden-file changes (if any) are deliberate and explained in the description

## Community listing PRs

<!-- Only when touching docs/Blaizio.Docs/wwwroot/community/*.json - otherwise delete this section. -->

- [ ] Registry: the URL serves a manifest that passes `blaizio registry validate`, and the homepage is real
- [ ] Theme: values are valid CSS colors and readable in BOTH light and dark (check with the site's Apply preview)
