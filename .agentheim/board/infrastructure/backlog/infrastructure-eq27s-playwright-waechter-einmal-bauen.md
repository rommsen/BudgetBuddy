---
id: infrastructure-eq27s
title: Playwright-Wächter in test:scripts bauen den Client einmal und laufen gegen einen gemeinsamen Preview-Server statt je Datei einen eigenen Vite mit Fable-Kompilierung zu starten
status: backlog
type: chore
context: infrastructure
created: 2026-10-27
completed:
depends_on: []
blocks: []
tags: [test-scripts, playwright, laufzeit]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`npm run test:scripts` braucht seriell 8 bis 10 Minuten und hängt parallel auf Romans Rechner (zuletzt am 2026-10-27 im work-Lauf ledger-rjhwp: 22 Minuten Stillstand in `scripts/ledger-lena-batch.test.mjs`). Die Zeit kommt fast nur vom Aufbau: rund 27 Playwright-Dateien rufen in ihrem `before` je `startFrontend` (`scripts/screenshot-route.mjs`) auf, das startet `npx vite` mit `vite-plugin-fable`, und der Fable-Daemon übersetzt den ganzen Client jedes Mal neu. Das Backend ist in diesen Wächtern ohnehin gemockt (`page.route`, `mockFilterBarApi` in `scripts/ledger-filter-bar.mjs`), es braucht also nur ein statisches Bundle.

## What
Den Client einmal bauen (`vite build`) und alle Browser-Wächter gegen einen gemeinsamen statischen Server (`vite preview` oder ein kleiner Node-Static-Server) laufen lassen. Offen beim Refine: wie der gemeinsame Server gestartet wird (globales Setup für `node --test`, ein Wrapper-Skript für `test:scripts`, oder `startFrontend` erkennt ein vorhandenes Bundle), und ob Wächter, die echtes Backend brauchen (`screenshot-route.test.mjs`, `pwa-update.test.mjs`), beim alten Weg bleiben.

## Acceptance criteria
- [ ] To be defined during refinement. Richtwerte: eine Fable-Kompilierung pro `test:scripts`-Lauf statt eine pro Datei; serielle Laufzeit messbar kürzer (vorher/nachher per `date` messen); alle bestehenden Wächter bleiben grün; der Lauf hängt auch mit Standard-Parallelität nicht mehr.

## Notes
Erfasst am 2026-10-27 aus dem work-Lauf ledger-rjhwp, auf Romans Frage, warum der Worker so lange braucht. Bisherige Umgehung: `caffeinate -i node --test --test-concurrency=1 scripts/*.test.mjs` (steht in jedem Worker-Prompt). `package.json`: `"test:scripts": "node --test scripts/*.test.mjs"`.
