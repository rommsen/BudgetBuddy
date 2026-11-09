---
id: ledger-m7a9q
title: "Probelauf: Betrag eines Übertrags per Update in YNAB ändern"
status: backlog
type: spike
context: ledger
created: 2026-11-09
completed:
depends_on: []
blocks: []
tags: [ynab, betrag, uebertrag]
related_adrs: [0092, 0058]
related_research: []
prior_art: [ledger-m7mye]
---

## Why
ADR 0092 Punkt 4 sperrt den Betrag von Überträgen (ganz oder als Split-Zeile), weil nicht geprüft ist, ob YNAB die Gegenbuchung beim Update des Betrags mitzieht. Die Folgen-Zeile des ADR nennt einen Probelauf gegen das Test-Budget; danach kann die Sperre fallen.

## What
Probelauf gegen das Wegwerf-Budget "Test": PUT `amount` auf einen Übertrag zwischen zwei Budget-Konten und auf eine Split-Buchung mit Übertrags-Zeile (Ersatz). Ergebnis als Nachtrag zu ADR 0092.

## Acceptance criteria
- [ ] Probelauf dokumentiert (Aufruf, Antwort, Zustand der Gegenbuchung danach).
- [ ] Nachtrag ADR 0092 mit der Entscheidung, ob `amountTransferReason` und `amountSplitTransferReason` fallen.