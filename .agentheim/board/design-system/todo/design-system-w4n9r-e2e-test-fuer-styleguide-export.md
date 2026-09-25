---
id: design-system-w4n9r
title: "Automatischer Test für den Design-System-Export: läuft in ein temporäres Verzeichnis und prüft das vollständige Bundle samt @dsCard-Markern"
status: todo
type: chore
context: design-system
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [design-system, testing, playwright, export]
related_adrs: []
related_research: []
prior_art: [design-system-t82sg, design-system-wa6wz, design-system-q2bmv]
---

## Why
`design-system-t82sg` (Export-Bundle für Claude Design) hat keinen automatisierten Test.
Der Task wurde manuell verifiziert: zweifacher Lauf von `npm run export:design-system`
(byte-identisches Bundle), Screenshot-Vergleich Live-Route gegen exportierte
`preview/*.html`. Das reichte für den Erst-Export, aber es gibt keine
Regressions-Absicherung, falls jemand eine `data-ds-card`-Markierung in
`src/Client/Components/Styleguide/View.fs` versehentlich entfernt, eine Karte in
`COMPONENT_CARDS` vergisst oder das Export-Skript beim Umbau bricht. Seit `design-system-an22r`
im Backlog steht (Export soll zusätzliche Marker-Attribute erzeugen), wird das Skript
weiter angefasst; ohne Test bemerkt man einen Bruch erst beim nächsten Re-Sync.

## What
Kein neues Test-Framework. Das Projekt hat für Skripte bereits Node's eingebauten Runner
(`npm run test:scripts` = `node --test scripts/*.test.mjs`), und
`scripts/screenshot-route.test.mjs` zeigt das Muster für browser-gebundene Tests
(Playwright per dynamischem Import, benannter Skip, wenn kein Browser da ist,
`design-system-q2bmv`). Genau dort wird angebaut:

1. **`scripts/export-design-system.mjs`**
   - Das Zielverzeichnis wird überschreibbar: Umgebungsvariable `DS_EXPORT_OUT_DIR`
     (absoluter Pfad); ohne sie bleibt `dist/design-system` wie heute. Die Auflösung
     liegt in einer reinen, exportierten Funktion `resolveOutDir(env, root)`, damit sie
     ohne Build testbar ist. `OUT_DIR`, `PREVIEW_DIR`, `ASSETS_DIR` und `fonts/` leiten
     sich daraus ab (heute Konstanten auf Modulebene, Zeile 46 bis 48 und 210).
   - `COMPONENT_CARDS` wird exportiert, dazu eine reine, exportierte Funktion
     `expectedBundleFiles(cards)`, die die Soll-Liste des Bundles relativ zur Wurzel
     liefert: `colors_and_type.css`, `styles.css`, `README.md`, `SKILL.md`,
     `fonts/LICENSE.md`, und je Karte `preview/<id>.html`. Der Test bezieht die Soll-Liste
     aus dieser Funktion, statt sie abzuschreiben; ändert sich die Kartenliste, wandert
     der Test mit.
   - `isMainModule`-Wächter (Zeile 765) bleibt, damit der Import im Test nichts startet.
2. **`scripts/export-design-system.test.mjs`** (neu; der Glob in `test:scripts` nimmt ihn
   ohne Änderung an `package.json` auf):
   - Browserfrei: `resolveOutDir` liefert ohne Variable `<root>/dist/design-system`, mit
     Variable deren Wert; `expectedBundleFiles(COMPONENT_CARDS)` enthält genau eine
     `preview/components-*.html` je Karte (heute 16) plus die fünf festen Dateien.
   - Browser-gebunden (Skip mit benanntem Grund, wenn Playwright/Chromium fehlt): das Skript
     per `spawnSync(process.execPath, ["scripts/export-design-system.mjs"])` mit
     `DS_EXPORT_OUT_DIR` auf ein `mkdtemp`-Verzeichnis laufen lassen, Exit-Code 0 erwarten,
     danach: jede Datei aus `expectedBundleFiles` existiert und ist nicht leer; jede
     `preview/components-*.html` beginnt mit `<!-- @dsCard`; `fonts/` enthält neben
     `LICENSE.md` mindestens eine Font-Datei; `dist/design-system` im Repo wurde **nicht**
     angefasst (mtime unverändert oder nicht vorhanden). Temporäres Verzeichnis wird in
     `finally` gelöscht. Test-Timeout großzügig (`vite build` plus Preview plus Playwright,
     Richtwert 300 s).
3. **`scripts/README.md`**: ein kurzer Abschnitt, dass `npm run test:scripts` den Export
   mitprüft und einen Browser braucht (Skip-Verhalten ohne).

**Nicht in dieser Aufgabe:** ein visueller Regressionstest (Screenshot-Diff) der
`/styleguide`-Route; dafür gibt es `scripts/screenshot-route.mjs`. Ebenso nicht: der
Doppel-Lauf auf Byte-Gleichheit (verdoppelt die Laufzeit; bleibt manuelle Prüfung bei
Änderungen am Skript).

## Acceptance criteria
- [ ] `resolveOutDir` und `expectedBundleFiles` sind reine, exportierte Funktionen in
      `scripts/export-design-system.mjs`; das Skript benutzt sie selbst (kein zweiter,
      abweichender Dateiplan im Skript).
- [ ] Ohne `DS_EXPORT_OUT_DIR` schreibt `npm run export:design-system` weiterhin nach
      `dist/design-system` (Test über `resolveOutDir`, kein Vollauf nötig).
- [ ] Browserfreie Tests: `resolveOutDir` mit und ohne Variable; `expectedBundleFiles`
      liefert je Karte genau einen `preview/`-Eintrag plus die festen Dateien.
- [ ] Browser-gebundener Test: Vollauf in ein temporäres Verzeichnis, Exit-Code 0, alle
      Dateien aus `expectedBundleFiles` vorhanden und nicht leer, jede
      `preview/components-*.html` beginnt mit `<!-- @dsCard`, `fonts/` enthält
      `LICENSE.md` und mindestens eine Font-Datei; `dist/design-system` bleibt unberührt;
      das temporäre Verzeichnis ist danach weg.
- [ ] Ohne verfügbaren Browser wird der Vollauf-Test mit benanntem Grund übersprungen, die
      browserfreien Tests laufen trotzdem (Muster `screenshot-route.test.mjs`).
- [ ] Mutationsprobe im Ergebnis dokumentiert, nicht committet: eine aus `COMPONENT_CARDS`
      entfernte Karte oder ein aus `Styleguide/View.fs` entferntes `data-ds-card` macht
      den Vollauf-Test rot.
- [ ] `npm run test:scripts` nimmt die neue Datei über den bestehenden Glob auf und ist
      grün; `package.json` ist unverändert.
- [ ] `scripts/README.md` beschreibt den Test und das Skip-Verhalten.

## Notes
Verfeinert am 2026-09-25. Nicht Teil von `design-system-t82sg` selbst: die manuelle
Verifikation dort reicht für den Erst-Export; dieser Task ist die Nachrüstung der
Automatisierung.

Stolpersteine: das Skript reserviert vorab einen freien Port für `vite preview` und räumt
in `finally` auf; der Test darf keinen zweiten Export parallel starten. Playwright liegt
als devDependency vor, Chromium muss installiert sein (`npx playwright install chromium`);
fehlt es, greift der Skip. `design-system-an22r` (weitere Marker-Attribute) baut auf
denselben Marker-Anfang `<!-- @dsCard` auf; dieser Test prüft nur den Anfang, nicht die
Attribute, und muss dafür nicht angepasst werden.
