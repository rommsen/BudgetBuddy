---
id: design-system-w4n9r
title: E2E-/Playwright-Testinfrastruktur für die /styleguide-Route und den Export-Bundle-Build
status: backlog
type: chore
context: design-system
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [design-system, testing, playwright, export]
related_adrs: []
related_research: []
prior_art: [design-system-t82sg]
---

## Why
`design-system-t82sg` (Export-Bundle für Claude Design) hat keinen automatisierten Test:
das Projekt hat keine Playwright-/E2E-Testinfrastruktur, die in `dotnet test` eingehängt
ist (`RUN_SMOKE_TESTS` aus `.claude/rules/testing.md`/`build-verification.md` existiert im
Repo nicht: bekannt stale, s. Tooling-Quirks-Notiz). Der Task wurde stattdessen manuell
verifiziert: zweifacher Lauf von `npm run export:design-system` (byte-identisches Bundle),
Screenshot-Vergleich Live-Route vs. exportierte `preview/*.html` (Diary-Eintrag im
Task-File). Das ist ausreichend für den Erst-Export, aber es gibt keine Regression-Absicherung,
falls jemand künftig eine `data-ds-card`-Markierung in
`src/Client/Components/Styleguide/View.fs` versehentlich entfernt oder das Export-Skript
kaputt refactored.

## What
- Playwright (bereits als devDependency vorhanden) in eine `dotnet test`-erreichbare oder
  separat laufende Test-Suite einhängen (z. B. `npm test` als eigener CI-Schritt, oder ein
  `.NET`-Test, der das Node-Skript aufruft).
- Mindestens: `npm run export:design-system` läuft ohne Fehler und erzeugt alle
  erwarteten Dateien (`colors_and_type.css`, `styles.css`, `README.md`, `SKILL.md`, die
  sechs `preview/components-*.html`); jede `preview/*.html` beginnt mit `<!-- @dsCard`.
- Optional: einfacher visueller Regressionstest (Screenshot-Diff) für die
  `/#/styleguide`-Route, wiederverwendbar für künftige Design-System-Änderungen.

## Acceptance criteria
- [ ] Ein automatisierter Test (egal in welchem Runner) deckt ab, dass
      `npm run export:design-system` erfolgreich läuft und die erwarteten Dateien mit dem
      `@dsCard`-Marker erzeugt.
- [ ] Test ist Teil einer dokumentierten Kommando-Sequenz (README/CLAUDE.md-Hinweis oder
      package.json-Script), die vor einem Release laufen kann.

## Notes
Nicht Teil von `design-system-t82sg` selbst: die manuelle Verifikation dort reicht für den
Erst-Export; dieser Task ist die Nachrüstung der Automatisierung, falls sich das Skript oder
die Styleguide-Sektionen künftig häufiger ändern.
