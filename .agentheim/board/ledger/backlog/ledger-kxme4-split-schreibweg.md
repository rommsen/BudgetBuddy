---
id: ledger-kxme4
title: "Split-Buchungen in BB bearbeiten, Schreibweg: Befehl setEntrySplits mit Prüfung Summe gleich Betrag, EntryRecategorized mit Splits, eigener Push nach YNAB, derselbe Fehlschlag-Zustand wie bei der Kategorie"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [ledger-k2rbn, ledger-mas5g]
blocks: [ledger-pafkp]
tags: [backend, kassenbuch, split, v2, schreibweg]
related_adrs: [0006, 0019, 0028, 0038, 0045, 0046, 0054, 0055]
related_research: []
prior_art: [ledger-k2rbn]
---

## Why
Roman hat am 2026-10-24 entschieden, die Split-Bearbeitung mitzubauen (der Pfeil an der
Kategorie einer Split-Buchung öffnet einen Split-Editor). Dies ist der Server-Teil. Der
Aufruf nach YNAB hängt am Ergebnis von `ledger-mas5g`.

## What
Wird nach dem Spike verfeinert; der Rahmen steht:
1. **Befehl** `setEntrySplits: Guid -> TransactionSplit list -> Async<Result<CategoryChangeOutcome, string>>`
   (Ergebnis-Typ aus `ledger-k2rbn`). Leere Liste hebt den Split auf, dann braucht die Buchung
   eine Kategorie (`setEntryCategory` danach oder gleichzeitig, Entscheidung beim Bau).
2. **Validierung** über die gemeinsamen Helfer aus `Shared.Domain` (ADR 0006: Summe der
   Zeilen gleich Betrag, mindestens zwei Zeilen, jede Zeile Kategorie, Übertrag oder
   unkategorisiert). `apply` bleibt bei "Kategorie XOR Splits".
3. **Ereignis** `EntryRecategorized` (`OldSplits`, `NewSplits`), danach der Push über eine
   eigene benannte Funktion `YnabClient.updateSplits` (Form laut Spike), dieselben Zustände
   `EntryCategorizationWriteBackFailed` mit Baseline und `WrittenBackToYnab`, dieselbe
   Schreib-Sperre, dieselbe `observe`-Regel (ADR 0055, Punkte 4 bis 6).
4. **Übertrags-Zeilen:** Der Schreibweg ändert Gegenbuchungen nicht (ADR 0046); ob eine
   Übertrags-Zeile neu entstehen darf, steht im Spike-Ergebnis.

## Acceptance criteria
- [ ] To be defined after `ledger-mas5g`.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (e, Server). Ziel-Lage: backlog.
Dateien: `src/Shared/Domain.fs`, `Api.fs`, `src/Server/Api.fs`, `YnabClient.fs`,
`Ledger/CommandHandlers.fs`, `Domain.fs` (nur Nutzung), Tests. Überschneidung mit
`ledger-k2rbn` und `ledger-w29y6` in `CommandHandlers.fs`/`YnabClient.fs`: nach
`ledger-k2rbn` bauen.
