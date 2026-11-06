---
id: ledger-p3vq8
title: "addCashExpense: gelingt der Push und scheitert danach das Schreiben der Verknüpfung, legt der Spiegel eine zweite Buchung an"
status: backlog
type: bug
context: ledger
created: 2026-11-06
completed:
depends_on: []
blocks: []
tags: [ynab, quick-add, bot]
related_adrs: [0021, 0004]
related_research: []
prior_art: [ledger-z71ka]
---

## Why
Fund aus dem Review-Lauf zu ledger-z71ka (backend-code-quality, Warnung 2). `Ledger.CommandHandlers.addCashExpense` schreibt `CashExpenseAdded`, pusht und schreibt danach `EntryLinkedToYnab`. Wirft `appendAndApply [ linked ]` nach erfolgreichem Push, oder wirft `push ()` selbst, nachdem YNAB die Buchung schon angelegt hat, steht im Stream nur `CashExpenseAdded`. Laut Kommentar an `addCashExpense` sieht der Spiegel die YNAB-Buchung dann ungematcht und legt eine zweite Kassenbuch-Buchung an (Quick Add und Bob tragen keine ImportId, ADR 0004). Ein "Erneut senden" würde in YNAB doppelt buchen. Das betrifft Quick Add und Bob gleich und besteht seit ADR 0021.

## What
Klären und beheben, wie eine Buchung ohne Verknüpfung und ohne Fehlschlag-Ereignis behandelt wird: etwa die YNAB-Id vor dem Schreiben der Verknüpfung festhalten, einen eigenen Zustand "Push-Ergebnis unbekannt" einführen, oder Erneut senden und Spiegel für diesen Fall abgleichen lassen.

## Acceptance criteria
- [ ] Kaputt-Szenario: Push gelingt, das Schreiben von `EntryLinkedToYnab` wirft; danach läuft der Spiegel. Es entsteht keine zweite Kassenbuch-Buchung. Test rot vor dem Fix.
- [ ] Kaputt-Szenario: dieselbe Lage, danach "Erneut senden". In YNAB entsteht keine zweite Buchung.
- [ ] Intakt: Push gelingt und Verknüpfung wird geschrieben, Push scheitert mit `EntryPushToYnabFailed`, beides unverändert.

## Notes
Braucht Modeling: Welche der Varianten aus "What" passt zu ADR 0021, und ob dafür ein Nachtrag nötig ist. Der Bot meldet den Zustand seit dem Review-Lauf zu ledger-z71ka als "Push-Ergebnis unbekannt" in `push_error`.
