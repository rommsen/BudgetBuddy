---
id: design-system-wa6wz
title: "Skript für headless Screenshots beliebiger App-Routen (Werkzeug für das visuelle Gate)"
status: todo
type: feature
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [test-infra, visuelles-gate, playwright]
related_adrs: [0030]
related_research: []
prior_art: [design-system-t82sg]
---

## Why
Das design-system-README verlangt seit dem 2026-09-12 für jede Frontend-Arbeit ein
visuelles Gate: vor dem Integrieren wird die gebaute Seite aufgenommen und neben ihr
Mockup gelegt. Ohne Werkzeug macht das jeder von Hand neu, mit jedes Mal anderen
Viewports und anderem Vorgehen, und genau deshalb unterbleibt es.

Anlass war `ledger-b1xwe`: alle Akzeptanzkriterien erfüllt, Verifikation bestanden, und
das Ergebnis sah trotzdem nicht wie das Zielbild aus. Niemand hatte das Bild angesehen.

## What
Ein Skript `scripts/screenshot-route.mjs` nach dem Muster von
`scripts/export-design-system.mjs`, aber für beliebige Routen der laufenden App. Aufruf:

```
node scripts/screenshot-route.mjs '#/ledger' --out docs/ux/kassenbuch/screenshots/
```

Anders als der Styleguide-Export braucht das **den Server**, weil die meisten Routen Daten
laden. Der Styleguide-Export kommt mit `vite preview` aus (rein präsentationale Route);
hier muss zusätzlich der Giraffe-Server laufen, und der Client erreicht ihn über den
Vite-Proxy `/api` auf `localhost:5081` (`vite.config.js`).

Festgelegt mit Roman am 2026-09-18:

- **Nur aufnehmen, nicht vergleichen.** Das Skript legt die Bilder ab, der Abgleich mit dem
  Mockup bleibt Augenarbeit von Worker und Roman. Ein Pixelvergleich mit Toleranz wäre als
  Gate mächtiger, ist aber nicht stabil zu halten, solange die Oberfläche in Scheiben
  wächst und sich das Zielbild bewusst noch unterscheidet.
- **Die Datenbank ist eine Kopie in einem temporären Verzeichnis.** Das Skript kopiert die
  Entwicklungsdatenbank (`DATA_DIR`, Vorgabe `~/my_apps/budgetbuddy-dev/budgetbuddy.db`) in
  ein Temp-Verzeichnis und startet den Server mit `DATA_DIR` darauf. Echte Daten im Bild,
  garantiert kein Schreibzugriff auf das Original. "Lesend gegen die Entwicklungsdatenbank"
  gibt es nicht: der Server legt beim Start Tabellen an und migriert (ADR 0030 ist genau an
  dieser Stelle entstanden).

Weiter vorgesehen:
- beide Viewports in einem Lauf (390 × 844 und 1440 × 900), Dateinamen nach der bereits
  gewachsenen Konvention in `docs/ux/kassenbuch/screenshots/`: `handy-390x844-<name>.png`
  und `pc-1440x900-<name>.png`
- Warten auf einen echten Ladezustand, nicht auf eine feste Zeitspanne
- Aufräumen garantiert, auch bei Abbruch: `startPreviewServer`/`stopServer` und der
  `finally`-Block in `export-design-system.mjs` zeigen das Muster, dazu das Temp-Verzeichnis

## Acceptance criteria
- [ ] `node scripts/screenshot-route.mjs '#/ledger'` legt ohne weitere Argumente zwei
      PNG-Dateien an, eine je Viewport, benannt nach der Konvention oben.
- [ ] Ein `--out <pfad>`-Argument steuert das Zielverzeichnis; ohne Angabe gilt eine
      dokumentierte Vorgabe.
- [ ] Das Skript startet Server und Frontend selbst und beendet beide wieder. Nach einem
      Lauf laufen keine Kindprozesse weiter und die belegten Ports sind frei.
- [ ] Bei Abbruch (Fehler in der Aufnahme, `SIGINT`) werden Kindprozesse und
      Temp-Verzeichnis ebenfalls aufgeräumt; ein Test oder ein nachweisbarer Lauf belegt das.
- [ ] Die Entwicklungsdatenbank ist nach einem Lauf **byte-identisch** zu vorher
      (Prüfsumme vor und nach dem Lauf im Verifikationsprotokoll).
- [ ] Der Server des Skripts läuft nachweislich gegen das Temp-Verzeichnis: der Lauf
      scheitert laut, wenn `DATA_DIR` auf das Original zeigt.
- [ ] Das Skript wartet auf einen Ladezustand der Seite, nicht auf eine feste Zeitspanne;
      im Code ist kein unbedingtes `setTimeout` als Warte-Ersatz.
- [ ] Eine unbekannte oder nicht ladende Route beendet den Lauf mit einem Fehlercode und
      einer Meldung, die die Route nennt, statt ein leeres Bild abzulegen.
- [ ] `npm run` kennt einen Eintrag für das Skript, analog `export:design-system`.
- [ ] Das design-system-README verweist beim visuellen Gate auf das Skript statt auf
      "von Hand aufnehmen".
- [ ] Die aufgenommenen Bilder zeigen die Seite vollständig und geladen, ohne Spinner oder
      abgeschnittene Bereiche. [human-eye]

## Notes
Captured am 2026-09-12 beim Aufsetzen des visuellen Gates, verfeinert am 2026-09-18 mit
Roman. Playwright liegt bereits im Projekt (`playwright` in `package.json`).

Vorlage für Aufbau und Aufräumen: `scripts/export-design-system.mjs` (`getFreePort`,
`startPreviewServer`, `stopServer`, `finally`-Block, `chromium.launch`).

Der Bildvergleich gegen ein hinterlegtes Mockup ist bewusst nicht Teil dieser Aufgabe.
Wenn die Oberfläche steht und das Zielbild nicht mehr absichtlich abweicht, lohnt ein
eigener Task dafür.

Dateien: nur `scripts/`, `package.json` und das design-system-README. Keine Überschneidung
mit `ledger-kdnnz`.
