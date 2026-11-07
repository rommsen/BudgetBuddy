---
id: infrastructure-nx14s
title: "Layout-Invarianten als ein generischer Sweep über alle Routen und Breiten statt als Designwerte je Komponente, mit Doktrin-Regel für Layout-Regressionen"
status: done
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

## Verifier note (iteration 1)

**REASONS**
- `./verify.sh` im Worktree: `VERIFY: FAIL (rote Schritte: test:scripts)`, 326 von 327. Rot: `[layout] Handy: die Prioritätspfeile sind mindestens 48 mal 48 px …` (`scripts/rules-page-mobile.test.mjs:154`, Assertion :160), Meldung `Priorität erhöhen: 48 mal 47.99998474121094`.
- Flackern kommt aus dem Diff: im Worktree 3 von 8 Läufen rot (zweimal „Priorität erhöhen: 48 mal 47.99998…“, einmal „Knopf 48 mal 47.99999…“), auf main 3 von 3 grün. Verdacht: `PageHeader.fs` (Titel `min-w-0`, `hyphens-auto break-words`, `lang="de"`, Aktionen `shrink-0`) oder `Navigation.fs` (`px-1`, `min-w-12`).
- `TOUCH_FROM = 390` (`scripts/layout-invariants.mjs:33`) ist keine Styleguide-Schwelle und schaltet Invariante 2 und 3 bei 320 px für alle 21 Zustände ab. Das ist eine pauschale Ausnahme, breiter als der eine Fund (budgeting-q7s3w).
- Nebenbei: `Detail.fs` `reviewedSwitch` ist jetzt `h-12 w-12` auf jeder Breite ohne `md:`-Variante, gegen „Ab md bleiben die PC-Maße“.

**SUGGESTED_FIX**
Die Änderung finden, die Pfeile und Knopf der Regelseite am Handy auf 47,99… px drückt, und im Layout-Code beheben, ohne Toleranz im Test zu lockern. Danach `./verify.sh` mehrmals hintereinander grün, dazu zehn grüne Sweep-Läufe. Die sechs Schritte bei 320 px beheben (etwa zwei Zeilen) und `TOUCH_FROM` auf 320 setzen, oder die Ausnahme auf diesen einen Zustand begrenzen und in der Task als Ausnahme nennen, statt einer globalen Breitengrenze.

**ITERATION_HINT:** likely-fixable

## Outcome

Der Layout-Sweep steht. `./verify.sh` zeigte dreimal hintereinander `VERIFY: PASS` (je dotnet 4286 grün, 13 opt-in-Integrationstests übersprungen, node 328 grün). `npm run test:scripts -- scripts/layout-invariants.test.mjs` lief zehnmal hintereinander grün (je 11 von 11 Tests), `scripts/rules-page-mobile.test.mjs` ebenfalls zehnmal hintereinander grün (je 9 von 9). Gesamtstand gegen das Bundle aus `npm run build` (`BB_GUARD_DIST=dist/public`).

**Iteration 2 (Verifier-Punkte)**
- Flackern der Regelseite (`rules-page-mobile.test.mjs:154` und `:184`, „48 mal 47,99998“): Ursache gemessen, nicht vermutet. Die Knöpfe sind im Layout exakt 48 px. Der Test maß während `pageEnter` (die Seite fährt 0,3 s per `transform: translateY` ein). Mit transform rechnet Chromium die Box in float32; liegen Ober- und Unterkante eines Knopfs beiderseits einer Zweierpotenz (128 oder 256 px), ergibt die Differenz 47,99998. Seit der Titel „Kategorisierungsregeln“ am Handy umbricht (nötig, vorher war die Seite bei 390 px 400 px breit), liegen Pfeile (227 bis 275 px) und Overflow-Knopf (89 bis 137 px) genau über diesen Grenzen. Ein Layout-Eingriff hätte das nur verschoben; jede andere Position über einer Zweierpotenz flackert genauso. Behoben, indem der Test nach der Messregel des Sweeps erst misst, wenn die endlichen Animationen fertig sind (`settle` aus `layout-invariants.mjs` in `openPage`). Keine Toleranz gelockert, die Schwelle bleibt `>= 48`.
- `TOUCH_FROM = 390` entfernt: Invarianten 2 und 3 gelten am Handy für alle Zustände ab 320 px. Der Lauf bei 320 px meldete nur die sechs Schritte im Tab Geld (44,7 × 48 px), sonst nichts.
- Die Schritte bei 320 px: zwei Zeilen (`grid-cols-3`) ausprobiert. Dann rutscht „Nur noch … zu verteilen“ bei 320 und 390 px aus dem 500-px-Sheet (841 bis 889 px bei 844 px Höhe, Invariante 7, budgeting-c5w2n). Platz für eine zweite Zeile gäbe es nur mit Umbau des Tabs (reservierter Vorschau-Platz, Abstände) oder Schritten ohne Abstand, beides kein kleiner Eingriff. Darum zurückgenommen und die Ausnahme auf genau diesen Zustand begrenzt: `exceptions: [{ width: 320, inv: 2, selector: 'button[data-testid="assign-step"]' }]` im Zustand `budget-geld-zuweisen` (`layout-states.mjs`), ausgewertet in `runState`. Der Test „[layout] Sweep: Ausnahmen sind einzeln benannt …“ hält die Liste auf genau diesen Eintrag fest und zeigt per Gegenbeweis, dass dieselbe Invariante bei 390 px für die Schritte greift. Doktrin Punkt 5: eine Ausnahme nennt Zustand, Breite, Invariante und Selektor, nie eine ganze Breite, und steht in der Kann-Sammlung.
- `Detail.fs` `reviewedSwitch`: am Handy 48 × 48 px, ab `md` wieder so hoch wie die Schiene (`h-12 md:h-7 w-12`).

**Zeile für die Kann-Sammlung budgeting (kein Ticket):** Tab Geld bei 320 px: die sechs Schritte messen 44,7 × 48 px statt 48. Zwei Zeilen schieben den Knopf aus dem 500-px-Sheet (budgeting-c5w2n); eine Lösung braucht einen kleinen Umbau des Tabs. Danach die Ausnahme in `scripts/layout-states.mjs` (`budget-geld-zuweisen`) und im Test „Ausnahmen sind einzeln benannt“ streichen.

**Dateien**
- `scripts/layout-invariants.mjs`: Messung und Ablauf. `settle` (endliche Animationen fertig, unendliche übergangen, dann fünf Frames ohne Änderung an Ebenen und Seitenhöhe), `scrollToEndAndRest`, `measure` (Invarianten 1 bis 5, 7, 8 im Browser), `measureSticky` (6), `snapshotPositions` und `measureJump` (9), `runState` (ein Zustand bei einer Breite, mit `inject` für Gegenbeweise und den benannten `exceptions` des Zustands), `sweep` (alle Zustände, vier Seiten zugleich), `formatViolation` (Zustand, Breite, Invariante, Selektor, Messwert). Feste Werte sind nur die Styleguide-Schwellen `TOUCH_MIN = 48`, `INPUT_FONT_MIN = 16` und `MOBILE_BELOW = 768` (`md`). Toleranz 1 px. Nicht gemockte API-Aufrufe und ein fehlender Primärknopf lassen einen Zustand scheitern.
- `scripts/layout-states.mjs`: 21 Zustände mit synthetischen Mocks (lange Namen, fünfstellige Beträge), je Zustand im Kommentar der Bug, aus dem er kommt: `budget`, `budget-geld-zuweisen`, `budget-geld-abgeben`, `budget-picker`, `budget-buchungen`, `budget-historie-tab`, `budget-target`, `budget-target-anlegen`, `budget-target-warnung`, `budget-targets-anwenden`, `budget-historie`, `kassenbuch`, `kassenbuch-filter-kategorie`, `kassenbuch-filter-zeitraum`, `kassenbuch-detail`, `kassenbuch-aufteilen`, `kassenbuch-kategorie-toast`, `kassenbuch-auswahl-popover`, `regeln`, `regeln-menue`, `regeln-dialog-picker`, `sync-start` (Invariante 9 über verzögerte `getBankConnectionStatus`).
- `scripts/layout-invariants.test.mjs`: `[layout] Sweep: …`, `[layout] Sweep: Ausnahmen sind einzeln benannt …` und neun `[layout] Gegenbeweis Invariante N: …`. Jeder Gegenbeweis injiziert CSS in einen echten Zustand und prüft, dass die Meldung Zustand, Breite, Selektor und Messwert trägt und derselbe Zustand ohne das CSS grün ist.
- `scripts/rules-page-mobile.test.mjs`: `openPage` wartet auf `settle` (siehe oben).
- Doktrin `.claude/rules/test-doctrine.md`: Punkt 5 beschreibt den Sweep, feste Designwerte werden nicht im Browser getestet (Styleguide, `guard:tokens`, `guard:classes`), Ausnahmen nur einzeln benannt. Punkt 8: ein Layoutbug bekommt seinen Regressionstest als Zustand oder Invariante im Sweep, eine neue Invariante mit Gegenbeweis. `[layout]` heißt in Tabelle und `TAGS` (`scripts/browser-usage.mjs`) jetzt „Layout-Invarianten …“ ohne berechnete Farben, damit ist n2w8q Punkt 6 entschieden.
- Ebenen-Marke ohne Tailwind-Selektor: `data-layer="popover"` in `FilterPopover.fs`, `data-layer="dialog"` in `Modal.fs`.

**Gefundene Verstöße der heutigen UI, im selben Schritt behoben** (der Sweep war vorher rot):
1. Seitliches Scrollen: Regelseite bei 320 und 390 px 400 px breit, weil „Kategorisierungsregeln“ nicht umbrach (`PageHeader.fs`: Titel `min-w-0`, `hyphens-auto break-words`, `lang="de"`, Aktionen `shrink-0`).
2. Navigation: bei 320 px ragte „Settings“ aus der unteren Leiste, bei 768 und 800 px rechts aus der oberen (Markentext erst ab `lg`), beides `Navigation.fs`.
3. Trefferflächen unter 48 px am Handy (vorher teils gegen 44 geprüft): „Geprüft“ in der Kassenbuch-Zeile, Kontowechsler, Schnellwahl „Dieser Monat“ (`Ledger/View.fs`); im Detail Filter-Knopf, Kategorie-Pfeil, Aufteilen, Memo, Lena fragen, Bestell-Link, Schalter „Geprüft“ (`Detail.fs`); Split-Ziele (`SplitEditor.fs`); Picker-Zeilen (`CategoryPicker.fs`); Kalenderknopf (`DateField.fs`); Übernehmen (`PickerParts.fs`); „Versteckt (n)“ (`CategoryMultiSelect.fs`); Toast-Aktion und Schließen (`Toast.fs`). Ab `md` bleiben die PC-Maße.
4. Platzhalter „TT.MM.JJJJ“ abgeschnitten: Zeitraum-Filter und Tab Target bei 320 px.
5. Budget bei 1024 px: Tabelle breiter als ihr Platz, Verfügbar unter dem Panel, Tabellenkopf über dem Panel (`MonthView.fs`, Target bricht unter `xl` um).
6. Layout-Sprung: bei 320 px rutschte „Sync starten“ nach der Antwort zum Bankstand um 16 px (`StatusViews.fs`, Hinweis einzeilig mit `title`).
7. Gekürzter Text ohne `title`: Budget-Zeile am Handy, Sheet-Titel und Untertitel (`BottomSheet.fs`), Picker-Namen und Änderungskarte (`PickerParts.fs`), Buchungen, Historie, Inspektor, Targets-Vorschau, Target-Check, Regel-Dialog Kategorie, Kassenbuch Payee, Kategorie, Memo, Hinweise, Kontonamen (`Money.fs`), Hinweise im SyncFlow.

Die bestehenden Layout-Tests bleiben inhaltlich unverändert und grün. Das Löschen der Designwert-Tests folgt in infrastructure-jc1y1.
