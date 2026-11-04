---
id: budgeting-a3k7m
title: "Budget-Kopf bei 1024 px in einer Zeile, Historie am Handy ohne abgeschnittene Kategorien und Beträge"
status: todo
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: [budgeting-r7h2q]
tags: [budget, layout, historie, handy]
related_adrs: []
related_research: []
prior_art: [budgeting-c6vn4, budgeting-h7q4v]
---

## Why
Aus budgeting-h9c3w (Design-Review zu c6vn4, Punkte 1 und 2). Roman 2026-11-04: aufteilen und selbst entscheiden, Screenreader ist gerade egal.

## What

### 1. Kopf bei 1024 px
`src/Client/Components/Budget/View.fs` (Kopf, etwa Zeilen 93 bis 112) und `MonthView.fs` (etwa Zeile 340). Mit echten Daten braucht der Kopf bei 1024 px etwa 1012 px auf 984 px, die Uhr (Knopf Historie) steht allein in Zeile 2; erst ab etwa 1060 px passt alles in eine Zeile. Der `[layout]`-Test ist grün, weil seine Fixture kürzere Werte hat.

Entscheidung: zwischen `md` und `xl` mindestens 28 px gewinnen, zuerst über die Suchbreite (etwa `md:w-24 xl:w-32`) und, falls das nicht reicht, über einen kleineren Abstand bis `xl`. Die Fixture des Tests bekommt realistische Längen: "Zu verteilen" fünfstellig mit Nachkommastellen, Chip-Zähler zweistellig, Monat "September 2026".

### 2. Historie am Handy
`src/Client/Components/Budget/HistoryView.fs` (Kopf eines Durchlaufs, etwa Zeile 62, `truncate min-w-0`). Am Handy schneidet `truncate` die tragende Info ab: "Verschieben, Essen/Einkaufen → ..." (Ziel, Betrag mit Vorzeichen und Richtung fehlen), "Targets anwenden, 2 Kate...".

Entscheidung: am Handy bricht der Kopf des Durchlaufs um statt abzuschneiden (höchstens zwei Zeilen, `line-clamp-2`), der Betrag bleibt rechts in voller Länge. Am PC bleibt die Zeile, wie sie ist.

## Acceptance criteria
- [ ] `[layout]` PC 1024 px mit realistischer Fixture: Kopf in einer Zeile, Knopf Historie in derselben Zeile wie die Suche. Mit der alten Klasse rot (Gegenprobe im Outcome nennen).
- [ ] `[layout]` Handy 375 px: in der Historie sind bei "Verschieben, Essen/Einkaufen → Urlaub" Quelle und Ziel ganz sichtbar (kein Abschneiden mit Ellipse, `scrollWidth <= clientWidth` am Kopf), und der Betrag steht ungekürzt.
- [ ] PC-Ansicht der Historie unverändert (bestehende Tests grün).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Bestehende Tests in `scripts/budget-page.test.mjs` und dem Test der allgemeinen Historie erweitern, keine zweite Datei. Je Aussage höchstens ein Browser-Test (Doktrin Punkt 5).
