---
id: budgeting-a3k7m
title: "Budget-Kopf bei 1024 px in einer Zeile, Historie am Handy ohne abgeschnittene Kategorien und Beträge"
status: done
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

## Outcome

Beide Mängel aus dem Design-Review zu c6vn4 sind behoben. Es gibt keine neue Testdatei: zwei bestehende `[layout]`-Tests wurden um Regressionsprüfungen erweitert, beide waren vor dem Fix rot.

**1. Kopf bei 1024 px.** `src/Client/Components/Budget/MonthView.fs` (`categorySearchField`): `md:min-w-32 md:w-32` wurde zu `md:min-w-28 md:w-28 xl:min-w-32 xl:w-32`. `src/Client/Components/Budget/View.fs` (`wideHead`): `gap-x-3` wurde zu `gap-x-2 xl:gap-x-3`. Zusammen gewinnt das zwischen `md` und `xl` 32 px (16 px Suche, 4 × 4 px Abstand), ab 1280 px bleibt alles wie vorher. Ich habe bewusst nicht `md:w-24` allein genommen. Das hätte ebenfalls für eine Zeile gereicht, die Suche bei 1024 px wäre dann aber auf etwa 110 px geschrumpft und hätte die bestehende Untergrenze "die Suche bleibt benutzbar" (120 px) verfehlt. Mit der Kombination bleibt die Suche über 120 px, und es bleiben etwa 14 px Reserve.
Test: `scripts/budget-page.test.mjs`, "[layout] Kopf: ab 1024 px eine Zeile ...". Er läuft jetzt mit der neuen Fixture `realisticHeadFixture`: Zu verteilen 12.345,67 €, beide Zähler zweistellig, Monat "September 2026" (dazu eine neue Option `current` in `openPage` und `frameFor`). Der Test prüft, dass die Fixture wirklich so angezeigt wird. Gegenprobe mit der alten Klasse: rot mit "history steht in der Zeile des Monats", der Knopf Historie stand bei 1024 px in Zeile 2 (top 134 statt 91, Ende von Targets anwenden bei 1000 von 1004 px).

**2. Historie am Handy.** `src/Client/Components/Budget/HistoryView.fs` (`runHead`): Unter `md` steht der Text in `line-clamp-2`, die Zeile hat `items-start min-h-5`, der Betrag bleibt `shrink-0 whitespace-nowrap` rechts neben der ersten Zeile. Ab `md` gelten genau die bisherigen Klassen (`items-center h-5`, `truncate`), über `max-md:` und `md:` getrennt, die PC-Ansicht ist also unverändert.
Test: `scripts/budget-history.test.mjs`, der Handy-`[layout]`-Test bei 375 px. Er hat jetzt eine Verschiebung "Verschieben, Essen/Einkaufen → Urlaub" mit 1.234,56 € und prüft, dass am Kopf weder in der Breite noch in der Höhe abgeschnitten wird, dass der Text höchstens zwei Zeilen hat und dass der Betrag ungekürzt innerhalb der Zeile steht. Vor dem Fix: rot mit `textClipped: true`.

`./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3776, node 300 grün). Der bekannte wacklige Layout-Test (r7h2q) ist in diesem Lauf nicht aufgetreten.
