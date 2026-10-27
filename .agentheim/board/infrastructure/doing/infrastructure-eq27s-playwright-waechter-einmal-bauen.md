---
id: infrastructure-eq27s
title: Playwright-Wächter in test:scripts bauen den Client einmal und laufen gegen einen gemeinsamen Preview-Server statt je Datei einen eigenen Vite mit Fable-Kompilierung zu starten
status: doing
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
`npm run test:scripts` braucht seriell 15 bis 20 Minuten und hing parallel auf Romans Rechner (work-Lauf ledger-rjhwp am 2026-10-27: 22 Minuten Stillstand, danach zwei serielle Gesamtläufe; rund 60 von 85 Minuten Laufzeit gingen in die Wächter, 9 Minuten in den Code). Die Zeit kommt fast nur vom Aufbau: 28 Browser-Wächter rufen in ihrem `before` je `startFrontend(port, undefined)` (`scripts/screenshot-route.mjs`) auf, das startet `npx vite` mit `vite-plugin-fable`, und der Fable-Daemon übersetzt den ganzen Client jedes Mal neu. Kein einziger dieser Aufrufe übergibt einen Backend-Port: alle Wächter mocken die Fable.Remoting-Aufrufe per `page.route`. Ein einmal gebautes, statisch ausgeliefertes Bundle genügt ihnen also.

## What
1. **Einmal bauen.** Ein neues Skript `scripts/run-guards.mjs` baut den Client einmal (`npx vite build --outDir <tmp-Verzeichnis> --emptyOutDir`, wie `scripts/pwa-update.test.mjs` es schon tut), setzt eine Umgebungsvariable (Vorschlag `BB_GUARD_DIST=<tmp-Verzeichnis>`), startet `node --test` über `scripts/*.test.mjs` und räumt das Verzeichnis am Ende auf, auch bei rotem Lauf oder Abbruch. Argumente nach `--` gehen an `node --test` weiter, damit sich einzelne Dateien gezielt laufen lassen (`npm run test:scripts -- scripts/ledger-detail.test.mjs`). `package.json`: `"test:scripts": "node scripts/run-guards.mjs"`.
2. **Statisch ausliefern.** `startFrontend(port, backendPort)` behält Signatur und Rückgabe (ein Kindprozess, den `stopServer(child, port)` beendet). Ist `BB_GUARD_DIST` gesetzt und `backendPort` undefined, startet es statt Vite einen kleinen statischen Node-Server als Kindprozess (neues Skript, z. B. `scripts/static-serve.mjs <dir> <port>`, nur Node-Standardbibliothek): liefert Dateien aus dem Verzeichnis, fällt für unbekannte Pfade ohne Dateiendung auf `index.html` zurück und antwortet für `/sw.js` (und `registerSW`-Varianten) mit 404, damit kein Service Worker die `page.route`-Mocks umgeht (Playwright fängt Anfragen eines Service Workers nicht ab). Ohne `BB_GUARD_DIST` bleibt alles wie heute, sodass `node --test scripts/x.test.mjs` direkt weiter funktioniert.
3. **Parallelität nach Messung.** Mit statischem Server ist die Ursache des Hängers (parallele Fable-Kompilierungen) weg. Der Worker misst den Gesamtlauf seriell und mit Standard-Parallelität und setzt in `run-guards.mjs` den schnelleren der beiden Modi, sofern er dreimal hintereinander ohne Hänger und grün durchläuft; sonst bleibt `--test-concurrency=1`.
4. **Doppelten Build vermeiden (optional, wenn billig).** `scripts/pwa-update.test.mjs` nutzt `BB_GUARD_DIST`, wenn gesetzt, statt selbst ein zweites Mal zu bauen.

## Acceptance criteria
- [ ] `npm run test:scripts` übersetzt den Client genau einmal: ein Test über `scripts/run-guards.mjs` (oder eine Zusicherung im Lauf) belegt, dass `startFrontend` bei gesetztem `BB_GUARD_DIST` keinen Vite-Prozess startet, sondern den statischen Server.
- [ ] Der statische Server liefert `index.html` für `/`, Assets aus dem Build-Verzeichnis, `index.html` als Rückfall für Pfade ohne Endung und 404 für `/sw.js`. Eigener Node-Test in `scripts/`.
- [ ] Ohne `BB_GUARD_DIST` verhält sich `startFrontend` unverändert (Vite-Entwicklungsserver); ein einzelner Wächter lässt sich weiter direkt mit `node --test scripts/<datei>.test.mjs` laufen.
- [ ] `run-guards.mjs` reicht Dateiargumente an `node --test` durch, gibt den Exit-Code von `node --test` zurück und entfernt das Build-Verzeichnis auch bei rotem Lauf.
- [ ] Alle bestehenden Wächter sind im neuen Gesamtlauf grün, ohne dass ein Wächter fachlich geändert wurde (außer `pwa-update.test.mjs` für Punkt 4 und dem allgemeinen Start-Helfer).
- [ ] Laufzeit vorher und nachher gemessen (per `date`, Gesamtlauf seriell, gleiche Maschine) und im Outcome genannt; der gewählte Parallelitäts-Modus lief dreimal hintereinander grün und ohne Hänger.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`, `npm run guard:tokens` grün.

## Notes
Entscheidungen beim Refine (2026-10-27): statischer Node-Server statt `vite preview`, weil `vite preview` die `vite.config.js` samt `vite-plugin-fable` lädt und dabei womöglich den Fable-Daemon startet; ein Standardbibliothek-Server hat diese Abhängigkeit nicht. Service Worker per 404 auf `/sw.js` ausgeschaltet statt `serviceWorkers: 'block'` in jedem der rund 40 `browser.newContext`-Aufrufe.

Risiken für den Worker: Produktions-Bundle statt Entwicklungsmodus (Minifizierung, React-Produktionsbuild). Ein Wächter, der sich auf ein Verhalten nur des Dev-Servers stützt, fällt dann auf; das ist im Outcome zu nennen, nicht still zu umgehen. `waitForPageLoaded` wartet auf das App-eigene Bereitschaftssignal, das gilt in beiden Modi.

Wo die Wächter-Regel überall steht (nach der Änderung prüfen und anpassen): `CLAUDE.md` (README-Deltas, `npm run test:scripts`), `standards/frontend/styleguide.md` Zeile 159 und 379, `.claude/rules/design-tokens.md`. Die BC-README von infrastructure bekommt einen Eintrag für den Wächter-Lauf.

Erfasst am 2026-10-27 aus dem work-Lauf ledger-rjhwp. Seitdem steht in `package.json` vorläufig `node --test --test-concurrency=1 scripts/*.test.mjs` (Commit 00c3057).
