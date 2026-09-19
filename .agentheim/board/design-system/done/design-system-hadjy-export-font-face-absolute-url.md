---
id: design-system-hadjy
title: "Bug: Claude-Design-Export trägt @font-face mit absolutem url(/fonts/...) in styles.css, Schriften laden in Claude Design nicht"
status: done
type: bug
context: design-system
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [captured, claude-design, fonts]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Beim Re-Sync in `ledger-xbrht` (2026-09-11) blieb in Claude Design die Warnung "Missing
brand fonts" stehen. Die Browser-Konsole zeigt: die Previews werden unter
`.../serve/preview/...` ausgeliefert, die Font-Anfragen gehen aber an die Origin-Wurzel
(`https://<projekt>.claudeusercontent.com/fonts/inter-latin-wght-normal.woff2`) und
liefern 404 ("downloadable font: download failed").

## What
`scripts/export-design-system.mjs` übernimmt die `@font-face`-Regeln der App aus dem
gebauten `styles.css` unverändert, also mit absolutem `src: url(/fonts/...)`. Weil
`styles.css` in jeder Preview nach `colors_and_type.css` eingebunden ist, gewinnt die
absolute Regel gegenüber der relativen aus `FONT_FACE_CSS` (`url('fonts/...')`), und die
Schriften laden im Claude-Design-Projekt nicht.

Fix: im Export `url(/fonts/` nach `url(fonts/` umschreiben (relativ zur Bundle-Wurzel,
wie `FONT_FACE_CSS` in `colors_and_type.css`), plus Regressionstest, der das Bundle
(`styles.css`, `colors_and_type.css`, Previews) auf absolute `url(/`-Pfade prüft.

## Acceptance criteria
- [ ] Nach `npm run export:design-system` enthält keine Datei unter `dist/design-system/`
      einen absoluten Asset-Pfad der Form `url(/…)`. Geprüft über das ganze Bundle
      (`styles.css`, `colors_and_type.css`, jede `preview/*.html`), nicht nur über die
      Datei, in der der Fehler gefunden wurde.
- [ ] `dist/design-system/styles.css` bindet die drei Schriften relativ zur Bundle-Wurzel
      ein (`url(fonts/…)`), wie `FONT_FACE_CSS` in `colors_and_type.css` es bereits tut.
- [ ] Das Umschreiben geschieht beim Erzeugen des Bundles, nicht von Hand danach: die
      Stelle, die das gebaute App-CSS ins Bundle kopiert (heute ein reines `copyFile` in
      `scripts/export-design-system.mjs`), liest, schreibt um und schreibt.
- [ ] Ein Regressionstest scheitert, wenn ein absoluter `url(/…)`-Pfad ins Bundle
      zurückkehrt. Andockpunkt ist `src/Tests/FontFaceRegressionTests.fs`, das bereits
      `src/Client/styles.css` auf lokale `@font-face`-Regeln prüft — erweitern statt eine
      zweite Prüfstelle daneben zu stellen.
- [ ] Zwei Läufe hintereinander erzeugen ein byte-identisches Bundle (Idempotenz-Kriterium
      aus `design-system-t82sg` bleibt erfüllt).
- [ ] `dotnet test` ist grün.

## Notes
Captured via `quick-capture` on 2026-09-11 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Workaround in der Session: `dist/design-system/styles.css` von Hand
gepatcht (`sed 's#url(/fonts/#url(fonts/#g'`) und nur diese Datei per DesignSync neu
hochgeladen. Der nächste `npm run export:design-system` erzeugt den Fehler wieder, bis das
Skript korrigiert ist.

## Outcome

Bug behoben: `scripts/export-design-system.mjs` kopierte das gebaute App-CSS bislang
unverändert (`copyFile`) ins Claude-Design-Bundle, inklusive der drei absoluten
`@font-face`-Regeln aus `src/Client/styles.css` (`src: url('/fonts/...')`, korrekt für die
App selbst, die von der Origin-Wurzel ausgeliefert wird). Weil `styles.css` im Bundle nach
`colors_and_type.css` eingebunden ist, gewann diese absolute Regel gegenüber der relativen
aus `FONT_FACE_CSS` (`url('fonts/...')`); die Previews liegen unter `.../serve/preview/...`,
die Font-Anfrage ging aber an die Bundle-Origin-Wurzel und lieferte 404. Ergebnis: "Missing
brand fonts" in Claude Design.

Fix: eine neue reine Funktion `rewriteAbsoluteAssetUrls(css)` in
`scripts/export-design-system.mjs` schneidet jeden führenden `/` direkt nach `url(`
(optional gequotet) weg, außer bei protokoll-relativen `url(//...)`-Pfaden. `copyBuiltCss`
liest das gebaute CSS jetzt, schreibt es damit um und schreibt erst dann `styles.css` ins
Bundle, statt es per `copyFile` unverändert zu kopieren. Die Umschreibung ist generisch für
das ganze kopierte CSS (nicht nur für die drei Font-Dateien), falls künftig weitere
root-relative Asset-Pfade ins gebaute CSS gelangen.

Damit `rewriteAbsoluteAssetUrls` unabhängig von Vite/Playwright testbar ist, ruft der Aufruf
von `main()` am Skriptende jetzt nur noch, wenn das Skript direkt gestartet wurde (Vergleich
`import.meta.url` gegen `process.argv[1]`) - beim Import als Modul (wie im Regressionstest)
läuft der volle Export nicht mehr mit.

`src/Tests/FontFaceRegressionTests.fs` (Andockpunkt aus der Task, keine zweite Prüfstelle)
bekam vier neue Testfälle, die `rewriteAbsoluteAssetUrls` per Node-Subprozess aus der realen
`scripts/export-design-system.mjs` importieren (kein Playwright-Harness in `dotnet test`,
wie schon bei design-system-t82sg): absolute `url(/...)`-Pfade werden relativ, bereits
relative und protokoll-relative `url(//...)`-Pfade bleiben unverändert, und das reale
`src/Client/styles.css` ist nach der Umschreibung frei von absoluten `url(/...)`-Pfaden
(alle drei Marken-Fonts landen bei `url('fonts/...')`). Verifiziert: die vier Tests
schlagen vor dem Fix fehl (Node-Import von `rewriteAbsoluteAssetUrls` schlägt fehl, weil die
Funktion noch nicht existiert) und bestehen danach.

`npm run export:design-system` zweimal hintereinander ausgeführt und mit `diff -r`
verglichen: byte-identisches Bundle (Idempotenz-Kriterium aus design-system-t82sg bleibt
erfüllt). Zusätzlich das ganze `dist/design-system/`-Bundle (`styles.css`,
`colors_and_type.css`, jede `preview/*.html`) nach absoluten `url(/...)`-Pfaden durchsucht:
keine gefunden.

`dotnet test`: 878 bestanden, 7 übersprungen (Comdirect/YNAB-Integrationstests, benötigen
`.env`), 0 fehlgeschlagen.

**Verifikation (Verifier, Iteration 1, unabhängig nachgefahren):** Export selbst zweimal
ausgeführt, beide Bundles mit `diff -r` verglichen: identisch, 33 Dateien. Scan über alle
33 Bundle-Dateien auf `url(\s*['"]?/(?!/)`: 0 Treffer, zusätzlich keine absoluten
`href="/…"`/`src="/…"` in den Previews. Der `isMainModule`-Guard greift auf dieser
Plattform korrekt, das Skript lief über `npm run` vollständig durch (kein stilles Exit-0
ohne Bundle). `dotnet test` aus dem Worktree: Fehler 0, erfolgreich 878, übersprungen 7.
Benannte Restlücke des Verifiers, kein unerfülltes Kriterium: die vier Tests prüfen die
Rewrite-Funktion und `src/Client/styles.css`, nicht das erzeugte Bundle selbst; ein
Rückfall von `copyBuiltCss` auf `copyFile` bei erhaltener Funktion bliebe ungefangen. Ein
Bundle-Test scheidet aus, weil kein E2E-Harness in `dotnet test` existiert; die Nachrüstung
liegt als `design-system-w4n9r` im Backlog.
