---
id: ynab-sync-c7aag
title: "YNABs automatische Payee-Zuordnung ändert importierte Buchungen, der Spiegel holt es zurück ins Kassenbuch"
status: backlog
type: idea
context: ynab-sync
created: 2026-11-13
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: [0093]
related_research: []
prior_art: []
---

## Why
Roman, 2026-11-13: Nach dem Import benennt YNAB Payees per eigener Zuordnung um und setzt die dazu gemerkte
Kategorie (Beispiel: der Name des Kontoinhabers wurde zu einem Händler-Payee samt dessen Kategorie, Sekunden nach dem Push). Abschalten lässt sich das
in YNAB nur je Payee. Der Spiegel übernimmt die Änderung als `EntryDetailsChangeObservedInYnab` und
`EntryCategorizationChangeObservedInYnab`, damit überschreibt YNAB, was BB entschieden hat. „Da müsste man auch
nochmal drüber nachdenken.“

## What
Offen. Ideen: BB erkennt eine Änderung durch YNAB kurz nach dem eigenen Push und schreibt den eigenen Stand zurück,
oder BB schickt Payee und Kategorie so, dass YNAB nicht umbenennt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured raw, unrefined. Verwandt: ledger-nkwqb (Abgleich in YNAB).
