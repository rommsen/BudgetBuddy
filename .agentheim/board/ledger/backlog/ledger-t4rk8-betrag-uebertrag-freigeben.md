---
id: ledger-t4rk8
title: "Betrag eines Übertrags in BB ändern (Sperre nach Probelauf aufheben)"
status: backlog
type: feature
context: ledger
created: 2026-11-10
completed:
depends_on: []
blocks: []
tags: [laufend, ynab, betrag, uebertrag]
related_adrs: [0092, 0058]
related_research: []
prior_art: [ledger-m7mye, ledger-m7a9q]
---

## Why
Der Probelauf ledger-m7a9q zeigt: YNAB zieht beim PUT `amount` die Gegenbuchung eines Übertrags mit (von beiden Seiten), und der Split-Ersatz legt die Gegenbuchung einer Übertrags-Zeile mit dem neuen Betrag an (ADR 0092, Nachtrag 2026-11-10). Heute sperrt BB den Betrag jedes Übertrags (`amountTransferReason`, `amountSplitTransferReason` in `src/Server/Ledger/CommandHandlers.fs`), Roman muss solche Beträge in YNAB ändern.

## What
- `amountTransferReason` entfällt für ganze Überträge zwischen Budget-Konten. `setEntryAmount` schreibt den Betrag per `updateAmount` und hält in BB auch für den Eintrag der Gegenbuchung (`TransferEntryId`) den neuen Betrag mit umgekehrtem Vorzeichen fest; Rückgängig auf demselben Weg.
- Die Gegenbuchung einer Split-Übertrags-Zeile bleibt gesperrt (YNAB antwortet 400), mit eigenem Grundtext, der auf die Aufteilung der Ursprungsbuchung verweist.
- `amountSplitTransferReason` entfällt: der Betrag geht wie bei jedem Split als Ersatz; die neue Gegenbuchung und die Verknüpfung (ledger-h6mer) laufen wie nach jedem Ersatz.
- Zu klären beim Verfeinern: welches Ereignis die Gegenbuchung in BB bekommt, und ob Überträge auf Tracking-Konten dazugehören (im Probelauf nicht geprüft).

## Acceptance criteria
- [ ] Betrag eines ganzen Übertrags lässt sich in BB ändern, beide Einträge in BB zeigen danach den neuen Betrag, YNAB per Update.
- [ ] Die Gegenbuchung einer Split-Übertrags-Zeile bleibt gesperrt, mit Grund.
- [ ] Betrag einer Split-Buchung mit Übertrags-Zeile geht über den Split-Editor als Ersatz.
- [ ] Tests für die Sperr-Regel (`amountNotChangeableReason`) und den Schreibweg, gegen Fakes.

## Notes
Probelauf-Ergebnisse im Outcome von ledger-m7a9q. Sichtbares Verhalten (Sperrgrund am Feld) beim Verfeinern gegen den Styleguide prüfen (`.claude/rules/task-ui-vorgaben.md`).