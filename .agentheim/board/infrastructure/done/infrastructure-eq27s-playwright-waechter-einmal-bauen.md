---
id: infrastructure-eq27s
title: Playwright-Wächter in test:scripts bauen den Client einmal und laufen gegen einen gemeinsamen Preview-Server statt je Datei einen eigenen Vite mit Fable-Kompilierung zu starten
status: done
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

## Verifier note (iteration 1)

REASONS:
- In `scripts/ledger-category-change.test.mjs` (Test "PC: Bei Sortierung nach Kategorie erscheint die Leiste Neu laden, Neu laden lädt ab Seite 1", um Zeile 330) wird `const last = state.queries[state.queries.length - 1];` VOR dem neuen `await pollUntil(() => state.queries.length > before);` gelesen. Läuft der Zähler der Oberfläche hinterher, ist `last` noch die Anfrage vor dem Klick; die Zusicherungen auf `Page` und `Sort.Field` prüfen dann alte Daten. Das schwächt den Wächter (Kriterium 5).
- Alles andere geprüft und in Ordnung: `npm run test:scripts` 278/278 in 112 s, Pfad-Ausbruchsschutz und `/sw.js`-404 in `static-serve.mjs` echt und getestet, `run-guards.mjs` reicht Exit-Code und Dateiargumente durch und räumt im `finally` auf, keine Gedankenstriche.

SUGGESTED_FIX: `const last = ...` unter das `await pollUntil(...)` ziehen, dann die Datei und `npm run test:scripts` erneut laufen lassen.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)

REASONS:
- Check 6c (ADR 0059): der README-Bullet "Wächter-Lauf" formulierte eine Regel für künftige Wächter ("Wächter, die gemockte Anfragen zählen, warten per `pollUntil` auf den Zähler, nicht auf die Oberfläche."), ohne Durchsetzung und ohne Vermerk "prose-only, unenforced".
- Sonst alles grün: Iteration-1-Fehler behoben; `dotnet build` 0 Warnungen, `dotnet test` 2426 bestanden, Build und Guards grün, `npm run test:scripts` 278/278 in 109 s, Vite-Pfad direkt 3/3.

SUGGESTED_FIX: Satz als Beschreibung der Änderung formulieren oder als "prose-only, unenforced" kennzeichnen.

ITERATION_HINT: likely-fixable

Vom Conductor erledigt (reine Doku): der Satz beschreibt jetzt, was diese Änderung getan hat ("Die sechs Wächter, die gemockte Anfragen zählen, warten seit dieser Änderung per `pollUntil` ... auf den Zähler, bevor sie ihn prüfen."). Kein weiterer Worker-Lauf.

## Outcome

`npm run test:scripts` ruft jetzt `scripts/run-guards.mjs`: ein `vite build` in ein temporäres Verzeichnis, `BB_GUARD_DIST` setzen, `node --test` über alle `scripts/*.test.mjs` (Dateiargumente nach `--` werden durchgereicht, Exit-Code wird zurückgegeben, Verzeichnis wird im `finally` entfernt). `startFrontend` startet bei gesetztem `BB_GUARD_DIST` und ohne Backend-Port `scripts/static-serve.mjs` (detached Kindprozess, nur Node-Bibliothek: Dateien, SPA-Rückfall für Pfade ohne Endung, 404 für `/sw.js`, `registerSW.js`, `workbox-*.js`, Schutz vor Pfad-Ausbruch). Ohne `BB_GUARD_DIST` bleibt alles wie bisher (Vite). `pwa-update.test.mjs` nutzt `BB_GUARD_DIST`, wenn gesetzt (kein zweiter Build).

Neue Tests: `scripts/static-serve.test.mjs` (7: startFrontend startet den statischen Server und nicht Vite, `/`, Assets, Rückfall, 404, `/sw.js` 404, kein Pfad-Ausbruch), `scripts/run-guards.test.mjs` (4: einmal bauen und Env setzen, Exit-Code und Aufräumen bei rotem Lauf, Aufräumen bei Wurf, Dateiargumente). Der Static-Test war vor der Änderung rot (Vite lief weiter).

### Messung (Gesamtlauf, gleiche Maschine, per date)
- Vorher (Conductor, serieller Dev-Server-Lauf, 267 Tests): rund 15 bis 20 Minuten.
- Nachher seriell (BB_GUARD_CONCURRENCY=1): 19:35:10 bis 19:41:38, rund 6,5 Minuten, 278 Tests grün.
- Nachher mit 4 Dateien parallel: 112 s, 112 s, 111 s (duration_ms 111832, 112602, 111246), dreimal hintereinander 278/278 grün, keine Datei länger als 5 Minuten, kein Hänger. Gewählt: 4 (Konstante `TEST_CONCURRENCY` in `run-guards.mjs`, `BB_GUARD_CONCURRENCY=1` erzwingt seriell). Den reinen Node-Standardwert (CPU-Anzahl minus 1) habe ich nicht gemessen.

### Produktions-Bundle deckte latente Wächter-Rennen auf (Risiko aus den Notes)
Der erste Gesamtlauf gegen das Bundle hatte 10 rote Tests in 7 Dateien (ledger-account-choice, -category-change, -detail, -recent-categories, -selection, -sort-chip). Ursache: Die Wächter prüfen den Zähler einer gemockten Anfrage (`queries.length`, `calls.set.length`, `sortsSeen`, `requestedAccounts`) sofort nach einem Oberflächenzustand. Im Produktions-Bundle ist die Oberfläche schneller als der Zähler der Playwright-Route, im Dev-Server war es umgekehrt. Fachlich ist nichts geändert: vor jeder dieser Zusicherungen steht jetzt ein `await pollUntil(() => <Zähler erreicht>)` (neuer Helfer in `screenshot-route.mjs`, Polling bis 5 s). Zusicherungen selbst blieben. Iteration 2: in ledger-category-change wird `last` jetzt nach dem `pollUntil` gelesen (Verifier-Hinweis); die anderen Stellen lesen korrekt. Danach ledger-category-change einzeln ohne BB_GUARD_DIST (Vite) 7/7 grün und ein weiterer Gesamtlauf 278/278 in 112 s.

Doku: `standards/frontend/styleguide.md` (Durchsetzungs-Absatz nennt run-guards). `CLAUDE.md` und `.claude/rules/design-tokens.md` bleiben richtig und unverändert. Pflichtläufe: `dotnet build` 0 Warnungen 0 Fehler, `dotnet test` 2426 bestanden/7 übersprungen/0 Fehler, `npm run build`, `guard:classes`, `guard:tokens` grün. Ein temporäres Build-Verzeichnis aus der Zwischenmessung (Pfad in `/var/folders/.../T/tmp.*`) konnte ich wegen der rm-Sicherheitsprüfung nicht löschen; es liegt im Temp-Verzeichnis des Systems.
