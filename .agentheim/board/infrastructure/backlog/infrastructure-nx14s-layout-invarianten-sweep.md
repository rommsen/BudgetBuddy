---
id: infrastructure-nx14s
title: "Layout-Invarianten als ein generischer Sweep über alle Routen und Breiten statt als Designwerte je Komponente, mit Doktrin-Regel für Layout-Regressionen"
status: backlog
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: [infrastructure-jc1y1]
tags: [etappe-1, tests, browser, layout, doktrin]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b, infrastructure-b3t9k]
---

## Why
Die 61 `[layout]`-Tests schreiben überwiegend Designwerte fest (Sheet 500 px, Panel 340 px, Spalte 128 px, Farben, Reihenfolge der Knöpfe). Sie werden bei jeder gewollten Layoutänderung rot und flackern. Vor einem Design-Overhaul hat Roman entschieden: Designwerte löschen statt nachziehen. Was für jedes Design gelten muss, soll ein einziger generischer Test prüfen, der nach einem Redesign keine Anpassung braucht.

Die Einordnung aller 61 Tests nach Assertions steht im Anhang von `infrastructure-jc1y1`.

## What
Eine neue Datei `scripts/layout-invariants.test.mjs` mit gemeinsamen Hilfsfunktionen (etwa `scripts/layout-invariants.mjs`), die eine feste Liste von Zuständen bei mehreren Breiten besucht und für jeden Zustand dieselben Invarianten prüft. Zustände sind die Routen und die geöffneten Sheets, Popover und Dialoge, die heute Layout-Regressionen hatten. Die API wird wie üblich per `page.route` gemockt.

Invarianten (Schwellen aus dem Styleguide, nicht erfunden):
1. Kein seitliches Scrollen von Seite und Sheet (`scrollWidth <= clientWidth`), Breiten 320, 390, 768, 1024, 1440, 1920.
2. Trefferflächen sichtbarer `button`, `a`, `[role=tab]`, `[role=option]` am Handy mindestens 48 × 48 px (`TouchTargets.minSize`, Styleguide). Heute prüfen `ledger-external-links` und `ledger-split-editor` gegen 44 px, `rules-page-mobile` und `touch-target-small-button` gegen 48 px. Es gilt 48.
3. Eingabefelder am Handy mindestens 16 px Schrift (iOS zoomt sonst beim Fokus, Bug design-system-btwhg).
4. Kein sichtbarer Text abgeschnitten (`scrollWidth/scrollHeight` gegen `client*`, Ellipse mit `title` ist erlaubt). Fixtures mit langen Namen und großen Beträgen.
5. Nichts verdeckt: ein offenes Popover, Sheet oder Dialog liegt ganz im Viewport bzw. im Panel. Sein Primärknopf und Toasts sind per `elementFromPoint` das oberste Element.
6. Sticky-Köpfe verdecken nach dem Scrollen keinen Inhalt (Navbar, Filterleiste, Tabellenkopf überlappen nicht).
7. Primärknopf eines Sheets ohne Scrollen erreichbar, auch bei verkleinertem Viewport (offene Tastatur, `--vvh`).
8. Spaltenköpfe der Kassenbuch-Tabelle überlappen nicht (800 bis 1440 px).
9. Kein Layout-Sprung beim Nachladen: Zeilen, die erst nach einer Antwort gefüllt werden, behalten ihre Lage (heute `sync-start:92`).

Messregeln gegen Flackern: erst messen, wenn endliche Animationen fertig sind (unendliche wie Spinner ausfiltern, Vorbild `scrollIntoStickyState` in `ledger-filter-bar.test.mjs:268`), Scrollen bis zum Stillstand, keine exakten Pixelwerte. Eine Verletzung meldet Zustand, Breite, Selektor und Messwert.

Doktrin: `.claude/rules/test-doctrine.md` bekommt in Punkt 5 und 8 eine Regel: Ein Layoutbug bekommt seinen Regressionstest als neuen Zustand oder neue Invariante im Sweep, nicht als eigenen Test pro Komponente. Feste Designwerte (Größen, Abstände, Farben, Reihenfolge, Animation) werden nicht im Browser getestet. Sie stehen im Styleguide, Tokens prüfen `guard:tokens` und `guard:classes`.

## Acceptance criteria
- [ ] `scripts/layout-invariants.test.mjs` prüft die Invarianten 1 bis 9 über eine Zustandsliste, die mindestens jeden Zustand abdeckt, für den einer der bugbezogenen Tests im Anhang von `infrastructure-jc1y1` heute eine Invariante prüft.
- [ ] Gegenbeweis je Invariante: ein Testfall mit einer absichtlich kaputten Seite (etwa per injiziertem CSS) zeigt, dass der Sweep die Verletzung meldet.
- [ ] Der Sweep enthält keine festen Designwerte außer den Styleguide-Schwellen (48 px, 16 px) und keine Selektoren über Tailwind-Klassenketten.
- [ ] Verstößt die heutige UI gegen eine Invariante (etwa Trefferflächen unter 48 px, wo bisher gegen 44 geprüft wurde), ist das als Bug-Task erfasst oder im selben Schritt behoben, nicht per Ausnahme stillgelegt.
- [ ] Die Doktrin-Regel steht in `.claude/rules/test-doctrine.md`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`, der Sweep läuft zehnmal hintereinander grün (`npm run test:scripts -- scripts/layout-invariants.test.mjs`).

## Notes
- Überschneidung mit `infrastructure-n2w8q` Punkt 6 (berechnete Farben unter `[layout]`): mit dieser Regel testet `[layout]` keine Farben mehr. n2w8q Punkt 6 ist damit entschieden, die Kennungstabelle in Doktrin Punkt 5 und `TAGS` in `scripts/browser-usage.mjs` folgen.
- Vorhandene Hilfsfunktionen zum Wiederverwenden: `noSideScroll` (budget-page.test.mjs:874), `stickyRects` (:627), `nothingCut` und `insideBody` (budget-money-tab.test.mjs:108, :114), `scrollIntoStickyState` (ledger-filter-bar.test.mjs:268), `waitForStableHeaderWidths` (ledger-status-column.test.mjs:54). Teile davon mischen Invariante und Designwert (`assertStickyWithoutCollision` mit `top === 64`, `assertNoOverlap` mit `MIN_WIDTH`), diese Teile entfallen.
- Fokus sichtbar nur bei Tastatur ist ein Kandidat, gehört aber zu `[fokus]`, nicht in diesen Sweep.
