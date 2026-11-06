---
id: infrastructure-shsvf
title: "Gate übersetzt den Client einmal: test:scripts und der Design-System-Export nutzen das Bundle aus npm run build"
status: done
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: []
tags: [verify, gate, laufzeit, fable, test-scripts]
related_adrs: []
related_research: []
prior_art: [infrastructure-eq27s, infrastructure-r5k2w, infrastructure-dvy29]
---

## Why
Ein Gate-Lauf übersetzt den Client dreimal mit Fable und Vite: Schritt 3 `npm run build` (24 s), `scripts/run-guards.mjs` vor `node --test` (22 s, gemessen 2026-11-05) und `scripts/export-design-system.mjs` im Test `[flow:design-system-export]` (eigener `vite build`, der Test braucht 37 s und ist der längste der 300). Alle drei bauen dieselbe `vite.config.js`. Der Build im Export-Test läuft unter der Last von drei weiteren Testdateien und verlängert den kritischen Pfad von `test:scripts`.

## What
1. **`run-guards.mjs` nimmt ein fertiges Bundle an.** Ist `BB_GUARD_DIST` beim Start schon gesetzt und enthält `index.html`, baut `runWithDist` nicht und löscht das Verzeichnis am Ende nicht (es gehört dem Aufrufer). Ohne die Variable bleibt alles wie heute: eigener Build in ein temporäres Verzeichnis, Aufräumen im `finally`. Ein gesetztes `BB_GUARD_DIST` ohne `index.html` ist ein Fehler mit klarer Meldung, kein stiller Rückfall.
2. **`verify.sh` reicht das Bundle aus Schritt 3 durch.** War Schritt 3 grün, startet Schritt 6 mit `BB_GUARD_DIST="$ROOT/dist/public"` (`build.outDir` in `vite.config.js`). War Schritt 3 rot, startet Schritt 6 ohne die Variable und baut selbst, wie heute.
3. **Der Design-System-Export nutzt das Bundle.** `buildApp()` in `scripts/export-design-system.mjs` gibt bei gesetztem `BB_GUARD_DIST` dieses Verzeichnis zurück, statt zu bauen; das `finally` in `main` löscht es dann nicht. `vite preview` und `copyBuiltCss` laufen dagegen wie bisher. Direkt gestartet (`npm run export:design-system`, ohne Variable) baut es weiter selbst.

Die Wächter lesen das Bundle nur (`static-serve.mjs`, `pwa-update.test.mjs` lesen, `vite preview` liefert aus). Das geteilte Verzeichnis darf kein Test verändern; das bleibt so.

## Acceptance criteria
- [ ] GIVEN `BB_GUARD_DIST` zeigt auf ein Verzeichnis mit `index.html` WHEN `runWithDist` läuft THEN wird `build` nicht aufgerufen, `run` bekommt genau dieses Verzeichnis, und das Verzeichnis existiert nach dem Lauf noch, auch bei rotem Lauf und bei Wurf. Test in `scripts/run-guards.test.mjs` mit austauschbarem `build`.
- [ ] GIVEN `BB_GUARD_DIST` ist nicht gesetzt WHEN `runWithDist` läuft THEN unverändertes Verhalten (bauen, durchreichen, im `finally` löschen). Die bestehenden Tests bleiben grün.
- [ ] GIVEN `BB_GUARD_DIST` zeigt auf ein Verzeichnis ohne `index.html` WHEN `run-guards.mjs` startet THEN Exit ungleich 0 mit einer Meldung, die den Pfad nennt; kein Test läuft.
- [ ] GIVEN `BB_GUARD_DIST` ist gesetzt WHEN `export-design-system.mjs` läuft THEN startet es keinen `vite build` und löscht das Verzeichnis nicht. Ohne Variable baut es selbst. Test über eine austauschbare Build-Funktion oder einen reinen Helfer, der aus der Umgebung entscheidet; kein zusätzlicher Browser-Test.
- [ ] `verify.sh` setzt `BB_GUARD_DIST` für Schritt 6 nur, wenn Schritt 3 grün war. Ein Wächtertest liest `verify.sh` und belegt die Kopplung (Muster wie die Quelltext-Wächter in `scripts/screenshot-route.test.mjs`).
- [ ] Im Gate-Lauf erscheint genau eine Vite-Build-Ausgabe (`built in`) über alle Logs von Schritt 3 und 6 zusammen. Im Outcome mit `grep -c "built in"` belegt.
- [ ] Laufzeit von Schritt 6 und des Tests `[flow:design-system-export]` vorher und nachher gemessen, Werte im Outcome. Referenz 2026-11-05: Schritt 6 180 s, Export-Test 37 s.
- [ ] Der Abschnitt "Wächter-Lauf" in der infrastructure-README nennt den Durchreich-Weg.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Refine 2026-11-05. Stellen im Code:
- `scripts/run-guards.mjs`: `runWithDist({ build, run, files, baseEnv, makeDir })`, heute immer `makeDir` plus `build`.
- `scripts/export-design-system.mjs`: `buildApp()` (Zeile um 409) und das `finally` in `main()` (um Zeile 1000), das `buildDir` löscht.
- `scripts/export-design-system-run.test.mjs` startet den Export per `spawnSync` mit `env: { ...process.env, DS_EXPORT_OUT_DIR }`, `BB_GUARD_DIST` kommt also schon an.
- `scripts/pwa-update.test.mjs` nutzt `BB_GUARD_DIST` bereits und liest nur.

Risiko: `dist/public` aus Schritt 3 ist dasselbe Produktions-Bundle wie der temporäre Build in `run-guards.mjs` (gleiche Konfiguration, nur anderes `outDir`). Unterscheidet sich etwas (etwa ein Pfad, der von `outDir` abhängt), fällt das im Gesamtlauf auf und gehört ins Outcome, nicht still umgangen.

Unabhängig von infrastructure-d1dwb, beide ändern aber `verify.sh`: beim Merge nacheinander integrieren.

Kein Gedankenstrich in Kommentaren und Doku (globale Schreibregel).

## Outcome

Ein Gate-Lauf übersetzt den Client jetzt einmal, in Schritt 3 (`npm run build`). Die beiden weiteren Builds in Schritt 6 (run-guards und Design-System-Export) entfallen.

- `scripts/run-guards.mjs`: `runWithDist` übernimmt ein schon gesetztes `BB_GUARD_DIST` aus `baseEnv`: kein `makeDir`, kein `build`, kein Löschen (das Verzeichnis gehört dem Aufrufer). Neuer Helfer `assertPrebuiltDist`: fehlt `index.html`, wirft er einen Fehler mit Pfad, der Hauptteil meldet ihn auf stderr und endet mit Exit 1, bevor ein Test läuft. Ohne Variable bleibt alles wie vorher.
- `verify.sh`: Schritt 3 setzt `BUILD_DIST="$ROOT/dist/public"` nur im grünen Zweig. Schritt 6 läuft dann mit `env BB_GUARD_DIST="$BUILD_DIST"`, sonst mit `env -u BB_GUARD_DIST` (eigener Build wie vorher). Geändert sind nur der Kopfkommentar zu Schritt 3 und 6 sowie die Blöcke von Schritt 3 und 6. Schritt 2 und `verify-lib.sh` sind unberührt (wegen infrastructure-d1dwb).
- `scripts/export-design-system.mjs`: neuer exportierter Helfer `resolveAppBuild(env, build)` liefert `{ dir, owned }`. Bei gesetztem `BB_GUARD_DIST` ist `dir` dieses Verzeichnis und `owned: false`, und `main` löscht es im `finally` nicht. `vite preview` und `copyBuiltCss` laufen unverändert. Direkt gestartet (ohne Variable) baut das Skript weiter selbst.

Tests (7 neu): `scripts/run-guards.test.mjs` (fertiges Bundle: kein Build, kein Löschen; bleibt bei rotem Lauf und bei Wurf liegen; ohne index.html Fehler mit Pfad und kein Lauf; Subprozess `run-guards.mjs` endet mit Exit ungleich 0, Pfad steht auf stderr, kein Test läuft; Quelltext-Wächter für die Kopplung in `verify.sh` plus `outDir` in `vite.config.js`), `scripts/export-design-system.test.mjs` (`resolveAppBuild` mit und ohne Variable). Alle neuen Tests waren vor der Änderung rot. Die drei vorhandenen `runWithDist`-Tests geben jetzt `baseEnv: {}` mit, denn im Gesamtlauf ist `BB_GUARD_DIST` im Testprozess gesetzt und hätte sonst den neuen Zweig ausgelöst.

Messung (Gate-Lauf in diesem Worktree, Logs von Schritt 3 und 6 während des Laufs mitgeschrieben):
- `cat 3.log 6.log | grep -c "built in"` ergibt 1 (`✓ built in 23.54s` in Schritt 3). Schritt 6 meldet `Bundle aus BB_GUARD_DIST: .../dist/public (kein eigener Build)`.
- Schritt 6: vorher 180 s (Referenz 2026-11-05), nachher 153 s (duration_ms 152394).
- `[flow:design-system-export]`: vorher 37 s (Referenz), nachher 12,6 s.
- Risiko aus den Notes: Gegen `dist/public` aus Schritt 3 war der Gesamtlauf auf Anhieb grün (307/307). Ein Unterschied durch das andere `outDir` ist nicht aufgefallen.

`./verify.sh`: `VERIFY: PASS (dotnet 3807 grün, node 307 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`. Das README-Delta für den Abschnitt "Wächter-Lauf" hat `npm run check:readme-delta` bestanden (Exit 0).
