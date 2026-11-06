---
id: budgeting-m3x7f
title: "Monatserster: Leiste mit Targets prüfen, Targets anwenden, rot decken und verteilen, Vorschau mit abwählbaren Zeilen"
status: backlog
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-t6p8z, budgeting-q4v8n, budgeting-eh8xe, budgeting-hbhzf]
blocks: []
tags: [budget, monatserster, targets]
related_adrs: [0065, 0082]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-h2zfn]
---

## Why
Task 5 von 5 des Gesamtkonzepts. Der Monatserste ist Romans wichtigster Ablauf. Die Leiste führt durch ihn, ohne ihn in einem Modus festzuhalten.

## What
Regeln: `docs/ux/budget/konzept.md`, Abschnitt Die vier Abläufe.
- Leiste im aktuellen Monat, Raster aus vier Feldern. "Targets prüfen" führt auf die Seite `#/budget/target-check` (budgeting-q4v8n, ADR 0082), Überzogene filtert die Liste, Anwenden öffnet die Vorschau, Verteilen ist Anzeige.
- Vorschau bekommt abwählbare Zeilen, Stand-Prüfung und Bericht bleiben.
- Leiste endet, wenn die ersten drei Felder erledigt sind. Ausblenden gilt für den Monat.

## Acceptance criteria
Werden in der taktischen Modellierung geschärft.
- [ ] Reihenfolge und Erledigt-Zustände je Feld, Filter Überzogen mit leerer Ansicht.
- [ ] Das Feld "Targets prüfen" führt auf `#/budget/target-check`.
- [ ] Abgewählte Zeilen werden nicht geschrieben, Summe und Zu verteilen danach folgen der Auswahl.
- [ ] Ausblenden überlebt das Neuladen im selben Monat, nicht den Monatswechsel.
- [ ] Browser-Wächter: Happy Path am Handy von der Leiste bis zum Bericht.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Hinweise aus dem Review von budgeting-g8s3w
`PreviewReady` und `ApplyDone` (`Types.fs:22`, `:29`) für die abwählbaren Zeilen zu Records machen. `confirmTargets` schickt heute die ganze Vorschau.

## Hinweis aus Romans Durchsicht
Das Feld heißt "Überzogene decken" statt "rot decken", der Filter "Überzogen" statt "Rot" (budgeting-h3k9d).

## Hinweis aus der Verfeinerung von budgeting-p2zr6 (2026-11-05)
Roman hat entschieden: "Targets prüfen" ist eine eigene Seite (ADR 0082), kein Prüf-Filter der Budget-Liste. Das alte Kriterium "Prüf-Filter mit leerer Ansicht" und der Fall `CheckOnly` in `CategoryFilter` entfallen. Das Feld ist ein Weg zur Seite aus budgeting-q4v8n, daher hängt diese Task an ihr statt an budgeting-p2zr6.

Offen, klärt diese Task: wann das Feld "Targets prüfen" als erledigt gilt und ob es eine Zahl zeigt. Eine Zahl bräuchte auf der Budget-Seite `getTargetCheck` und `targetCheckGroups` aus budgeting-p2zr6, ein Erledigt-Zustand ohne Gedächtnis (ADR 0082 Punkt 2) etwa "heute im laufenden Monat geöffnet".

## Hinweis aus budgeting-zkbbd (2026-11-05)
- Nach dem Wechsel, auf den neuen Schreibweg (ADR 0084 Punkt 7, entschieden von Roman am
  2026-11-05): Dieser Task wird erst nach budgeting-eh8xe gebaut und schreibt Targets als
  Ereignisse in BB; der Zwei-Schritt-Weg nach YNAB entfällt.
