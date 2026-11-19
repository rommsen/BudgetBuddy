---
id: ledger-3pq7c
title: "Durchgehen am Handy: Wischen mit Fläche darunter (A1), Karte füllt den Platz bis zu den Knöpfen, Seite ohne Scrollen"
status: backlog
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [design-exploration, durchgehen, wischen, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Roman am 2026-11-19: Wischen fühlt sich nicht nach Wischen an. Heute rückt die Karte nur 60 % des Fingerwegs mit
(`SwipeableReviewCard` in `Ledger/View.fs`), wird blasser, darunter ist nichts. Dazu: die Karte soll immer den vollen
Platz einnehmen, und die Seite soll ohne Scrollen aufs Handy passen.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- Karte folgt dem Finger 1:1. Darunter eine Fläche über die ganze Karte: rechts grün mit Haken „Passt“, links neutral mit
  Uhr „Später“, Symbol über dem Wort (56 px breit, bei 90 px ganz sichtbar). Ab 90 px (`reviewSwipeThresholdPx`)
  rastet sie ein (kräftigere Farbe, Kreis gefüllt, kurzer Vibrationsimpuls, wo erlaubt). Darunter federt die Karte
  zurück, darüber fliegt sie raus, die nächste rückt nach. Sprache wie Wisch geprüft im Kassenbuch (ledger-th36h).
- Ohne Kategorie: nur ein gelbes „!“ unter der Karte, die Karte wird nach etwa 72 px zäh und rastet nie ein; der Satz
  „Erst Kategorie wählen“ steht dabei an der Stelle des Wisch-Hinweises, das Kategorie-Feld leuchtet gelb. Der Toast
  `reviewNeedsTargetText` entfällt.
- Wisch-Hinweis einzeilig „Wischen: rechts „Passt“, links „Später““, Zeile mit fester Höhe (kein Springen der Knöpfe).
- Die Karte füllt den Raum zwischen Fortschrittsbalken und Knöpfen; „Regel anlegen“ und „Details“ am unteren Rand.
  Die Seite passt ohne Scrollen; ist der Inhalt länger, scrollt er in der Karte, die Knöpfe bleiben fest.
- `prefers-reduced-motion`: kein Flug, Fläche erscheint trotzdem. Knöpfe, Enter und S bleiben der Weg ohne Wischen.

## Acceptance criteria
- [ ] Wischen nach der Skizze (Fläche, Einrasten, Zurückfedern, Rausfliegen), reine Entscheidung weiter in `reviewSwipeOf`.
- [ ] Karte füllt den Platz, Seite ohne Scrollen bei 412 und 320 px; die Sweep-Ausnahme `durchgehen-amazon` @ 320 (Kann-Sammlung ledger Punkt 150) entfällt.
- [ ] Browser-Test `[flow:...]` für Wisch mit und ohne Kategorie.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6.
