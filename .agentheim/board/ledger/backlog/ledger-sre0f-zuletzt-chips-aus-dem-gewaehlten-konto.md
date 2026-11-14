---
id: ledger-sre0f
title: "Zuletzt-Chips in „Neue Buchung“ aus den Buchungen des gewählten Kontos, fünf mit Memo"
status: backlog
type: bug
context: ledger
created: 2026-11-14
completed:
depends_on: []
blocks: []
tags: [captured, erfassen, frontend]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Romans Blick nach dem Deploy von ledger-ea70s (2026-11-14): Erfassen ohne Quick Add fühlt sich langsamer an, weil
die Zuletzt-Chips die üblichen Bar-Buchungen nicht zeigen (etwa „Anastasia“). Das [human-eye]-Kriterium von
ledger-ea70s („mindestens so schnell wie vorher“) ist damit nicht erfüllt. Quick Add konnte das, „Neue Buchung“
kann es schlechter (Fehler der Verfeinerung von ledger-ea70s: verglichen wurden nur Anzahl und Konto, nicht die
Herkunft der Vorlagen).

## What
Heute: höchstens 3 Chips, nur aus in BB erfassten Buchungen (`CashExpenseAdded` mit Actor `UserInBb` oder
`QuickAdd`, `src/Server/Ledger/Persistence.fs:2050`), über alle Konten gemischt, ohne Memo
(`Shared.Domain.recentBookingTemplates`, `LedgerBookingRecentCount = 3`). Buchungen, die Lena, Bob oder YNAB
anlegen, fehlen. Früher Quick Add: 5 Vorlagen aus YNAB, aus den Buchungen des Quick-Add-Kontos, mit Memo.

Entschieden (Roman, 2026-11-14):
- Die Chips sind die letzten Buchungen des vorbelegten bzw. gewählten Kontos aus dem Kassenbuch, gleich wer sie
  angelegt hat (BB, Bob, Lena, YNAB-Spiegel).
- Wechselt das Konto im Formular, wechseln die Chips.
- 5 Chips.
- Ein Tipp füllt auch das Memo.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-14, raw, unrefined. Needs a `modeling` refine pass before it can be promoted.
Offen beim Verfeinern: welche Buchungen ausgeschlossen werden (Überträge, Splits, gelöschte, Importe vom
Comdirect-Konto, das ohnehin nicht wählbar ist); Doppelte nach Payee, Ziel und Betrag wie heute; Laden je Konto
(eigener Endpoint oder alles vorab). Prior art: ledger-ea70s (Ein Erfassen), ledger-at63w (Buchung im Kassenbuch
erfassen).
Kein Bug: Mit Filter auf dem Comdirect-Girokonto kommt Bar, weil das Import-Konto zum Erfassen gesperrt ist
(ledger-at63w); Roman bestätigt, dass Bar dort richtig ist.
