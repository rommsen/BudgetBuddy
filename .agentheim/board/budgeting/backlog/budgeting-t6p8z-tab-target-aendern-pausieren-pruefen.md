---
id: budgeting-t6p8z
title: "Tab Target: Targets in BB anlegen, ändern und entfernen, je Monat pausieren und die Frage Passt das Target noch? mit gespeichertem So lassen"
status: backlog
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-dyn8g, budgeting-v4ht9]
blocks: [budgeting-m3x7f]
tags: [budget, target, pausieren]
related_adrs: [0063, 0065]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-kjsht, budgeting-h2zfn]
---

## Why
Task 4 von 5 des Gesamtkonzepts. Targets leben nur in YNAB. Roman will sie in BB pflegen, Monatsausnahmen pausieren und erkennen, wenn ein Target nicht mehr zur Wirklichkeit passt. Das Kassenbuch macht das möglich.

## What
Regeln: `docs/ux/budget/konzept.md`, Abschnitt Targets.
- Target anlegen, ändern, entfernen über YNAB nur in den Grenzen aus budgeting-q7m2c, mit `DateField` für das Datum. Änderungen erscheinen als Budget-Bewegung neuer Herkunft.
- Pause je Kategorie und Monat in BB. Sie gilt für `fehlt`, Vorschau und Targets anwenden, in BB oder YNAB gesetzt.
- Prüfung aus dem Kassenbuch, Vergleichsregel je Target-Art, Schwelle und gespeichertes "So lassen" in der taktischen Modellierung festlegen, anhand echter Daten.
- Tab zeigt `Target •`, wenn die Prüfung offen ist. Vergangene Monate erlauben kein Ändern und Pausieren.

## Notes
Aus budgeting-q7m2c (Research `ynab-reihenfolge-monate-target-schreiben-2026-11-02`): Beim Ändern nur geänderte Felder senden, beim Betrag nur `goal_target`. `goal_frequency` ersetzt das Target, das Datum geht dabei immer verloren, der Tag beim Wechsel der Art, deshalb warnen. "Jährlich mit Datum" braucht zwei Aufrufe. **Vor dem ersten Schreiben in Romans Budget:** Roman legt im Budget Test von Hand ein Monats-Target mit Tag an, ein `PATCH` nur mit `goal_target` belegt dann, dass der Tag bleibt (42 Targets haben diese Form). Unbelegt bleiben außerdem alle 3 Monate mit Datum (4) und MF (18).

## Acceptance criteria
Werden in der taktischen Modellierung geschärft.
- [ ] Der Knopf "Target pausieren" steht im Tab Geld im Modus Zuweisen, auf dem in budgeting-c5w2n frei gelassenen Platz.
- [ ] Pause wirkt in Fehlbetrag, Vorschau und Targets anwenden, endet mit dem Monat, und eine YNAB-Pause wird weiter beachtet.
- [ ] "So lassen" überlebt das Neuladen und meldet sich erst bei spürbar anderem Vergleichswert.
- [ ] Target-Änderung, die YNAB ablehnt, wird konkret gemeldet.
- [ ] Neue Spalten und Tabellen sind auf bestehender Datenbank wiederholbar angelegt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Hinweise aus dem Review von budgeting-g8s3w
`matchesFilter` (`Types.fs:409`) sieht nur die Kategorie. Der Prüf-Filter aus budgeting-m3x7f braucht ein Feld an `BudgetCategory` oder einen Parameter, das hier entscheiden.
