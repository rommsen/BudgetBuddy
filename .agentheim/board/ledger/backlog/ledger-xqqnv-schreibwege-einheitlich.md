---
id: ledger-xqqnv
title: "Schreibwege einheitlich: Schreib-Sperre, Spiegel-Auslöser nach Freigabe und vorhandene import_id in allen Befehlen gleich und durch Verhalten belegt"
status: backlog
type: refactor
context: ledger
created: 2026-11-12
completed:
depends_on: []
blocks: []
tags: [bestandsreview, captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger vom 2026-11-12, Gruppe G4. Roman hat die Gruppe als Task gewählt.

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`:

### G4 Schreibwege uneinheitlich: Sperre, Spiegel-Auslöser, vorhandene `import_id` (hoch, 10 Befunde, ledger)
Gleiche Regeln (Schreib-Sperre, Auslöser nach Freigabe, 409 heißt angekommen) gelten nur in einem Teil der Befehle, und
die Lücken sind ungetestet.
- src/Tests/LedgerBookingCaptureTests.fs:482 | test-doctrine.md Punkt 4; ADR 0091 | hoch | Fall „abgeglichene erfasste Buchung: Error discardReconciledText, kein DELETE, kein Ereignis“ und „in YNAB gelöscht“ ergänzen; ohne Test bliebe eine entfernte Reconciled-Prüfung grün.
- src/Server/Ledger/CommandHandlers.fs:705 | ADR 0021, ledger-p3vq8; semantic-messaging.md | mittel | Eine schon vorhandene `import_id` behandeln `pushEntryAndRecord`, `addCashExpenseWith` und der erneute Import-Sammelpush verschieden; in einem Helfer überall als angekommen behandeln.
- src/Tests/LedgerBookingCaptureTests.fs:475 | test-doctrine.md Punkt 3, 4 | mittel | Spiegel-Auslöser von `discardCapturedBooking` zählen: einmal nach Erfolg, nie nach Ablehnung.
- src/Tests/LedgerBookingCaptureTests.fs:482 | test-doctrine.md Punkt 4; README *ErfassteBuchungVerwerfen* | mittel | Schreib-Sperre-Fall mit `commandWaitsForMirror` für `discardCapturedBooking`.
- src/Tests/LedgerTransferTests.fs:280 | test-doctrine.md Punkt 4; README *ÜbertragSetzen* | mittel | Schreib-Sperre-Fall für `setEntryTransfer`.
- src/Server/Ledger/CommandHandlers.fs:1276 | test-doctrine.md Punkt 4; README *BuchungÜbernehmen* | mittel | Schreib-Sperre um `pushComdirectImports` per Verhalten belegen.
- src/Tests/LedgerEntryDeletionTests.fs:518 | test-doctrine.md Punkt 4 | mittel | „Spiegel-Auslöser erst nach Freigabe“ (`SyncMirrorTrigger`) für `deleteEntry`, `restoreDeletedEntry`, `discardCapturedBooking`, `pushComdirectImports`.
- src/Tests/LedgerDuplicateDecisionTests.fs:347 | test-doctrine.md Punkt 4 | niedrig | Auslöser außerhalb der Sperre mit `SyncMirrorTrigger` belegen statt „triggers = 0 während des Laufs“.
- src/Server/Ledger/CommandHandlers.fs:2327 | ADR 0093 Punkt 6 | niedrig | `isHeldAsPossibleDuplicate` auch in `discardCapturedBooking` und `restoreDeletedEntry` (3412) als ersten Zweig.
- src/Tests/LedgerBatchReadTests.fs:54 | README *Lesen für eine Auswahl*; query-safety.md | niedrig | Zahl der Abfragen der Sammelbefehle und von `runMirror` unabhängig von der Auswahlgröße belegen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-12, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn Roman das will.
