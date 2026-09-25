---
id: ledger-ggzdw
title: "Spiegel wertet eine Kategorie-Umbenennung in YNAB als Umkategorisierung jeder Buchung: observe vergleicht CategoryRef samt Name statt nur die Id"
status: backlog
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, spiegel, kassenbuch, historie]
related_adrs: [0013, 0014, 0039]
related_research: []
prior_art: [ledger-m8r2v, ledger-p3wnq]
---

## Why
`Ledger.Domain.observe` (`src/Server/Ledger/Domain.fs:514`) vergleicht `entry.Category <>
obs.Category` als volle Record-Gleichheit. `CategoryRef` (`src/Server/Ledger/Events.fs:66-69`)
trägt `CategoryId` und `CategoryName`. Benennt Roman eine Kategorie in YNAB um, ohne eine
einzige Buchung umzukategorisieren, schreibt der nächste Spiegel-Lauf für jede bereits
gespiegelte Buchung dieser Kategorie ein `EntryCategorizationChangeObservedInYnab`
("Kategorie in YNAB geändert: Lebensmittel → Essen"), einmal je Buchung, append-only für
immer (ADR 0013). Bei rund 14.000 Buchungen ist das potenziell die ergiebigste Quelle
falscher Historie-Zeilen im Kassenbuch. Gefunden vom tactical-modeler bei der Verfeinerung
von `ledger-tpbe7` am 2026-09-25; bewusst nicht dort mitgelöst, weil es jede Umbenennung
betrifft, nicht nur den Push-Zeitpunkt.

## What
Zu entscheiden und dann umzusetzen: `categorizationChanged` (oder der Vergleich in
`observe`) vergleicht nur die Kategorie-**Identität** (`CategoryId`), der Name ist Anzeige.
ADR 0039 hat für den Lesepfad bereits festgehalten, dass die Identität die Id ist und der
Name aus den gespiegelten Stammdaten (`ledger_categories`) kommt. Offen bleibt, was mit dem
Namen im Snapshot passiert, wenn nur er sich ändert: still übernehmen (kein Ereignis, der
Zustand trägt dann weiter den alten Namen, bis ein echtes Ereignis kommt) oder ein eigenes,
ausblendbares Ereignis wie beim Cleared-Status. Das ist ADR-würdig (scope: ledger).

Dasselbe gilt sinngemäß für Split-Zeilen: `entry.Splits <> obs.Splits` vergleicht
`ToCategory`-Ziele vermutlich ebenfalls samt Name.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Erfasst am 2026-09-25 aus der Verfeinerung von `ledger-tpbe7` (Orchestrator, tactical-modeler).
Vor der Verfeinerung lohnt ein Blick in die Live-Historie, ob solche Zeilen schon
vorkommen: `getEntryHistory`-Sätze mit gleicher Id, aber anderem Namen.
